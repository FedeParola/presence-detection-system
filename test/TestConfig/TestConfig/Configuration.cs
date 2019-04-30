using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TestConfig
{
    class Configuration
    {
        public Configuration(long timestamp, String ipAddress, String port, Int16 channel, Int16 timer_count)
        {
            this.Timestamp = timestamp;
            this.IpAddress = ipAddress;
            this.Port = port;
            this.Channel = channel;
            this.Timer_count = timer_count;
        }

        [JsonProperty(PropertyName = "timestamp")]
        public long Timestamp
        {
            set; get;
        }

        [JsonProperty(PropertyName = "ipAddress")]
        public String IpAddress
        {
            set; get;
        }

        [JsonProperty(PropertyName = "port")]
        public String Port
        {
            set; get;
        }

        [JsonProperty(PropertyName = "channel")]
        public Int16 Channel
        {
            set; get;
        }

        [JsonProperty(PropertyName = "timer_count")]
        public Int16 Timer_count
        {
            set; get;
        }
    }
}


