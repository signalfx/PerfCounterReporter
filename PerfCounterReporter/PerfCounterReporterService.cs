using Metrics.Reporters;
using Metrics.SignalFx;
using NLog;
using PerfCounterReporter.Configuration;
using System;
using System.ServiceProcess;

namespace PerfCounterReporter
{
    public partial class PerfCounterReporterService : ServiceBase
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        private PerfCounterReporter _pcr;
        public PerfCounterReporterService()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            

            _log.Info("Reading signalFx reporter config");
            Tuple<MetricsReport, TimeSpan> reporter = SignalFxReporterBuilder.FromAppConfig().Build();
            SyntheticCountersReporter synCR = SyntheticCountersReporter.createDefaultReporter(_log, CounterSamplingConfiguration.FromConfig());
            _pcr = new PerfCounterReporter(reporter.Item1, reporter.Item2, CounterSamplingConfiguration.FromConfig());
            if (synCR != null)
            {
                _pcr._couunterReporters.Add(synCR);
            }
            
            _log.Info("Done reading config");
        }

        protected override void OnStop()
        {
            if (_pcr != null)
            {
                _pcr.Dispose();
            }
        }
    }
}
