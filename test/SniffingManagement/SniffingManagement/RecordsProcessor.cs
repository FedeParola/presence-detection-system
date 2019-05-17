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

        public List<Packet> Process(KeyValuePair<String, List<Record>>[] rawRecords)
        {
            List<Packet> packets = new List<Packet>();

            bool found, diffTimestamp, nextRecord;
            int espCount = sniffers.Count;
            int[] RSSIs = new int[espCount];
            /*Optimization: Sort the array of rawRecords in ascending order for the number of records associated to each esp*/
            Array.Sort(rawRecords, 0, espCount, comparer);
            /*Optimization: mark the first packet considered for each list of packets; when working on the next packet
             * (which will be more recent) we can start looking for it among the ones captured by the others esp32 
             * starting from the marked packet and ignoring the previous ones (they will be older)*/
            int[] startIndex = new int[espCount-1]; //creates an Array of int with default value 0
            Boolean startIndexUpdated;

            List<Record> firstRecordsList = rawRecords[0].Value;
            RemoveDuplicates(firstRecordsList);

            /*Go through the records of the first esp32*/
            foreach (Record record in firstRecordsList){
                RSSIs[0] = record.Rssi;
                nextRecord = false;
                /*Go through each esp32*/
                for (int i = 1; i < espCount && nextRecord == false; i++){
                    List<Record> recordsList = rawRecords[i].Value;
                    found = false;
                    diffTimestamp = false;
                    startIndexUpdated = false;
                    /*Go through each packet captured by the esp32*/
                    for (int j = startIndex[i-1]; j < recordsList.Count && found == false && diffTimestamp == false; j++){
                        /*index i represents the i-th esp while index j represents the j-th record captured by the i-th esp*/
                        if (recordsList[j].Timestamp > record.Timestamp + TIME_TOLERANCE){
                            /*The i-th esp did not capture the record: we go to the next record and ignore this one*/
                            diffTimestamp = true;
                        }
                        else if (recordsList[j].Timestamp > record.Timestamp - TIME_TOLERANCE){
                            if (startIndexUpdated == false){
                                startIndex[i-1] = j;
                                startIndexUpdated = true;
                            }
                            if (recordsList[j].Hash.Equals(record.Hash)){
                                /*The i-th esp  did capture the record: we have to see if the others did the same*/
                                found = true;
                                RSSIs[i] = recordsList[j].Rssi;
                            }
                        }
                    }
                    if (diffTimestamp == true || found == false){
                        nextRecord = true;
                    }
                }

                if (nextRecord == false){
                    /*The record was captured by each and any esp*/
                    /*Compute position*/
                    TrilaterationCalculator TC = new TrilaterationCalculator();
                    for (int i = 0; i < espCount; i++){
                        double d = RssiToMeters(RSSIs[i]);
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

        /*Eliminate "duplicate" packets (packets with the same hash within the same time window)*/
        private void RemoveDuplicates(List<Record> recordsList)
        {
            bool next;
            for (int i = 0; i < recordsList.Count; i++){
                next = false;
                for (int j = i + 1; j < recordsList.Count && next == false; j++){
                    if (recordsList[j].Timestamp > recordsList[i].Timestamp + TIME_TOLERANCE){
                        next = true;
                    }
                    else if (recordsList[j].Timestamp > recordsList[i].Timestamp - TIME_TOLERANCE){
                        if (recordsList[i].Hash.Equals(recordsList[j].Hash)){
                            recordsList.RemoveAt(j);
                        }
                    }
                }
            }
        }

        private double RssiToMeters(int RSSI)
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
