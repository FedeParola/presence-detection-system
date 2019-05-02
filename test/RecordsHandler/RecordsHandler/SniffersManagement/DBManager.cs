using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;

namespace RecordsHandler
{
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

        public void closeConn()
        {
            if (conn != null)
            {
                ((IDisposable)conn).Dispose();
            }
        }

        public int insertRecord(String hash, String MAC, String SSID, long timestamp, double x, double y)
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
    }
}
