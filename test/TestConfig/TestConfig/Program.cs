using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

//TODO: gestire le eccezioni
namespace TestConfig
{
    class Program
    {
        //Define Ack message 
        private const String ackMsg = "A";
        private const int ackSize = 1;

        //Define the port where the server waits for connections from the esp32 clients and the channel used by the esp32 clients when sniffing
        //TODO: poi saranno da inserire via interfaccia grafica dall'utente e non definite come costanti
        private const String listeningPort = "13000";
        private const Int16 channel = 3;
        private const Int16 timer_count = 10;
        static void Main(string[] args)
        {
            // Connect to the esp32
            TcpClient client = new TcpClient();
            String espAddr = "192.168.1.8";
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

            JsonSerializer serializer = new JsonSerializer();
            // Get the current timestamp and the local IP address create the configuration object
            IPEndPoint localEndPoint = client.Client.LocalEndPoint as IPEndPoint;
            Configuration conf = new Configuration(new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds(), localEndPoint.Address.ToString(), listeningPort, channel, timer_count);

            // Set a 10 seconds timeout when receiving the ack
            netStream.ReadTimeout = 10 * 1000;
            //Flag needed when receiving the ack from the esp32
            bool retry;

            /*TODO: testare l'app quando l'ack non viene ricevuto e scatta il timeout (ma connessione
            ancora aperta) e quando il server (ESP32) chiude la connessione prima di inviare l'ack*/
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
                    //TODO: controllare che l'ACK sia il msg giusto
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
