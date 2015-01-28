using System.Collections.Generic;
using System.Configuration;
namespace PerfCounterReporter.Configuration
{
    public class CounterNameConfigurationCollection : ConfigurationElementCollection
    {
        public CounterNameConfigurationCollection()
        { }

        public CounterNameConfigurationCollection(IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                this.BaseAdd(new CounterName() { Name = name });
            }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new CounterName();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((CounterName)element).Name;
        }
    }
}