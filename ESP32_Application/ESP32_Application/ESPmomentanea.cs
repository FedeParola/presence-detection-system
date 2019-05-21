using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ESP32_Application
{
    public class ESPdatiGlobali
    {
        int espNumber;
        int channel;
        int width;
        int length;
        int timer;
        int port;

        public ESPdatiGlobali(int espNumber, int channel, int width, int length, int timer, int port)
        {
            this.espNumber = espNumber;
            this.channel = channel;
            this.width = width;
            this.length = length;
            this.timer = timer;
            this.port = port;
        }
        public int EspNumber
        {
            get { return espNumber; }
            set { espNumber = value; }
        }

        public int Channel
        {
            get { return channel; }
            set { channel = value; }
        }

        public int Width
        {
            get { return width; }
            set { width = value; }
        }

        public int Length
        {
            get { return length; }
            set { length = value; }
        }

        public int Timer
        {
            get { return timer; }
            set { timer = value; }
        }

        public int Port
        {
            get { return port; }
            set { port = value; }
        }

    }
    public class ESPmomentanea
    {
        string id;
        string ipadd;
        string stato;
        int x;
        int y;

        public ESPmomentanea(string id, string ipadd, string stato, int x, int y)
        {
            this.id = id;
            this.ipadd = ipadd;
            this.stato = stato;
            this.x = x;
            this.y = y;
        }

        public string Id
        {
            get { return id; }
            set { id = value; }
        }
        public string Ipadd
        {
            get { return ipadd; }
            set { ipadd = value; }
        }

        public string Port
        {
            get { return stato; }
            set { stato = value; }
        }

        public int X
        {
            get { return x; }
            set { x = value; }
        }

        public int Y
        {
            get { return y; }
            set { y = value; }
        }

    }
}
