using Newtonsoft.Json;
using SniffingManagement.Persistence;
using SniffingManagement.Trilateration;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SniffingManagement {
    class SniffingManager {
        
        private const int SNIFFER_LISTEN_PORT = 13000;
        private const byte ACK_BYTE = (byte) 'A';
        private const byte RESET_BYTE = (byte) 'R';
        private const byte TERMINATION_BYTE = 0;

        private DBManager db = new DBManager("127.0.0.1", "user", "pass", "pds");
        private RecordsProcessor processor;

        /* Following properties are set during the construction and can't be changed (for now) */
        public UInt16 Port { get; }
        public UInt16 SniffingPeriod { get; }
        public Byte Channel { get; }

        /* Sniffing start/stop fields */
        private bool sniffing = false;
        private AutoResetEvent listeningStopped = new AutoResetEvent(false);
        private Task processRecordsTask;
        private SynchCounter handleSnifferTasksCount = new SynchCounter(0);
        private CancellationTokenSource cancelSniffing;

        private Dictionary<string, Sniffer> sniffers = new Dictionary<string, Sniffer>();
        private TcpListener tcpListener;
        private JsonSerializer serializer = new JsonSerializer();
        /* 
         * Records received from the last communication with the sniffers.
         * Key: the string representation of the ip addr of the sniffer that sent the records
         * Value: list of raw records that needs to be processed
         */
        private ConcurrentDictionary<String, List<Record>> rawRecords = new ConcurrentDictionary<String, List<Record>>();

        /* N of missing transmissions from sniffers before proceeding with processing */
        private CountdownEvent missingTransmissionsCountdown;
        /* Enable HandleSniffer tasks to send the configuration to the sniffer */
        private SemaphoreSlim sniffersConfigurationSemaphore;


        public SniffingManager(UInt16 port, UInt16 sniffingPeriod, Byte channel /* DB config to be added */) {
            Port = port;
            SniffingPeriod = sniffingPeriod;
            Channel = channel;
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
            /* Check enough sniffers configured */
            if (sniffers.Count < 1 /* CAMBIALO A 2!!! */) {
                throw new InvalidOperationException("Needed at least 2 sniffers to start sniffing");
            }

            /* Initialize structures */
            cancelSniffing = new CancellationTokenSource();
            missingTransmissionsCountdown = new CountdownEvent(sniffers.Count);
            sniffersConfigurationSemaphore = new SemaphoreSlim(0, sniffers.Count);
            processor = new RecordsProcessor(sniffers);

            /* Start records processing task */
            processRecordsTask = Task.Factory.StartNew(ProcessRecords);

            /* Start the listening task */
            tcpListener = new TcpListener(IPAddress.Any, Port);
            tcpListener.Start();
            tcpListener.BeginAcceptTcpClient(new AsyncCallback(AcceptClient), tcpListener);

            /* Config sniffers */
            foreach (Sniffer s in sniffers.Values) {
                ConfigSniffer(s.Ip);
                s.Status = Sniffer.SnifferStatus.Running;
            }

            sniffing = true;
        }

        public void StopSniffing() {
            cancelSniffing.Cancel();

            /* Reset sniffers */
            foreach (Sniffer s in sniffers.Values) {
                ResetSniffer(s.Ip);
                s.Status = Sniffer.SnifferStatus.Stopped;
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

        private void AcceptClient(IAsyncResult ar) {
            TcpListener listener = (TcpListener) ar.AsyncState;

            /* Avoid a call to tcpListener.Stop() during the following code */
            lock (listener) {
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

        /* TODO: handle exceptions */
        private void HandleSniffer(object arg) {
            TcpClient client = (TcpClient) arg;

            /* Retrieve sniffer ip addr */
            string snifferAddr = ((IPEndPoint) client.Client.RemoteEndPoint).Address.ToString();

            /* Check the connection is from a known sniffer */
            if (!sniffers.ContainsKey(snifferAddr)) {
                Console.WriteLine("(HandleSniffer " + snifferAddr + ") Received connection from unknown ip " + snifferAddr);
                client.Close();
                Console.WriteLine("(HandleSniffer " + snifferAddr + ") Connection closed");
                return;
            }

            Console.WriteLine("(HandleSniffer " + snifferAddr + ") Receiving records from " + snifferAddr + "...");

            /* Unmarshal the json stream */
            List<Record> records = (List<Record>) serializer.Deserialize(new StreamReader(client.GetStream()), typeof(List<Record>));
            Console.WriteLine("(HandleSniffer " + snifferAddr + ") Records received");
            rawRecords[snifferAddr] = records;

            missingTransmissionsCountdown.Signal();

            try {
                sniffersConfigurationSemaphore.Wait(cancelSniffing.Token);

            } catch (OperationCanceledException) {
                client.Close();
                Console.WriteLine("(HandleSniffer " + snifferAddr + ") Connection closed");
                handleSnifferTasksCount.Dec();
                return;
            }

            /* Get the current timestamp and send it to the sniffer */
            Configuration conf = new Configuration();
            conf.Timestamp = new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds();
            Console.WriteLine("(HandleSniffer " + snifferAddr + ") Sending configuration... ");
            StreamWriter writer = new StreamWriter(client.GetStream());
            serializer.Serialize(writer, conf);
            writer.Flush();
            Console.WriteLine("(HandleSniffer " + snifferAddr + ") Configuration sent");

            /* Shutdown and end connection */
            client.Close();
            Console.WriteLine("(HandleSniffer " + snifferAddr + ") Connection closed");

            handleSnifferTasksCount.Dec();

            return;
        }

        private void ProcessRecords() {
            while (true) {
                /* Wait until all sniffers transmit records */
                Console.WriteLine("(ProcessRecords) Waiting for new records...");
                try {
                    missingTransmissionsCountdown.Wait(cancelSniffing.Token);

                } catch (OperationCanceledException) {
                    return;
                }
                missingTransmissionsCountdown.Reset();

                Console.WriteLine("(ProcessRecords) All records ready, beginning processing");

                List<Packet> packets = processor.Process(rawRecords.ToArray());
                int result = db.InsertRecords(packets);

                Console.WriteLine("(ProcessRecords) Records processed");

                /* Enable HandleSniffer tasks to proceed with the configuration of the sniffers */
                sniffersConfigurationSemaphore.Release(sniffers.Count);
            }
        }

        /* TODO: handle exceptions */
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

                /* Set a 10 seconds timeout when receiving the ack */
                netStream.ReadTimeout = 10000;

                /* Prepare configuration data */
                Configuration conf = new Configuration {
                    Timestamp = new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds(),
                    IpAddress = (client.Client.LocalEndPoint as IPEndPoint).Address.ToString(),
                    Port = Port.ToString(), // Why don't we use a number???
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

        /* TODO: handle exceptions */
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
