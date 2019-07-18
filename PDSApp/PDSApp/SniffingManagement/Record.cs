using Newtonsoft.Json;

namespace PDSApp.SniffingManagement {

    class Record {
        [JsonProperty(PropertyName = "timestamp")]
        public long Timestamp {
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

        [JsonProperty(PropertyName = "sequence_ctrl")]
        public int SequenceCtrl {
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
