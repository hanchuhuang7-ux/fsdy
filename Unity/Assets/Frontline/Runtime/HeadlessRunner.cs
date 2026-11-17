using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Frontline.Core;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif
namespace Frontline.UnityGame
{
    [Serializable] public class BotProfile {public string command;public string[] args=new string[0];}
    [Serializable] public class BridgeStats {public int observations,accepted,rejected,timeouts;public List<string> reasons=new List<string>();}
    public sealed class HeadlessRunner:MonoBehaviour
    {
        Catalog data;IWorldFactory factory;readonly string[] args=Environment.GetCommandLineArgs();public static bool Requested {get{return Environment.GetCommandLineArgs().Contains("--frontline-task");}}
        public void Initialize(Catalog c,IWorldFactory f){data=c;factory=f;StartCoroutine(Guard(Run()));}
        string Option(string key,string fallback=null){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        static void Quit(int code){
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }
        IEnumerator Guard(IEnumerator routine){while(true){bool next;object current;try{next=routine.MoveNext();current=next?routine.Current:null;}catch(Exception e){UnityEngine.Debug.LogException(e);(routine as IDisposable)?.Dispose();Quit(1);yield break;}if(!next)break;yield return current;}(routine as IDisposable)?.Dispose();Quit(0);}
        static void Save(string path,object value){File.WriteAllText(path,Json.Write(value),new System.Text.UTF8Encoding(false));}
        IEnumerator Run()
        {
            string task=Option("--frontline-task"),output=Path.GetFullPath(Option("--out","frontline-results"));Directory.CreateDirectory(output);var c=Option("--config")==null?data.Default():data.Import(File.ReadAllText(Option("--config")));
            if(task=="checks"){
                c.commander.enabled=true;string expected=null;for(int run=0;run<2;run++){using(var b=new Battle(data,c,factory)){var frames=new List<Frame>{b.Snapshot()};if(!b.IssueOrder("red",new Order("inspire",-6,0),out var reason))throw new Exception(reason);while(b.Result==null){for(int i=0;i<120&&b.Result==null;i++){b.Step();if(b.Tick%6==0)frames.Add(b.Snapshot());}yield return null;}if(frames[frames.Count-1].tick!=b.Tick)frames.Add(b.Snapshot());var replay=Replay.Create(data,c,frames,b.Result);Replay.Import(data,Json.Write(replay));string actual=Json.Write(b.Result);if(run==0)expected=actual;else if(expected!=actual)throw new Exception("Same-build PhysX outcome diverged");Save(Path.Combine(output,"repeat-"+run+".replay.json"),replay);UnityEngine.Debug.Log("FRONTLINE_PHYSICS_OK run="+run+" ticks="+b.Tick+" winner="+b.Result.winner);}yield return null;}Save(Path.Combine(output,"checks.json"),new{sameBuildDeterministic=true,engine=Replay.Engine,unity=Application.unityVersion});yield break;
            }
            if(task=="tournament"){
                var entrants=Option("--entrants")==null?Catalog.Styles.Take(3).Select(s=>new Entrant{id=s,name=s,ai=s}).ToList():Json.Read<List<Entrant>>(File.ReadAllText(Option("--entrants")));
                var maps=Option("--maps",c.map).Split(',').ToList();var seeds=Option("--seeds","42,77").Split(',').Select(uint.Parse).ToList();var t=new Tournament(data,factory,entrants,maps,seeds,c.budget,c.commander.enabled);foreach(var p in t.Run()){if(p.completed>0)UnityEngine.Debug.Log(p.phase+" "+p.completed+" / "+p.total);yield return null;}Save(Path.Combine(output,"tournament.json"),t.Report);yield break;
            }
            if(task=="benchmark"){
                var rows=new List<object>();foreach(string fixture in new[]{"default-battle","large-battle"}){var config=data.Import(Resources.Load<TextAsset>(fixture).text);for(int run=0;run<6;run++){yield return null;using(var b=new Battle(data,config,factory)){var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<720&&b.Result==null;i++){b.Step();if(b.Tick%6==0)b.Snapshot();}watch.Stop();rows.Add(new {fixture=fixture,run=run,steps=b.Tick,milliseconds=watch.Elapsed.TotalMilliseconds,millisecondsPerStep=watch.Elapsed.TotalMilliseconds/Math.Max(1,b.Tick),units=config.red.Count+config.blue.Count});}}}Save(Path.Combine(output,"benchmark.json"),new {engine=Replay.Engine,unity=Application.unityVersion,platform=Application.platform.ToString(),warmupRuns=1,measuredRuns=5,rows=rows});yield break;
            }
            if(task=="smoke"){
                var results=new List<object>();foreach(string map in data.Maps.Keys){var config=data.Default(map);config.commander.enabled=true;config.commander.aiTeams=new List<string>{"red","blue"};using(var b=new Battle(data,config,factory)){var frames=new List<Frame>{b.Snapshot()};while(b.Result==null){for(int i=0;i<120&&b.Result==null;i++){b.Step();if(b.Tick%6==0)frames.Add(b.Snapshot());}yield return null;}if(frames[frames.Count-1].tick!=b.Tick)frames.Add(b.Snapshot());var replay=Replay.Create(data,config,frames,b.Result);Replay.Import(data,Json.Write(replay));results.Add(new{map=map,ticks=b.Tick,winner=b.Result.winner,frames=frames.Count,orders=b.Commander.log.Count});Save(Path.Combine(output,map+".replay.json"),replay);}}Save(Path.Combine(output,"smoke.json"),results);yield break;
            }
            if(task!="agent")throw new ArgumentException("--frontline-task 需要 agent / tournament / benchmark / smoke");
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            c.commander.enabled=true;c.commander.aiTeams.Clear();var series=new BattleSeries(data,c);var stats=new BridgeStats();int timeout=Mathf.Clamp(int.Parse(Option("--timeout","250")),10,5000);
            var red=Json.Read<BotProfile>(File.ReadAllText(Option("--red-bot")??throw new ArgumentException("缺少 --red-bot")));var blue=Json.Read<BotProfile>(File.ReadAllText(Option("--blue-bot")??throw new ArgumentException("缺少 --blue-bot")));
            while(!series.Complete){int round=series.Archive.rounds.Count+1;if(round>1)c=series.Next();var frames=new List<Frame>();using(var rbot=new StdioBot(red))using(var bbot=new StdioBot(blue))using(var b=new Battle(data,c,factory)){
                frames.Add(b.Snapshot());while(b.Result==null){if(b.Tick%AgentProtocol.Interval==0){string rid=round+":red:"+b.Tick,bid=round+":blue:"+b.Tick;var ro=Json.Write(AgentProtocol.Observe(b,"red",rid,round));var bo=Json.Write(AgentProtocol.Observe(b,"blue",bid,round));stats.observations+=2;var rq=rbot.Query(ro,timeout);var bq=bbot.Query(bo,timeout);while(!rq.IsCompleted||!bq.IsCompleted)yield return null;foreach(var item in new[]{Tuple.Create("red",rid,rq.Result),Tuple.Create("blue",bid,bq.Result)}){if(item.Item3==null){stats.timeouts++;continue;}var receipt=AgentProtocol.Accept(b,item.Item1,item.Item2,item.Item3);if(receipt.ok&&!receipt.skipped)stats.accepted++;if(!receipt.ok){stats.rejected++;if(stats.reasons.Count<20)stats.reasons.Add(receipt.reason);}}}
                    for(int i=0;i<60&&b.Result==null;i++){b.Step();if(b.Tick%6==0)frames.Add(b.Snapshot());}yield return null;}
                if(frames[frames.Count-1].tick!=b.Tick)frames.Add(b.Snapshot());var replay=Replay.Create(data,c,frames,b.Result);Save(Path.Combine(output,"round-"+round+".replay.json"),replay);series.Record(c,b.Result);UnityEngine.Debug.Log("Agent round "+round+": "+b.Result.winner+" at "+b.Time+"s");}
            }Save(Path.Combine(output,"series.json"),series.Archive);Save(Path.Combine(output,"agent-stats.json"),stats);
#else
            throw new PlatformNotSupportedException("外部子进程 Bot 需要 Windows 版本");
#endif
        }
    }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    sealed class StdioBot:IDisposable
    {
        readonly Process process;readonly ConcurrentQueue<string> lines=new ConcurrentQueue<string>();volatile bool dead;int count;
        public StdioBot(BotProfile profile)
        {
            if(profile==null||string.IsNullOrWhiteSpace(profile.command)||profile.args==null||profile.args.Length>64)throw new ArgumentException("Bot 启动配置无效");var start=new ProcessStartInfo{FileName=profile.command,Arguments=string.Join(" ",profile.args.Select(Quote)),UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};process=Process.Start(start)??throw new IOException("无法启动 Bot");new Thread(Read){IsBackground=true}.Start();new Thread(()=>{try{var buffer=new char[1024];while(process.StandardError.Read(buffer,0,buffer.Length)>0){}}catch{}}){IsBackground=true}.Start();
        }
        static string Quote(string s){if(s==null||s.IndexOf('\0')>=0)throw new ArgumentException("Bot 参数无效");var b=new StringBuilder("\"");int slash=0;foreach(char c in s){if(c=='\\'){slash++;continue;}if(c=='\"'){b.Append('\\',slash*2+1);b.Append(c);}else{b.Append('\\',slash);b.Append(c);}slash=0;}b.Append('\\',slash*2);b.Append('"');return b.ToString();}
        void Read(){try{var line=new StringBuilder();while(!dead){int c=process.StandardOutput.Read();if(c<0)break;if(c=='\n'){if(Interlocked.Increment(ref count)>4)break;lines.Enqueue(line.ToString().TrimEnd('\r'));line.Clear();}else{line.Append((char)c);if(line.Length>AgentProtocol.MaxLine)break;}}}catch{}finally{dead=true;}}
        public async Task<string> Query(string observation,int timeout)
        {
            if(dead)return null;while(lines.TryDequeue(out _))Interlocked.Decrement(ref count);try{var send=process.StandardInput.WriteLineAsync(observation);if(await Task.WhenAny(send,Task.Delay(timeout))!=send){Dispose();return null;}await send;var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<timeout&&!dead){if(lines.TryDequeue(out var line)){Interlocked.Decrement(ref count);return line;}await Task.Delay(1);}Dispose();return null;}catch{Dispose();return null;}
        }
        public void Dispose(){dead=true;try{if(!process.HasExited)process.Kill();}catch{}try{process.Dispose();}catch{}}
    }
#endif
}

