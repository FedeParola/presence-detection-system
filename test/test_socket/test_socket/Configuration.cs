using Newtonsoft.Json;

namespace test_socket {

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
