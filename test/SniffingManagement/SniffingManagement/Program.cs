using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SniffingManagement.Trilateration;

namespace SniffingManagement {
    class Program {
        static void Main(string[] args) {
            SniffingManager sm = new SniffingManager(13000, 20, 13);

            sm.AddSniffer(new Sniffer("192.168.1.6", new Point(0, 0)));
            sm.AddSniffer(new Sniffer("192.168.1.7", new Point(0, 1)));

            sm.StartSniffing();
            Console.WriteLine("(Main) Sniffing started");

            Console.ReadLine();

            sm.StopSniffing();
            Console.WriteLine("(Main) Sniffing stopped");
            Console.Read();
        }
    }
}
