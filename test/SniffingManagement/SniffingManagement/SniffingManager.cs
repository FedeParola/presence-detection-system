using Newtonsoft.Json;
using SniffingManagement.Persistence;
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
        /*Il comparer e la parte di codice relativa al processamento dei record è da organizzare meglio 
         (creare una classe apposita!)*/
        class Comparer : IComparer
        {
            public int Compare(Object x, Object y)
            {
                var a = (KeyValuePair<String, List<Record>>)x;
                var b = (KeyValuePair<String, List<Record>>)y;
                return a.Value.Count.CompareTo(b.Value.Count);
            }
        }
        private IComparer comparer = new Comparer();
        private DBManager db;

        private const int SNIFFER_LISTEN_PORT = 13000;
        private const byte ACK_BYTE = (byte) 'A';
        private const byte RESET_BYTE = (byte) 'R';
        private const byte TERMINATION_BYTE = 0;

        /* Following properties are set during the construction and can't be changed (for now) */
        public UInt16 Port { get; }
        public UInt16 SniffingPeriod { get; }
        public Byte Channel { get; }

        /* Sniffing start/stop fields */
        private bool sniffing = false;
        private volatile bool stopping = false; // Can be read/written by multiple threads
        private AutoResetEvent listeningStopped = new AutoResetEvent(false);
        private Task processRecordsTask;
        private SynchCounter handleSnifferTasksCount = new SynchCounter(0);

        private Dictionary<string, Sniffer> sniffers = new Dictionary<string, Sniffer>();
        private TcpListener tcpListener;
        private JsonSerializer serializer = new JsonSerializer();
        /* 
         * Records received from the last communication with the sniffers.
         * Key: the string representation of the ip addr of the sniffer that sent the records
         * Value: list of raw records that needs to be processed
         */
        private ConcurrentDictionary<String, List<Record>> rawRecords = new ConcurrentDictionary<String, List<Record>>();
        /* For every sniffer (-identified by ip) tells if there are new records to process */
        private Dictionary<String, bool> newRecordsFlags = new Dictionary<string, bool>();


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
            foreach (string ip in sniffers.Keys) {
                newRecordsFlags[ip] = false;
            }

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
            stopping = true;

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

            /* Wake ProcessRecords and HandleSniffer tasks eventually waiting */
            lock (newRecordsFlags) {
                Monitor.Pulse(newRecordsFlags);
            }

            /* Wait for all HandleSniffer tasks to end */
            handleSnifferTasksCount.WaitZero();
            Console.WriteLine("(StopSniffing) All HandleSniffer tasks ended");

            /* Wait for ProccessRecords task to end */
            processRecordsTask.Wait();
            
            /* Clear structures */
            newRecordsFlags.Clear();
            rawRecords.Clear();

            sniffing = false;
            stopping = false;
        }

        private void AcceptClient(IAsyncResult ar) {
            TcpListener listener = (TcpListener) ar.AsyncState;

            /* Avoid a call to tcpListener.Stop() during the following code */
            lock (listener) {
                if (stopping) {
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
                Console.WriteLine("(HandleSniffer) Received connection from unknown ip " + snifferAddr);
                client.Close();
                Console.WriteLine("(HandleSniffer) Connection closed");
                return;
            }

            Console.WriteLine("(HandleSniffer) Receiving records from " + snifferAddr + "...");

            /* Unmarshal the json stream */
            List<Record> records = (List<Record>) serializer.Deserialize(new StreamReader(client.GetStream()), typeof(List<Record>));
            Console.WriteLine("(HandleSniffer) Records received");

            /* Get the current timestamp and send it to the sniffer */
            Configuration conf = new Configuration();
            conf.Timestamp = new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds();
            Console.WriteLine("(HandleSniffer) Sending configuration... ");
            StreamWriter writer = new StreamWriter(client.GetStream());
            serializer.Serialize(writer, conf);
            writer.Flush();
            Console.WriteLine("(HandleSniffer) Configuration sent");

            /* Shutdown and end connection */
            client.Close();
            Console.WriteLine("(HandleSniffer) Connection closed");

            lock (newRecordsFlags) {
                /* Check that old records have been processed */
                while (newRecordsFlags[snifferAddr]) {
                    /* Check if stopping required */
                    if (stopping) {
                        handleSnifferTasksCount.Dec();
                        return;
                    }

                    /* Wait for a change in flags */
                    Monitor.Wait(newRecordsFlags);
                }

                /* Store records into the map */
                rawRecords[snifferAddr] = records;
                newRecordsFlags[snifferAddr] = true;
                Monitor.PulseAll(newRecordsFlags); 
            }

            handleSnifferTasksCount.Dec();

            return;
        }

        private void ProcessRecords() {
            while (true) {
                /* Check if stopping required */
                if (stopping) {
                    Console.WriteLine("(ProcessRecords) Stopping ProcessRecords()");
                    return;
                }

                /* Check that all sniffers transmitted new records */
                lock (newRecordsFlags) {
                    foreach (var key in sniffers.Keys) {
                        while (!newRecordsFlags[key]) {
                            /* Check if stopping required */
                            if (stopping) {
                                Console.WriteLine("(ProcessRecords) Stopping ProcessRecords()");
                                return;
                            }

                            /* Wait for a change in flags */
                            Console.WriteLine("(ProcessRecords) Waiting for new records...");
                            Monitor.Wait(newRecordsFlags);
                        }
                    }
                }

                Console.WriteLine("(ProcessRecords) All records ready, beginning processing");

                /* Process records here */

                /*PARTE DI CODICE DA ORGANIZZARE MEGLIO, CREARE UNA CLASSE APPOSITA!*/
                Boolean found;
                Boolean diffTimestamp;
                Boolean nextRecord;
                long timeTolerance = 1 * 1000; //1 second
                var rawRecordsArray = rawRecords.ToArray();
                int espCount = rawRecordsArray.Length;
                /*Opt: Sort the array of rawRecords in ascending order for the number of records associated to each esp*/
                Array.Sort(rawRecordsArray, 0, espCount, comparer);
                /*Opt: mark the first packet considered for each list of packets; when working on the next packet
                 * (which will be more recent) we can start looking for it among the ones captured by the others esp32 
                 * starting from the marked packet and ignoring the previous ones (they will be older)*/
                int[] startIndex = new int[espCount];
                for (int i = 0; i < espCount; i++)
                {
                    startIndex[i] = 0;
                }
                Boolean startIndexUpdated;

                /*Eliminate "duplicate" packets (packets with the same hash within the same time window)*/
                var recordsList = rawRecordsArray[0].Value.ToArray();
                for (int i = 0; i < recordsList.Length; i++)
                {
                    nextRecord = false;
                    for (int j = i + 1; j < recordsList.Length && nextRecord == false; j++)
                    {
                        if (recordsList[j].Timestamp > recordsList[i].Timestamp + timeTolerance)
                        {
                            nextRecord = true;
                        }
                        else if (recordsList[j].Timestamp > recordsList[i].Timestamp - timeTolerance)
                        {
                            if (recordsList[i].Hash.Equals(recordsList[j].Hash))
                            {
                                rawRecordsArray[0].Value.RemoveAt(j);
                            }
                        }
                    }
                }

                /*Go through the records of the first esp32*/
                foreach (var record in rawRecordsArray[0].Value)
                {
                    nextRecord = false;
                    /*Go through each esp32*/
                    for (int i = 1; i < espCount && nextRecord == false; i++)
                    {
                        recordsList = rawRecordsArray[i].Value.ToArray();
                        found = false;
                        diffTimestamp = false;
                        startIndexUpdated = false;
                        /*Go through each packet captured by the esp32*/
                        for (int j = startIndex[i]; j < recordsList.Length && found == false && diffTimestamp == false; j++)
                        {
                            if (recordsList[j].Timestamp > record.Timestamp + timeTolerance)
                            {
                                /*The esp32 we are considering did not capture the record: 
                                 * we can consider the next record and ignore this one*/
                                diffTimestamp = true;
                            }
                            else if (recordsList[j].Timestamp > record.Timestamp - timeTolerance)
                            {
                                if (startIndexUpdated == false)
                                {
                                    startIndex[i] = j;
                                    startIndexUpdated = true;
                                }
                                if (recordsList[j].Hash.Equals(record.Hash))
                                {
                                    /*The esp32 we are considering did capture the record: 
                                     * we have to see if the others did the same*/
                                    found = true;
                                }
                            }
                        }
                        if (diffTimestamp == true || found == false)
                        {
                            nextRecord = true;
                        }
                    }

                    if (nextRecord == false)
                    {
                        /*The record was captured by each and any esp32*/
                        /*Compute position*/
                        /*double p=pow(10,(((-52) - ppkt->rx_ctrl.rssi) / (10 * 1.8)));*/
                        double x = 0;
                        double y = 0;
                        /*Insert the record into the db*/
                        db.InsertRecord(record.Hash, record.MacAddr, record.Ssid, record.Timestamp, x, y);
                    }
                }

                Console.WriteLine("(ProcessRecords) Records processed");

                /* Mark all records as processed */
                lock (newRecordsFlags) {
                    foreach (var key in sniffers.Keys) {
                        newRecordsFlags[key] = false;
                    }
                    Monitor.PulseAll(newRecordsFlags);
                }
                Console.WriteLine("(ProcessRecords) All flags reset");
            }
        }

        /* TODO: handle exceptions */
        private void ConfigSniffer(String ip) {
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
