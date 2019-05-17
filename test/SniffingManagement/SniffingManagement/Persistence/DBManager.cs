using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using SniffingManagement.Trilateration;

namespace SniffingManagement.Persistence
{
    /*TODO: manage exceptions and prevent SQL injections (e.g. SSIDs)*/
    class DBManager
    {
        /*TODO: creare il database da codice*/
        private NpgsqlConnection conn;
        public DBManager(String host, String user, String pass, String database)
        {
            conn = new NpgsqlConnection("Host = " + host +
                                        "; Username = " + user +
                                        "; Password = " + pass +
                                        "; Database = " + database);
        }

        /*TODO: aggiungere la chiamata a questo metodo nello sniffingManager*/
        public void CloseConn()
        {
            if (conn != null)
            {
                ((IDisposable)conn).Dispose();
            }
        }

        /*X e Y causano problemi nella insert se hanno la virgola!*/
        public int InsertRecords(List<Packet> packets)
        {
            int returnValue;
 
            conn.Open();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText =
                    "INSERT INTO \"Record\" (\"Hash\", \"MAC\", \"SSID\", \"Timestamp\", \"X\", \"Y\") VALUES ";
                foreach (Packet p in packets){
                    cmd.CommandText += "(" +
                                        "'" + p.Hash + "', " +
                                        "'" + p.MacAddr + "', " +
                                        "'" + p.Ssid + "', " +
                                        p.Timestamp + ", " +
                                        p.Position.X + ", " +
                                        p.Position.Y +
                                        "), ";
                }
                cmd.CommandText = cmd.CommandText.Remove(cmd.CommandText.Length - 2, 2) + ";";
                    
                returnValue = cmd.ExecuteNonQuery();
            }
            conn.Close();

            return returnValue;
        }

        /*DA TESTARE*/
        /*'timeInterval' represents the length (in milliseconds) of the previous period of time during which
         we have to count the distinct detected devices*/
        public int CountDetectedDevices(long timeInterval)
        {
            int result;
            long startingTimeInstant = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds() - timeInterval;

            conn.Open();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = 
                    "SELECT COUNT (DISTINCT \"MAC\") " +
                    "FROM \"Record\" WHERE \"Timestamp\" >= " + startingTimeInstant + ";";
                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();
                    result = reader.GetInt32(0);
                }
            }
            conn.Close();

            return result;
        }

        /*DA TESTARE*/
        /*'timeInterval' represents the length (in milliseconds) of the previous period of time considered for the statistics*/
        public Dictionary<String, Point> EstimateDevicesPosition(long timeInterval)
        {
            Dictionary <String, Point> DevicesPositions = new Dictionary<String, Point>();
            long startingTimeInstant = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds() - timeInterval;

            conn.Open();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText =
                "SELECT \"MAC\", \"X\", \"Y\" FROM(" +
                "   SELECT \"MAC\", \"X\", \"Y\", ROW_NUMBER () OVER(PARTITION BY \"MAC\"" +
                                                                    "ORDER BY \"Timestamp\" DESC)" +
                "   FROM \"Record\"" +
                "   WHERE \"Timestamp\" >= " + startingTimeInstant +
                ") \"Devices\"" +
                "WHERE ROW_NUMBER = 1;";
                using (var reader = cmd.ExecuteReader())
                {
                    while(reader.Read())
                    {
                        Point p = new Point(reader.GetDouble(1), reader.GetDouble(2));
                        DevicesPositions.Add(reader.GetString(0), p);
                    }
                }
            }
            conn.Close();

            return DevicesPositions;
        }

        /*OPTIONAL EXTENSIONS*/

        /*DA TESTARE*/
        /*The 'minThreshold' represents the minimum number of times a device must be detected to be considered 'talkative' */
        public List<String> GetTalkativeDevices(long startInstant, long stopInstant, int minThreshold)
        {
            List<String> macAddresses = new List<String>();

            conn.Open();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText =
                    "SELECT \"MAC\", \"NoOfAppearances\" FROM (" +
                    "   SELECT \"MAC\", COUNT(*) AS \"NoOfAppearances\" " +
                    "   FROM \"Record\"" +
                    "   WHERE \"Timestamp\" >=" + startInstant + "AND" +
                             "\"Timestamp\" <" + stopInstant +
                    "   GROUP BY \"MAC\" " +
                    "   ) \"Macs\"" +
                    "WHERE \"NoOfAppearances\" >= " + minThreshold + ";";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        macAddresses.Add(reader.GetString(0));
                    }
                }
            }
            conn.Close();

            return macAddresses;

            /*...e riportante IN QUALI INTERVALLI TALI DISPOSITIVI SONO STATI RILEVATI!*/
        }

        /*DA TESTARE! Inoltre al momento memorizzo per ogni mac semplicemente la successione delle posizioni...
         dovrei anche apporre il timestamp a ogni posizione? Inoltre ha senso considerare posizioni relative allo stesso mac
         temporalmente molto vicine?*/
        public Dictionary<String, List<Point>> GetDevicesMovements(long startInstant, long stopInstant)
        {
            Dictionary <String, List<Point>> positionsSequence = new Dictionary<String, List<Point>>();

            conn.Open();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText =
                    "SELECT \"MAC\", \"X\", \"Y\"" +
                    "FROM \"Record\"" +
                    "WHERE \"Timestamp\" >= " + startInstant + "AND" +
                    "      \"Timestamp\" < " + stopInstant + "" +
                    "ORDER BY \"Timestamp\" ASC;";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!positionsSequence.ContainsKey(reader.GetString(0)))
                        {
                            positionsSequence.Add(reader.GetString(0), new List<Point>());
                        }

                        Point p = new Point(reader.GetDouble(1), reader.GetDouble(2));
                        positionsSequence.TryGetValue(reader.GetString(0), out List<Point> l);
                        l.Add(p);
                    }
                }
            }
            conn.Close();

            return positionsSequence;
        }
    }
}
