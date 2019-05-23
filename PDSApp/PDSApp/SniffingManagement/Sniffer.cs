using System;
using System.Net;
using PDSApp.SniffingManagement.Trilateration;

namespace PDSApp.SniffingManagement {
    class Sniffer {
        public String Ip { get; }
        public Point Position { get; }
        /* 
         * Setting the Sniffer status should be done only by another class inside the SniffersManagement namespace
         * but there's no way in C# to force this behaviour (no package visibility like Java)
         */
        public SnifferStatus Status {
            get;
            set;
        }

        public Sniffer(String ip, Point position) {
            if(ip == null || position == null) {
                throw new ArgumentNullException();
            }
            /* Check ip validity */
            IPAddress.Parse(ip);
            Ip = ip;
            Position = position;
            Status = SnifferStatus.Stopped;
        }

        public enum SnifferStatus { Stopped, Running, Error }
    }
}
