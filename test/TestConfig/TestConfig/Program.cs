using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TestConfig
{
    class Program
    {
        //Define Ack message 
        private const String ackMsg = "A";
        private const int ackSize = 1;
        static void Main(string[] args)
        {
            // Connect to the esp32
            TcpClient client = new TcpClient();
            String espAddr = "192.168.1.6";
            IPAddress espIpAddr = IPAddress.Parse(espAddr);
            Int32 espPort = 13000;
            Console.WriteLine("Trying to connect to {0}:{1}...", espAddr, espPort.ToString());
            client.Connect(espIpAddr, espPort);
            Console.WriteLine("Connected!");

            // Get a client stream for reading and writing and a stream writer for writing json documents
            NetworkStream netStream = client.GetStream();
            StreamWriter streamWriter = new StreamWriter(netStream);
            //Buffer to store data to send/receive
            Byte[] data = new Byte[256];

            // Get the current timestamp and create the configuration object
            JsonSerializer serializer = new JsonSerializer();
            Configuration conf = new Configuration(new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds());

            // Set a 10 seconds timeout when receiving the ack
            netStream.ReadTimeout = 10 * 1000;
            //Flag needed when receiving the ack from the esp32
            bool retry;

            //TODO: TESTARE L'APP QUANDO L'ACK NON VIENE RICEVUTO E SCATTA IL TIMEOUT (MA CONNESSIONE
            //ANCORA APERTA) E QUANDO IL SERVER (ESP32) CHIUDE LA CONNESSIONE PRIMA DI INVIARE L'ACK
            do
            {
                retry = false;
                try
                {
                    //Send the configuration
                    Console.Write("Sending configuration... ");
                    serializer.Serialize(streamWriter, conf);
                    streamWriter.Flush();
                    Console.WriteLine("Configuration sent.");
                    // Send the terminator byte
                    netStream.WriteByte(0);

                    //Receive ack
                    if (netStream.Read(data, 0, ackSize) == 0)
                    {
                        // Close everything
                        streamWriter.Close();
                        Console.WriteLine("Stream closed!");
                        client.Close();
                        Console.WriteLine("TcpClient closed!");

                        //Reconnect to the esp32
                        client = new TcpClient();
                        Console.WriteLine("Trying to connect to {0}:{1}...", espAddr, espPort.ToString());
                        client.Connect(espIpAddr, espPort);
                        Console.WriteLine("Connected!");

                        // Get a client stream for reading and writing and a stream writer for writing json documents
                        netStream = client.GetStream();
                        streamWriter = new StreamWriter(netStream);
                        // Set a 10 seconds timeout for reading
                        netStream.ReadTimeout = 10 * 1000;

                        retry = true;
                    }
                    else
                    {
                        Console.WriteLine("Received: {0}", System.Text.Encoding.ASCII.GetString(data, 0, 1));
                    }
                }
                //If the stream.Read waits for a time longer than the timeout it throws an IOException
                catch (IOException e)
                {
                    Console.WriteLine("{0}", e.GetType().Name);
                    Console.WriteLine("{0}", e.ToString());

                    // Close everything
                    streamWriter.Close();
                    Console.WriteLine("Stream closed!");
                    client.Close();
                    Console.WriteLine("TcpClient closed!");

                    //Reconnect to the esp32
                    client = new TcpClient();
                    Console.WriteLine("Trying to connect to {0}:{1}...", espAddr, espPort.ToString());
                    client.Connect(espIpAddr, espPort);
                    Console.WriteLine("Connected!");

                    // Get a client stream for reading and writing and a stream writer for writing json documents
                    netStream = client.GetStream();
                    streamWriter = new StreamWriter(netStream);
                    // Set a 10 seconds timeout for reading
                    netStream.ReadTimeout = 10 * 1000;

                    retry = true;
                }
            }
            while (retry);
            
            // Close everything
            streamWriter.Close();
            Console.WriteLine("Stream closed!");
            client.Close();
            Console.WriteLine("TcpClient closed!");
      
            Console.WriteLine("Program terminating...");

            //Solo per non far terminare il programma e poter leggere le stampe su console
            Console.ReadLine();
        }
    }
}
