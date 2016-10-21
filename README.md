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

The file `PerfCounterReporter.exe.config` controls the configuration of PerfCounterReporter. If you installed this tool using the .MSI installer, it is not necessary to directly modify this file. 

`PerfCounterReporter.exe.config` controls what performance counters are enabled and how often they are sampled. Paths to these counters may be absolute, relative to the current working directory of the application, or relative to the current directory of where the binaries are installed. This file also controls the configuration of how to report metrics to SignalFx.

The `signalFxReporter` block includes the following options: 

| Setting            | Description     | Default  |
|--------------------|----------------------------|----------|
| APIToken | Your SignalFx API token. | No default. |
| SourceType | Configuration for what the "source" of metrics will be. Value must be one of `netbios` (use the netbios name of the server), `dns` (use the DNS name of the server), `fqdn` (use the FQDN name of the server), or `custom` (use a custom value specified in a parameter `SourceValue`.) | netbios |
| DefaultDimensions | A hashtable of default dimensions to pass to SignalFx | Empty dictionary |
| AwsIntegration | If set to "true" then AWS metadata will accompany metrics. | false |
| SampleInterval | Controls the interval at which to send metrics to SignalFx, as hh:mm:ss. | 00:00:05 |

**Example:** 

```
<signalFxReporter apiToken="<yourtoken>" sampleInterval="00:00:05" sourceType="netbios"/>
```

The `counterSampling` block includes the following options:

| Setting            | Description     | Default  |
|--------------------|----------------------------|----------|
| definitionFilePaths | List of file paths with counter definitions (see [Selecting counter sets](#selecting-counter-sets) below) |  CounterDefinitions\system.counters |
| counterNames | Names of indiviual counters to collect (see [Extra counter definitions](#extra-counter-definitions) below) | No default. |

**Example:** 

```
<counterSampling>
  <definitionFilePaths>
    <definitionFile path="CounterDefinitions\\system.counters" />
    <!-- <definitionFile path="CounterDefinitions\\aspnet.counters" /> -->
    <!-- <definitionFile path="CounterDefinitions\\dotnet.counters" /> -->
    <!-- <definitionFile path="CounterDefinitions\\sqlserver.counters" /> -->
    <!-- <definitionFile path="CounterDefinitions\\webservice.counters" /> -->
  </definitionFilePaths>
  <!--
  <counterNames>
    <counter name="\network interface(*)\bytes total/sec" />
  </counterNames>
  -->
</counterSampling>
```

#### Selecting counter sets

Counter files (`*.counter`) define the metrics that PerfCounterReporter will collect. The following counter sets accompany this tool. Enable them by adding entries to `definitionFilePaths` in `PerfCounterReporter.exe.config`: 

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

#### Configuration via Command Line Parameters / Non-interactive Installs

The PerfCounterReporter .MSI supports parameters being passed into it at the command line. This is particularly useful when you are looking to do non-interactive deployment and configuration operations involving it via msiexec (or your tool of choice).

The following command line parameters are supported:
  
| Parameter            | Description     | Default  |
|--------------------|----------------------------|----------|
| APITOKEN | Your SignalFx API token | No default |
| SOURCETYPE | Configuration for what the "source" of metrics will be. Value must be one of `netbios` (use the netbios name of the host), `dns` (use the DNS name of the hst), `fqdn` (use the FQDN name of the host), or `custom` (use a custom value specified in a parameter SOURCEVALUE) | netbios |
| SOURCEVALUE | A string indicating the custom name describing the source of the metrics. This value is only used if `custom` is specified as the value for SOURCETYPE | No default |
| AWSINT | If set to "true" then AWS metadata will accompany metrics. Only specify a value of "true" if the host is running in AWS | false |
| SAMPLEINTERVAL | Controls the interval at which to send metrics to SignalFx from this host, as hh:mm:ss. | 00:00:05 |
| DIMENSIONNAME | String indicating the name of an optional dimension that you want to add to the metrics that are being sent from this host to SignalFx | No default |
| DIMENSIONVALUE | String indicating the value for the dimension specified in the DIMENSIONNAME parameter that you want to add to the metrics that are being sent from this host to SignalFx | No default |

NOTE #1: The above parameter names are case-sensitive and must be entered in all CAPS per the requirements that Windows places on an installer's public properties.

NOTE #2: Only the name and value for one dimension can be passed in via the command line at this time. If you wish to add more than one dimension you must make the appropriate modifications to your `PerfCounterReporter.exe.config` file as noted above.

**Example usage:** 

To perform a non-interactive install of PerfCounterReporter v1.5.0 on a host where you don't want AWS metadata reported, where you want the source name to be a custom value called "TestMachine", where you want the reporting frequency of metrics to be every minute, and where you want metrics from the host to include a dimension of "Data Center" with the value of "East Coast", you would issue the following command:

```
msiexec /i PerfCounterReporterInstaller-1.5.0.msi /passive APITOKEN="MyOrgsApiToken" AWSINT="false" SOURCETYPE="custom" SOURCEVALUE="TestMachine" SAMPLEINTERVAL="00:01:00" DIMENSIONNAME="Data Center" DIMENSIONVALUE="East Coast"
```

To perform a non-interactive uninstall of PerfCounterReporter v1.5.0, issue the following command on the host where it is already installed and where the .MSI binary is present:

```
msiexec /x PerfCounterReporterInstaller-1.5.0.msi /passive
```

NOTE: Any combination of the above command line parameters can also be passed when running the PerfCounterReporter Installer interactively. This eliminates the need to edit the `PerfCounterReporter.exe.config` file post-install to configure the reporting interval or other such configuration options that are not currently prompted for in the installer's UI.

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
