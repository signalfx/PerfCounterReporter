using NLog;
using System;
using System.Configuration.Install;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;

namespace PerfCounterReporter
{
    public class Program
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                _log.Info(() => "PerfCounterReporter Started");
                if (Environment.UserInteractive)
                {
                    string action = args.Length >= 1 ? args[0] : "";

                    switch (action)
                    {
                        case "--install":
                            InstallService();
                            break;
                        case "--uninstall":
                            UninstallService();
                            break;
                        case "--version":
                            PrintVersion();
                            break;
                        case "--console":
                            RunConsoleMode();
                            break;
                        case "--help":
                            PrintHelp();
                            break;

                        default:
#if DEBUG
                            if (global::System.Diagnostics.Debugger.IsAttached)
                            {
                                RunConsoleMode();
                            }
#endif

                            PrintHelp(action);
                            Environment.Exit(1);
                            break;
                    }
                    Environment.Exit(0);
                }
                else
                {
                    ServiceBase.Run(new PerfCounterReporterService());
                }
            }
            catch (Exception ex)
            {
                _log.Fatal(ex, String.Format("An unhandled error occurred in the PerfCounterReporter Service on [{0}]",
                Environment.MachineName));
                throw;
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _log.Fatal(e.ExceptionObject as Exception, String.Format("An unhandled error occurred in the PerfCounterReporter Service on [{0}]",
            Environment.MachineName));
        }
        private static void PrintHelp(string action = null)
        {
            Action<string> C = (input) => { Console.WriteLine(input); };
            if (action != null)
            {
                C("Error - unknown option: " + action);
            }
            C("Usage: PerfCounterReporter [ --install | --uninstall | --console | --version | --help ]");
            C("  --install              Install PerfCounterReporter.exe as a Windows Service.");
            C("  --uninstall            Uninstall PerfCounterReporter");
            C("  --console              Run PerfCounterReporter in console mode (does not need to be installed first)");
            C("  --version              Prints the service version");
            C("  --help                 Prints this help information.");
        }

        private static void InstallService()
        {
            try
            {
                ManagedInstallerClass.InstallHelper(new[] { Assembly.GetExecutingAssembly().Location });
                Console.WriteLine("Service installed successfully (don't forget to start it!)");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not install the service: " + ex.Message);
                Console.WriteLine(ex.ToString());
            }
        }

        private static void UninstallService()
        {
            try
            {
                ManagedInstallerClass.InstallHelper(new[] { "/u", Assembly.GetExecutingAssembly().Location });
                Console.WriteLine("Service uninstalled successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not uninstall the service: " + ex.Message);
                Console.WriteLine(ex.ToString());
            }
        }

        private static void PrintVersion()
        {
            Console.WriteLine("PerfCounterReporter v" + Assembly.GetExecutingAssembly().GetName().Version.ToString());
        }

        private static void RunConsoleMode()
        {
            // Debug code: this allows the process to run as a non-service.
            // It will kick off the service start point, but never kill it.
            // Shut down the debugger to exit
            //TODO: this factory needs to be registered in a container to make this more general purpose 
            using (var service = new PerfCounterReporterService())
            {
                // Put a breakpoint in OnStart to catch it
                typeof(PerfCounterReporterService).GetMethod("OnStart", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(service, new object[] { null });
                //make sure we don't release the instance yet ;0
                Thread.Sleep(Timeout.Infinite);
            }
        }
    }
}
