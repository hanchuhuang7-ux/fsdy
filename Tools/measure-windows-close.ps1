param(
    [Parameter(Mandatory)][string]$Player,
    [Parameter(Mandatory)][string]$Output,
    [ValidateRange(1,20)][int]$Runs=5,
    [ValidateSet('Mouse','SystemCommand')][string]$Method='Mouse',
    [ValidateRange(0,500)][int]$HoldMilliseconds=0,
    [string[]]$PlayerArgs=@()
)
$ErrorActionPreference='Stop'

if(!('FrontlineCloseTest' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public static class FrontlineCloseTest
{
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int x,y; }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int left,top,right,bottom; }
    [StructLayout(LayoutKind.Sequential)] struct Mouse { public int x,y;public uint data,flags,time;public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct Input { public uint kind;public Mouse mouse; }
    delegate bool Windows(IntPtr window, IntPtr state);
    [DllImport("user32.dll")] static extern bool EnumWindows(Windows callback,IntPtr state);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out int pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int count);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr window);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint caller,uint target,bool attach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr window,uint flags);
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,Input[] inputs,int size);
    [DllImport("user32.dll",SetLastError=true,CharSet=CharSet.Unicode)]
    static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wp,IntPtr lp,uint flags,uint ms,out IntPtr result);

    public static IntPtr Find(int pid)
    {
        IntPtr result=IntPtr.Zero;
        var name=new StringBuilder(96);
        EnumWindows((window,state)=>{
            int owner;
            GetWindowThreadProcessId(window,out owner);
            if(owner!=pid||!IsWindowVisible(window))return true;
            GetClassName(window,name,name.Capacity);
            if(name.ToString()!="UnityWndClass")return true;
            result=window;
            return false;
        },IntPtr.Zero);
        return result;
    }

    static IntPtr XY(Point p) => (IntPtr)((p.x & 65535) | (p.y << 16));

    static bool CaptionClose(IntPtr window,Point p)
    {
        IntPtr hit;
        return SendMessageTimeout(window,0x0084,IntPtr.Zero,XY(p),2,150,out hit)!=IntPtr.Zero&&hit.ToInt64()==20;
    }

    public static Point PrepareClick(IntPtr window)
    {
        var awareness=SetThreadDpiAwarenessContext((IntPtr)(-4));
        try
        {
            if(GetForegroundWindow()!=window)
            {
                uint caller=GetCurrentThreadId();
                var front=GetForegroundWindow();
                uint frontThread=front==IntPtr.Zero?0:GetWindowThreadProcessId(front,out _);
                bool attached=frontThread!=0&&caller!=frontThread&&AttachThreadInput(caller,frontThread,true);
                try { BringWindowToTop(window); SetForegroundWindow(window); }
                finally { if(attached)AttachThreadInput(caller,frontThread,false); }
            }
            Rect rect;
            if(!GetWindowRect(window,out rect))throw new Win32Exception();
            int left=rect.right,top=rect.bottom,right=rect.left,bottom=rect.top;
            for(int y=rect.top+4;y<=Math.Min(rect.top+58,rect.bottom);y+=4)
                for(int x=rect.right-6;x>=Math.Max(rect.left,rect.right-118);x-=4)
                {
                    var p=new Point{x=x,y=y};
                    if(GetAncestor(WindowFromPoint(p),2)!=window||!CaptionClose(window,p))continue;
                    left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                }
            if(left>right||top>bottom)throw new InvalidOperationException("The tested window's X is not visible on the input desktop.");
            var point=new Point{x=(left+right)/2,y=(top+bottom)/2};
            if(GetAncestor(WindowFromPoint(point),2)!=window||!CaptionClose(window,point)||!SetCursorPos(point.x,point.y))
                throw new InvalidOperationException("The pointer did not reach HTCLOSE on the tested player.");
            return point;
        }
        finally { if(awareness!=IntPtr.Zero)SetThreadDpiAwarenessContext(awareness); }
    }

    [DllImport("kernel32.dll")] static extern void GetSystemTimePreciseAsFileTime(out long utc);
    public static long InputUtc { get; private set; }
    public static long InputStamp { get; private set; }
    static void MarkInput()
    {
        GetSystemTimePreciseAsFileTime(out long utc);
        InputUtc=utc;
        InputStamp=Stopwatch.GetTimestamp();
    }

    public static void Click(IntPtr window,Point point,int holdMilliseconds)
    {
        Point cursor;
        var previous=SetThreadDpiAwarenessContext((IntPtr)(-4));
        try
        {
            if(!GetCursorPos(out cursor)||cursor.x!=point.x||cursor.y!=point.y||GetAncestor(WindowFromPoint(point),2)!=window)
                throw new InvalidOperationException("Input target moved before SendInput.");
            var down=new Input{mouse=new Mouse{flags=2}};
            var up=new Input{mouse=new Mouse{flags=4}};
            int size=Marshal.SizeOf<Input>();
            if(holdMilliseconds==0)
            {
                var events=new[]{down,up};
                MarkInput();
                if(SendInput(2,events,size)!=2)throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            else
            {
                MarkInput();
                if(SendInput(1,new[]{down},size)!=1)throw new Win32Exception(Marshal.GetLastWin32Error());
                Thread.Sleep(holdMilliseconds);
                if(SendInput(1,new[]{up},size)!=1)throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        finally { if(previous!=IntPtr.Zero)SetThreadDpiAwarenessContext(previous); }
    }

    [DllImport("kernel32.dll",SetLastError=true)]
    static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);

    public static double InputToProcessSignal(IntPtr process,IntPtr window,Point point,bool mouse,int hold)
    {
        if(mouse) Click(window,point,hold); else SendCloseCommand(window);
        uint status=WaitForSingleObject(process,25000);
        long signal=Stopwatch.GetTimestamp();
        if(status!=0)throw new InvalidOperationException("Player process handle did not signal normally: "+status);
        return (signal-InputStamp)*1000.0/Stopwatch.Frequency;
    }

    public static void SendCloseCommand(IntPtr window)
    {
        // This variant preserves WM_SYSCOMMAND's synchronous delivery. It never posts WM_CLOSE.
        MarkInput();
        SendMessageTimeout(window,0x0112,(IntPtr)0xf060,(IntPtr)(-1),2,18000,out _);
    }
}
'@
}

$exe=(Resolve-Path -LiteralPath $Player).Path
$directory=[IO.Path]::GetFullPath($Output)
[void][IO.Directory]::CreateDirectory($directory)
$crashes=Join-Path $env:LOCALAPPDATA 'Temp/Frontline Lab/Frontline Lab/Crashes'
function CrashInventory {
    @(Get-ChildItem -LiteralPath $crashes -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        [pscustomobject]@{name=$_.Name;dumps=@(Get-ChildItem -LiteralPath $_.FullName -File -Filter '*.dmp' |
            ForEach-Object {[pscustomobject]@{name=$_.Name;bytes=$_.Length}})}
    })
}

$samples=for($n=1;$n -le $Runs;$n++){
    $log=Join-Path $directory ("run-$n.log")
    if(Test-Path -LiteralPath $log){throw "Use a fresh output directory: $log"}
    $previous=@(CrashInventory)
    $argsForPlayer=@('-force-d3d11','-screen-width','1280','-screen-height','800','-screen-fullscreen','0',
        '-logFile',('"'+$log+'"'),'--trace-shutdown')+$PlayerArgs
    $app=Start-Process -FilePath $exe -ArgumentList $argsForPlayer -WorkingDirectory (Split-Path $exe) -WindowStyle Normal -PassThru
    try {
        $ready=[Diagnostics.Stopwatch]::StartNew()
        do {
            if($app.HasExited -or $ready.Elapsed.TotalSeconds -gt 25){throw 'The test player did not become ready.'}
            Start-Sleep -Milliseconds 35
            $window=[FrontlineCloseTest]::Find($app.Id)
        } while($window -eq [IntPtr]::Zero -or !(Test-Path -LiteralPath $log) -or
            !(Select-String -LiteralPath $log -Pattern '^UnloadTime:' -Quiet))
        Start-Sleep -Milliseconds 700
        $point=$null
        if($Method -eq 'Mouse'){$point=[FrontlineCloseTest]::PrepareClick($window)}
        $processHandle=$app.Handle
        $testedPoint=if($point){$point}else{[FrontlineCloseTest+Point]::new()}
        $watch=[Diagnostics.Stopwatch]::StartNew()
        $signalMs=[FrontlineCloseTest]::InputToProcessSignal($processHandle,$window,$testedPoint,($Method -eq 'Mouse'),$HoldMilliseconds)
        $exitStamp=[Diagnostics.Stopwatch]::GetTimestamp()
        $watch.Stop()
        # The kernel exit timestamp is independent of when the measuring PowerShell thread awakens.
        $kernelMs=($app.ExitTime.ToUniversalTime().ToFileTimeUtc()-[FrontlineCloseTest]::InputUtc)/10000.0
        $inputToWaitMs=1000*($exitStamp-[FrontlineCloseTest]::InputStamp)/[Diagnostics.Stopwatch]::Frequency
        # The crash handler can finish writing a dump after the game process signals.
        Start-Sleep -Milliseconds 1500
        $new=@(CrashInventory | Where-Object {$_.name -notin $previous.name})
        $markers=@(Select-String -LiteralPath $log -Pattern 'FRONTLINE_WINDOW_CLOSE|FRONTLINE_SHUTDOWN|CodeReloadManager destroyed|Crash!!!|uxtheme|SendMessageW|fb69|Access violation' |
            ForEach-Object {$_.Line})
        [ordered]@{run=$n;pid=$app.Id;method=$Method;captionPoint=$point;hitTest=if($point){'HTCLOSE'}else{$null};
            # Start the release metric at the actual native input injection. The outer
            # stopwatch also includes PowerShell binding/JIT and target verification before the click.
            elapsedMs=[Math]::Round($signalMs,2);nativeSignalMs=[Math]::Round($signalMs,2);kernelExitMs=[Math]::Round($kernelMs,2);
            harnessMs=[Math]::Round($watch.Elapsed.TotalMilliseconds,2);
            beforeInputMs=[Math]::Round($watch.Elapsed.TotalMilliseconds-$inputToWaitMs,2);
            inputToWaitMs=[Math]::Round($inputToWaitMs,2);exitCode=$app.ExitCode;newCrashDirectories=$new;
            crashCount=$new.Count;dumpCount=@($new | ForEach-Object {$_.dumps}).Count;log=$log;markers=$markers}
        Write-Host ("{0} {1}/{2}: {3:N2} ms; exit={4}; new crash directories={5}" -f $Method,$n,$Runs,$signalMs,$app.ExitCode,$new.Count)
    }
    finally {
        try {
            if(!$app.HasExited -and $app.CloseMainWindow()){[void]$app.WaitForExit(2500)}
        } finally {$app.Dispose()}
    }
}
$runtime=Join-Path (Split-Path $exe) 'FrontlineLab_Data/Managed/Frontline.Runtime.dll'
[ordered]@{player=$exe;fileVersion=(Get-Item -LiteralPath $exe).VersionInfo.ProductVersion;
    executableSha256=(Get-FileHash -LiteralPath $exe).Hash.ToLowerInvariant();
    runtimeSha256=(Get-FileHash -LiteralPath $runtime).Hash.ToLowerInvariant();
    capturedAt=[DateTimeOffset]::Now.ToString('o');clockScope='elapsedMs/nativeSignalMs measure SendInput/SC_CLOSE to WaitForSingleObject on the tested process, without returning to PowerShell. kernelExitMs uses the kernel exit timestamp. inputToWaitMs includes resuming the shell. harnessMs also includes pre-input target checks.';method=$Method;holdMilliseconds=$HoldMilliseconds;samples=@($samples)} |
    ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $directory 'timing.json') -Encoding utf8NoBOM
