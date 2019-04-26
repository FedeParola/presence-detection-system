using Newtonsoft.Json;

namespace RecordsHandler.SniffersManagement {

    class Configuration {
        public Configuration(long timestamp) {
            this.Timestamp = timestamp;
        }

        [JsonProperty(PropertyName = "timestamp")]
        public long Timestamp {
            set; get;
        }
    }
}
