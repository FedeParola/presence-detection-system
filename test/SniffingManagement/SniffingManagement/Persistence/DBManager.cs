using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using SniffingManagement.Trilateration;

namespace SniffingManagement.Persistence
{
    /*TODO: manage exceptions*/
    class DBManager
    {
        private NpgsqlConnection conn;
        public DBManager(String host, String user, String pass, String database)
        {
            conn = new NpgsqlConnection("Host = " + host +
                                        "; Username = " + user +
                                        "; Password = " + pass +
                                        "; Database = " + database);
            conn.Open();
        }

        public void CloseConn()
        {
            if (conn != null)
            {
                ((IDisposable)conn).Dispose();
            }
        }

        public int InsertRecord(String hash, String MAC, String SSID, long timestamp, double x, double y)
        {
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = "INSERT INTO \"Record\" (\"Hash\", \"MAC\", \"SSID\", \"Timestamp\", \"X\", \"Y\") " +
                    "VALUES (" +
                    "'" + hash + "', " +
                    "'" + MAC + "', " +
                    "'" + SSID + "', " +
                    timestamp + ", " +
                    x + ", " +
                    y +
                    ");";
                return cmd.ExecuteNonQuery();
            }
        }

        /*DA TESTARE*/
        /*'timeInterval' represents the length (in milliseconds) of the previous period of time during which
         we have to count the distinct detected devices*/
        public int CountDetectedDevices(long timeInterval)
        {
            int result;
            long startingTimeInstant = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds() - timeInterval;

            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = "SELECT COUNT (DISTINCT \"MAC\") FROM \"Record\" WHERE \"Timestamp\" >= " + startingTimeInstant + ";";
                using (var reader = cmd.ExecuteReader())
                {
                    reader.Read();
                    result = reader.GetInt32(0);
                }
            }

            return result;
        }

        /*DA TESTARE*/
        /*'timeInterval' represents the length (in milliseconds) of the previous period of time considered for the statistics*/
        public Dictionary<String, Point> EstimateDevicesPosition(long timeInterval)
        {
            Dictionary <String, Point> DevicesPositions = new Dictionary<String, Point>();
            long startingTimeInstant = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds() - timeInterval;

            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText =
                    "SELECT \"MAC\", \"X\", \"Y\", ROW_NUMBER () OVER(PARTITION BY \"MAC\"" +
                                                                    " ORDER BY \"Timestamp\" DESC)" +
                    "FROM TABLE \"Record\"" +
                    "WHERE ROW_NUMBER = 1 AND " +
                    "\"Timestamp\" >= " + startingTimeInstant + ";";
                using (var reader = cmd.ExecuteReader())
                {
                    while(reader.Read())
                    {
                        Point p = new Point(reader.GetDouble(1), reader.GetDouble(2));
                        DevicesPositions.Add(reader.GetString(0), p);
                    }
                }
            }

            return DevicesPositions;
        }
    }
}
