using SniffingManagement.Trilateration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SniffingManagement
{
    class RecordsProcessor
    {
        private Dictionary<String, Sniffer> sniffers;
        private IComparer comparer;

        private const long TIME_TOLERANCE = 1 * 1000; //1 second
        private const int MEASURED_POWER = -52;
        private const double ENVIRONMENTAL_FACTOR = 1.8;

        public RecordsProcessor(Dictionary<string, Sniffer> sniffers)
        {
            comparer = new Comparer();
            this.sniffers = sniffers;
        }

        /*GESTIRE ECCEZIONI!*/
        public List<Packet> process(KeyValuePair<String, List<Record>>[] rawRecords)
        {
            List<Packet> packets = new List<Packet>();

            bool found, diffTimestamp, nextRecord;
            int espCount = sniffers.Count;
            /*Opt: Sort the array of rawRecords in ascending order for the number of records associated to each esp*/
            Array.Sort(rawRecords, 0, espCount, comparer);
            /*Opt: mark the first packet considered for each list of packets; when working on the next packet
             * (which will be more recent) we can start looking for it among the ones captured by the others esp32 
             * starting from the marked packet and ignoring the previous ones (they will be older)*/
            int[] startIndex = new int[espCount];
            for (int i = 0; i < espCount; i++){
                startIndex[i] = 0;
            }
            Boolean startIndexUpdated;

            int[] RSSIs = new int[espCount];

            /*Eliminate "duplicate" packets (packets with the same hash within the same time window)*/
            var recordsList = rawRecords[0].Value.ToArray();
            for (int i = 0; i < recordsList.Length; i++)
            {
                nextRecord = false;
                for (int j = i + 1; j < recordsList.Length && nextRecord == false; j++)
                {
                    if (recordsList[j].Timestamp > recordsList[i].Timestamp + TIME_TOLERANCE)
                    {
                        nextRecord = true;
                    }
                    else if (recordsList[j].Timestamp > recordsList[i].Timestamp - TIME_TOLERANCE)
                    {
                        if (recordsList[i].Hash.Equals(recordsList[j].Hash))
                        {
                            rawRecords[0].Value.RemoveAt(j);
                        }
                    }
                }
            }

            //List<Record[]> recordsLists = new List<Record[]>();
            //for (int i = 1; i < espCount; i++)
            //{
            //    recordsLists[i -1] = rawRecordsArray[i].Value.ToArray();
            //}

            /*Go through the records of the first esp32*/
            foreach (var record in rawRecords[0].Value)
            {
                RSSIs[0] = record.Rssi;
                nextRecord = false;
                /*Go through each esp32*/
                for (int i = 1; i < espCount && nextRecord == false; i++)
                {
                    recordsList = rawRecords[i].Value.ToArray();
                    found = false;
                    diffTimestamp = false;
                    startIndexUpdated = false;
                    /*Go through each packet captured by the esp32*/
                    for (int j = startIndex[i]; j < recordsList.Length && found == false && diffTimestamp == false; j++)
                    {
                        /*index i represents the i-th esp while index j represents the j-th record captured by the esp*/
                        if (recordsList[j].Timestamp > record.Timestamp + TIME_TOLERANCE)
                        {
                            /*The esp32 we are considering did not capture the record: 
                             * we can consider the next record and ignore this one*/
                            diffTimestamp = true;
                        }
                        else if (recordsList[j].Timestamp > record.Timestamp - TIME_TOLERANCE)
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
                                RSSIs[i] = recordsList[j].Rssi;
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
                    TrilaterationCalculator TC = new TrilaterationCalculator();
                    for (int i = 0; i < espCount; i++)
                    {
                        double d = rssiToMeters(RSSIs[i]);
                        Sniffer s = sniffers[rawRecords[i].Key];
                        Measurement m = new Measurement(s.Position, d);
                        TC.AddMeasurement(m);
                    }
                    Point position = TC.Compute();

                    Packet p = new Packet()
                    {
                        Hash = record.Hash,
                        MacAddr = record.MacAddr,
                        Ssid = record.Ssid,
                        Timestamp = record.Timestamp,
                        Position = position
                    };
                    packets.Add(p);
                }
            }

            return packets;
        }

        /*private Point computePosition()
        {
            TrilaterationCalculator TC = new TrilaterationCalculator();
            for (int i = 0; i < espCount; i++)
            {
                double d = rssiToMeters(RSSIs[i]);
                Sniffer s = sniffers[rawRecords[i].Key];
                Measurement m = new Measurement(s.Position, d);
                TC.AddMeasurement(m);
            }
            Point p = TC.Compute();
        }*/

        private double rssiToMeters(int RSSI)
        {
            return Math.Pow(10, (((MEASURED_POWER) - RSSI) / (10 * ENVIRONMENTAL_FACTOR)));
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
}
