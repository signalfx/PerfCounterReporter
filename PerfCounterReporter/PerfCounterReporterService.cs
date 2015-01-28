using Metrics;
using NLog;
using PerfCounterReporter.Configuration;
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
            Metric.Config.WithReporting(report => report.WithSignalFxFromAppConfig());
            _pcr = new PerfCounterReporter(CounterSamplingConfiguration.FromConfig());
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
