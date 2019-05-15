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
        int height;
        int timer;

        public ESPdatiGlobali(int espNumber, int channel, int width, int height, int timer)
        {
            this.espNumber = espNumber;
            this.channel = channel;
            this.width = width;
            this.height = height;
            this.timer = timer;
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

        public int Height
        {
            get { return height; }
            set { height = value; }
        }

        public int Timer
        {
            get { return timer; }
            set { timer = value; }
        }

    }
    public class ESPmomentanea
    {
        string id;
        string ipadd;
        string port;
        int x;
        int y;

        public ESPmomentanea(string id, string ipadd, string port, int x, int y)
        {
            this.id = id;
            this.ipadd = ipadd;
            this.port = port;
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
            get { return port; }
            set { port = value; }
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
