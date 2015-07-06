using Metrics;
using Metrics.Core;
using Metrics.MetricData;
using Metrics.Reporters;
using Metrics.Utils;
using NLog;
using PerfCounterReporter.Configuration;
using PerfCounterReporter.Interop;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PerfCounterReporter
{

    class PerfCounterReporter : IDisposable
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();
        private const string _badChars = " ;:/.()*";

        private readonly MetricsReport _report;
        private readonly ICounterSamplingConfiguration _counterSamplingConfig;

        private readonly PdhPathHandler _handler;
        private readonly HealthStatus _healthStatus;
        private readonly ConcurrentDictionary<string, MetricInfo> _timers;
        private IDictionary<string, MetricInfo> _currentMetrics;
        private readonly Scheduler _reportScheduler;
        private readonly Scheduler _timerScheduler;

        public PerfCounterReporter(MetricsReport report, TimeSpan interval, ICounterSamplingConfiguration counterSamplingConfig)
        {
            _report = report;
            _counterSamplingConfig = counterSamplingConfig;

            _handler = new PdhPathHandler();
            _healthStatus = new HealthStatus();
            _timers = new ConcurrentDictionary<string, MetricInfo>();
            _currentMetrics = new Dictionary<string, MetricInfo>();

            this._reportScheduler = new ActionScheduler();
            this._reportScheduler.Start(interval, t => RunReport(t));
            this._timerScheduler = new ActionScheduler();
            this._timerScheduler.Start(TimeSpan.FromSeconds(1), t => ReportMetrics());
        }

        private void ReportMetrics()
        {
            foreach (var mInfo in _timers.Values)
            {
                long value = (long)(mInfo.PerfCounter.Value * 1000000);
                mInfo.Timer.Record(value, TimeUnit.Microseconds);
            }
        }

        private void RunReport(CancellationToken t)
        {
            var counterPaths = _counterSamplingConfig.DefinitionFilePaths
                .SelectMany(path => CounterFileParser.ReadCountersFromFile(path.Path))
                .Union(_counterSamplingConfig.CounterNames.Select(name => name.Name.Trim()))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            MetricsData metricsData = SetupMetricsData(counterPaths);
            _report.RunReport(metricsData, () => { return _healthStatus; }, t);
        }

        private MetricsData SetupMetricsData(IEnumerable<string> paths)
        {
            IDictionary<String, IList<GaugeValueSource>> allContextCounters = new Dictionary<String, IList<GaugeValueSource>>();
            IDictionary<String, IList<TimerValueSource>> allContextTimers = new Dictionary<String, IList<TimerValueSource>>();
            IDictionary<String, MetricInfo> newMetrics = new Dictionary<string, MetricInfo>();
            foreach (var pathElement in _handler.GetPathElements(paths))
            {
                MetricInfo minfo;
                try
                {
                    minfo = RegisterMetric(pathElement, _currentMetrics);
                }
                catch (Exception e)
                {
                    continue;
                }
                if (minfo == null)
                {
                    continue;
                }
                newMetrics[minfo.Key] = minfo;
                if (minfo.Timer == null)
                {
                    IList<GaugeValueSource> contextCounters;
                    if (!allContextCounters.TryGetValue(minfo.Context, out contextCounters))
                    {
                        contextCounters = new List<GaugeValueSource>();
                        allContextCounters[minfo.Context] = contextCounters;
                    }
                    contextCounters.Add(new GaugeValueSource(minfo.MetricName, minfo.PerfCounter, Unit.Custom(""), minfo.Tags));
                }
                else
                {
                    IList<TimerValueSource> contextTimers;
                    if (!allContextTimers.TryGetValue(minfo.Context, out contextTimers))
                    {
                        contextTimers = new List<TimerValueSource>();
                        allContextTimers[minfo.Context] = contextTimers;
                    }
                    contextTimers.Add(new TimerValueSource(minfo.MetricName, minfo.Timer, Unit.Custom(""), TimeUnit.Seconds, TimeUnit.Microseconds, minfo.Tags));
                }
            }

            foreach (var deadMInfoKey in _currentMetrics.Keys)
            {
                MetricInfo _dc;
                _timers.TryRemove(deadMInfoKey, out _dc);
            }
            _currentMetrics = newMetrics;

            // Create the MetricsData we need for the report
            DateTime now = DateTime.UtcNow;
            IList<MetricsData> subContexts = new List<MetricsData>(allContextCounters.Count);
            // First get all gauge and possible timers
            foreach (var item in allContextCounters)
            {
                IEnumerable<TimerValueSource> timersEnumerable = Enumerable.Empty<TimerValueSource>();
                IList<TimerValueSource> timers;
                if (allContextTimers.TryGetValue(item.Key, out timers))
                {
                    timersEnumerable = allContextTimers[item.Key];
                    allContextTimers.Remove(item.Key);
                }
                subContexts.Add(new MetricsData(item.Key, now,
                    Enumerable.Empty<EnvironmentEntry>(),
                    item.Value,
                    Enumerable.Empty<CounterValueSource>(),
                    Enumerable.Empty<MeterValueSource>(),
                    Enumerable.Empty<HistogramValueSource>(),
                    Enumerable.Empty<TimerValueSource>(),
                    Enumerable.Empty<MetricsData>()
                    ));
            }

            // now get the remaining timer only contexts
            foreach (var item in allContextTimers)
            {

                subContexts.Add(new MetricsData(item.Key, now,
                    Enumerable.Empty<EnvironmentEntry>(),
                    Enumerable.Empty<GaugeValueSource>(),
                    Enumerable.Empty<CounterValueSource>(),
                    Enumerable.Empty<MeterValueSource>(),
                    Enumerable.Empty<HistogramValueSource>(),
                    item.Value,
                    Enumerable.Empty<MetricsData>()
                    ));
            }

            return new MetricsData("", now,
                 Enumerable.Empty<EnvironmentEntry>(),
                    Enumerable.Empty<GaugeValueSource>(),
                    Enumerable.Empty<CounterValueSource>(),
                    Enumerable.Empty<MeterValueSource>(),
                    Enumerable.Empty<HistogramValueSource>(),
                    Enumerable.Empty<TimerValueSource>(),
                    subContexts);
        }



        private MetricInfo RegisterMetric(PdhCounterPathElement pathElement, IDictionary<String, MetricInfo> existingMetrics)
        {
            string contextName = CleanName(pathElement.ObjectName);
            string metricName = CleanName(pathElement.CounterName);
            string instanceName = CleanName(pathElement.InstanceName);

            PerformanceCounterType type = pathElement.InstanceName == null ?
                   new PerformanceCounter(pathElement.ObjectName, pathElement.CounterName, true).CounterType :
                   new PerformanceCounter(pathElement.ObjectName, pathElement.CounterName, pathElement.InstanceName, true).CounterType;

            MetricTags tags = default(MetricTags);
            if (instanceName != null)
            {
                tags = "instance=" + instanceName;
            }
            string keyName = MetricInfo.TagName(contextName + metricName, tags);
            MetricInfo mInfo;
            if (existingMetrics.TryGetValue(keyName, out mInfo))
            {
                existingMetrics.Remove(keyName);
                return mInfo;
            }
            PerfCounterGauge pcGauge = null;
            TimerMetric timer = null;
            switch (type)
            {
                //these types of counters are not usable
                case PerformanceCounterType.AverageBase:
                case PerformanceCounterType.CounterMultiBase:
                case PerformanceCounterType.RawBase:
                case PerformanceCounterType.SampleBase:
                    _log.Error(String.Format("Don't know how to handle metric of type {0} for {1}", type.ToString(), metricName));
                    return null;
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
                    pcGauge = new PerfCounterGauge(pathElement.ObjectName, pathElement.CounterName, pathElement.InstanceName);
                    break;

                //timers
                case PerformanceCounterType.AverageTimer32:
                case PerformanceCounterType.ElapsedTime:
                    timer = new TimerMetric(SamplingType.FavourRecent);
                    pcGauge = new PerfCounterGauge(pathElement.ObjectName, pathElement.CounterName, pathElement.InstanceName);
                    break;
            }
            mInfo = new MetricInfo(contextName, metricName, tags, timer, pcGauge);
            if (mInfo.Timer != null)
            {
                _timers[keyName] = mInfo;
            }
            return mInfo;
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
            if (_timerScheduler != null)
            {
                _timerScheduler.Stop();
            }
            if (_reportScheduler != null)
            {
                _reportScheduler.Stop();
            }
        }

        private sealed class MetricInfo
        {
            public string Key { get; private set; }
            public string Context { get; private set; }
            public string MetricName { get; private set; }
            public MetricTags Tags { get; private set; }
            public TimerMetric Timer { get; private set; }
            public PerfCounterGauge PerfCounter { get; private set; }

            public MetricInfo(string context, string metricName, MetricTags tags, TimerMetric timer, PerfCounterGauge perfCounter)
            {
                this.Context = context;
                this.MetricName = metricName;
                this.Tags = tags;
                this.Timer = timer;
                this.PerfCounter = perfCounter;
                this.Key = TagName(this.Context + this.MetricName, this.Tags);
            }

            public static string TagName(string name, MetricTags? tags)
            {
                if (!tags.HasValue)
                {
                    return name;
                }
                return name + string.Join(".", tags.Value.Tags);
            }
        }
    }
}
