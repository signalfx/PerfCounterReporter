
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;

namespace PerfCounterReporter.Configuration
{
    public class CounterSamplingConfiguration : ConfigurationSection, ICounterSamplingConfiguration
    {
        public static CounterSamplingConfiguration FromConfig(string section = "counterSampling")
        {
            return (CounterSamplingConfiguration)ConfigurationManager.GetSection(section);
        }

        [ConfigurationProperty("definitionFilePaths", IsDefaultCollection = true, IsRequired = false)]
        [ConfigurationCollection(typeof(CounterDefinitionsFilePathConfigurationCollection), AddItemName = "definitionFile")]
        public CounterDefinitionsFilePathConfigurationCollection DefinitionFilePaths
        {
            get { return (CounterDefinitionsFilePathConfigurationCollection)this["definitionFilePaths"]; }
            set { this["definitionFilePaths"] = value; }
        }

        ReadOnlyCollection<ICounterDefinitionsFilePath> ICounterSamplingConfiguration.DefinitionFilePaths
        {
            get
            {
                return new ReadOnlyCollection<ICounterDefinitionsFilePath>(DefinitionFilePaths.OfType<ICounterDefinitionsFilePath>().ToList()
                    ?? (IList<ICounterDefinitionsFilePath>)new ICounterDefinitionsFilePath[0]);
            }
        }

        [ConfigurationProperty("counterNames", IsDefaultCollection = true, IsRequired = false)]
        [ConfigurationCollection(typeof(CounterNameConfigurationCollection), AddItemName = "counter")]
        public CounterNameConfigurationCollection CounterDefinitions
        {
            get { return (CounterNameConfigurationCollection)this["counterNames"]; }
            set { this["counterNames"] = value; }
        }

        ReadOnlyCollection<ICounterName> ICounterSamplingConfiguration.CounterNames
        {
            get { return new ReadOnlyCollection<ICounterName>(CounterDefinitions.OfType<ICounterName>().ToList() ?? (IList<ICounterName>)new ICounterName[0]); }
        }
    }
}