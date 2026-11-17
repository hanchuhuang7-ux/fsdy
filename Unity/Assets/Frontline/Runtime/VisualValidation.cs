using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Frontline.Core;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Networking;

namespace Frontline.UnityGame
{
    // Opt-in acceptance runner. Never active in a normal game session.
    public sealed partial class VisualValidation:MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]static extern void FrontlineQAInit();
        [DllImport("__Internal")]static extern int FrontlineQABatches();
        static long Allocated(){return -1;}
#else
        static long Allocated(){return AllocationMeter.Read();}
#endif
        FrontlineApp app;string output;readonly List<object> rows=new List<object>();
        public static bool Requested {get {return Array.IndexOf(Environment.GetCommandLineArgs(),"--visual-qa")>=0||Application.absoluteURL.Contains("frontlineQA=1");}}
        public void Initialize(FrontlineApp value){app=value;StartCoroutine(GuardValidation(Run()));}
        IEnumerator GuardValidation(IEnumerator routine)
        {
            var stack=new Stack<IEnumerator>();stack.Push(routine);
            while(stack.Count>0){
                bool more=false;object current=null;Exception failure=null;
                try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}catch(Exception e){failure=e;}
                if(failure!=null){Debug.LogException(failure);app.Tell("验收失败："+failure.Message);
#if !UNITY_WEBGL && !UNITY_EDITOR
                    Application.Quit(1);
#endif
                    yield break;}
                if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(current is IEnumerator nested){stack.Push(nested);continue;}yield return current;
            }
        }
        string Option(string name,string fallback){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        IEnumerator Save(string name,byte[] bytes,string type)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var origin=new Uri(Application.absoluteURL);if(origin.Host!="127.0.0.1"&&origin.Host!="localhost")throw new InvalidOperationException("Visual QA upload is restricted to loopback");
            using(var request=new UnityWebRequest(origin.GetLeftPart(UriPartial.Authority)+"/__qa/"+name,"POST")){request.uploadHandler=new UploadHandlerRaw(bytes);request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type",type);yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success)throw new IOException(request.error);}
#else
            Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,name),bytes);yield return null;
#endif
        }
        IEnumerator Capture(string map,string phase)
        {
            yield return new WaitForEndOfFrame();var texture=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0,false);texture.Apply();var bytes=texture.EncodeToPNG();Destroy(texture);yield return Save("v08-"+Application.platform+"-"+Screen.width+"-"+map+"-"+phase+".png",bytes,"image/png");
        }
        static double Median(double[] values){Array.Sort(values);return values[values.Length/2];}
        IEnumerator Run()
        {
            output=Option("--qa-out",Path.Combine(Application.persistentDataPath,"Visual08"));yield return null;app.SetMode("sandbox");app.View.SetQuality(1);app.Muted=true;
#if UNITY_WEBGL && !UNITY_EDITOR
            FrontlineQAInit();
#endif
            foreach(string map in new[]{"meadow","ruins","highland"})
            {
                app.SetMap(map);yield return new WaitForSecondsRealtime(1);yield return Capture(map,"deploy");app.RequestStart();yield return new WaitForSecondsRealtime(3);app.Paused=true;yield return Capture(map,"battle");app.ReturnToDeploy();yield return null;
            }
            yield return CombatScenes();
            app.SetMap("meadow");var qualityRows=new List<object>();for(int q=0;q<3;q++){app.View.SetQuality(q);yield return null;yield return new WaitForEndOfFrame();qualityRows.Add(new {quality=q,instances=app.View.RenderInstances,submissions=app.View.RenderSubmissions,shadowDistance=QualitySettings.shadowDistance,staticRebuilds=app.View.StaticRebuilds});}app.View.SetQuality(1);
            foreach(int count in new[]{45,60})
            {
                app.ReturnToDeploy();app.SetMap("meadow");var c=app.Config;c.budget=10000;c.red.Clear();c.blue.Clear();c.props.Clear();
                if(count==45){var sample=app.Data.Import(Resources.Load<TextAsset>("large-battle").text);c.red=sample.red;c.blue=sample.blue;}
                else {string[] types={"militia","shield","spear","archer","bomber","giant","cavalry","crossbow","medic","catapult"};for(int i=0;i<60;i++){float x=3.2f+(i/10)*2.65f,z=-9+(i%10)*2;string type=types[i%10];c.red.Add(new UnitPlacement(type,-x,z));c.blue.Add(new UnitPlacement(type,x,z));}}
                app.Refresh();app.RequestStart();yield return new WaitForSecondsRealtime(1);
                const int samples=360;var milliseconds=new double[samples];var batchValues=new double[samples];var allocation=new double[samples];var submissions=new double[samples];
                var batches=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Draw Calls Count",1);var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);long previous=Allocated();int rebuilds=app.View.StaticRebuilds;
                for(int i=0;i<samples;i++){yield return null;long now=Allocated();milliseconds[i]=Time.unscaledDeltaTime*1000;batchValues[i]=batches.Valid?batches.LastValue:-1;
#if UNITY_WEBGL && !UNITY_EDITOR
                    int glCalls=FrontlineQABatches();if(!batches.Valid)batchValues[i]=glCalls;
#endif
                    allocation[i]=gc.Valid?gc.LastValue:now<0?-1:now-previous;previous=now;submissions[i]=app.View.RenderSubmissions;}
                double mean=0,max=-1;foreach(double a in allocation){mean+=a;if(a>max)max=a;}mean/=samples;
                rows.Add(new {unitsPerTeam=count,width=Screen.width,height=Screen.height,quality=app.View.QualityLevel,samples=samples,medianFrameMilliseconds=Median(milliseconds),p95FrameMilliseconds=milliseconds[(int)(samples*.95)],medianFps=1000/milliseconds[samples/2],batchesCounterAvailable=batches.Valid,batchMeasurement=batches.Valid?"Unity Draw Calls Count (current Unity 6.6 rendering counter)":Application.platform==RuntimePlatform.WebGLPlayer?"Actual WebGL draw calls including shadow and UI passes":"Unavailable",medianBatches=Median(batchValues),medianRenderSubmissions=Median(submissions),gcCounterAvailable=gc.Valid,gcMeasurement=gc.Valid?"Unity GC Allocated In Frame":!AllocationMeter.Available?"Unavailable in this player; -1 is not zero allocation":"Calibrated per-thread counter",medianGcBytes=Median(allocation),meanGcBytes=mean,maxGcBytes=max,staticRebuildsDuringSample=app.View.StaticRebuilds-rebuilds,projectilePoolMisses=((UnityWorld)app.Battle.World).ProjectilePoolMisses,corpsePoolMisses=((UnityWorld)app.Battle.World).CorpsePoolMisses});batches.Dispose();gc.Dispose();app.Paused=true;yield return Capture("meadow",count+"v"+count);app.ReturnToDeploy();yield return null;
            }
            string report=Json.Write(new {developmentBuild=Debug.isDebugBuild,version=Application.version,unity=Application.unityVersion,platform=Application.platform.ToString(),graphics=SystemInfo.graphicsDeviceName,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),width=Screen.width,height=Screen.height,targetFps=Application.targetFrameRate,qualityAudit=qualityRows,rows=rows});yield return Save("visual-"+Application.platform+"-"+Screen.width+".json",System.Text.Encoding.UTF8.GetBytes(report),"application/json");Debug.Log("FRONTLINE_VISUAL_QA_OK "+Application.platform+" "+Screen.width);app.SetMap("meadow");app.Tell("视觉与性能验收完成");
#if !UNITY_WEBGL && !UNITY_EDITOR
            Application.Quit(0);
#endif
        }
    }
}



