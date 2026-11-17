param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputPath=[IO.Path]::GetFullPath($Destination)
$stagePath=Join-Path $projectRoot 'Builds/Delivery07'
New-Item -ItemType Directory -Path $outputPath,$stagePath -Force | Out-Null
$windowsPath=Join-Path $stagePath 'frontline-lab-windows-v0.7.0'
$webPath=Join-Path $stagePath 'frontline-lab-webgl-v0.7.0'
$agentsPath=Join-Path $stagePath 'frontline-lab-agent-replays-v0.7.0'
New-Item -ItemType Directory -Path $windowsPath,$webPath,$agentsPath -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Builds/Windows') | Where-Object {$_.Name -notlike '*DontShip*' -and $_.Name -notlike '*.log'} | Copy-Item -Destination $windowsPath -Recurse -Force
New-Item -ItemType Directory -Path (Join-Path $windowsPath 'bots'),(Join-Path $windowsPath 'docs'),(Join-Path $webPath 'dist'),(Join-Path $webPath 'Tools') -Force | Out-Null
Copy-Item -Path (Join-Path $projectRoot 'scripts/bots/minimal.*') -Destination (Join-Path $windowsPath 'bots') -Force
foreach($team in @('red','blue')){'{"command":"node","args":["bots/minimal.mjs"]}' | Set-Content -LiteralPath (Join-Path $windowsPath "bots/$team-bot.json") -Encoding utf8}
@'
@echo off
cd /d "%~dp0"
if not exist agent-results mkdir agent-results
start /wait "" FrontlineLab.exe -batchmode -nographics --frontline-task agent --red-bot bots/red-bot.json --blue-bot bots/blue-bot.json --out agent-results -logFile agent-results/player.log
if exist agent-results\agent-stats.json type agent-results\agent-stats.json
pause
'@ | Set-Content -LiteralPath (Join-Path $windowsPath 'play-bots.cmd') -Encoding ascii
@'
阵线实验室 Unity 0.7.0 · Windows x64

完整解压后运行 FrontlineLab.exe。运行成品无需安装 Unity 或 .NET SDK。
保留 FrontlineLab_Data、MonoBleedingEdge 与 DLL。
策略菜单可选择流畅 / 均衡 / 精细画质。
Space 暂停；Tab 切视角；1/2/3 军令；Ctrl+Z 撤销。

0.1–0.6 存档与完整轨迹继续导入；旧 Rapier 种子须先转换。
0.6 Unity 种子使用冻结适配器重建；0.7 新物理引擎身份为 unity-0.7.0。
跨平台复盘交换完整轨迹，不保证 PhysX 跨平台位级一致。
实测结果与限制见 docs/V0.7_VALIDATION.md。
'@ | Set-Content -LiteralPath (Join-Path $windowsPath 'README.txt') -Encoding utf8
foreach($name in @('UNITY_MIGRATION.md','AGENT_PROTOCOL.md','V0.7_VALIDATION.md')){Copy-Item -LiteralPath (Join-Path $projectRoot "docs/$name") -Destination (Join-Path $windowsPath 'docs') -Force}
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/validation') -Destination (Join-Path $windowsPath 'docs') -Recurse -Force
Copy-Item -Path (Join-Path $projectRoot 'dist/*') -Destination (Join-Path $webPath 'dist') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'Tools/serve-web.mjs') -Destination (Join-Path $webPath 'Tools') -Force
@'
@echo off
cd /d "%~dp0"
node Tools/serve-web.mjs
pause
'@ | Set-Content -LiteralPath (Join-Path $webPath 'start-web.cmd') -Encoding ascii
'安装 Node 22.12+ 后运行 start-web.cmd，打开打印的本地地址；不要用 file:// 直接打开。托管需上传整个 dist。' | Set-Content -LiteralPath (Join-Path $webPath 'README.txt') -Encoding utf8
foreach($folder in @($windowsPath,$webPath)){Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE'),(Join-Path $projectRoot 'Unity/Assets/Frontline/Noto-OFL.txt') -Destination $folder -Force}
foreach($name in @('round-1.replay.json','round-2.replay.json','series.json','agent-stats.json')){Copy-Item -LiteralPath (Join-Path $projectRoot "Builds/Agents07/$name") -Destination $agentsPath -Force}
foreach($pair in @(@($windowsPath,'frontline-lab-v0.7-windows.zip'),@($webPath,'frontline-lab-v0.7-webgl.zip'),@($agentsPath,'frontline-lab-v0.7-agent-replays.zip'))){Compress-Archive -LiteralPath $pair[0] -DestinationPath (Join-Path $outputPath $pair[1]) -CompressionLevel Optimal -Force}
Push-Location $projectRoot
try {& git archive --format=zip --prefix=frontline-lab-unity-v0.7.0/ -o (Join-Path $outputPath 'frontline-lab-v0.7-unity-source.zip') HEAD;if($LASTEXITCODE -ne 0){throw 'Source archive failed'}}finally{Pop-Location}
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/V0.7_VALIDATION.md') -Destination (Join-Path $outputPath 'frontline-lab-v0.7-validation.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/validation/unity-v0.7.json') -Destination (Join-Path $outputPath 'frontline-lab-v0.7-validation.json') -Force
Get-ChildItem -LiteralPath $outputPath -Filter 'frontline-lab-v0.7-*.zip' | Get-FileHash -Algorithm SHA256 | ForEach-Object {"$($_.Hash.ToLower())  $([IO.Path]::GetFileName($_.Path))"} | Set-Content -LiteralPath (Join-Path $outputPath 'frontline-lab-v0.7-SHA256.txt') -Encoding ascii
Get-ChildItem -LiteralPath $outputPath -Filter 'frontline-lab-v0.7*' | Select-Object Name,Length
