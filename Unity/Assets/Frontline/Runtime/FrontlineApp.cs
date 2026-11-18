using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public static class Viewport {public static bool Compact {get{return Screen.width<700||Screen.height>Screen.width*1.25f;}}}
    public sealed class FrontlineApp:MonoBehaviour
    {
        public Catalog Data {get;private set;}public Deployment Config {get;private set;}public BattleView View {get;private set;}public Battle Battle {get;private set;}
        public ReplayPackage ReplayFile {get;private set;}public TournamentReport ArenaReport {get;private set;}public BattleSeries Series {get;private set;}
        public readonly IWorldFactory Factory=new UnityWorldFactory();public readonly List<int> Selected=new List<int>();public readonly List<Entrant> ImportedEntrants=new List<Entrant>();
        public string Phase="deploy",Mode="ai",Team="red",Hotseat="red",Style="counter",UnitType="militia",PropType,Stance="advance",Tool="place",OrderType="inspire",Message="欢迎来到阵线实验室",Progress;
        public bool Paused,Bo3,TouchCamera=true,Modal,Busy,Muted,BriefOpen,Mirror;public float Speed=1,ReplayTime;public int PlayerLimit=2200,FavoriteSlot;public List<Favorite> Favorites=new List<Favorite>();
        public event Action Changed;public event Action<string> Notice;readonly Stack<Deployment> undo=new Stack<Deployment>();readonly List<Frame> recorded=new List<Frame>();Frame live;Frame[] archive;int archiveIndex;float accumulator;Coroutine job;IEnumerator activeJob;bool loaded;
        bool HasJob => Busy || job != null || activeJob != null;
        public string EditingTeam {get{return Mode=="hotseat"?Hotseat=="blue"?"blue":"red":Team;}}
        public bool CanEdit {get{return Phase=="deploy"&&!Busy&&(Mode!="hotseat"||Hotseat=="red"||Hotseat=="blue");}}
        public Deployment PublicConfig {get{return Mode=="hotseat"&&Hotseat!="revealed"?Catalog.Visible(Config,Hotseat):Json.Clone(Config);}}
        public string Diagnosis {get{return Data.Diagnose(PublicConfig,EditingTeam);}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Bootstrap(){if(FindAnyObjectByType<FrontlineApp>()==null)new GameObject("Frontline").AddComponent<FrontlineApp>();}
        void Awake()
        {
            Application.targetFrameRate=60;Time.fixedDeltaTime=Catalog.Step;Screen.sleepTimeout=SleepTimeout.NeverSleep;
            Data=new Catalog(Resources.Load<TextAsset>("catalog").text);if(HeadlessRunner.Requested){gameObject.AddComponent<HeadlessRunner>().Initialize(Data,Factory);enabled=false;return;}
            Config=Data.Default();try{var favorites=PlayerPrefs.GetString("frontline.unity.favorites","");if(favorites.Length>0)Favorites=Json.Read<List<Favorite>>(favorites,1024*1024);}catch(Exception e){Debug.LogWarning(e.Message);}while(Favorites.Count<5)Favorites.Add(null);if(Favorites.Count>5)Favorites=Favorites.Take(5).ToList();
            View=gameObject.AddComponent<BattleView>();View.Data=Data;gameObject.AddComponent<BattleInput>().Initialize(this);gameObject.AddComponent<NativeHud>().Initialize(this);gameObject.AddComponent<BattleAudio>().Initialize(this);loaded=true;Refresh();if(VisualValidation.Requested)gameObject.AddComponent<VisualValidation>().Initialize(this);
        }
        public void Refresh(){if(!loaded)return;View.Config=Config;View.Current=Phase=="deploy"?null:live;View.Deploying=Phase=="deploy";View.HiddenTeam=Mode=="hotseat"&&Hotseat!="revealed"?(Hotseat=="red"?"blue":Hotseat=="blue"?"red":"both"):null;View.Selection.Clear();int offset=EditingTeam=="red"?1:Config.red.Count+1;foreach(int i in Selected)View.Selection.Add(i+offset);Changed?.Invoke();}
        public void Tell(string message){Message=message;Notice?.Invoke(message);}
        public void Try(Action action){try{action();}catch(Exception e){Tell(e.Message);Debug.LogWarning(e.Message);}}
        void Remember(){undo.Push(Json.Clone(Config));while(undo.Count>40){var list=undo.Reverse().Skip(1).ToArray();undo.Clear();foreach(var c in list)undo.Push(c);}}
        void Change(Deployment config){Config=Data.Validate(config);Selected.Clear();Refresh();}
        public void Undo(){if(!CanEdit||undo.Count==0)return;Change(undo.Pop());Tell("已撤销布阵");}
        public void SetMode(string mode){if(Busy)return;ReturnToDeploy();Mode=mode;Team="red";Hotseat="red";if(mode=="hotseat"){Config.red.Clear();Config.blue.Clear();Config.props.Clear();}Series=null;undo.Clear();Tell("已切换模式："+ModeName(mode));Refresh();}
        internal static string ModeName(string mode)
        {
            switch(mode){case "ai":return "人机";case "sandbox":return "沙盘";case "hotseat":return "热座";case "auto":return "AI 观战";default:return mode;}
        }
        public void SetMap(string map){if(!CanEdit)return;if(Series!=null&&!Series.Complete&&Series.Archive.rounds.Count>0){Tell("Bo3 小局间保留地图；切换模式可开始新系列");return;}Remember();var seed=Config.seed;Config=Data.Default(map,Config.budget);Config.seed=seed;Series=null;if(Mode=="hotseat"){Hotseat="red";Config.red.Clear();Config.blue.Clear();Config.props.Clear();}Tell("已切换地图："+Data.Maps[Config.map].name);Refresh();}
        public void SetBudget(int budget){if(!CanEdit||Series!=null&&Series.Archive.rounds.Count>0)return;budget=Mathf.Clamp(budget,100,10000);Remember();Config=Data.Default(Config.map,budget);PlayerLimit=budget;Refresh();}
        public void SetSeed(string value){if(CanEdit&&uint.TryParse(value,out uint seed))Config.seed=seed;}
        public void SetTeam(string team){if(!CanEdit||Mode=="hotseat"||Mode=="ai")return;Team=team;Selected.Clear();Refresh();}
        public void SetCommander(bool value){if(CanEdit&&(Series==null||Series.Archive.rounds.Count==0)){Config.commander.enabled=value;Refresh();}}
        public void SetBo3(bool value){if(Phase!="deploy"||Series!=null&&Series.Archive.rounds.Count>0)return;Bo3=value;Series=null;Refresh();}
        public void SelectTool(string type,bool prop=false){UnitType=prop?UnitType:type;PropType=prop?type:null;Tool="place";Selected.Clear();Refresh();}
        public void SetStance(string stance){Stance=stance;if(CanEdit&&Selected.Count>0){Remember();foreach(int i in Selected)Config.Army(EditingTeam)[i].stance=stance;}Refresh();}
        public void Preset(string preset){if(!CanEdit)return;Try(()=>{Remember();var c=Json.Clone(Config);Data.ApplyFormation(c,EditingTeam,preset);if(Data.Cost(c,EditingTeam)>PlayerLimit)throw new FormatException("超出玩家军费");Change(c);});}
        public void ClearTeam(){if(!CanEdit)return;Remember();Config.Army(EditingTeam).Clear();Config.props.RemoveAll(p=>p.team==EditingTeam);Selected.Clear();Refresh();}
        public void SelectAt(Vector3 p,bool add=false)
        {
            if(!CanEdit)return;int best=-1;float distance=1.2f;var army=Config.Army(EditingTeam);for(int i=0;i<army.Count;i++){float d=Catalog.Distance(p.x,p.z,army[i].x,army[i].z);if(d<distance){best=i;distance=d;}}if(!add)Selected.Clear();if(best>=0){if(Selected.Contains(best)&&add)Selected.Remove(best);else Selected.Add(best);}Refresh();
        }
        public void SelectRectangle(Vector2 a,Vector2 b)
        {
            if(!CanEdit)return;Selected.Clear();var army=Config.Army(EditingTeam);for(int i=0;i<army.Count;i++){var u=army[i];var s=View.ViewCamera.WorldToScreenPoint(new Vector3(u.x,Data.Height(Config.map,u.x,u.z)+.8f,u.z));if(s.x>=Mathf.Min(a.x,b.x)&&s.x<=Mathf.Max(a.x,b.x)&&s.y>=Mathf.Min(a.y,b.y)&&s.y<=Mathf.Max(a.y,b.y))Selected.Add(i);}Refresh();
        }
        public void PlaceAt(Vector3 p)
        {
            if(!CanEdit)return;Try(()=>{if(Tool=="select"){SelectAt(p);return;}Remember();if(Tool=="move"&&Selected.Count>0){var army=Config.Army(EditingTeam);float x=Selected.Average(i=>army[i].x),z=Selected.Average(i=>army[i].z);Change(Data.Move(Config,EditingTeam,Selected,p.x-x,p.z-z,PlayerLimit));return;}
                if(PropType!=null){if(Config.props.Count(q=>q.team==EditingTeam)>=12||!Data.CanPlace(Config,EditingTeam,PropType,p.x,p.z,true))throw new FormatException("道具位置无效或重叠");var c=Json.Clone(Config);c.props.Add(new PropPlacement{type=PropType,team=EditingTeam,x=p.x,z=p.z});if(Data.Cost(c,EditingTeam)>PlayerLimit)throw new FormatException("军费不足");Change(c);}
                else {var list=new List<UnitPlacement>{new UnitPlacement(UnitType,p.x,p.z,Stance)};if(Mirror&&Math.Abs(p.z)>.5f)list.Add(new UnitPlacement(UnitType,p.x,-p.z,Stance));Change(Data.Place(Config,EditingTeam,list,null,PlayerLimit));}});
        }
        public void DeleteSelection(){if(!CanEdit||Selected.Count==0)return;Remember();foreach(int i in Selected.OrderByDescending(i=>i))Config.Army(EditingTeam).RemoveAt(i);Selected.Clear();Refresh();}
        public void Arrange(string shape){if(!CanEdit)return;Try(()=>{var indices=Selected.Count>0?Selected.ToList():Enumerable.Range(0,Config.Army(EditingTeam).Count).ToList();var roster=indices.Select(i=>Config.Army(EditingTeam)[i]).ToList();if(roster.Count==0)return;var list=Catalog.Squad(roster,EditingTeam,shape,EditingTeam=="red"?-6:6,0,false);Remember();Change(Data.Place(Config,EditingTeam,list,indices,PlayerLimit));});}
        public void SaveFavorite(){if(!CanEdit)return;Favorites[FavoriteSlot]=Data.SaveFavorite(Config,EditingTeam,"收藏 "+(FavoriteSlot+1));PlayerPrefs.SetString("frontline.unity.favorites",Json.Write(Favorites));PlayerPrefs.Save();Tell("已保存到收藏 "+(FavoriteSlot+1));Refresh();}
        public void LoadFavorite(){if(!CanEdit)return;Try(()=>{Remember();Change(Data.LoadFavorite(Config,EditingTeam,Favorites[FavoriteSlot],PlayerLimit));});}
        public void SealHotseat(){if(Mode!="hotseat"||Phase!="deploy")return;if((Hotseat=="red"||Hotseat=="blue")&&Config.Army(Hotseat).Count==0){Tell("至少招募一名士兵后才能封存");return;}if(Hotseat=="red"){Hotseat="handoff";Selected.Clear();Tell("红方已封存，请交给蓝方");}else if(Hotseat=="handoff")Hotseat="blue";else if(Hotseat=="blue"){Hotseat="revealed";Tell("双方阵型已公开，可以开战");}Refresh();}
        public void Mission(int index){ReturnToDeploy();Mode="ai";var m=Data.Data.missions[index];Config=Data.Default(m.map,m.budget);Data.ApplyFormation(Config,"blue",m.enemy);Config.red.Clear();PlayerLimit=m.budget;undo.Clear();Series=null;Tell(m.name+"："+m.description);Refresh();}
        public void PlanArmy(string team,bool start=false)
        {
            if(!CanEdit)return;if(Mode=="hotseat"&&team!=EditingTeam){Tell("封存期间不能推演对手");return;}StartJob(PlanRoutine(team,start));
        }
        IEnumerator PlanRoutine(string team,bool start)
        {
            var publicConfig=PublicConfig;Plan plan=null;var enemy=publicConfig.Army(Catalog.Other(team));if(Style=="oracle")foreach(var progress in Tactics.Search(Data,publicConfig,team,enemy,Factory,Series?.Last)){Progress="算策 · "+progress.phase+" "+progress.completed+" / "+progress.total;if(progress.result!=null)plan=progress.result;yield return null;}
            else {Progress="正在谋算阵型";yield return null;plan=Tactics.Plan(Data,publicConfig,team,Style,enemy,Series?.Last,unchecked(Config.seed+(team=="blue"?7919u:0)));}
            Config=Tactics.Apply(Data,Config,team,plan);Selected.Clear();Busy=false;Progress=null;if(start)StartBattle();else Refresh();
        }
        void StartJob(IEnumerator routine){if(Busy)return;Busy=true;activeJob=routine;job=StartCoroutine(Guard(routine));Refresh();}
        IEnumerator Guard(IEnumerator routine)
        {
            while(true){bool next;object current;try{next=routine.MoveNext();current=next?routine.Current:null;}catch(Exception e){Tell(e.Message);Debug.LogException(e);break;}if(!next)break;yield return current;}
            (routine as IDisposable)?.Dispose();activeJob=null;job=null;Busy=false;Progress=null;Refresh();
        }
        public void CancelJob()
        {
            bool wasRunning=HasJob;
            if(job!=null)StopCoroutine(job);
            (activeJob as IDisposable)?.Dispose();activeJob=null;job=null;Busy=false;Progress=null;
            if(wasRunning){Tell("已停止计算");Refresh();}
        }
        public void RequestStart(){if(Busy||Phase!="deploy")return;if(Mode=="hotseat"&&Hotseat!="revealed"){SealHotseat();return;}if(Mode=="ai")PlanArmy("blue",true);else if(Mode=="auto")StartJob(AutoPlan());else Try(StartBattle);}
        IEnumerator AutoPlan(){foreach(string team in new[]{"red","blue"}){Busy=true;Refresh();var r=PlanRoutine(team,false);try{while(r.MoveNext()){Busy=true;yield return r.Current;}}finally{(r as IDisposable)?.Dispose();}}Busy=false;StartBattle();}
        void StartBattle()
        {
            Config=Data.Validate(Config);if(Config.red.Count==0||Config.blue.Count==0)throw new FormatException("双方至少需要一名士兵");Config.commander.aiTeams=Config.commander.enabled?(Mode=="auto"?new List<string>{"red","blue"}:Mode=="ai"?new List<string>{"blue"}:new List<string>()):new List<string>();Battle?.Dispose();Battle=new Battle(Data,Config,Factory);ReplayFile=null;recorded.Clear();archive=new Frame[902];for(int i=0;i<archive.Length;i++)archive[i]=Battle.PrepareSnapshot();archiveIndex=0;recorded.Add(RecordFrame());live=Battle.Snapshot(Battle.PrepareSnapshot());Phase="battle";Paused=false;accumulator=0;Selected.Clear();if(Bo3&&Series==null)Series=new BattleSeries(Data,Config);Tell("战斗进行中");Refresh();
        }
        void Update()
        {
            if(!loaded||Busy)return;
            if(Phase=="replay"&&ReplayFile!=null&&!Paused){ReplayTime=Mathf.Min(ReplayFile.result.time,ReplayTime+Time.unscaledDeltaTime*Speed);live=ReplayFile.frames[Replay.AtTime(ReplayFile.frames,ReplayTime)];View.Current=live;if(ReplayTime>=ReplayFile.result.time)Paused=true;return;}
            if(Phase!="battle"||Paused||Battle==null)return;accumulator=Mathf.Min(.25f,accumulator+Time.unscaledDeltaTime*Speed);int steps=0;while(accumulator>=Catalog.Step&&steps++<16&&Battle.Result==null){Battle.Step();accumulator-=Catalog.Step;if(Battle.Tick%6==0)recorded.Add(RecordFrame());}live=Battle.Snapshot(live);View.Current=live;
            if(Battle.Result!=null){if(recorded[recorded.Count-1].tick!=Battle.Tick)recorded.Add(RecordFrame());ReplayFile=Replay.Create(Data,Config,recorded,Battle.Result);if(Bo3&&Series!=null)Series.Record(Config,Battle.Result);Phase="result";Tell(Battle.Result.winner=="draw"?"本局平局":(Battle.Result.winner=="red"?"红方":"蓝方")+"获胜");Refresh();}
        }
        Frame RecordFrame(){if(archiveIndex>=archive.Length)throw new InvalidOperationException("Replay frame limit exceeded");return Battle.Snapshot(archive[archiveIndex++]);}
        public Frame CurrentFrame {get{return live;}}
        public IReadOnlyList<Highlight> Highlights => (IReadOnlyList<Highlight>)(ReplayFile?.highlights??Battle?.Highlights)??Array.Empty<Highlight>();
        public void TogglePause(){if(Phase=="battle"||Phase=="replay"){Paused=!Paused;Refresh();}}
        public void CommandAt(Vector3 p){if(Phase!="battle"||Battle==null)return;string team=Team;if(Mode=="ai")team="red";if(!Battle.IssueOrder(team,new Order(OrderType,p.x,p.z),out string reason))Tell(reason);else Tell((team=="red"?"红方":"蓝方")+"军令已下达");Refresh();}
        public void ReturnToDeploy(){bool returned=Phase!="deploy"&&!HasJob;CancelJob();Battle?.Dispose();Battle=null;archive=null;Phase="deploy";live=null;ReplayTime=0;Paused=false;Selected.Clear();if(Mode=="hotseat")Hotseat="revealed";if(returned)Tell("已返回布阵");Refresh();}
        public void NextRound(){if(Series==null||Series.Complete)return;var next=Series.Next();ReturnToDeploy();Config=next;Hotseat=Mode=="hotseat"?"red":"revealed";if(Mode=="hotseat"){Config.red.Clear();Config.blue.Clear();Config.props.Clear();}undo.Clear();Tell("下一局可调整布阵，地图与总军费已锁定");Refresh();}
        public void Watch(){if(ReplayFile?.kind!="trajectory")return;Battle?.Dispose();Battle=null;Phase="replay";ReplayTime=0;Paused=false;live=ReplayFile.frames[0];Tell("回放播放中");Refresh();}
        public void Seek(float time){if(ReplayFile?.kind!="trajectory")return;Phase="replay";ReplayTime=Mathf.Clamp(time,0,ReplayFile.result.time);live=ReplayFile.frames[Replay.AtTime(ReplayFile.frames,ReplayTime)];View.Current=live;Paused=true;}
        public void SeekHighlight(Highlight h){if(ReplayFile==null)return;int index=Replay.AfterTick(ReplayFile.frames,h.tick);Seek(ReplayFile.frames[index].time);Refresh();}
        public void ExportConfig(){Try(()=>FileBridge.Export("frontline-formation.json",Json.Write(Mode=="hotseat"?PublicConfig:Config)));}
        public void ExportReplay(bool seed=false){if(ReplayFile==null)return;Try(()=>FileBridge.Export(seed?"frontline-seed.json":"frontline-replay.json",Json.Write(seed?Replay.Create(Data,ReplayFile.config,ReplayFile.frames,ReplayFile.result,true,ReplayFile.engineVersion):ReplayFile)));}
        public void ExportSeries(){if(Series!=null)Try(()=>FileBridge.Export("frontline-bo3.json",Json.Write(Series.Archive)));}
        public void ImportFile(){Try(()=>FileBridge.Import(text=>Try(()=>ImportText(text))));}
        public void ImportText(string text)
        {
            if(Busy)throw new InvalidOperationException("请先停止当前计算");var raw=Json.Parse(text) as Dictionary<string,object>;if(raw==null)throw new FormatException("JSON 需要是对象");
            if(raw.TryGetValue("format",out var format)&&(string)format=="frontline-replay"){var replay=Replay.Import(Data,text);ReturnToDeploy();Config=replay.config;ReplayFile=replay;if(replay.kind=="seed")StartJob(ExpandSeed(replay));else Watch();return;}
            if(!CanEdit)throw new InvalidOperationException("请先返回布阵");var imported=Data.Import(text);if(Mode=="hotseat"&&Hotseat!="revealed"){if(imported.map!=Config.map||imported.budget!=Config.budget)throw new FormatException("热座封存不能变更地图和军费");Remember();Config=Data.LoadFavorite(Config,EditingTeam,Data.SaveFavorite(imported,EditingTeam,"导入"),PlayerLimit);}else{Remember();Config=imported;PlayerLimit=Config.budget;Series=null;Mode="sandbox";Team="red";Hotseat="revealed";}Selected.Clear();Tell("阵型已导入");Refresh();
        }
        IEnumerator ExpandSeed(ReplayPackage replay)
        {
            var frames=new List<Frame>();using(IRecordedBattle b=replay.engineVersion=="unity-0.6.0"||replay.engineVersion=="unity-0.7.0"?(IRecordedBattle)new Frontline.Core.History07.Battle(Data,replay.config,replay.engineVersion=="unity-0.6.0"?(IWorldFactory)new LegacyUnityWorldFactory():new Unity07WorldFactory()):replay.engineVersion=="unity-0.8.0"?(IRecordedBattle)new Frontline.Core.History08.Battle(Data,replay.config,new Unity08WorldFactory()):new Battle(Data,replay.config,Factory)){b.UseOrderLog(replay.orderLog);frames.Add(b.Snapshot());while(b.Result==null){for(int i=0;i<60&&b.Result==null;i++){b.Step();if(b.Tick%6==0)frames.Add(b.Snapshot());}Progress="重建 Unity 种子回放 · "+b.Tick+" / 5400";yield return null;}if(frames[frames.Count-1].tick!=b.Tick)frames.Add(b.Snapshot());ReplayFile=Replay.Create(Data,replay.config,frames,b.Result,false,replay.engineVersion);}Busy=false;Watch();
        }
        public void AddArenaFormation(string json){var c=Data.Import(json);string team=c.red.Count>0?"red":"blue";if(c.Army(team).Count==0)throw new FormatException("导入阵型为空");ImportedEntrants.Add(new Entrant{id="import-"+ImportedEntrants.Count,name="导入阵型 "+(ImportedEntrants.Count+1),map=c.map,team=team,units=c.Army(team),props=c.props.Where(p=>p.team==team).ToList()});Tell("已加入擂台参赛列表");}
        public void Arena(List<Entrant> entrants,List<string> maps,List<uint> seeds){if(Busy)return;var tournament=new Tournament(Data,Factory,entrants,maps,seeds,Config.budget,Config.commander.enabled);StartJob(ArenaRoutine(tournament));}
        IEnumerator ArenaRoutine(Tournament tournament){foreach(var p in tournament.Run()){Progress="演武擂台 · "+p.phase+" "+p.completed+" / "+p.total;yield return null;}ArenaReport=tournament.Report;Tell("循环赛完成，可查看排名与胜率矩阵");}
        public void ExportArena(){if(ArenaReport!=null)Try(()=>FileBridge.Export("frontline-tournament.json",Json.Write(ArenaReport)));}
        void OnDestroy(){Battle?.Dispose();}
    }
}
