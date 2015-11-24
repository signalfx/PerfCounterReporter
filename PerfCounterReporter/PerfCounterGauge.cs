using Metrics.MetricData;
using System;
using System.Diagnostics;

namespace PerfCounterReporter
{

    public class PerfCounterGauge : MetricValueProvider<double>
    {
        public readonly PerformanceCounter performanceCounter;

        public PerfCounterGauge(string category, string counter)
            : this(category, counter, instance: null)
        { }

        public PerfCounterGauge(string category, string counter, string instance)
        {
            this.performanceCounter = instance == null ?
                new PerformanceCounter(category, counter, true) :
                new PerformanceCounter(category, counter, instance, true);
        }

        public double GetValue(bool resetMetric = false)
        {
            return this.Value;
        }

        public double Value
        {
            get
            {
                try
                {
                    return this.performanceCounter != null ? this.performanceCounter.NextValue() : double.NaN;
                }
                catch (Exception _)
                {
                    return double.NaN;
                }
            }
        }
    }
}
