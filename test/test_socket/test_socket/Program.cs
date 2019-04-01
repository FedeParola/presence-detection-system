using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace test_socket {
    class Program {
        static void Main(string[] args) {
            TcpListener server = null;

            try {
                /* Set the TcpListener on port 13000 */
                Int32 port = 13000;
                server = new TcpListener(IPAddress.Any, port);

                /* Start listening for client requests */
                server.Start();

                JsonSerializer serializer = new JsonSerializer();

                /* Enter the listening loop */
                while (true) {
                    Console.Write("Waiting for a connection... ");

                    /* Perform a blocking call to accept requests */
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("Connected!");

                    /* Unmarshal the json stream */
                    Console.Write("Receiving records... ");
                    List<Record> records = (List<Record>) serializer.Deserialize(new StreamReader(client.GetStream()),
                        typeof(List<Record>));
                    Console.WriteLine("Records received:");

                    /* Print the records */
                    foreach (Record r in records) {
                        Console.WriteLine("{{\n" +
                            "\t\"tstamp_sec\":{4},\n" +
                            "\t\"tstamp_msec\":{5},\n" +
                            "\t\"ssid\":\"{0}\",\n" +
                            "\t\"mac\":\"{1}\",\n" +
                            "\t\"rssi\":{2},\n" +
                            "\t\"hash\":\"{3}\"\n" +
                            "}}", r.Ssid, r.MacAddr, r.Rssi, r.Hash, r.TstampSec, r.TstampMsec);
                    }

                    /* Get the current timestamp and send it to the esp */
                    Configuration conf = new Configuration(new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds());
                    Console.Write("Sending configuration... ");
                    StreamWriter writer = new StreamWriter(client.GetStream());
                    serializer.Serialize(writer, conf);
                    writer.Flush();
                    Console.WriteLine("Configuration sent.");

                    /* Shutdown and end connection */
                    client.Close();
                    Console.WriteLine("Connection closed.\n");
                }

            } catch (SocketException e) {
                Console.WriteLine("SocketException: {0}", e);

            } catch (IOException e) {
                Console.WriteLine("IOException: {0}", e);

            } finally {
                /* Stop listening for new clients */
                server.Stop();
            }
        }
    }
}