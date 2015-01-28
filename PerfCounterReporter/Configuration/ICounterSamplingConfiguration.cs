using System;
using System.Collections.ObjectModel;

namespace PerfCounterReporter.Configuration
{
    public interface ICounterSamplingConfiguration
    {
        ReadOnlyCollection<ICounterDefinitionsFilePath> DefinitionFilePaths { get; }
        ReadOnlyCollection<ICounterName> CounterNames { get; }
    }
}