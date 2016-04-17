# PerfCounterReporter
A Windows service for reporting Windows Perfomance Counters to SignalFx

This code is based on/inspired by PerfTap (https://github.com/Iristyle/PerfTap) as a means of sending performance data to a monitoring system.

### Requirements

* Windows Server 2008 (or later) if running on a Windows Server; Windows 7 (or later) if running on a Windows Client
* .NET Framework 4.5+
* Administrator rights for installing services (the service is installed to run as LOCAL SERVICE)

### Installation and Configuration Steps
Download the latest release of the .MSI from the [PerfCounterReporter Releases Page](https://github.com/signalfx/PerfCounterReporter/releases).

Launch the .MSI via your preferred invocation method.

Follow the prompts to enter the API token for your SignalFx organization (this can be found in your user profile within SignalFx), whether or not you would like integration with AWS enabled, and your preferred installation location for PerfCounterReporter.

After clicking "Finish" to complete the installation, the host will immediately start sending metrics to SignalFx. This can be verified by looking for metrics from this host in the SignalFx catalog. If you do not see metrics from the host you should examine the PerfCounterReporter log file for troubleshooting purposes (details below). 
 

### Configuration

If at any time you would like to change you API token or any of the other configuration settings you will need to edit the PerfCounterReporter.exe.config file to do so. It controls what performance counters are enabled and how often they are sampled. Paths to these counters may be absolute, relative to the current working directory of the application, or relative to the current directory of where the binaries are installed. This file also controls the configuration of how to report metrics to SignalFx.

* APIToken - Your SignalFx API token. No default.
* SourceType - Configuration for what the "source" of metrics will be. No default. Value must be one of:
	* netbios - use the netbios name of the server
	* dns - use the DNS name of the server
	* fqdn - use the FQDN name of the server
	* custom - use a custom value. If the is specified then a SourceValue parameter must be specified.
* DefaultDimensions - A hashtable of default dimensions to pass to SignalFx. Default: (Empty dictionary).
* AwsIntegration - If set to "true" then AWS integration will be turned on for SignalFx reporting. Default Value: false
* SampleInterval - TimeSpan of how often to send metrics to SignalFx. Default Value: 00:00:05
* DefinitionPaths - List of file paths with counter definitions. Default Value: CounterDefinitions\system.counters
* CounterNames - List of strings. Any additional "one off" counters to collect. Default Value: (empty list)


#### Counter Definitions available out of the box

<table>
<thead><tr><td>File</td><td>Purpose</td></tr></thead>
<tr>
	<td>system.counters</td>
	<td>Standard Windows counters for CPU, memory and paging, disk IO and NIC. This is the only category of counter definitions that are enabled by default.</td>
</tr>
<tr>
	<td>dotnet.counters</td>
	<td>The most critical .NET performance counters - exceptions, logical and physical threads, heap bytes, time in GC, committed bytes, pinned objects, etc.  System totals are returned, as well as stats for all managed processes, as counters are specified with wildcards.</td>
</tr>
<tr>
	<td>aspnet.counters</td>
	<td>Information about requests, errors, sessions, worker processes</td>
</tr>
<tr>
	<td>sqlserver.counters</td>
	<td>The kitchen sink for things that are important to SQL server (including some overlap with system.counters) - CPU time for SQL processes, data access performance counters, memory manager, user database size and performance, buffer manager and memory performance, workload (compiles, recompiles), users, locks and latches, and some mention in the comments of red herrings.  This list of counters was heavily researched.</td>
</tr>
<tr>
	<td>webservice.counters</td>
	<td>Wild card counters for current connections, isapi extension requests, total method requests and bytes</td>
</tr>
</table>

#### Extra Counter Definitions

One-off counters may be added to the configuration file as shown in the example above.  Counter files may also be created to group things together.  Blank lines and lines prefixed with the # character are ignored.

The names of all counters are combined together from all the configured files and individually specified names.  However, these names have not yet been wildcard expanded.  So, if for instance, both the name "\processor(*)\% processor time" and "\processor(_total)\% processor time" have been specified, "\processor(_total)\% processor time" will be read twice.

### Logging

NLog is used for logging, and the default configuration ships with just file logging enabled.  The logs are placed in %ALLUSERSPROFILE%\PerfCounterReporter\logs.  Generally speaking, on modern Windows installations this will be the C:\ProgramData\PerfCounterReporter\logs directory. This can be changed per the NLog [documentation](http://nlog-project.org/wiki/Configuration_File).

### Uninstall Steps

If at any point you would like to remove the PerfCounterReporter Service from your system, this can be accomplished by selecting PerfCounterReporter from the 'Uninstall or Change a Program' option on that system's Control Panel or by using one of the many Windows management tools that are available for removing installed software from one or more Windows machines at a time.

### Legacy Installation Instructions via PowerShell Script (this is not the recommended method and in its current state it will likely require time and effort to get it to work properly)
Download the latest release from https://github.com/signalfx/PerfCounterReporter/releases and unzip it.

At a PowerShell admin prompt 
     
     ./Install.ps1

Alternatively, specify any or all of the configuration options.

    ./Install.ps1 @{APIToken='yourtoken';SourceType='netbios';DefinitionPaths='CounterDefinitions\system.counters','CounterDefinitions\webservice.counters';CounterNames='\Processor(*)\% Processor Time';}

Or if readability is your thing:

    $config = @{
        APIToken='yourtoken';
        SourceType='netbios';
        SampleInterval = '00:00:01'; 
        DefinitionPaths = 'CounterDefinitions\system.counters','CounterDefinitions\webservice.counters'; 
        CounterNames = '\Processor(*)\% Processor Time';
    }
    ./Install.ps1 $config

For hash values not supplied the following defaults are used. APIToken and SourceType are required.   