using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RecordsHandler.SniffersManagement {
    class SniffersManager {
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

        public Int32 Port {
            set; get;
        }

        public SniffersManager() {
            Port = 13000;

            /* Initialize flags for all sniffers */
            newRecordsFlags["127.0.0.1"] = false;
        }

        public void ListenSniffers() {
            TcpListener server = null;

            ThreadPool.QueueUserWorkItem(ProcessRecords);

            try {
                /* Create a TcpListener on the given port */
                server = new TcpListener(IPAddress.Any, Port);

                /* Start listening for client requests */
                server.Start();

                /* Enter the listening loop */
                while (true) {
                    Console.WriteLine("(LISTENER) Waiting for a connection...");

                    /* Perform a blocking call to accept requests */
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("(LISTENER) Connected!");

                    /* Delegate the client management to a new thread */
                    ThreadPool.QueueUserWorkItem(HandleSniffer, client);
                }

            } catch (SocketException e) {
                Console.WriteLine("(LISTENER)  SocketException: {0}", e);

            } finally {
                /* Stop listening for new clients */
                server.Stop();
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

            Console.WriteLine("(HANDLER) Receiving records from " + snifferAddr + "...");

            /* Unmarshal the json stream */
            List<Record> records = (List<Record>) serializer.Deserialize(new StreamReader(client.GetStream()), typeof(List<Record>));
            Console.WriteLine("(HANDLER) Records received:");

            /* Print the records */
            foreach (Record r in records) {
                Console.WriteLine("{{\n" +
                    "\t\"timestamp\":{4},\n" +
                    "\t\"ssid\":\"{0}\",\n" +
                    "\t\"mac\":\"{1}\",\n" +
                    "\t\"rssi\":{2},\n" +
                    "\t\"hash\":\"{3}\"\n" +
                    "}}", r.Ssid, r.MacAddr, r.Rssi, r.Hash, r.Timestamp);
            }

            /* Store records into the map */
            rawRecords[snifferAddr] = records;
            newRecordsFlags[snifferAddr] = true;
            newRecordsEvent.Set();

            /* Get the current timestamp and send it to the sniffer */
            Configuration conf = new Configuration(new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds());
            Console.WriteLine("(HANDLER) Sending configuration... ");
            StreamWriter writer = new StreamWriter(client.GetStream());
            serializer.Serialize(writer, conf);
            writer.Flush();
            Console.WriteLine("(HANDLER) Configuration sent.");

            /* Shutdown and end connection */
            client.Close();
            Console.WriteLine("(HANDLER) Connection closed.\n");

            return;
        }

        private void ProcessRecords(object arg) {
            while (true) {
                /* Check that all sniffers transmitted new records */
                Console.WriteLine("(PROCESSOR) Checking flags");
                foreach (var key in newRecordsFlags.Keys) {
                    while (!newRecordsFlags[key]) {
                        newRecordsEvent.WaitOne();
                    }
                }
                Console.WriteLine("(PROCESSOR) All records ready, beginning processing");

                /* Process records here */

                Console.WriteLine("(PROCESSOR) Records processed");

                /* Mark all records as processed */
                foreach (var key in newRecordsFlags.Keys) {
                    newRecordsFlags[key] = false;
                }
                recordsProcessedEvent.Set();
                Console.WriteLine("(PROCESSOR) All flags reset");
            }
        }
    }
}