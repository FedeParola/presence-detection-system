using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SniffingManagement.Trilateration;

namespace SniffingManagement
{
    class Packet
    {
        public string Hash
        {
            set; get;
        }

        public string MacAddr
        {
            set; get;
        }

        public string Ssid
        {
            set; get;
        }

        public long Timestamp
        {
            set; get;
        }

        public Point Position
        {
            set; get;
        }        
    }
}
