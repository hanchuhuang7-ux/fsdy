param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$destinationPath=[IO.Path]::GetFullPath($Destination)
New-Item -ItemType Directory -Path $destinationPath -Force | Out-Null
$stage=Join-Path $root 'Builds/Delivery'
$win=Join-Path $stage 'frontline-lab-windows-v0.6.0'
$web=Join-Path $stage 'frontline-lab-webgl-v0.6.0'
$replays=Join-Path $stage 'frontline-lab-agent-replays-v0.6.0'
foreach($path in @($win,$web,$replays)){New-Item -ItemType Directory -Path $path -Force | Out-Null}
Copy-Item -Path (Join-Path $root 'Builds/Windows/*') -Destination $win -Recurse -Force
New-Item -ItemType Directory -Path (Join-Path $win 'bots') -Force | Out-Null
Copy-Item -Path (Join-Path $root 'scripts/bots/minimal.*') -Destination (Join-Path $win 'bots') -Force
foreach($team in @('red','blue')){'{"command":"node","args":["bots/minimal.mjs"]}' | Set-Content -LiteralPath (Join-Path $win "bots/$team-bot.json") -Encoding utf8}
@'
@echo off
cd /d "%~dp0"
if not exist agent-results mkdir agent-results
start /wait "" FrontlineLab.exe -batchmode -nographics --frontline-task agent --red-bot bots/red-bot.json --blue-bot bots/blue-bot.json --out agent-results -logFile agent-results/player.log
if exist agent-results\agent-stats.json type agent-results\agent-stats.json
pause
'@ | Set-Content -LiteralPath (Join-Path $win 'play-bots.cmd') -Encoding ascii
@'
阵线实验室 Unity 0.6.0 · Windows x64

解压整个文件夹后，双击 FrontlineLab.exe。运行游戏不需要安装 Unity Editor。
请保留 FrontlineLab_Data、MonoBleedingEdge 与 DLL，不能只复制 EXE。

Space 暂停/继续；1/2/3 选择军令；Tab 切视角；Ctrl+Z 撤销布阵。
工坊可导入阵型与回放；网页版本也能播放完整轨迹。

可选：安装 Node 后运行 play-bots.cmd，启动两份示例 Bot 完成 Bo3。
结果在 agent-results/；两个 bots/*-bot.json 可改为自己的程序。
详细协议与限制见 docs/AGENT_PROTOCOL.md、docs/UNITY_MIGRATION.md。

旧 Rapier 完整轨迹可直接导入；旧种子先用源码包中的转换工具展开。
跨平台复盘使用完整轨迹，不保证跨平台 PhysX 种子位级一致。
'@ | Set-Content -LiteralPath (Join-Path $win 'README.txt') -Encoding utf8
New-Item -ItemType Directory -Path (Join-Path $win 'docs') -Force | Out-Null
foreach($name in @('AGENT_PROTOCOL.md','UNITY_MIGRATION.md','V0.6_VALIDATION.md')){Copy-Item -LiteralPath (Join-Path $root "docs/$name") -Destination (Join-Path $win 'docs') -Force}
New-Item -ItemType Directory -Path (Join-Path $win 'docs/screenshots') -Force | Out-Null
Copy-Item -Path (Join-Path $root 'docs/screenshots/unity*') -Destination (Join-Path $win 'docs/screenshots') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs/validation') -Destination (Join-Path $win 'docs') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $win -Force
Copy-Item -LiteralPath (Join-Path $root 'Unity/Assets/Frontline/Noto-OFL.txt') -Destination $win -Force
New-Item -ItemType Directory -Path (Join-Path $web 'dist'),(Join-Path $web 'Tools') -Force | Out-Null
Copy-Item -Path (Join-Path $root 'dist/*') -Destination (Join-Path $web 'dist') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'Tools/serve-web.mjs') -Destination (Join-Path $web 'Tools') -Force
@'
@echo off
cd /d "%~dp0"
node Tools/serve-web.mjs
pause
'@ | Set-Content -LiteralPath (Join-Path $web 'start-web.cmd') -Encoding ascii
'安装 Node 22.12+ 后双击 start-web.cmd，浏览器打开终端打印的本地地址。不要用 file:// 直接打开 index.html。托管时上传完整 dist/ 内容。游戏内不访问模型 API。' | Set-Content -LiteralPath (Join-Path $web 'README.txt') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $web -Force
Copy-Item -LiteralPath (Join-Path $root 'Unity/Assets/Frontline/Noto-OFL.txt') -Destination $web -Force
Copy-Item -Path (Join-Path $root 'Builds/Agents/*.json') -Destination $replays -Force
foreach($pair in @(@($win,'frontline-lab-v0.6-windows.zip'),@($web,'frontline-lab-v0.6-webgl.zip'),@($replays,'frontline-lab-v0.6-agent-replays.zip'))){Compress-Archive -LiteralPath $pair[0] -DestinationPath (Join-Path $destinationPath $pair[1]) -CompressionLevel Optimal -Force}
Push-Location $root
try {& git archive --format=zip --prefix=frontline-lab-unity-v0.6.0/ -o (Join-Path $destinationPath 'frontline-lab-v0.6-unity-source.zip') HEAD;if($LASTEXITCODE -ne 0){throw 'git archive failed'}}finally{Pop-Location}
$report=Get-Content -LiteralPath (Join-Path $root 'docs/V0.6_VALIDATION.md') -Raw
$report=$report.Replace('screenshots/unity-windows.png','unity-windows-desktop.png').Replace('screenshots/unity-desktop.jpg','unity-web-desktop.jpg').Replace('screenshots/unity-390.jpg','unity-390-deploy.jpg').Replace('screenshots/unity-arena.jpg','unity-desktop-arena.jpg')
$report | Set-Content -LiteralPath (Join-Path $destinationPath 'frontline-lab-v0.6-validation.md') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $root 'docs/validation/unity-v0.6.json') -Destination (Join-Path $destinationPath 'frontline-lab-v0.6-validation.json') -Force
Get-ChildItem -LiteralPath $destinationPath -Filter 'frontline-lab-v0.6-*.zip' | Get-FileHash -Algorithm SHA256 | ForEach-Object {"$($_.Hash.ToLower())  $([IO.Path]::GetFileName($_.Path))"} | Set-Content -LiteralPath (Join-Path $destinationPath 'frontline-lab-v0.6-SHA256.txt') -Encoding ascii
Get-ChildItem -LiteralPath $destinationPath -Filter 'frontline-lab-v0.6*' | Select-Object Name,Length
