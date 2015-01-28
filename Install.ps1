param
(
	[Parameter()]
	[Hashtable] $settings = @{}
)

if(!$PSScriptRoot){ $PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent }

Set-StrictMode -version Latest

function Test-IsAdmin
{
	$identity = [Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()
    If (-NOT $identity.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
    {
		throw 'You are not currently running this installation under an Administrator account.  Installation aborted!'
	}
}

function CheckPolicy
{
    $executionPolicy  = (Get-ExecutionPolicy)
    $executionRestricted = ($executionPolicy -eq "Restricted")
    if ($executionRestricted){
        throw @"
Your execution policy is $executionPolicy, this means you will not be able import or use any scripts including modules.
To fix this change you execution policy to something like RemoteSigned.

        PS> Set-ExecutionPolicy RemoteSigned

For more information execute:

        PS> Get-Help about_execution_policies

"@
    }
}


function Merge-Parameters()
{
	param
	(
		[Parameter(Mandatory=$true)]
		[Hashtable] $Hash
	)

	$defaults =
	@{
		SampleInterval = '00:00:05';
		DefinitionPaths = @('CounterDefinitions\system.counters');
		CounterNames = @()
		DefaultDimensions= @{}
		SourceValue= ''
		AwsIntegration=$false
	}

	$allowedKeys = ($defaults | Select -ExpandProperty Keys) + @('ApiToken', 'SourceType')
	$Hash | Select -ExpandProperty Keys |
	% {
		if (-not $allowedKeys -contains $_)
		{
			$msg = "Parameter $_ not expected"
			Write-Error -Message $msg -Category InvalidArgument
			throw $msg
		}
		$defaults.$_ = $Hash.$_
	}
	$defaults
}

function Install()
{
	$path = "${PSScriptRoot}\PerfCounterReporter";
	$shellApplication = New-Object -com Shell.Application

	$perfCounterReporterItems = $shellApplication.NameSpace($path).Items();
	$extracted = "${Env:ProgramFiles}\PerfCounterReporter"
	if (!(test-path $extracted))
	{
		[Void](New-Item $extracted -type directory)
	}
	$shellApplication.NameSpace($extracted).CopyHere($perfCounterReporterItems, 0x14)
}

function Modify-ConfigFile()
{

	param
	(

	    [parameter(Mandatory=$true)]
            [string]
	    $ApiToken,

	    [parameter(Mandatory=$true)]
	    [ValidateSet('netbios','dns','fqdn','custom')]
	    [string]
	    $SourceType,

	    [parameter(Mandatory=$false)]
	    [string]
	    $SourceValue= '',

	    [parameter(Mandatory=$true)]
	    [bool]
	    $AwsIntegration,

	    [parameter(Mandatory=$true)]
	        [AllowEmptyCollection()]
	    [Hashtable]
	    $DefaultDimensions,

	    [Parameter(Mandatory=$true)]
		[AllowEmptyCollection()]
	    [string[]]
	    $CounterNames,

	    [parameter(Mandatory=$true)]
		[AllowEmptyCollection()]
	    [string[]]
	    $DefinitionPaths,

	    [parameter(Mandatory=$true)]
	    [TimeSpan]
	    $SampleInterval

	)

        if ($SourceType -eq "custom" -and $SourceValue -eq "")
	{
		throw "SourceValue must be specified if SourceType is 'custom'"
	}

	$path = "${Env:ProgramFiles}\PerfCounterReporter\PerfCounterReporter.exe.config"
	$xml = New-Object Xml
	$xml.Load($path)

        # configure reporting to SignalFx
	$xml.configuration.signalFxReporter.SetAttribute("apiToken", $ApiToken)
	$xml.configuration.signalFxReporter.SetAttribute("sourceType", $SourceType)
	$xml.configuration.signalFxReporter.SetAttribute("sourceValue", $SourceValue)
	$xml.configuration.signalFxReporter.SetAttribute("sampleInterval", $SampleInterval)
	if ($AwsIntegration)
	{
		$xml.configuration.signalFxReporter.SetAttribute("awsIntegration", "true");
	}


	# configure performance counters to collection
	$defaultDimensionsNode = $xml.SelectSingleNode('//defaultDimensions')
	if ($defaultDimensionsNode -ne $null)
	{
                $defaultDimensionsNode.RemoveAll()
        }
	else
	{
	        $defaultDimensionsNode = $xml.CreateElement('defaultDimensions')
		[Void]$xml.configuration.signalFxReporter.AppendChild($defaultDimensionsNode)
	}

	$DefaultDimensions.GetEnumerator() | % {
	                  $defaultDimensionNode = $xml.CreateElement('defaultDimension')
			  $defaultDimensionNode.SetAttribute('name', $_.Key)
			  $defaultDimensionNode.SetAttribute('value', $_.Value)
			  [Void]$defaultDimensionsNode.AppendChild($defaultDimensionNode)
		}

	$definitionFilePathsNode = $xml.SelectSingleNode('//definitionFilePaths')
	if ($xml.SelectSingleNode('//definitionFilePaths') -ne $null)
	{
		$definitionFilePathsNode.RemoveAll()
	}
	else
	{
		$definitionFilePathsNode = $xml.CreateElement('definitionFilePaths')
		[Void]$xml.configuration.counterSampling.AppendChild($definitionFilePathsNode)
	}

	$DefinitionPaths | % {
			$filePath = $xml.CreateElement('definitionFile')
			$filePath.SetAttribute('path', $_)
			[Void]$definitionFilePathsNode.AppendChild($filePath)
		}

	$counterNamesNode = $xml.SelectSingleNode('//counterNames')
	if ($counterNamesNode -ne $null)
	{
		$counterNamesNode.RemoveAll()
	}
	else
	{
		$counterNamesNode = $xml.CreateElement('counterNames')
		[Void]$xml.configuration.counterSampling.AppendChild($counterNamesNode)
	}

	$CounterNames | % {
			$name = $xml.CreateElement('counter')
			$name.SetAttribute('name', $_)
			[Void]$counterNamesNode.AppendChild($name)
		}

	$xml.Save($path)
}

function Install-Service()
{
	[CmdletBinding()]
	param
	(

	    [parameter(Mandatory=$true)]
            [string]
	    $ApiToken,

	    [parameter(Mandatory=$true)]
	    [ValidateSet('netbios','dns','fqdn','custom')]
	    [string]
	    $SourceType,

	    [parameter(Mandatory=$false)]
	    [string]
	    $SourceValue= '',

	    [parameter(Mandatory=$false)]
	    [Hashtable]
	    $DefaultDimensions = @{},

	    [parameter(Mandatory=$false)]
	    [bool]
	    $AwsIntegration = $false,

	    [Parameter(Mandatory=$true)]
		[AllowEmptyCollection()]
	    [string[]]
	    $CounterNames,

	    [parameter(Mandatory=$true)]
	    [string[]]
	    $DefinitionPaths,

	    [parameter(Mandatory=$false)]
	    [TimeSpan]
	    [ValidateRange('00:00:01', '00:05:00')] # 1s -> 5m
	    $SampleInterval = [TimeSpan]::FromSeconds(5)

	)

	# install and start


	if ((Get-Service PerfCounterReporter -ErrorAction SilentlyContinue) -ne $null)
	{
		Stop-Service PerfCounterReporter
		$service = Get-WmiObject -Class Win32_Service -Filter "Name='PerfCounterReporter'"
		$service.delete()
	}

	Install
	& "${Env:ProgramFiles}\PerfCounterReporter\PerfCounterReporter.exe" --install
	Modify-ConfigFile -ApiToken $ApiToken -SourceType $SourceType `
	                  -DefaultDimensions $DefaultDimensions `
			  -AwsIntegration $AwsIntegration `
			  -CounterNames $CounterNames `
			  -DefinitionPaths $DefinitionPaths `
			  -SampleInterval $SampleInterval

	Start-Service PerfCounterReporter

}

Test-IsAdmin
CheckPolicy
$mergedSettings = Merge-Parameters -Hash $settings
Install-Service @mergedSettings
