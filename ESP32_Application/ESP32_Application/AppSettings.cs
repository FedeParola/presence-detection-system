using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cinchoo.Core;
using Cinchoo.Core.Configuration;

namespace ESP32_Application
{
    [ChoNameValueConfigurationSection("appSettings")]
    public class AppSettings : ChoConfigurableObject
    {
        [ChoPropertyInfo("name", DefaultValue = "Mark")]
        public string Name;

        [ChoPropertyInfo("address", DefaultValue = "100, Madison Avenue, New York, NY 10010")]
        public string Address;
    }
}
