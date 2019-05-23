using System;
using Newtonsoft.Json;

namespace PDSApp.SniffingManagement {
    class Configuration
    {
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
        public Byte Channel
        {
            set; get;
        }

        [JsonProperty(PropertyName = "timer_count")]
        public UInt16 SniffingPeriod
        {
            set; get;
        }
    }
}


