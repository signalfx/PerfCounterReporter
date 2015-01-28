using Metrics;
using Metrics.Core;
using Metrics.PerfCounters;
using Metrics.Utils;
using NLog;
using PerfCounterReporter.Configuration;
using PerfCounterReporter.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PerfCounterReporter
{

    class PerfCounterReporter : IDisposable
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private const string _badChars = " ;:/.()*";

        private readonly PdhPathHandler _handler;
        private readonly IDictionary<Timer, PerformanceCounterGauge> _timers;
        private readonly Scheduler _scheduler;

        public PerfCounterReporter(ICounterSamplingConfiguration counterSamplingConfig)
        {
            _handler = new PdhPathHandler();
            _timers = new Dictionary<Timer, PerformanceCounterGauge>();
            var counterPaths = counterSamplingConfig.DefinitionFilePaths
                .SelectMany(path => CounterFileParser.ReadCountersFromFile(path.Path))
                .Union(counterSamplingConfig.CounterNames.Select(name => name.Name.Trim()))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            SetupCounters(counterPaths);

            this._scheduler = new ActionScheduler();
            this._scheduler.Start(TimeSpan.FromSeconds(1), t => ReportMetrics());
        }

        private void ReportMetrics()
        {
            foreach (KeyValuePair<Timer, PerformanceCounterGauge> entry in _timers)
            {
                long value = (long)(entry.Value.Value * 1000000); // m
                entry.Key.Record(value, TimeUnit.Microseconds);
            }
        }

        private void SetupCounters(IEnumerable<string> paths)
        {
            foreach (var pathElement in _handler.GetPathElements(paths))
            {
                RegisterMetric(pathElement);
            }
        }


        private void RegisterMetric(PdhCounterPathElement pathElement)
        {
            string contextName = CleanName(pathElement.ObjectName);
            string metricName = CleanName(pathElement.CounterName);
            string instanceName = CleanName(pathElement.InstanceName);

            PerformanceCounterType type = pathElement.InstanceName == null ?
                   new PerformanceCounter(pathElement.ObjectName, pathElement.CounterName, true).CounterType :
                   new PerformanceCounter(pathElement.ObjectName, pathElement.CounterName, pathElement.InstanceName, true).CounterType;

            MetricsContext context = Metric.Context(contextName, (ctxName) => { return new TaggedMetricsContext(ctxName); });

            MetricTags tags = default(MetricTags);
            if (instanceName != null)
            {
                tags = "instance=" + instanceName;
            }

            switch (type)
            {
                //#these types of counters are not usable
                case PerformanceCounterType.AverageBase:
                case PerformanceCounterType.CounterMultiBase:
                case PerformanceCounterType.RawBase:
                case PerformanceCounterType.SampleBase:
                    _log.Error(String.Format("Don't know how to handle metric of type {0} for {1}", type.ToString(), metricName));
                    return;
                //record as simple key value pairs
                case PerformanceCounterType.AverageCount64:
                case PerformanceCounterType.CounterDelta32:
                case PerformanceCounterType.CounterDelta64:
                case PerformanceCounterType.CounterMultiTimer:
                case PerformanceCounterType.CounterMultiTimer100Ns:
                case PerformanceCounterType.CounterMultiTimer100NsInverse:
                case PerformanceCounterType.CounterMultiTimerInverse:
                case PerformanceCounterType.CounterTimer:
                case PerformanceCounterType.CounterTimerInverse:
                case PerformanceCounterType.CountPerTimeInterval32:
                case PerformanceCounterType.CountPerTimeInterval64:
                case PerformanceCounterType.NumberOfItems32:
                case PerformanceCounterType.NumberOfItems64:
                case PerformanceCounterType.NumberOfItemsHEX32:
                case PerformanceCounterType.NumberOfItemsHEX64:
                case PerformanceCounterType.RateOfCountsPerSecond32:
                case PerformanceCounterType.RateOfCountsPerSecond64:
                case PerformanceCounterType.RawFraction:
                case PerformanceCounterType.SampleCounter:
                case PerformanceCounterType.SampleFraction:
                case PerformanceCounterType.Timer100Ns:
                case PerformanceCounterType.Timer100NsInverse:
                default:
                    context.PerformanceCounter(metricName, pathElement.ObjectName, pathElement.CounterName, pathElement.InstanceName, Unit.Custom(""), tags);
                    break;

                //timers
                case PerformanceCounterType.AverageTimer32:
                case PerformanceCounterType.ElapsedTime:
                    Timer t = context.Timer(metricName, Unit.Custom(""), durationUnit: TimeUnit.Microseconds, tags: tags);
                    _timers.Add(t, new PerformanceCounterGauge(pathElement.ObjectName, pathElement.CounterName, pathElement.InstanceName));
                    break;
            }
        }

        private string CleanName(string name)
        {
            if (name != null)
            {
                var builder = new StringBuilder(150);
                builder.Append(name.ToLower());
                for (int i = 0; i < builder.Length; ++i)
                {
                    if (_badChars.Contains(builder[i])) { builder[i] = '_'; }
                }
                builder.Replace('\\', '.');
                builder.Replace("#", "num");
                builder.Replace("%", "pct");
                if (builder[0] == '.')
                {
                    builder.Remove(0, 1);
                    builder.Insert(0, "dot");
                }
                return Regex.Replace(builder.ToString(), "_+", "_");
            }
            return null;
        }

        public void Dispose()
        {
            if (_handler != null)
            {
                using (var _ = _handler) { }
            }
            if (_scheduler != null)
            {
                _scheduler.Stop();
            }
        }
    }
}
