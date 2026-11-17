param([Parameter(Mandatory=$true)][string]$EditorPath,[ValidateSet('Prepare','Windows','Web','Both','CheckPhysics')][string]$Target='Both')
$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot '../Unity'
$logDirectory=Join-Path $PSScriptRoot '../Builds/Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$log=[System.IO.Path]::GetFullPath((Join-Path $logDirectory "$Target.log"))
$projectPath=(Resolve-Path $project).Path
$arguments=@('-batchmode','-nographics','-quit','-projectPath',('"'+$projectPath+'"'),'-executeMethod',"Frontline.Editor.BuildTools.$Target",'-logFile',('"'+$log+'"'))
if($Target -eq 'CheckPhysics'){$arguments=$arguments | Where-Object {$_ -ne '-quit'};$arguments+=@('--frontline-task','checks','--out',('"'+([System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Builds/Checks')))+'"'))}
$process=Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){if(Test-Path -LiteralPath $log){Get-Content -LiteralPath $log -Tail 90};throw "Unity exited with $($process.ExitCode)"}
Get-Content -LiteralPath $log -Tail 30
