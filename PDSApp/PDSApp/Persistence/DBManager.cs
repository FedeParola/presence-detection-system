﻿using System;
using System.Collections.Generic;
using System.Globalization;
using Npgsql;
using PDSApp.SniffingManagement.Trilateration;

namespace PDSApp.Persistence {
    /*TODO: Test all the queries!*/
    class DBManager
    {
        private NpgsqlConnection conn;
        private NumberFormatInfo nfi = new NumberFormatInfo();
        
        public DBManager(String host, String user, String pass, String database){
            conn = new NpgsqlConnection("Host = " + host + ";" + 
                                        "Username = " + user + ";" +
                                        "Password = " + pass + ";" +
                                        "Database = " + database);

            //Creates the table 'Record' if it doesn't exist
            CreateRecordsTable();
            CreateLocalRecordView();

            nfi.NumberDecimalSeparator = ".";
        }

        private void CreateRecordsTable(){

            using (var cmd = new NpgsqlCommand()){
                cmd.Connection = conn;
                cmd.CommandText =
                    "CREATE TABLE IF NOT EXISTS \"Record\"(" +
                    "\"Id\" bigserial PRIMARY KEY," +
                    "\"Hash\" character(32) NOT NULL," +
                    "\"MAC\" character(17) NOT NULL," +
                    "\"SSID\" character varying(256) NOT NULL," +
                    "\"Timestamp\" bigint NOT NULL," +
                    "\"SequenceCtrl\" int NOT NULL," +
                    "\"X\" real NOT NULL," +
                    "\"Y\" real NOT NULL)";
                conn.Open();
                cmd.ExecuteNonQuery();
                conn.Close();
            }
        }

        private void CreateLocalRecordView() {
            using (var cmd = new NpgsqlCommand()) {
                cmd.Connection = conn;
                cmd.CommandText =
                    "CREATE OR REPLACE VIEW \"LocalRecord\" AS " +
                    "SELECT * FROM \"Record\" " +
                    "WHERE((('x' || substr(\"MAC\", 2, 1)))::bit(4) & '0010') = '0010';";
                conn.Open();
                cmd.ExecuteNonQuery();
                conn.Close();
            }
        }

        public void CloseConn(){
            if (conn != null)
                ((IDisposable)conn).Dispose();
        }

        /*The use of parameters should prevent from (non intentional) SQL injection because of the SSID*/
        public int InsertRecords(List<Packet> packets){
            int returnValue;
            int counter = 0;

            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText =
                    "INSERT INTO \"Record\" (\"Hash\", \"MAC\", \"SSID\", \"Timestamp\", \"SequenceCtrl\", \"X\", \"Y\") VALUES ";
                /*The drawback of the use of parameters this way is that we could end with a lot of parameters
                 (is that the case or usually we end up with not so many packets since they represent the packets
                 generated by smartphones in just one room (more or less) and also have to be collected by every esp?)
                 The alternative could be to use just one parameter and have one insert for each packet*/
                foreach (Packet p in packets){
                    cmd.CommandText += String.Format("(" +
                                        "'" + p.Hash + "', " +
                                        "'" + p.MacAddr + "', " +
                                        "@ssid{0}," +
                                        p.Timestamp + ", " +
                                        p.SequenceCtrl + ", " +
                                        p.Position.X.ToString(nfi) + ", " +
                                        p.Position.Y.ToString(nfi) +
                                        "), ", counter);
                    cmd.Parameters.AddWithValue("@ssid" + counter, p.Ssid);
                    counter++;
                }
                cmd.CommandText = cmd.CommandText.Remove(cmd.CommandText.Length - 2, 2) + ";";

                conn.Open();
                returnValue = cmd.ExecuteNonQuery();
                conn.Close();
            }

            /*number of rows affected... consider returning void*/
            return returnValue;
        }

        /*'timeInterval' represents the length (in milliseconds) of the previous period of time during which
         we have to count the distinct detected devices*/
        public int CountDetectedDevices(long timeInterval){
            int result;
            long startingTimeInstant = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds() - timeInterval;

            using (var cmd = new NpgsqlCommand()){
                cmd.Connection = conn;
                cmd.CommandText = 
                    "SELECT COUNT (DISTINCT \"MAC\") " +
                    "FROM \"Record\" WHERE \"Timestamp\" >= " + startingTimeInstant + ";";
                conn.Open();
                using (var reader = cmd.ExecuteReader()){
                    reader.Read();
                    result = reader.GetInt32(0);
                }
                conn.Close();
            }

            return result;
        }

        /*'timeInterval' represents the length (in milliseconds) of the previous period of time considered for the statistics*/
        public List<Tuple<String, Location>> EstimateDevicesPosition(long timeInterval) {
            List<Tuple<String, Location>> devicesPositions = new List<Tuple<String, Location>>();
            long startingTimeInstant = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds() - timeInterval;

            using (var cmd = new NpgsqlCommand()) {
                cmd.Connection = conn;
                cmd.CommandText =
                "SELECT \"MAC\", \"Timestamp\", \"X\", \"Y\" FROM(" +
                "   SELECT \"MAC\", \"Timestamp\", \"X\", \"Y\", ROW_NUMBER () OVER(PARTITION BY \"MAC\"" +
                                                                                   "ORDER BY \"Timestamp\" DESC)" +
                "   FROM \"Record\"" +
                "   WHERE \"Timestamp\" >= " + startingTimeInstant +
                ") \"Devices\"" +
                "WHERE ROW_NUMBER = 1;";
                conn.Open();
                using (var reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        Point p = new Point(reader.GetDouble(2), reader.GetDouble(3));
                        Location l = new Location(p, reader.GetInt64(1));
                        devicesPositions.Add(new Tuple<String, Location>(reader.GetString(0), l));
                    }
                }
                conn.Close();
            }

            return devicesPositions;
        }

        /*OPTIONAL EXTENSIONS*/

        /* Get the MACs of the n devices that sent the most packets in the given time interval */
        public List<string> GetTalkativeDevices(long startInstant, long stopInstant, int devicesCount) {
            var devices = new List<string>();

            using (var cmd = new NpgsqlCommand()) {
                cmd.Connection = conn;
                cmd.CommandText =
                    "   SELECT \"MAC\" " +
                    "   FROM \"Record\" " +
                    "   WHERE \"Timestamp\" >= " + startInstant + " AND " +
                    "         \"Timestamp\" < " + stopInstant +
                    "   GROUP BY \"MAC\" " +
                    "   ORDER BY COUNT(*) DESC" +
                    "   LIMIT " + devicesCount + ";";
                conn.Open();
                using (var reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        devices.Add(reader.GetString(0));
                    }
                }
                conn.Close();
            }

            return devices;
        }

        /*The 'devicesCount' represents the number of 'talkative' devices considered (the top n)*/
        public Dictionary<long, List<Tuple<string, int>>> GetTalkativeDevices(long startInstant, long stopInstant,
            long timeStep, int devicesCount) {
            var detections = new Dictionary<long, List<Tuple<string, int>>>();

            using (var cmd = new NpgsqlCommand()){
                cmd.Connection = conn;
                cmd.CommandText =
                    "SELECT \"MAC\", \"Timestamp\"-(\"Timestamp\"%" + timeStep + ") AS Interval, COUNT(*) " +
                    "FROM \"Record\" " +
                    "WHERE \"Timestamp\" >= " + startInstant + " AND " +
                    "      \"Timestamp\" < " + stopInstant + " AND " +
                    "      \"MAC\" IN (" +
                    "   SELECT \"MAC\" " +
                    "   FROM \"Record\" " +
                    "   WHERE \"Timestamp\" >= " + startInstant + " AND " +
                    "         \"Timestamp\" < " + stopInstant +
                    "   GROUP BY \"MAC\" " +  
                    "   ORDER BY COUNT(*) DESC" +
                    "   LIMIT " + devicesCount + ")" +
                    "   GROUP BY \"MAC\", Interval;";
                conn.Open();
                using (var reader = cmd.ExecuteReader()){
                    while (reader.Read()){
                        if (!detections.ContainsKey(reader.GetInt64(1))) {
                            detections.Add(reader.GetInt64(1), new List<Tuple<string, int>>());
                        }
                        detections[reader.GetInt64(1)].Add(new Tuple<string, int>(reader.GetString(0), reader.GetInt32(2)));
                    }
                }
                conn.Close();
            }           

            return detections;
        }

        public int GetLocalAddressesCount(long startInstant, long stopInstant) {
            int count;

            using (var cmd = new NpgsqlCommand()) {
                cmd.Connection = conn;
                cmd.CommandText =
                "SELECT COUNT(DISTINCT(\"MAC\")) " +
                "FROM \"LocalRecord\" " +
                "WHERE \"Timestamp\" >= " + startInstant + " AND \"Timestamp\" < " + stopInstant + ";";
                conn.Open();
                using (var reader = cmd.ExecuteReader()) {
                    reader.Read();
                    count = reader.GetInt32(0);
                }
                conn.Close();
            }

            return count;
        }

        /* Packets returned miss the Hash and the Ssid fields */
        public List<Packet> GetLocalPackets(long startInstant, long stopInstant) {
            List<Packet> packets = new List<Packet>();

            using (var cmd = new NpgsqlCommand()) {
                cmd.Connection = conn;
                cmd.CommandText =
                "SELECT \"MAC\", \"SSID\", \"Timestamp\", \"SequenceCtrl\", \"X\", \"Y\" " +
                "FROM \"LocalRecord\" " +
                "WHERE \"Timestamp\" >= " + startInstant + " AND \"Timestamp\" < " + stopInstant + ";";
                conn.Open();
                using (var reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        packets.Add(new Packet() {
                            MacAddr = reader.GetString(0),
                            Ssid = reader.GetString(1),
                            Timestamp = reader.GetInt64(2),
                            SequenceCtrl = reader.GetInt32(3),
                            Position = new Point(reader.GetDouble(4), reader.GetDouble(5))
                        });
                    }
                }
                conn.Close();
            }

            return packets;
        }

        public List<String> GetAddrList(long startInstant, long stopInstant){
            List<String> addrList = new List<String>();

            using (var cmd = new NpgsqlCommand()){
                cmd.Connection = conn;
                cmd.CommandText =
                "SELECT DISTINCT \"MAC\" " +
                "FROM \"Record\" " +
                "WHERE \"Timestamp\" >= " + startInstant + " AND" +
                "         \"Timestamp\" <= " + stopInstant + ";";
                conn.Open();
                using (var reader = cmd.ExecuteReader()){
                    while (reader.Read()){
                        addrList.Add(reader.GetString(0));
                    }
                }
                conn.Close();
            }

            return addrList;
        }

        public List<Location> GetDeviceMovements(String macAddr, long startInstant, long stopInstant, long resolution) {
            List<Location> deviceMovements = new List<Location>();

            using (var cmd = new NpgsqlCommand()) {
                cmd.Connection = conn;
                cmd.CommandText =
                "SELECT \"X\", \"Y\", \"Timestamp\" " +
                "FROM \"Record\" " +
                "WHERE \"Id\" IN" +
                "(" +
                "   SELECT MAX(\"Id\")" +
                "   FROM \"Record\"" +
                "   WHERE \"Timestamp\" >= " + startInstant + " AND" +
                "         \"Timestamp\" <= " + stopInstant + " AND" +
                "         \"MAC\" = '" + macAddr + "'" +
                "   GROUP BY (\"Timestamp\" - \"Timestamp\" % " + resolution + ")" +
                ");";
                conn.Open();
                using (var reader = cmd.ExecuteReader()) {
                    while (reader.Read()) {
                        Point p = new Point(reader.GetDouble(0), reader.GetDouble(1));
                        Location l = new Location(p, reader.GetInt64(2));
                        deviceMovements.Add(l);
                    }
                }
                conn.Close();
            }

            return deviceMovements;
        }

        /*public Dictionary<String, List<Location>> GetDevicesMovements(long startInstant, long stopInstant, long resolution){
            Dictionary <String, List<Location>> positionsSequence = new Dictionary<String, List<Location>>();

            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText =
                "SELECT \"MAC\", \"X\", \"Y\", (\"Timestamp\" - \"Timestamp\" % " + resolution + ")time_sample" +
                "FROM \"Record\"" +
                "WHERE \"Id\" IN" +
                "(" +
                "   SELECT MAX(\"Id\")" +
                "   FROM \"Record\"" +
                "   WHERE \"Timestamp\" >= " + startInstant + "AND" +
                "         \"Timestamp\" < " + stopInstant +
                "   GROUP BY (\"Timestamp\" - \"Timestamp\" % " + resolution + "), \"MAC\"" +
                ");";
                conn.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read()){
                        if (!positionsSequence.ContainsKey(reader.GetString(0))){
                            positionsSequence.Add(reader.GetString(0), new List<Location>());
                        }

                        Point p = new Point(reader.GetDouble(1), reader.GetDouble(2));
                        Location l = new Location(p, reader.GetInt64(3));
                        positionsSequence[reader.GetString(0)].Add(l);
                    }
                }
                conn.Close();
            }

            return positionsSequence;
        }*/
    }
}
