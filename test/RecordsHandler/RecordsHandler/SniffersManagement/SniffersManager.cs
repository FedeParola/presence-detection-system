using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RecordsHandler.SniffersManagement {
    class SniffersManager {
        //il comparer eventualmente è da spostare in una classe a parte insieme al processamento/merging dei record
        private IComparer comparer = new Comparer();

        private DBManager db;

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
            db = new DBManager("127.0.0.1", "user", "pass", "pds");

            /* Initialize flags for all sniffers */
            newRecordsFlags["127.0.0.1"] = false;
            newRecordsFlags["127.0.0.2"] = false;
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
                db.closeConn();
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
                for(int i = 0; i < espCount; i++)
                {
                    startIndex[i] = 0;
                }
                Boolean startIndexUpdated;

                /*Eliminate "duplicate" packets (packets with the same hash within the same time window)*/
                var recordsList = rawRecordsArray[0].Value.ToArray();
                for (int i = 0; i < recordsList.Length; i++)
                {
                    nextRecord = false;
                    for (int j = i+1; j < recordsList.Length && nextRecord == false; j++)
                    {
                        if(recordsList[j].Timestamp > recordsList[i].Timestamp + timeTolerance)
                        {
                            nextRecord = true;
                        }
                        else if(recordsList[j].Timestamp > recordsList[i].Timestamp - timeTolerance)
                        {
                            if(recordsList[i].Hash.Equals(recordsList[j].Hash))
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
                            if(recordsList[j].Timestamp > record.Timestamp + timeTolerance)
                            {
                                /*The esp32 we are considering did not capture the record: 
                                 * we can consider the next record and ignore this one*/
                                diffTimestamp = true;
                            }
                            else if(recordsList[j].Timestamp > record.Timestamp - timeTolerance)
                            {
                                if(startIndexUpdated == false)
                                {
                                    startIndex[i] = j;
                                    startIndexUpdated = true;
                                }
                                if(recordsList[j].Hash.Equals(record.Hash))
                                {
                                    /*The esp32 we are considering did capture the record: 
                                     * we have to see if the others did the same*/
                                    found = true;
                                }
                            }
                        }
                        if(diffTimestamp == true || found == false)
                        {
                            nextRecord = true;
                        }
                    }

                    if(nextRecord == false)
                    {
                        /*The record was captured by each and any esp32*/
                        /*Compute position*/
                        double x = 0;
                        double y = 0;
                        /*Insert the record into the db*/
                        db.insertRecord(record.Hash, record.MacAddr, record.Ssid, record.Timestamp, x, y);
                    }
                }

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

    class Comparer : IComparer
    {
        public int Compare(Object x, Object y)
        {
            var a = (KeyValuePair<String, List<Record>>)x;
            var b = (KeyValuePair<String, List<Record>>)y;
            return a.Value.Count.CompareTo(b.Value.Count);
        }
    }
}