using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TestConfig
{
    //TODO: aggiungere ip e porta del server e il canale wi-fi su cui l'esp deve sniffare
    class Configuration
    {
        public Configuration(long timestamp)
        {
            this.Timestamp = timestamp;
        }

        [JsonProperty(PropertyName = "timestamp")]
        public long Timestamp
        {
            set; get;
        }
    }
}


