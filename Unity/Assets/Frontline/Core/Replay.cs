using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontline.Core
{
    public static class Replay
    {
        public const string Engine="unity-0.9.0";
        public const string ExpeditionEngine="unity-manor-1.2.0";
        public const string LegacyExpeditionEngine="unity-manor-1.1.0";
        public const string RefinedEngine="unity-1.3.5";
        public const string RefinedExpeditionEngine="unity-manor-1.3.5";
        public const string ArmamentExpeditionEngine="unity-manor-1.7.0";
        public static bool IsExpeditionEngine(string id) => id==ExpeditionEngine || id==LegacyExpeditionEngine || id==RefinedExpeditionEngine || id==ArmamentExpeditionEngine;
        public static bool UsesRefinedPhysics(string id) => id==RefinedEngine || id==RefinedExpeditionEngine || id==ArmamentExpeditionEngine;
        public static void ValidateOrders(Catalog catalog,Deployment c,List<OrderEntry> log)
        {
            if(log==null||log.Count>6||(!c.commander.enabled&&log.Count>0))throw new FormatException("军令日志无效");var points=new Dictionary<string,int>{{"red",3},{"blue",3}};var ready=new Dictionary<string,int>{{"red",0},{"blue",0}};int previous=-1;
            for(int i=0;i<log.Count;i++){var o=log[i];if(o==null||!Catalog.Team(o.team)||!Catalog.Orders.Contains(o.type)||o.id!=i+1||o.tick<previous||o.tick<0||o.tick>5399||o.tick<ready[o.team]||points[o.team]<1||!Catalog.Finite(o.x)||!Catalog.Finite(o.z)||Math.Abs(o.x)>18||Math.Abs(o.z)>12||!Catalog.Finite(catalog.Height(c.map,o.x,o.z)))throw new FormatException("军令日志越界、超额或违反冷却");previous=o.tick;points[o.team]--;ready[o.team]=o.tick+480;}
            if(c.manorHall>0&&log.Any(o=>o.team=="red"&&Manor.OrderLevel(o.type)>c.manorHall))throw new FormatException("远征军令尚未解锁");
        }
        public static ReplayPackage Create(Catalog catalog,Deployment c,List<Frame> frames,Result result,bool seed=false,string engineVersion=Engine)
        {
            // A battle can clear its recording list when leaving the result screen.
            var captured=seed?new List<Frame>():frames==null?null:new List<Frame>(frames);
            return Validate(catalog,new ReplayPackage{engineVersion=engineVersion,config=c,kind=seed?"seed":"trajectory",frames=captured,result=seed?null:result,orderLog=Json.Clone(result.orders),highlights=Json.Clone(result.highlights)});
        }
        public static ReplayPackage Import(Catalog catalog,string json)
        {
            var raw=Json.Parse(json);var map=raw as Dictionary<string,object>;if(map==null||!new[]{"format","version","engineVersion","kind","config"}.All(map.ContainsKey))throw new FormatException("回放缺少必要字段");
            return Validate(catalog,Json.Read<ReplayPackage>(json));
        }
        public static ReplayPackage Validate(Catalog catalog,ReplayPackage p)
        {
            if(p==null||p.format!="frontline-replay"||!(p.kind=="seed"||p.kind=="trajectory"))throw new FormatException("不支持的回放格式");
            if(p.config?.map==FrontierMap.Id)throw new FormatException("边疆巡逻没有轨迹回放");
            bool expedition=IsExpeditionEngine(p.engineVersion);
            bool native=p.version==3&&(expedition||p.engineVersion==Engine||p.engineVersion==RefinedEngine||p.engineVersion=="unity-0.8.0"||p.engineVersion=="unity-0.6.0"||p.engineVersion=="unity-0.7.0"),legacy=(p.version==1&&new[]{"0.1.0","0.2.0","0.3.0"}.Contains(p.engineVersion))||(p.version==2&&new[]{"0.4.0","0.5.0"}.Contains(p.engineVersion));
            if(!native&&!legacy)throw new FormatException("不支持的引擎版本");p.config=catalog.Validate(p.config,expedition);p.orderLog=p.orderLog??new List<OrderEntry>();ValidateOrders(catalog,p.config,p.orderLog);
            if(p.engineVersion!=ArmamentExpeditionEngine&&p.config.red.Any(unit=>unit.armament!=0))
                throw new FormatException("旧版战报不支持专属武装");
            if(p.kind=="seed"){if(!native)throw new FormatException("旧 Rapier 种子包请先用 Tools/convert-legacy-replay.mjs 转为完整轨迹，再在 Unity 导入。");if(p.frames!=null&&p.frames.Count>0)throw new FormatException("种子包不应包含轨迹");return p;}
            if(p.frames==null||p.frames.Count<2||p.frames.Count>902||p.result==null||!new[]{"red","blue","draw"}.Contains(p.result.winner)||!Catalog.Finite(p.result.time)||p.result.time<0||p.result.time>90.02f)throw new FormatException("回放帧数或战果无效");
            var roster=p.config.red.Concat(p.config.blue).ToList();float previous=-1;
            for(int index=0;index<p.frames.Count;index++)
            {
                var f=p.frames[index];if(f==null||!Catalog.Finite(f.time)||f.time<=previous||f.time<0||f.time>90.02f||f.units==null||f.units.Count!=roster.Count||f.projectiles==null||f.projectiles.Count>1024||f.ragdolls==null||f.ragdolls.Count>24||f.effects==null||f.effects.Count>((expedition||p.engineVersion==Engine||p.engineVersion==RefinedEngine||p.engineVersion=="unity-0.8.0")?CombatMotion.EffectCapacity:41)||f.props==null||f.props.Count>40)throw new FormatException("轨迹结构无效");
                if(index==0&&Math.Abs(f.time)>.0001f)throw new FormatException("缺少初始帧");if(index>0&&index<p.frames.Count-1&&Math.Abs((f.time-previous)-.1f)>.002f)throw new FormatException("轨迹间隔无效");previous=f.time;f.tick=(int)Math.Round(f.time*60);
                for(int i=0;i<f.units.Count;i++)
                {
                    var u=f.units[i];if(u==null||u.id!=i+1||u.type!=roster[i].type||u.team!=(i<p.config.red.Count?"red":"blue")||!Catalog.Finite(u.hp)||u.hp<0||u.hp>1000000||!Catalog.Finite(u.angle)||!Catalog.Finite(u.attack)||!Catalog.Finite(u.charge)||!Catalog.Finite(u.hitFlash)||u.hitFlash<0||u.hitFlash>1||!Catalog.Finite(u.hitTiltX)||!Catalog.Finite(u.hitTiltZ)||Math.Abs(u.hitTiltX)>1||Math.Abs(u.hitTiltZ)>1||!Catalog.Finite(u.moveSpeed)||u.moveSpeed<0||u.moveSpeed>1000||!Catalog.Finite(u.moveForward)||Math.Abs(u.moveForward)>1.001f||!Catalog.Finite(u.guard)||u.guard<0||u.guard>1||!Catalog.Finite(u.gait)||u.gait<0||u.alive&&!Transform(u.t))throw new FormatException("单位轨迹无效");if(string.IsNullOrEmpty(u.stance))u.stance=roster[i].stance;
                }
                foreach(var effect in f.effects)if(effect==null||!Catalog.Finite(effect.angle))throw new FormatException("特效方向无效");
                foreach(var shot in f.projectiles)if(shot==null||shot.id<=0||!new[]{"arrow","bolt","bomb","stone"}.Contains(shot.type)||!Transform(shot.t)||!Vector(shot.v))throw new FormatException("弹道轨迹无效");
                foreach(var corpse in f.ragdolls)if(corpse==null||corpse.parts==null||corpse.parts.Count!=6||corpse.parts.Select(x=>x.name).Distinct().Count()!=6||corpse.parts.Any(x=>!Transform(x.t)))throw new FormatException("尸体轨迹无效");
                foreach(var prop in f.props)if(prop==null||!catalog.Props.ContainsKey(prop.type??"")||!Catalog.Finite(prop.hp)||prop.hp<0||!Catalog.Finite(prop.x)||!Catalog.Finite(prop.z))throw new FormatException("道具轨迹无效");
                if(p.version>=2)ValidateCommander(f,p.orderLog);else{f.commander=new CommanderState();f.cues=new List<Cue>();}
                if(f.cues==null||f.cues.Count>24||f.cues.Any(c=>c==null||c.tick<0||c.tick>f.tick||!Vector(new V3(c.x,c.y,c.z))||!Catalog.Finite(c.strength)))throw new FormatException("视听提示无效");
            }
            var last=p.frames[p.frames.Count-1];if(Math.Abs(last.time-p.result.time)>.002f||p.result.red!=last.units.Count(u=>u.alive&&u.team=="red")||p.result.blue!=last.units.Count(u=>u.alive&&u.team=="blue")||p.orderLog.Any(o=>o.tick>=last.tick))throw new FormatException("轨迹和结算不一致");
            p.highlights=p.highlights??new List<Highlight>();if(p.highlights.Count==0&&p.result.highlights!=null)p.highlights=p.result.highlights;if(p.highlights.Count>24)throw new FormatException("高光数量超限");int tick=-1;for(int i=0;i<p.highlights.Count;i++){var h=p.highlights[i];if(h==null||h.id!=i+1||h.tick<tick||h.tick>last.tick||!new[]{"order","firstKill","blast","reversal"}.Contains(h.type)||!new[]{"red","blue","neutral"}.Contains(h.team)||!Catalog.Finite(h.value)||h.value<0)throw new FormatException("高光记录无效");tick=h.tick;}
            p.result.highlights=p.highlights;if(p.result.report==null)p.result.report=LegacyReport(catalog,p.config,last);return p;
        }
        static void ValidateCommander(Frame frame,List<OrderEntry> log)
        {
            var state=frame.commander;if(state==null||state.log==null||state.log.Count>6||state.active==null||state.active.Count>6)throw new FormatException("回放军令状态无效");var prefix=log.Where(o=>o.tick<frame.tick).ToList();if(state.log.Count!=prefix.Count)throw new FormatException("军令前缀无效");
            for(int i=0;i<prefix.Count;i++)if(state.log[i].id!=prefix[i].id||state.log[i].tick!=prefix[i].tick||state.log[i].type!=prefix[i].type||state.log[i].team!=prefix[i].team)throw new FormatException("军令日志不一致");
            foreach(var team in new[]{"red","blue"}){var own=prefix.Where(o=>o.team==team).ToList();int ready=own.Count==0?0:own[own.Count-1].tick+480;if(!state.points.ContainsKey(team)||state.points[team]!=3-own.Count||!state.readyAt.ContainsKey(team)||state.readyAt[team]!=ready)throw new FormatException("军令点数或冷却不一致");}
            if(state.active.Any(a=>!prefix.Any(o=>o.id==a.id)||a.endTick!=a.tick+(a.type=="focus"?360:300)||a.endTick<frame.tick))throw new FormatException("活动军令无效");
        }
        static bool Transform(float[] t){if(t==null||t.Length!=7||t.Any(x=>!Catalog.Finite(x)||Math.Abs(x)>100000))return false;float q=(float)Math.Sqrt(t[3]*t[3]+t[4]*t[4]+t[5]*t[5]+t[6]*t[6]);return Math.Abs(q-1)<.03f;}
        static bool Vector(V3 p){return Catalog.Finite(p.x)&&Catalog.Finite(p.y)&&Catalog.Finite(p.z)&&Math.Abs(p.x)<100000&&Math.Abs(p.y)<100000&&Math.Abs(p.z)<100000;}
        static BattleReport LegacyReport(Catalog catalog,Deployment config,Frame f)
        {
            var roster=config.red.Concat(config.blue).ToList();
            var r=new BattleReport();foreach(var u in f.units)r.units.Add(new UnitReport{id=u.id,type=u.type,team=u.team,alive=u.alive,hp=u.hp});foreach(var team in new[]{"red","blue"}){var t=new TeamReport();foreach(var u in r.units.Where(x=>x.team==team)){t.initial++;if(u.alive)t.survived++;else t.lost++;var def=catalog.Unit(u.type).AtLevel(roster[u.id-1].level);t.remainingValue+=u.hp/def.hp*def.cost;if(!t.byType.ContainsKey(u.type))t.byType.Add(u.type,new TypeReport());var row=t.byType[u.type];row.initial++;if(u.alive)row.survived++;else row.lost++;}r.teams.Add(team,t);}return r;
        }
        public static int AtTime(List<Frame> frames,float time){int low=0,high=frames.Count-1;while(low<high){int mid=(low+high+1)/2;if(frames[mid].time<=time)low=mid;else high=mid-1;}return low;}
        public static int AfterTick(List<Frame> frames,int tick){int low=0,high=frames.Count-1;while(low<high){int mid=(low+high)/2;if(frames[mid].tick<=tick)low=mid+1;else high=mid;}return low;}
        public static string HighlightText(Highlight h){string kind=h.type=="order"?"军令释放":h.type=="firstKill"?"首名阵亡":h.type=="blast"?"范围重创":"局势反超";return (h.tick/60f).ToString("0.0")+"s · "+(h.team=="red"?"红方":h.team=="blue"?"蓝方":"战场")+" "+kind;}
    }
}
