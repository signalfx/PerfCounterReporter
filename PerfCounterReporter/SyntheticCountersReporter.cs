using NLog;
using PerfCounterReporter.Configuration;
using PerfCounterReporter.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PerfCounterReporter
{
    public class SyntheticCountersReporter : CounterReporter
    {
        public delegate void Update(PdhCounterPathElement pathElement);
        public readonly Dictionary<string, Update> _sfxCountersUpdateMethods;
        private static readonly PdhPathHandler _handler = new PdhPathHandler();
        private readonly ICounterSamplingConfiguration _counterSamplingConfig;

        public SyntheticCountersReporter(ICounterSamplingConfiguration counterSamplingConfig)
        {
            _counterSamplingConfig = counterSamplingConfig;
            _sfxCountersUpdateMethods = new Dictionary<string, Update>();
        }

        public static SyntheticCountersReporter createDefaultReporter(Logger _log, ICounterSamplingConfiguration counterSamplingConfig)
        {
            string signalFxCategory = "SignalFX";

            try
            {
                System.Diagnostics.CounterCreationDataCollection CounterDatas =
                   new System.Diagnostics.CounterCreationDataCollection();

                createCounterIfNotExist(signalFxCategory, "UsedMemory", "Total used memory", System.Diagnostics.PerformanceCounterType.NumberOfItems64, CounterDatas);

                if (CounterDatas.Count != 0)
                {
                    System.Diagnostics.PerformanceCounterCategory.Create(
                        signalFxCategory, "SignalFx synthetic counters.",
                        System.Diagnostics.PerformanceCounterCategoryType.SingleInstance, CounterDatas);
                }
            }
            catch (Exception e)
            {
                _log.Info(e.ToString());
                return null;
            }

            SyntheticCountersReporter reporter = new SyntheticCountersReporter(counterSamplingConfig);
            reporter._sfxCountersUpdateMethods.Add("UsedMemory", updateUsedMemoryCounter);
            return reporter;
        }

        public void RunReport()
        {
            var counterPaths = _counterSamplingConfig.DefinitionFilePaths
                .SelectMany(path => CounterFileParser.ReadCountersFromFile(path.Path))
                .Union(_counterSamplingConfig.CounterNames.Select(name => name.Name.Trim()))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Where(path => Regex.Match(path, @".*SignalFX.*").Success)
                .ToList();

            updateSyntheticCounters(counterPaths);
        }

        private void updateSyntheticCounters(List<string> sfxCounters)
        {
            foreach (var pathElement in _handler.GetPathElements(sfxCounters))
            {
                Update updateFunc;
                if (_sfxCountersUpdateMethods.TryGetValue(pathElement.CounterName, out updateFunc))
                {
                    updateFunc(pathElement);
                }
            }
        }

        private static void updateUsedMemoryCounter(PdhCounterPathElement pathElement)
        {
            MEMORYSTATUSEX memStatus = new MEMORYSTATUSEX();
            if (Interop.Interop.GlobalMemoryStatusEx(memStatus))
            {
                ulong installedMemory = memStatus.ullTotalPhys / 1024 / 1024;
                PerformanceCounter usedMemCounter = pathElement.InstanceName == null ?
                   new PerformanceCounter(pathElement.ObjectName, pathElement.CounterName, true) :
                   new PerformanceCounter(pathElement.ObjectName, pathElement.CounterName, pathElement.InstanceName, true);

                var itr = _handler.GetPathElements(new List<string>() { "\\Memory\\Available MBytes" });
                var pe = itr.ElementAt(0);
                PerformanceCounter availMem = pathElement.InstanceName == null ?
                   new PerformanceCounter(pe.ObjectName, pe.CounterName, true) :
                   new PerformanceCounter(pe.ObjectName, pe.CounterName, pe.InstanceName, true);

                usedMemCounter.ReadOnly = false;
                usedMemCounter.RawValue = (long)(installedMemory - Convert.ToUInt64(availMem.NextValue()));
                usedMemCounter.Close();
            }
        }

        private static void createCounterIfNotExist(string categoryName, string counterName, string counterHelp, System.Diagnostics.PerformanceCounterType type, System.Diagnostics.CounterCreationDataCollection counterDatas)
        {
            if (!System.Diagnostics.PerformanceCounterCategory.Exists(categoryName) ||
                !System.Diagnostics.PerformanceCounterCategory.CounterExists(counterName, categoryName))
            {
                System.Diagnostics.CounterCreationData counter =
                   new System.Diagnostics.CounterCreationData();
                counter.CounterName = counterName;
                counter.CounterHelp = counterHelp;
                counter.CounterType = type;

                counterDatas.Add(counter);
            }
        }
    }
}
