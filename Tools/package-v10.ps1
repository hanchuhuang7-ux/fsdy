param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dest=[IO.Path]::GetFullPath($Destination)
$qa=Join-Path $repo 'Builds/V100Final'
$stage=Join-Path $qa ("delivery-"+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($stage)
[void][IO.Directory]::CreateDirectory($dest)
$prefix='frontline-lab-v1.0.0'
function Folder([string]$name){$p=Join-Path $stage "$prefix-$name";[void][IO.Directory]::CreateDirectory($p);return $p}
function Archive([string]$name,[string]$source){
    $archive=Join-Path $dest "$prefix-$name.zip"
    $temporary=Join-Path $stage "$name.tmp"
    [IO.Compression.ZipFile]::CreateFromDirectory($source,$temporary,[IO.Compression.CompressionLevel]::Optimal,$true)
    [IO.File]::Move($temporary,$archive,$true)
}
$windows=Folder 'windows'
$web=Folder 'webgl'
$agents=Folder 'agent-replays'
$expeditions=Folder 'manor-expeditions'
foreach($entry in Get-ChildItem -LiteralPath (Join-Path $repo 'Builds/Windows')){
    if ($entry.Name -notmatch 'DontShip|BurstDebugInformation|\.log$') {
        Copy-Item -LiteralPath $entry.FullName -Destination $windows -Recurse
    }
}
[void][IO.Directory]::CreateDirectory((Join-Path $windows 'bots'))
Copy-Item -LiteralPath (Join-Path $repo 'scripts/bots/minimal.mjs') -Destination (Join-Path $windows 'bots')
foreach($team in @('red','blue')){
    [IO.File]::WriteAllText((Join-Path $windows "bots/$team-bot.json"),'{"command":"node","args":["bots/minimal.mjs"]}')
}
@'
@echo off
cd /d "%~dp0"
if not exist agent-results mkdir agent-results
start /wait "" FrontlineLab.exe -batchmode -nographics --frontline-task agent --red-bot bots/red-bot.json --blue-bot bots/blue-bot.json --out agent-results -logFile agent-results/player.log
if exist agent-results\agent-stats.json type agent-results\agent-stats.json
pause
'@ | Set-Content -LiteralPath (Join-Path $windows 'play-bots.cmd') -Encoding ascii
'解压整个目录，运行 FrontlineLab.exe。用顶栏切换演武斗蛐蛐与领地庄园。庄园可点击地块、在卡片上建造和派工；工坊可导入导出 frontline-manor.json。外部 Bot 示例可由 play-bots.cmd 启动（需要 Node 22.12+）。' | Set-Content -LiteralPath (Join-Path $windows '开始游戏.txt') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $repo 'dist') -Destination (Join-Path $web 'dist') -Recurse
[void][IO.Directory]::CreateDirectory((Join-Path $web 'Tools'))
Copy-Item -LiteralPath (Join-Path $repo 'Tools/serve-web.mjs') -Destination (Join-Path $web 'Tools')
@'
@echo off
cd /d "%~dp0"
node Tools/serve-web.mjs
pause
'@ | Set-Content -LiteralPath (Join-Path $web 'start-web.cmd') -Encoding ascii
'安装 Node 22.12+，解压后运行 start-web.cmd，打开终端打印的本地地址。部署时上传整个 dist。' | Set-Content -LiteralPath (Join-Path $web '开始游戏.txt') -Encoding utf8
foreach($p in @($windows,$web)){
    Copy-Item -LiteralPath (Join-Path $repo 'LICENSE'),(Join-Path $repo 'Unity/Assets/Frontline/Noto-OFL.txt'),(Join-Path $repo 'THIRD_PARTY_NOTICES.md') -Destination $p
}
foreach($name in @('round-1.replay.json','round-2.replay.json','series.json','agent-stats.json')){
    Copy-Item -LiteralPath (Join-Path $qa "Agents/$name") -Destination $agents
}
foreach($dir in @('web-native','web-via-windows','windows-native','windows-via-web')){
    Copy-Item -LiteralPath (Join-Path $qa "Exchange/$dir") -Destination (Join-Path $expeditions $dir) -Recurse
}
foreach($file in Get-ChildItem -Path (Join-Path $qa 'Windows/*-expedition.json'),(Join-Path $qa 'Web/*-expedition.json')){
    Copy-Item -LiteralPath $file.FullName -Destination $expeditions
}
'各子目录中的 frontline-manor.json 可在庄园工坊导入。*-expedition.json 是 Windows、WebGL 两个视口在真实战场录制的 v3 轨迹，可在演武工坊播放。' | Set-Content -LiteralPath (Join-Path $expeditions '使用说明.txt') -Encoding utf8
Archive 'windows' $windows
Archive 'webgl' $web
Archive 'agent-replays' $agents
Archive 'manor-expeditions' $expeditions
Archive 'screenshots' (Join-Path $dest 'screenshots/v100')
$source=Join-Path $dest "$prefix-unity-source.zip"
& git -C $repo archive --format=zip --prefix=frontline-lab-unity-v1.0.0/ -o $source HEAD
if($LASTEXITCODE -ne 0){throw 'Source archive failed'}
Get-ChildItem -LiteralPath $dest -Filter "$prefix-*.zip" | Sort-Object Name | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(),$_.Name
} | Set-Content -LiteralPath (Join-Path $dest "$prefix-SHA256.txt") -Encoding ascii

