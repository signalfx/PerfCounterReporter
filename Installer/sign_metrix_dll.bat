"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\ildasm.exe" /all /out=..\packages\Metrics.NET.0.2.16\lib\net45\Metrics.il ..\packages\Metrics.NET.0.2.16\lib\net45\Metrics.dll

"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\ildasm.exe" /all /out=..\packages\Metrics.NET.SignalFX.0.0.3.2\lib\net45\Metrics.NET.SignalFX.il ..\packages\Metrics.NET.SignalFX.0.0.3.2\lib\net45\Metrics.NET.SignalFX.dll

C:\Windows\Microsoft.NET\Framework\v4.0.30319\ilasm.exe /dll /key=keys.snk ..\packages\Metrics.NET.0.2.16\lib\net45\Metrics.il

"C:\Program Files (x86)\Microsoft SDKs\Windows\v8.1A\bin\NETFX 4.5.1 Tools\sn.exe" -Tp ..\packages\Metrics.NET.0.2.16\lib\net45\Metrics.dll | tools\grep.exe "Public key token" | tools\sed "s/Public key token is \(.*\)/\1/" | tools\sed "s/.\{2\}/& /g" | tools\sed "s/./\u&/g" | ..\patcher\bin\Release\patcher.exe ..\packages\Metrics.NET.SignalFX.0.0.3.2\lib\net45\Metrics.NET.SignalFX.il

C:\Windows\Microsoft.NET\Framework\v4.0.30319\ilasm.exe /dll /key=keys.snk ..\packages\Metrics.NET.SignalFX.0.0.3.2\lib\net45\Metrics.NET.SignalFX.il