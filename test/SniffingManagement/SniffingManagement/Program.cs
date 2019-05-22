using System;
using SniffingManagement.Persistence;
using SniffingManagement.Trilateration;

namespace SniffingManagement {
    class Program {
        static void Main(string[] args) {
            SniffingManager sm = new SniffingManager(13000, 60, 1, 5, 5,
                new DBManager("127.0.0.1", "user", "pass", "pds"),
                HandleError);

            sm.AddSniffer(new Sniffer("192.168.1.13", new Point(0, 0)));
            sm.AddSniffer(new Sniffer("192.168.1.14", new Point(5, 5)));
            sm.AddSniffer(new Sniffer("192.168.1.15", new Point(5, 5)));

            sm.StartSniffing();
            Console.WriteLine("(Main) Sniffing started");

            Console.ReadLine();

            sm.StopSniffing();
            Console.WriteLine("(Main) Sniffing stopped");
            Console.Read();
        }

        static void HandleError() {
            Console.WriteLine("(Main) Error detected");
        }
    }
}
