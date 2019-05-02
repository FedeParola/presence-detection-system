using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SniffingManagement {
    class SniffingManager {
        /* Following properties are set during the construction and can't be changed (for now) */
        public int Port { get; }
        public int SniffingPeriod { get; }

        /* Sniffing start/stop fields */
        private bool sniffing = false;
        private volatile bool stopping = false; // Can be read/written by multiple threads
        private Task processRecordsTask;
        private AutoResetEvent acceptClientEnded = new AutoResetEvent(false);

        private Dictionary<string, Sniffer> sniffers = new Dictionary<string, Sniffer>();
        private TcpListener tcpListener;
        private JsonSerializer serializer = new JsonSerializer();

        /* 
         * Records received from the last communication with the sniffers.
         * Key: the string representation of the ip addr of the sniffer that sent the records
         * Value: list of raw records that needs to be processed
         */
        private ConcurrentDictionary<String, List<Record>> rawRecords = new ConcurrentDictionary<String, List<Record>>();

        /* For every sniffer (identified by ip) tells if there are new records to process */
        private ConcurrentDictionary<String, bool> newRecordsFlags = new ConcurrentDictionary<string, bool>();
        private AutoResetEvent newRecordsEvent = new AutoResetEvent(false);
        private ManualResetEvent recordsProcessedEvent = new ManualResetEvent(false);


        public SniffingManager(int port, int sniffingPeriod /* DB config to be added */) {
            Port = port;
            SniffingPeriod = sniffingPeriod;
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
            tcpListener.BeginAcceptTcpClient(new AsyncCallback(AcceptClient), null);

            /* Config ESPs */

            sniffing = true;
        }

        public void StopSniffing() {
            stopping = true;

            /* Reset ESPs */

            /* Stop listening task */
            tcpListener.Stop();             // Triggers a last call to AcceptClient() if it's not already running
            acceptClientEnded.WaitOne();    // Wait for the task to end

            /* Stop records processing task */
            newRecordsEvent.Set();          // Wake task in case it is waiting
            processRecordsTask.Wait();      // Wait for the task to end
            
            /* Clear structures */
            newRecordsFlags.Clear();
            rawRecords.Clear();

            sniffing = false;
            stopping = false;
        }

        private void AcceptClient(IAsyncResult ar) {
            if (stopping) {
                Console.WriteLine("(AcceptClient) Ending accept");
                acceptClientEnded.Set();
                return;
            }

            try {
                TcpClient client = tcpListener.EndAcceptTcpClient(ar);
                ThreadPool.QueueUserWorkItem(HandleSniffer, client);
                tcpListener.BeginAcceptTcpClient(new AsyncCallback(AcceptClient), null);
                    
            /* A call to tcpListener.Stop() was performend while accepting a new client */
            } catch (ObjectDisposedException) { 
                Console.WriteLine("(AcceptClient) Ending accept");
                acceptClientEnded.Set();
            }
        }

        private void HandleSniffer(object arg) {
            TcpClient client = (TcpClient) arg;

            /* Retrieve sniffer ip addr */
            string snifferAddr = ((IPEndPoint) client.Client.RemoteEndPoint).Address.ToString();

            /* Check that old records have been processed */
            while (newRecordsFlags[snifferAddr]) {
                recordsProcessedEvent.WaitOne();
                recordsProcessedEvent.Reset();
            }

            Console.WriteLine("(HandleSniffer) Receiving records from " + snifferAddr + "...");

            /* Unmarshal the json stream */
            List<Record> records = (List<Record>) serializer.Deserialize(new StreamReader(client.GetStream()), typeof(List<Record>));
            Console.WriteLine("(HandleSniffer) Records received");

            /* Store records into the map */
            rawRecords[snifferAddr] = records;
            newRecordsFlags[snifferAddr] = true;
            newRecordsEvent.Set();

            /* Get the current timestamp and send it to the sniffer */
            Configuration conf = new Configuration(new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds());
            Console.WriteLine("(HandleSniffer) Sending configuration... ");
            StreamWriter writer = new StreamWriter(client.GetStream());
            serializer.Serialize(writer, conf);
            writer.Flush();
            Console.WriteLine("(HandleSniffer) Configuration sent");

            /* Shutdown and end connection */
            client.Close();
            Console.WriteLine("(HandleSniffer) Connection closed");

            return;
        }

        private void ProcessRecords() {
            while (true) {
                /* Check that all sniffers transmitted new records */
                Console.WriteLine("(ProcessRecords) Checking flags");
                foreach (var key in newRecordsFlags.Keys) {
                    while (!newRecordsFlags[key]) {
                        newRecordsEvent.WaitOne();

                        if (stopping) {
                            Console.WriteLine("(ProcessRecords) Ending ProcessRecords()");
                            return;
                        }
                    }
                }
                Console.WriteLine("(ProcessRecords) All records ready, beginning processing");

                /* Process records here */

                Console.WriteLine("(ProcessRecords) Records processed");

                /* Mark all records as processed */
                foreach (var key in newRecordsFlags.Keys) {
                    newRecordsFlags[key] = false;
                }
                recordsProcessedEvent.Set();
                Console.WriteLine("(ProcessRecords) All flags reset");
            }
        }
    }
}
