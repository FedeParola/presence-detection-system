using Newtonsoft.Json;

namespace test_socket {

    class Record {
        [JsonProperty(PropertyName = "tstamp_sec")]
        public long TstampSec {
            set; get;
        }

        [JsonProperty(PropertyName = "tstamp_msec")]
        public long TstampMsec {
            set; get;
        }

        [JsonProperty(PropertyName = "SSID")]
        public string Ssid {
            set; get;
        }

        [JsonProperty(PropertyName = "MACADDR")]
        public string MacAddr {
            set; get;
        }

        [JsonProperty(PropertyName = "RSSI")]
        public int Rssi {
            set; get;
        }

        [JsonProperty(PropertyName = "hash")]
        public string Hash {
            set; get;
        }
    }
}
