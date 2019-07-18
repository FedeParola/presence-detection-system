using Newtonsoft.Json;
using PDSApp.Persistence;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PDSApp.SniffingManagement {
    class SniffingManager {
        private const int SNIFFER_LISTEN_PORT = 13000;
        private const byte ACK_BYTE = (byte) 'A';
        private const byte RESET_BYTE = (byte) 'R';
        private const byte TERMINATION_BYTE = 0;
        private const int ERROR_TIMEOUT_SECS = 10;

        public delegate void ErrorHandler();

        private DBManager db;
        private RecordsProcessor processor;
        private TcpListener tcpListener;
        private JsonSerializer serializer = new JsonSerializer();
        private ErrorHandler errorHandler;

        /* Public configuration properties */
        public UInt16 Port { get; set; }
        public UInt16 SniffingPeriod { get; set; }
        public Byte Channel { get; set; }
        public Double RoomLength { get; set; }
        public Double RoomWidth { get; set; }
        
        /* Sniffing start/stop fields */
        private bool sniffing = false;
        private AutoResetEvent listeningStopped = new AutoResetEvent(false);
        private Task processRecordsTask;
        private SynchCounter handleSnifferTasksCount = new SynchCounter(0);
        private CancellationTokenSource cancelSniffing;
        private object stopSniffingLock = new object(); // Enables mutual exclusion executing StopSniffing()

        private Dictionary<string, Sniffer> sniffers = new Dictionary<string, Sniffer>();
        /* 
         * Records received from the last communication with the sniffers.
         * Key: the string representation of the ip addr of the sniffer that sent the records
         * Value: list of raw records that needs to be processed
         */
        private ConcurrentDictionary<String, List<Record>> rawRecords = new ConcurrentDictionary<String, List<Record>>();

        /* N of missing transmissions from sniffers before proceeding with processing */
        private CountdownEvent missingTransmissionsCountdown;
        /* Enable HandleSniffer tasks to send the configuration to the sniffers */
        private SemaphoreSlim sniffersConfigurationSemaphore;


        public SniffingManager(UInt16 port, UInt16 sniffingPeriod, Byte channel, Double roomLength, 
                                Double roomWidth, DBManager db, ErrorHandler handler) {
            Port = port;
            SniffingPeriod = sniffingPeriod;
            Channel = channel;
            RoomLength = roomLength;
            RoomWidth = roomWidth;
            this.db = db;
            this.errorHandler = handler;
        }

        public void AddSniffer(Sniffer s) {
            if(sniffing) {
                throw new InvalidOperationException("Can't change the sniffers configuration while the sniffing process is running," +
                    " call StopSniffing() first");
            }

            if (s == null) {
                throw new ArgumentNullException();
            }

            /* Make sure there is no duplicate ip */
            if(sniffers.ContainsKey(s.Ip)) {
                throw new ArgumentException("Sniffer with ip " + s.Ip + " already configured");
            }

            /* Make sure the sniffer status is correct */
            s.Status = Sniffer.SnifferStatus.Stopped;

            /* Add the sniffer */
            sniffers[s.Ip] = s;
        }

        public int GetSniffersCount()
        {
            return sniffers.Count;
        }

        public bool RemoveSniffer(string snifferIp) {
            if (sniffing) {
                throw new InvalidOperationException("Can't change the sniffers configuration while the sniffing process is running," +
                    " call StopSniffing() first");
            }

            if (snifferIp == null) {
                throw new ArgumentNullException();
            }

            return sniffers.Remove(snifferIp);
        }

        public void ClearSniffers() {
            if (sniffing) {
                throw new InvalidOperationException("Can't change the sniffers configuration while the sniffing process is running," +
                    " call StopSniffing() first");
            }

            sniffers.Clear();
        }

        public Sniffer GetSniffer(string snifferIp) {
            Sniffer s;

            if (snifferIp == null) {
                throw new ArgumentNullException();
            }

            try {
                s = sniffers[snifferIp];
            } catch(KeyNotFoundException) {
                s = null;
            }

            return s;
        }

        public ICollection<Sniffer> GetSniffers() {
            return sniffers.Values;
        }

        public void StartSniffing() {
            if(sniffing) {
                throw new InvalidOperationException("Already sniffing, call StopSniffing() first");
            }

            /* Check enough sniffers configured */
            if (sniffers.Count < 1 /* CAMBIALO A 2!!! */) {
                throw new InvalidOperationException("Needed at least 2 sniffers to start sniffing");
            }

            /* Initialize structures */
            cancelSniffing = new CancellationTokenSource();
            missingTransmissionsCountdown = new CountdownEvent(sniffers.Count);
            sniffersConfigurationSemaphore = new SemaphoreSlim(0, sniffers.Count);
            processor = new RecordsProcessor(sniffers, RoomLength, RoomWidth);

            /* Start records processing task */
            processRecordsTask = Task.Factory.StartNew(ProcessRecords);

            /* Start the listening task */
            tcpListener = new TcpListener(IPAddress.Any, Port);
            tcpListener.Start();
            tcpListener.BeginAcceptTcpClient(new AsyncCallback(AcceptClient), tcpListener);

            /* Config sniffers */
            foreach (Sniffer s in sniffers.Values) {
                try {
                    ConfigSniffer(s.Ip);
                    s.Status = Sniffer.SnifferStatus.Running;

                /* Error configuring the sniffers */
                } catch (Exception e) when (e is SocketException || e is IOException) {
                    /* Stop sniffing and notify the caller */
                    Console.WriteLine("Error configuring sniffer " + s.Ip + ": " + e.Message);
                    s.Status = Sniffer.SnifferStatus.Error;
                    StopSniffing();
                    throw e; // Maybe wrap it in a custom Exception
                }
            }
            
            sniffing = true;
        }

        public void StopSniffing() {
            lock (stopSniffingLock) {
                if (!sniffing) {
                    return;
                }

                cancelSniffing.Cancel();

                /* Reset sniffers */
                foreach (Sniffer s in sniffers.Values) {
                    try {
                        if (s.Status == Sniffer.SnifferStatus.Running || s.Status == Sniffer.SnifferStatus.Error) {
                            ResetSniffer(s.Ip);
                            s.Status = Sniffer.SnifferStatus.Stopped;
                        }

                        /* Error in the communication with the sniffer */
                    } catch (Exception e) when (e is SocketException || e is IOException) {
                        s.Status = Sniffer.SnifferStatus.Error;
                        Console.WriteLine("Error resetting sniffer " + s.Ip + ": " + e.Message);
                    }
                }

                /* Stop listening task */
                lock (tcpListener) {
                    tcpListener.Stop();     // Triggers a last call to AcceptClient() if it's not already running
                }
                listeningStopped.WaitOne();
                Console.WriteLine("(StopSniffing) Listening task ended");

                /* Wait for all HandleSniffer tasks to end */
                handleSnifferTasksCount.WaitZero();
                Console.WriteLine("(StopSniffing) All HandleSniffer tasks ended");

                /* Wait for ProccessRecords task to end */
                processRecordsTask.Wait();
                Console.WriteLine("(StopSniffing) ProcessRecords task ended");

                /* Clear structures */
                rawRecords.Clear();
                cancelSniffing.Dispose();
                missingTransmissionsCountdown.Dispose();
                sniffersConfigurationSemaphore.Dispose();

                sniffing = false;
            }
        }
        
        public bool IsSniffing() {
            return sniffing;
        }

        private void AcceptClient(IAsyncResult ar) {
            TcpListener listener = (TcpListener) ar.AsyncState;

            lock (listener) {   // Avoids a call to tcpListener.Stop() during the following code
                if (cancelSniffing.Token.IsCancellationRequested) {
                    Console.WriteLine("(AcceptClient) Ending accept");
                    listeningStopped.Set();
                    return;
                }

                TcpClient client = listener.EndAcceptTcpClient(ar);
                handleSnifferTasksCount.Inc();
                ThreadPool.QueueUserWorkItem(HandleSniffer, client);
                listener.BeginAcceptTcpClient(new AsyncCallback(AcceptClient), listener);
            }
        }

        private void HandleSniffer(object arg) {
            string snifferAddr = null;
            TcpClient client = (TcpClient) arg;

            try {
                /* Retrieve sniffer ip addr */
                snifferAddr = ((IPEndPoint) client.Client.RemoteEndPoint).Address.ToString();

                /* Check the connection is from a known sniffer */
                if (!sniffers.ContainsKey(snifferAddr)) {
                    Console.WriteLine("(HandleSniffer " + snifferAddr + ") Received connection from unknown ip " + snifferAddr);
                    return;
                }

                /* Set a timeout when receiving the ack */
                client.GetStream().ReadTimeout = ERROR_TIMEOUT_SECS * 1000;

                /* Unmarshal the json stream */
                Console.WriteLine("(HandleSniffer " + snifferAddr + ") Receiving records from " + snifferAddr + "...");
                List<Record> records = (List<Record>) serializer.Deserialize(new StreamReader(client.GetStream()), typeof(List<Record>));
                Console.WriteLine("(HandleSniffer " + snifferAddr + ") Records received");
                rawRecords[snifferAddr] = records;

                /* Signal that records for the current sniffer are ready */
                missingTransmissionsCountdown.Signal();

                /* 
                 * Wait to proceed with configuration (awakes when all sniffers have transmitted records)
                 * Throws OperationCanceledException if sniffing is stopped while waiting
                 */
                sniffersConfigurationSemaphore.Wait(cancelSniffing.Token);

                /* Get the current timestamp and send it to the sniffer */
                Configuration conf = new Configuration();
                conf.Timestamp = new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds();
                Console.WriteLine("(HandleSniffer " + snifferAddr + ") Sending configuration... ");
                StreamWriter writer = new StreamWriter(client.GetStream());
                serializer.Serialize(writer, conf);
                writer.Flush();
                Console.WriteLine("(HandleSniffer " + snifferAddr + ") Configuration sent");

            /* Sniffing stopped while waiting to proceed with configuration */
            } catch (OperationCanceledException) {
                /* No action needed, simply close the connection */

            /* Exception in the communication with the sniffer */
            } catch (Exception e) when (e is SocketException || e is IOException) {
                /* Schedule a HandleError task */
                ThreadPool.QueueUserWorkItem(HandleError);
                return;

            } finally {
                /* Shutdown and end connection */
                client.Close();
                Console.WriteLine("(HandleSniffer " + snifferAddr + ") Connection closed");
                handleSnifferTasksCount.Dec();
            }

            return;
        }
        
        private void ProcessRecords() {
            bool countdownSet;

            while (true) {
                /* Wait until all sniffers transmit records */
                Console.WriteLine("(ProcessRecords) Waiting for new records...");
                try {
                    countdownSet = missingTransmissionsCountdown.Wait((SniffingPeriod + ERROR_TIMEOUT_SECS)*1000, cancelSniffing.Token);

                    if (!countdownSet) {
                        /* The timout expired before all sniffers transmitted records, schedule a HandleError task */
                        ThreadPool.QueueUserWorkItem(HandleError);
                        return;
                    }

                /* Sniffing stopped while waiting for records */
                } catch (OperationCanceledException) {
                    return;
                }
                missingTransmissionsCountdown.Reset();

                /* Make a copy of raw records for processing */
                var rawRecordsArray = rawRecords.ToArray();

                /* Enable HandleSniffer tasks to proceed with the configuration of the sniffers */
                sniffersConfigurationSemaphore.Release(sniffers.Count);

                Console.WriteLine("(ProcessRecords) All records ready, beginning processing");

                List<Packet> packets = processor.Process(rawRecordsArray);
                if (packets.Count > 0){
                    int result = db.InsertRecords(packets);
                }

                Console.WriteLine("(ProcessRecords) Records processed");
            }
        }

        private void ConfigSniffer(String ip) {
            /* DEBUG ONLY! Remove before submit */
            if (ip.Equals("127.0.0.1")) return;
            /* DEBUG ONLY! */

            TcpClient client = new TcpClient();
            StreamWriter writer = null;

            try {
                /* Connect to the sniffer */
                Console.WriteLine("Trying to connect to {0}:{1}...", ip, SNIFFER_LISTEN_PORT);
                client.Connect(IPAddress.Parse(ip), SNIFFER_LISTEN_PORT);
                Console.WriteLine("Connected!");

                NetworkStream netStream = client.GetStream();
                writer = new StreamWriter(netStream);

                /* Set a timeout when receiving the ack */
                netStream.ReadTimeout = ERROR_TIMEOUT_SECS*1000;

                /* Prepare configuration data */
                Configuration conf = new Configuration {
                    Timestamp = new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds(),
                    IpAddress = (client.Client.LocalEndPoint as IPEndPoint).Address.ToString(),
                    Port = Port.ToString(),
                    Channel = Channel,
                    SniffingPeriod = SniffingPeriod
                };
            
                /* Send the configuration */
                Console.WriteLine("Sending configuration... ");
                serializer.Serialize(writer, conf);
                writer.Flush();
                netStream.WriteByte(TERMINATION_BYTE);
                Console.WriteLine("Configuration sent");

                byte[] ack = new byte[1];

                /* Receive ACK */
                if (netStream.Read(ack, 0, 1) < 1) {    // Throws IOException if timeout expires
                    throw new IOException("Error receiving ACK");
                }

                /* Check ACK correctness */
                if (ack[0] != ACK_BYTE) {
                    throw new IOException("Received invalid ACK");
                }

            } finally {
                if (writer != null) {
                    writer.Close();
                }
                client.Close();
                Console.WriteLine("Connection closed");
            }
        }

        private void ResetSniffer(String ip) {
            TcpClient client = new TcpClient();

            /* DEBUG ONLY! Remove before submit */
            if (ip.Equals("127.0.0.1")) return;
            /* DEBUG ONLY! */

            try {
                /* Connect to the sniffer */
                Console.WriteLine("Trying to connect to {0}:{1}...", ip, SNIFFER_LISTEN_PORT);
                client.Connect(IPAddress.Parse(ip), SNIFFER_LISTEN_PORT);
                Console.WriteLine("Connected!");

                NetworkStream netStream = client.GetStream();

                /* Send the configuration */
                Console.Write("Sending reset command... ");
                netStream.WriteByte(RESET_BYTE);
                Console.WriteLine("Command sent");

            } finally {
                client.Close();
                Console.WriteLine("Connection closed");
            }
        }

        private void HandleError(object arg) {
            StopSniffing();

            /* Execute registered callback */
            errorHandler();
        }


        private class SynchCounter {
            private uint counter;
            private object counterLock;

            public SynchCounter(uint start) {
                counter = start;
                counterLock = new object();
            }

            public void Inc() {
                lock (counterLock) {
                    counter++;
                }
            }

            public void Dec() {
                lock (counterLock) {
                    if (counter > 0) {
                        counter--;
                    }
                    if (counter == 0) {
                        Monitor.PulseAll(counterLock);
                    }
                }
            }

            public void WaitZero() {
                lock (counterLock) {
                    while (counter > 0) {
                        Monitor.Wait(counterLock);
                    }
                }
            }
        }
    }
}
