using PDSApp.SniffingManagement.Trilateration;

namespace PDSApp.Persistence {
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

        public int SequenceCtrl {
            set; get;
        }

        public Point Position
        {
            set; get;
        }        
    }
}
