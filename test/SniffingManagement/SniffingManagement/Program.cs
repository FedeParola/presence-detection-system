using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SniffingManagement.Trilateration;

namespace SniffingManagement {
    class Program {
        static void Main(string[] args) {
            SniffingManager sm = new SniffingManager(13000, 60);

            sm.AddSniffer(new Sniffer("127.0.0.1", new Point(0, 0)));

            sm.StartSniffing();
            Console.WriteLine("(Main) Sniffing started");

            Thread.Sleep(10000);

            sm.StopSniffing();
            Console.WriteLine("(Main) Sniffing stopped");
            Console.Read();
        }
    }
}
