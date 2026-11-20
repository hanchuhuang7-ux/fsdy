using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Frontline.Core;
using Unity.Profiling;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        public static readonly bool PerformanceRequested =
            Array.IndexOf(Environment.GetCommandLineArgs(),"--perf-quick")>=0 ||
            (Uri.TryCreate(Application.absoluteURL,UriKind.Absolute,out var address) && address.IsLoopback &&
             address.Query.Contains("frontlineQA=perf"));

        bool gpuTiming;
        readonly FrameTiming[] gpuFrame=new FrameTiming[1];

        IEnumerator RunPerformance()
        {
            yield return null;
            output=Option("--qa-out",Path.Combine(Application.persistentDataPath,"QuickPerformance"));
            int oldQuality=app.View.QualityLevel,oldTarget=Application.targetFrameRate,oldVsync=QualitySettings.vSyncCount;
            int expectedDefault=Application.platform==RuntimePlatform.WebGLPlayer?1:2;
            Require(oldQuality==expectedDefault&&app.View.PreferredQuality==expectedDefault,"Platform launch quality mismatch");
            if(Application.platform==RuntimePlatform.WindowsPlayer)
            {
                Require(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Direct3D11,"Desktop QA requires D3D11");
                var expectedMode=CpuWork.SingleThreadQa?UnityEngine.Rendering.RenderingThreadingMode.Direct:
                    UnityEngine.Rendering.RenderingThreadingMode.LegacyJobified;
                Require(SystemInfo.renderingThreadingMode==expectedMode,
                    "Unexpected D3D11 threading mode. For the single-thread baseline pass -force-gfx-direct.");
            }
            app.Muted=true;
            app.FixedPerformanceStep=true;app.View.MeasureCasterVertices=true;
            QualitySettings.vSyncCount=0;
            Application.targetFrameRate=-1;
            int standardWidth=Screen.width,standardHeight=Screen.height;
            gpuTiming=Application.platform==RuntimePlatform.WindowsPlayer&&FrameTimingManager.IsFeatureEnabled();
#if UNITY_WEBGL && !UNITY_EDITOR
            FrontlineQAInit();
#endif
            int[] levels={expectedDefault,expectedDefault==2?1:2};
            const int forcedFps=-1, forcedVsync=0;
            try
            {
                foreach(int quality in levels)
                {
                    app.SetMode("sandbox");app.View.SetQuality(quality);
                    Require(app.View.PreferredQuality==quality,"Quality choice did not persist");
                    app.View.ResetCamera();
                    yield return MeasureQualityTier();
                    app.ReturnManor();app.SetManorMode(false);
                }
#if UNITY_STANDALONE_WIN && !UNITY_WEBGL
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"--qa-gpu-stress")>=0)
                {
                    Screen.SetResolution(2560,1600,FullScreenMode.Windowed);
                    yield return WaitForState(()=>Screen.width==2560&&Screen.height==1600,"GPU stress resolution did not apply");
                    app.SetMode("sandbox");app.View.SetQuality(2);
                    yield return MeasureArena(120,"120v120-gpu",capture:true);
                    Screen.SetResolution(standardWidth,standardHeight,FullScreenMode.Windowed);
                    yield return WaitForState(()=>Screen.width==standardWidth&&Screen.height==standardHeight,"Window resolution did not restore");
                }
#endif
            }
            finally
            {
                app.FixedPerformanceStep=false;app.View.MeasureCasterVertices=false;
                app.View.SetQuality(oldQuality);
                QualitySettings.vSyncCount=oldVsync;
                Application.targetFrameRate=oldTarget;
            }
            string result=Json.Write(new {version=Application.version,unity=Application.unityVersion,platform=Application.platform.ToString(),
                gpu=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),width=standardWidth,height=standardHeight,
                gpuFrameTimingEnabled=gpuTiming,instancingPageSize=BattleView.InstancingPageSize,
                processorCount=SystemInfo.processorCount,targetWorkers=CpuWork.TargetWorkers,unityJobWorkers=CpuWork.UnityWorkers,
                graphicsJobs=CpuWork.GraphicsJobs,graphicsJobMode=SystemInfo.renderingThreadingMode.ToString(),
                parallelRenderJobs=app.View.ParallelPoseWorkers>1,
                parallelTargetScan=CpuWork.TargetWorkers>1,singleThreadQa=CpuWork.SingleThreadQa,
                renderJobUnitThreshold=BattleView.ParallelPoseThreshold,targetScanUnitThreshold=Battle.ParallelTargetScanThreshold,
                targetScanMinPairs=Battle.ParallelTargetScanMinPairs,
                launchQuality=oldQuality,preferenceRoundtrip=true,qualityLevels=levels,targetFps=forcedFps,vSyncCount=forcedVsync,
                restoredTargetFps=Application.targetFrameRate,restoredVSyncCount=QualitySettings.vSyncCount,
#if UNITY_WEBGL && !UNITY_EDITOR
                shaderWarmupMilliseconds=app.View.WebShaderWarmupMs,
#endif
                rows});
            yield return Save("visual-perf-"+Application.platform+".json",Encoding.UTF8.GetBytes(result),"application/json");
            Debug.Log("FRONTLINE_QUICK_PERF_OK "+Application.platform+" tiers=2,1 scenes="+rows.Count);
#if !UNITY_WEBGL && !UNITY_EDITOR
            Application.Quit(0);
#endif
        }

        IEnumerator MeasureQualityTier()
        {
#if UNITY_STANDALONE_WIN && !UNITY_WEBGL
            int[] sizes={45,60,120};
#else
            int[] sizes={45,60};
#endif
            foreach(int count in sizes)yield return MeasureArena(count,count+"v"+count);
            app.SetManorMode(true);app.SetManorSpeed(0);
            var estate=new Manor {coin=5000,timber=5000,arms=200,food=500,day=4,cleared=3,raidReady=true};
            estate.Upgrade(0);estate.Upgrade(0);
            string[] layout={"cottage","farm","tavern","cottage","smithy","timber_camp","cottage","irrigated_field","pasture","drill_yard","chapel","cottage",null,"granary"};
            for(int i=0;i<layout.Length;i++)
            {
                if(layout[i]==null)continue;
                estate.Build(i+1,layout[i]);estate.Upgrade(i+1);estate.Upgrade(i+1);
            }
            estate.SetBackyard(1,"garden");estate.SetBackyard(4,"poultry");
            estate.SetBackyard(7,"workshop");estate.SetBackyard(12,"militia_yard");
            estate.ReclaimPlot(18);estate.Build(18,"farm");
            estate.ReclaimPlot(20);estate.Build(20,"timber_camp");
            estate.workers=24;estate.stock["militia"]=16;
            foreach(int plot in new[]{2,5,6,8,9})for(int n=0;n<3;n++)estate.Assign(plot,1);
            estate.Assign(3,1);estate.Assign(10,1);estate.Assign(0,1);
            estate.Assign(11,1);estate.Assign(14,1);
            foreach(var hero in app.Data.Data.heroes)estate.HireHero(3,hero);
            app.ImportText(estate.Export());
            app.View.FrameFrontier(0);yield return SamplePerformance("overview");
            app.View.FrameFrontier(1);yield return SamplePerformance("manor");
            app.PrepareFrontier();app.Battle.MarchFrontier(5,0);
            for(int i=0;i<500;i++)app.Battle.Step();
            app.View.FrameFrontier(2);yield return SamplePerformance("frontier");
        }

        IEnumerator MeasureArena(int count,string scene,bool capture=false)
        {
            app.SetMap("meadow");
            var c=app.Config;c.seed=42;c.budget=10000;c.props.Clear();c.red.Clear();c.blue.Clear();
            if(count==45)
            {
                var fixture=app.Data.Import(Resources.Load<TextAsset>("large-battle").text);
                c.red=fixture.red;c.blue=fixture.blue;
            }
            else
            {
                for(int i=0;i<count;i++)
                {
                    float x=count==120?3.2f+i/12*1.44f:3.2f+i/10*2.65f;
                    float z=count==120?-9.9f+i%12*1.8f:-9+i%10*2;
                    string type=app.Data.Data.units[i%10].id;
                    c.red.Add(new UnitPlacement(type,-x,z));c.blue.Add(new UnitPlacement(type,x,z));
                }
            }
#if UNITY_STANDALONE_WIN && !UNITY_WEBGL
            if(count==120)app.StartPerformanceStress();
            else
#endif
            {app.Refresh();app.RequestStart();}
            Require(app.Battle!=null&&app.Battle.Config.red.Count==count&&app.Battle.Config.blue.Count==count,
                "Invalid stress formation");
            app.View.ResetCamera();
            yield return SamplePerformance(scene);
#if UNITY_STANDALONE_WIN && !UNITY_WEBGL
            if(capture)
            {
                app.Paused=true;
                yield return ManorShot("gpu-stress-q2-"+Application.version);
            }
#endif
            app.ReturnToDeploy();
        }

        IEnumerator SamplePerformance(string scene)
        {
            const int count=81;
            var milliseconds=new double[count];var submissions=new double[count];var instances=new double[count];
            var draws=new double[count];var bytes=new double[count];var casters=new double[count];
            var mainVertices=new double[count];var mainSubmissions=new double[count];var shadowSubmissions=new double[count];
            var culledStaticInstances=new double[count];
            var gpuMilliseconds=new List<double>(count);
            ulong lastGpuStamp=0;
            var renderCpu=new double[count];var simulationCpu=new double[count];
            double millisecondsPerTick=1000.0/System.Diagnostics.Stopwatch.Frequency;
            using(var draw=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1))
            using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1))
            {
                // Warm up shadows and batched pages. Sample exactly one simulation step per rendered battle frame.
                for(int i=0;i<72;i++)
                {
                    if(gpuTiming)FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                }
#if UNITY_WEBGL && !UNITY_EDITOR
                FrontlineQABatches();
#endif
                long last=Allocated();double previous=Time.realtimeSinceStartupAsDouble;
                int startTick=app.Battle?.Tick??0,staticRebuilds=app.View.StaticRebuilds;
                int startPoseJobs=app.View.ParallelPoseFrames,startScanJobs=app.Battle?.ParallelTargetScanTicks??0;
                int startFallbacks=app.Battle?.ParallelTargetScanFallbacks??0;
                for(int i=0;i<count;i++)
                {
                    if(gpuTiming)FrameTimingManager.CaptureFrameTimings();
                    yield return null;
                    double now=Time.realtimeSinceStartupAsDouble;
                    if(gpuTiming&&FrameTimingManager.GetLatestTimings(1,gpuFrame)>0)
                    {
                        var timing=gpuFrame[0];
                        if(timing.frameStartTimestamp!=lastGpuStamp&&timing.gpuFrameTime>0&&
                            !double.IsNaN(timing.gpuFrameTime)&&!double.IsInfinity(timing.gpuFrameTime))
                            gpuMilliseconds.Add(timing.gpuFrameTime);
                        lastGpuStamp=timing.frameStartTimestamp;
                    }
                    milliseconds[i]=(now-previous)*1000;previous=now;
                    submissions[i]=app.View.RenderSubmissions;instances[i]=app.View.RenderInstances;
                    casters[i]=app.View.CasterVertices;
                    mainVertices[i]=app.View.MainVertices;culledStaticInstances[i]=app.View.CulledStaticInstances;
                    shadowSubmissions[i]=app.View.ShadowOnlySubmissions;
                    mainSubmissions[i]=app.View.RenderSubmissions-app.View.ShadowOnlySubmissions;
                    renderCpu[i]=app.View.PerformanceRenderTicks*millisecondsPerTick;
                    simulationCpu[i]=app.PerformanceSimulationTicks*millisecondsPerTick;
                    draws[i]=draw.Valid&&draw.Count>0?draw.LastValue:-1;
#if UNITY_WEBGL && !UNITY_EDITOR
                    draws[i]=FrontlineQABatches();
#endif
                    long current=Allocated();
                    bytes[i]=gc.Valid&&gc.Count>0?gc.LastValue:last>=0?current-last:-1;
                    last=current;
                }
                double total=0;foreach(double value in bytes)total+=value;
                var ordered=(double[])milliseconds.Clone();
                double median=Median(ordered);
                int longFrames=0;foreach(double value in milliseconds)if(value>33)longFrames++;
                int renderJobFrames=app.View.ParallelPoseFrames-startPoseJobs;
                int targetScanTicks=(app.Battle?.ParallelTargetScanTicks??0)-startScanJobs;
                int targetScanFallbacks=(app.Battle?.ParallelTargetScanFallbacks??0)-startFallbacks;
                var world=app.Battle?.World as UnityWorld;
                if(CpuWork.TargetWorkers==1)Require(renderJobFrames==0&&targetScanTicks==0,"Parallel work in the single-thread path");
                if((scene=="60v60"||scene=="120v120")&&app.View.ParallelPoseWorkers>1)
                    Require(renderJobFrames>0,"Render jobs did not run in "+scene);
                if(scene=="120v120"&&CpuWork.TargetWorkers>1)
                    Require(targetScanTicks>0,"Target scan did not run in "+scene);
                Require(world==null||world.ProjectilePoolMisses==0&&world.CorpsePoolMisses==0,"Unexpected physics pool miss in "+scene);
                gpuMilliseconds.Sort();
                int gpuCount=gpuMilliseconds.Count;
                int weatherParticles=app.View.WeatherParticles;
                if(app.View.ViewingFrontier)Require(app.View.Weather==ManorWeather.Drizzle&&
                    weatherParticles==app.View.WeatherBudget&&!app.View.WeatherCastsShadows,"Drizzle fixture did not exercise the manor weather");
                rows.Add(new {scene,quality=app.View.QualityLevel,width=Screen.width,height=Screen.height,
                    weather=app.View.Weather.ToString(),weatherParticles,
                    samples=count,warmupFrames=72,startTick,endTick=app.Battle?.Tick??0,
                    gpuFrameTimeMs=gpuCount==0?(double?)null:gpuMilliseconds[gpuCount/2],
                    p95GpuFrameTimeMs=gpuCount==0?(double?)null:gpuMilliseconds[(int)(gpuCount*.95)],
                    gpuSamples=gpuCount,gpuTimingSource=gpuCount>0?"Unity FrameTimingManager":null,
                    nearClip=app.View.ViewCamera.nearClipPlane,cascadeSplits=QualitySettings.shadowCascade4Split.ToString(),
                    msaa=QualitySettings.antiAliasing,shadowResolution=QualitySettings.shadowResolution.ToString(),cascades=QualitySettings.shadowCascades,
                    cameraDistance=app.View.CameraDistance,lowActorLod=app.View.LowActorLod,lowSceneryLod=app.View.LowSceneryLod,
                    medianFrameMilliseconds=median,p95FrameMilliseconds=ordered[(int)(count*.95)],maxFrameMilliseconds=ordered[count-1],
                    framesOver33ms=longFrames,frameMilliseconds=milliseconds,
                    medianRenderCpuMilliseconds=Median(renderCpu),p95RenderCpuMilliseconds=renderCpu[(int)(count*.95)],
                    medianSimulationCpuMilliseconds=Median(simulationCpu),p95SimulationCpuMilliseconds=simulationCpu[(int)(count*.95)],
                    renderJobFrames,targetScanTicks,targetScanFallbacks,targetScanWarmupTicks=startScanJobs,
                    renderSubmissions=Median(submissions),mainSubmissions=Median(mainSubmissions),
                    shadowOnlySubmissions=Median(shadowSubmissions),renderInstances=Median(instances),
                    submittedMainVertices=Median(mainVertices),culledStaticInstances=Median(culledStaticInstances),drawCalls=Median(draws),
                    gcBytes=Median(bytes),meanGcBytes=total/count,unityDrawCounter=draw.Valid&&draw.Count>0,unityGcCounter=gc.Valid&&gc.Count>0,
                    threadAllocationCounter=AllocationMeter.Available,submittedCasterVertices=Median(casters),shadowDistance=QualitySettings.shadowDistance,
                    staticRebuilds=app.View.StaticRebuilds-staticRebuilds,projectilePoolMisses=world?.ProjectilePoolMisses??0,
                    corpsePoolMisses=world?.CorpsePoolMisses??0});
            }
        }
    }
}
