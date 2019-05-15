using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ESP32_Application
{
    /// <summary>
    /// Logica di interazione per App.xaml, create 3 new classes for custom config settings
    /// </summary>
    public class MyConfigSection : ConfigurationSection
    {
        [ConfigurationProperty("", IsRequired = true, IsDefaultCollection = true)]
        public MyConfigInstanceCollection Instances
        {
            get { return (MyConfigInstanceCollection)this[""]; }
            set { this[""] = value; }
        }
    }
    public class MyConfigInstanceCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new MyConfigInstanceElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            //set to whatever Element Property you want to use for a key
            return ((MyConfigInstanceElement)element).IPadd;
        }

        public new MyConfigInstanceElement this[string elementName]
        {
            get
            {
                return this.OfType<MyConfigInstanceElement>().FirstOrDefault(item => item.IPadd == elementName);
            }
        }
    }

    public class MyConfigInstanceElement : ConfigurationElement
    {
        //Make sure to set IsKey=true for property exposed as the GetElementKey above
        [ConfigurationProperty("IPadd", IsKey = true, IsRequired = true)]
        public string IPadd
        {
            get { return (string)base["IPadd"]; }
            set { base["IPadd"] = value; }
        }

        [ConfigurationProperty("port", IsRequired = true)]
        public string Port
        {
            get { return (string)base["port"]; }
            set { base["port"] = value; }
        }

        [ConfigurationProperty("position", IsRequired = true)]
        public string Position
        {
            get { return (string)base["position"]; }
            set { base["position"] = value; }
        }
    }
}
