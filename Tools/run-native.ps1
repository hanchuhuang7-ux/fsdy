param([ValidateSet('smoke','checks','agent','benchmark','tournament')][string]$Task='smoke',[string]$Output='Builds/Results',[string]$Executable='Builds/Windows/FrontlineLab.exe',[string]$Config,[string]$RedBot,[string]$BlueBot,[int]$Timeout=250,[string]$Entrants,[string]$Maps='meadow',[string]$Seeds='42,77')
$ErrorActionPreference='Stop'
$root=[System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$exe=[System.IO.Path]::GetFullPath((Join-Path $root $Executable))
$out=[System.IO.Path]::GetFullPath((Join-Path $root $Output))
New-Item -ItemType Directory -Path $out -Force | Out-Null
$log=Join-Path $out 'player.log'
$arguments=@('-batchmode','-nographics','--frontline-task',$Task,'--out',('"'+$out+'"'),'-logFile',('"'+$log+'"'))
foreach($pair in @(@('--config',$Config),@('--red-bot',$RedBot),@('--blue-bot',$BlueBot),@('--entrants',$Entrants))){if($pair[1]){$arguments+=@($pair[0],('"'+([System.IO.Path]::GetFullPath((Join-Path $root $pair[1])))+'"'))}}
$arguments+=@('--timeout',$Timeout,'--maps',$Maps,'--seeds',$Seeds)
$p=Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if($p.ExitCode -ne 0){Get-Content -LiteralPath $log -Tail 40;throw "Native task $Task failed: $($p.ExitCode)"}
Write-Output "Native $Task succeeded: $out"
