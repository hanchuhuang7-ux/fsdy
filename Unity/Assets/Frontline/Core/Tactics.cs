// Persistence uses strict Frontline.Core.Json; Unity inspector serialization is intentionally unused.
#pragma warning disable UAC1015, UAC1009
using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontline.Core
{
    public sealed class Plan {public List<UnitPlacement> units;public List<PropPlacement> props;public string style;public float score;public int candidate,steps;}
    public sealed class PlanningProgress {public int completed,total;public string phase;public Plan result;}
    public static class Tactics
    {
        static readonly string[] Guardian={"shield","spear","archer","crossbow","medic","shield","spear","archer"};
        static readonly string[] Maneuver={"cavalry","militia","cavalry","crossbow","spear","medic","militia"};
        public static Plan Plan(Catalog db,Deployment source,string team,string style,List<UnitPlacement> opponent=null,SeriesRound previous=null,uint? seed=null)
        {
            if(!Catalog.Team(team)||!Catalog.Styles.Contains(style)||style=="oracle")throw new FormatException("未知 AI；oracle 请使用分片推演入口");
            var c=new Deployment{map=source.map,budget=source.budget,seed=seed??source.seed};var rng=new Seeded(c.seed);opponent=opponent??new List<UnitPlacement>();
            float heavy=opponent.Count(u=>u.type=="shield"||u.type=="giant")/(float)Math.Max(1,opponent.Count),rush=opponent.Count(u=>u.type=="cavalry"||u.type=="militia"||u.type=="bomber")/(float)Math.Max(1,opponent.Count);int cavalry=opponent.Count(u=>u.type=="cavalry");
            var back=opponent.Where(u=>db.Units[u.type].ranged||u.type=="medic").ToList();int pairs=0;for(int i=0;i<back.Count;i++)for(int j=i+1;j<back.Count;j++)if(Catalog.Distance(back[i].x,back[i].z,back[j].x,back[j].z)<3.5f)pairs++;bool clustered=back.Count>=3&&pairs>=Math.Max(2,back.Count*.6f);
            var cycle=new List<string>(style=="guardian"?Guardian:Maneuver);
            if(style=="counter")
            {
                var weights=new Dictionary<string,int>{{"militia",2},{"shield",3},{"spear",4},{"archer",3},{"crossbow",3},{"medic",2},{"catapult",2},{"cavalry",2},{"bomber",2},{"giant",1}};
                if(heavy>=.34f){weights["crossbow"]+=8;weights["catapult"]+=4;weights["bomber"]+=4;}if(cavalry>=2||rush>=.45f)weights["spear"]+=12;if(clustered){weights["cavalry"]+=8;weights["militia"]+=4;weights["catapult"]+=4;}
                if(c.map=="bridge"){weights["giant"]+=5;weights["bomber"]+=4;}if(c.map=="highland"){weights["archer"]+=5;weights["crossbow"]+=4;}
                cycle.Clear();cycle.Add(cavalry>=2?"spear":c.map=="highland"?"archer":c.map=="bridge"?"giant":heavy>=.34f?"crossbow":"shield");
                int sum=weights.Values.Sum();for(int i=0;i<40;i++){float pick=rng.Next()*sum;foreach(var row in weights){pick-=row.Value;if(pick<0){cycle.Add(row.Key);break;}}}
                if(heavy>=.34f&&c.budget<=1500)cycle.InsertRange(0,new[]{"shield","crossbow","catapult","spear","medic","archer","crossbow"});
            }
            float? lossLane=null;string threat=null;if(previous!=null&&previous.result!=null&&previous.result.report!=null)
            {
                var units=previous.result.report.units;var enemy=units.Where(u=>u.team!=team).OrderByDescending(u=>u.damageDealt+u.healing).ThenBy(u=>u.id).FirstOrDefault();if(enemy!=null)threat=enemy.type;var lost=units.Where(u=>u.team==team&&!u.alive).ToList();if(lost.Count>0)lossLane=lost.Average(u=>u.spawn.z);
                if(threat!=null)cycle.Insert(0,threat=="cavalry"||threat=="militia"?"spear":db.Units[threat].ranged?"cavalry":"crossbow");
            }
            var lanes=(style=="maneuver"||clustered&&style=="counter")&&(c.map=="ruins"||c.map=="marsh")?new[]{9.6f,-9.6f,7.1f,-7.1f,4.7f,-4.7f,2.3f,-2.3f,0}:new[]{0,2.4f,-2.4f,4.8f,-4.8f,7.2f,-7.2f,9.6f,-9.6f};
            float offset=(rng.Next()-.5f)*.7f;if(style=="counter")lanes=lanes.Select(z=>Math.Max(-10.1f,Math.Min(10.1f,z+offset))).OrderBy(z=>lossLane.HasValue?-Math.Abs(z-lossLane.Value):Math.Abs(z-(opponent.Count==0?0:opponent.Average(u=>u.z))*.4f)).ToArray();else if(lossLane.HasValue)lanes=lanes.OrderByDescending(z=>Math.Abs(z-lossLane.Value)).ToArray();
            int sign=team=="red"?-1:1;var propPlan=style=="guardian"?new List<string>{"banner","barricade","barricade"}:c.map=="bridge"?new List<string>{"barrel","barricade"}:new List<string>{"banner","spikes"};if(style=="counter"&&(cavalry>=2||rush>=.45f))propPlan=new List<string>{"barricade","barricade","banner"};else if(style=="counter"&&clustered)propPlan=new List<string>{"barrel","banner"};
            float reserve=c.budget>=650?Math.Min(c.budget*.18f,propPlan.Sum(p=>db.Props[p].cost)):0;
            Func<string,bool,bool> place=(type,prop)=>
            {
                bool ranged=!prop&&(db.Units[type].ranged||type=="medic"),mobile=!prop&&(type=="cavalry"||type=="militia");
                float[] depths=prop?(type=="banner"?new[]{9.8f,12.2f,14.7f,7.4f}:new[]{3.1f,4.2f,6.6f,9f,12f}):ranged?(c.map=="highland"&&style=="counter"?new[]{3.2f,5.8f,8.4f,11,13.6f,16.1f}:new[]{8.1f,10.7f,13.3f,15.9f,5.5f,3.2f}):new[]{4.5f,7.1f,9.7f,12.3f,14.9f};
                float flip=rng.Next()<.5f?-1:1;var slots=prop?new[]{1.4f,-1.4f,4,-4,6.6f,-6.6f,9.2f,-9.2f,0}:lanes;
                foreach(var depth in depths)foreach(var lane in slots){float x=sign*depth,z=lane*flip;if(!db.CanPlace(c,team,type,x,z,prop))continue;if(prop)c.props.Add(new PropPlacement{type=type,team=team,x=x,z=z});else c.Army(team).Add(new UnitPlacement(type,x,z,style=="guardian"?"hold":mobile&&style=="maneuver"?"flank":ranged&&style=="maneuver"?"hunt":style=="counter"&&mobile&&clustered?(c.map=="ruins"||c.map=="marsh"?"flank":"hunt"):"advance"));return true;}return false;
            };
            float prepaid=0;int propSkip=0;if(style=="counter"&&(cavalry>=2||rush>=.45f))foreach(var type in propPlan){if(type!="barricade"||db.Cost(c,team)+db.Props[type].cost>c.budget-40||!place(type,true))break;prepaid+=db.Props[type].cost;propSkip++;}
            int missed=0;for(int i=0;i<180&&missed<cycle.Count*2&&c.Army(team).Count<60;i++){string type=cycle[i%cycle.Count];if(db.Cost(c,team)+db.Units[type].cost<=c.budget-Math.Max(0,reserve-prepaid)&&place(type,false))missed=0;else missed++;}
            foreach(var type in propPlan.Skip(propSkip))if(c.props.Count<12&&db.Cost(c,team)+db.Props[type].cost<=c.budget)place(type,true);
            foreach(var type in new[]{"spear","militia","archer"})for(int i=0;i<12&&c.Army(team).Count<60&&db.Cost(c,team)+db.Units[type].cost<=c.budget;i++)if(!place(type,false))break;
            db.Validate(c);return new Plan{units=c.Army(team),props=c.props,style=style};
        }
        public static Deployment Apply(Catalog db,Deployment input,string team,Plan plan)
        {
            var c=db.Validate(input);if(team=="red")c.red=Json.Clone(plan.units);else c.blue=Json.Clone(plan.units);c.props.RemoveAll(p=>p.team==team);c.props.AddRange(Json.Clone(plan.props));return db.Validate(c);
        }
        public static IEnumerable<PlanningProgress> Search(Catalog db,Deployment source,string team,List<UnitPlacement> publicOpponent,IWorldFactory factory,SeriesRound previous=null,int candidates=4,int quick=360,int final=5400)
        {
            if(candidates<3||candidates>6||quick<60||quick>720||final<quick||final>5400)throw new ArgumentException("推演预算无效");
            uint seed=unchecked(source.seed+(team=="blue"?7919u:0));var opponent=(publicOpponent??new List<UnitPlacement>()).Select(u=>new UnitPlacement(u.type,u.x,u.z,u.stance)).ToList();if(opponent.Count==0)opponent=Plan(db,source,Catalog.Other(team),"guardian",seed:seed).units;
            var plans=new List<Plan>();for(int i=0;i<candidates;i++)plans.Add(Plan(db,source,team,new[]{"counter","guardian","maneuver"}[i%3],publicOpponent,previous,unchecked(seed+(uint)i*104729)));
            int total=candidates*quick+2*final,done=0;var scores=new List<Tuple<int,float,float>>();
            for(int phase=0;phase<2;phase++)
            {
                var indices=phase==0?Enumerable.Range(0,candidates).ToArray():scores.OrderByDescending(x=>x.Item2).ThenByDescending(x=>x.Item3).ThenBy(x=>x.Item1).Take(2).Select(x=>x.Item1).ToArray();if(phase==1)scores.Clear();int steps=phase==0?quick:final;
                foreach(int index in indices)
                {
                    var c=new Deployment{map=source.map,budget=source.budget,seed=seed};if(team=="red"){c.red=Json.Clone(plans[index].units);c.blue=Json.Clone(opponent);}else{c.blue=Json.Clone(plans[index].units);c.red=Json.Clone(opponent);}c.props=Json.Clone(source.props.Where(p=>p.team==Catalog.Other(team)).ToList());c.props.AddRange(Json.Clone(plans[index].props));
                    using(var b=new Battle(db,c,factory))
                    {
                        int used=0;while(used<steps&&b.Result==null){b.Step();used++;if(used%60==0)yield return new PlanningProgress{completed=done+used,total=total,phase=phase==0?"候选快筛":"完整验算"};}
                        scores.Add(Tuple.Create(index,b.Power(team),b.Power(team)-b.Power(Catalog.Other(team))));plans[index].steps+=used;
                    }done+=steps;
                }
            }
            var best=scores.OrderByDescending(x=>x.Item2).ThenByDescending(x=>x.Item3).ThenBy(x=>x.Item1).First();var selected=plans[best.Item1];selected.style="oracle";selected.score=best.Item2;selected.candidate=best.Item1;yield return new PlanningProgress{completed=total,total=total,phase="完成",result=selected};
        }
        public static Plan SearchSync(Catalog db,Deployment c,string team,List<UnitPlacement> opponent,IWorldFactory factory,SeriesRound previous=null){Plan best=null;foreach(var p in Search(db,c,team,opponent,factory,previous))if(p.result!=null)best=p.result;return best;}
    }
    [Serializable] public class Entrant {public string id,name,ai,map,team="red";public List<UnitPlacement> units=new List<UnitPlacement>();public List<PropPlacement> props=new List<PropPlacement>();}
    [Serializable] public class MatchRecord {public int red,blue;public string map;public uint seed;public Result result;}
    [Serializable] public class Standing {public int index,wins,draws,losses,games;public string name,stableMvp;public float winRate,scoreRate,averageRemaining,averageTime;public Dictionary<string,int> mvps=new Dictionary<string,int>();[NonSerialized] public float totalPower,totalTime;}
    [Serializable] public class TournamentReport {public string schema="frontline-unity-tournament@1";public List<Entrant> entrants=new List<Entrant>();public List<MatchRecord> matches=new List<MatchRecord>();public List<Standing> ranking=new List<Standing>();public List<List<Standing>> matrix=new List<List<Standing>>();}
    public sealed class Tournament
    {
        public readonly TournamentReport Report=new TournamentReport();readonly Catalog db;readonly IWorldFactory factory;readonly List<string> maps;readonly List<uint> seeds;readonly int budget;readonly bool commander;
        public Tournament(Catalog db,IWorldFactory factory,List<Entrant> entrants,List<string> maps,List<uint> seeds,int budget,bool commander=false)
        {
            if(entrants==null||entrants.Count<2||entrants.Count>8||maps==null||maps.Count<1||maps.Count>6||maps.Distinct().Count()!=maps.Count||maps.Any(m=>!db.Maps.ContainsKey(m))||seeds==null||seeds.Count<1||seeds.Count>10||seeds.Distinct().Count()!=seeds.Count||entrants.Count*(entrants.Count-1)*maps.Count*seeds.Count>600)throw new ArgumentException("擂台需要 2–8 方、唯一地图/种子，最多 600 局");
            this.db=db;this.factory=factory;this.maps=maps;this.seeds=seeds;this.budget=budget;this.commander=commander;Report.entrants=Json.Clone(entrants);
            foreach(var e in entrants)if(string.IsNullOrWhiteSpace(e.name)||e.name.Length>48||!string.IsNullOrEmpty(e.ai)&&!Catalog.Styles.Contains(e.ai)||string.IsNullOrEmpty(e.ai)&&(e.units==null||e.units.Count==0||!Catalog.Team(e.team)))throw new ArgumentException("参赛方无效");
        }
        void Place(Deployment c,Entrant e,string team){if(!string.IsNullOrEmpty(e.map)&&e.map!=c.map)throw new ArgumentException(e.name+" 只适用于 "+e.map);int flip=e.team==team?1:-1;c.Army(team).AddRange(e.units.Select(u=>new UnitPlacement(u.type,u.x*flip,u.z,u.stance)));foreach(var p in e.props)c.props.Add(new PropPlacement{type=p.type,team=team,x=p.x*flip,z=p.z});db.Validate(c);}
        public IEnumerable<PlanningProgress> Run()
        {
            int total=Report.entrants.Count*(Report.entrants.Count-1)*maps.Count*seeds.Count;
            for(int a=0;a<Report.entrants.Count;a++)for(int b=a+1;b<Report.entrants.Count;b++)foreach(string map in maps)foreach(uint seed in seeds)for(int flip=0;flip<2;flip++)
            {
                int red=flip==0?a:b,blue=flip==0?b:a;var c=new Deployment{map=map,budget=budget,seed=seed,commander=new CommanderConfig{enabled=commander,aiTeams=commander?new List<string>{"red","blue"}:new List<string>()}};
                foreach(var row in new[]{Tuple.Create(red,"red"),Tuple.Create(blue,"blue")}){var e=Report.entrants[row.Item1];if(string.IsNullOrEmpty(e.ai))Place(c,e,row.Item2);}
                var rd=string.IsNullOrEmpty(Report.entrants[red].ai)?c.red:Tactics.Plan(db,c,"red",Report.entrants[red].ai=="oracle"?"counter":Report.entrants[red].ai).units;
                var bd=string.IsNullOrEmpty(Report.entrants[blue].ai)?c.blue:Tactics.Plan(db,c,"blue",Report.entrants[blue].ai=="oracle"?"counter":Report.entrants[blue].ai,seed:unchecked(seed+7919)).units;
                foreach(var row in new[]{Tuple.Create(red,"red"),Tuple.Create(blue,"blue")})
                {
                    var e=Report.entrants[row.Item1];if(string.IsNullOrEmpty(e.ai))continue;Plan plan=null;var enemy=row.Item2=="red"?bd:rd;
                    if(e.ai=="oracle"){foreach(var progress in Tactics.Search(db,c,row.Item2,enemy,factory)){if(progress.result!=null)plan=progress.result;else yield return new PlanningProgress{completed=Report.matches.Count,total=total,phase="算策 · "+progress.phase};}}
                    else plan=Tactics.Plan(db,c,row.Item2,e.ai,enemy,seed:unchecked(seed+(row.Item2=="blue"?7919u:0)));c=Tactics.Apply(db,c,row.Item2,plan);
                }
                using(var battle=new Battle(db,c,factory)){while(battle.Result==null){battle.Step();if(battle.Tick%60==0)yield return new PlanningProgress{completed=Report.matches.Count,total=total,phase="循环赛对局"};}Report.matches.Add(new MatchRecord{red=red,blue=blue,map=map,seed=seed,result=Json.Clone(battle.Result)});}
                yield return new PlanningProgress{completed=Report.matches.Count,total=total,phase="小局完成"};
            }
            Summarize();yield return new PlanningProgress{completed=total,total=total,phase="全部完成"};
        }
        public void Summarize()
        {
            int count=Report.entrants.Count;Report.ranking=Enumerable.Range(0,count).Select(i=>new Standing{index=i,name=Report.entrants[i].name}).ToList();Report.matrix=Enumerable.Range(0,count).Select(i=>Enumerable.Range(0,count).Select(j=>new Standing{index=i,name=Report.entrants[i].name}).ToList()).ToList();
            foreach(var match in Report.matches)foreach(var row in new[]{Tuple.Create(match.red,match.blue,"red"),Tuple.Create(match.blue,match.red,"blue")})
            {
                var types=match.result.report.teams[row.Item3].byType;string mvp=types.OrderByDescending(x=>x.Value.Score).ThenBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key).FirstOrDefault();foreach(var s in new[]{Report.ranking[row.Item1],Report.matrix[row.Item1][row.Item2]}){s.games++;if(match.result.winner=="draw")s.draws++;else if(match.result.winner==row.Item3)s.wins++;else s.losses++;s.totalPower+=match.result.remainingValue[row.Item3];s.totalTime+=match.result.time;if(mvp!=null){if(!s.mvps.ContainsKey(mvp))s.mvps[mvp]=0;s.mvps[mvp]++;}}
            }
            foreach(var s in Report.ranking.Concat(Report.matrix.SelectMany(x=>x))){if(s.games>0){s.winRate=s.wins/(float)s.games;s.scoreRate=(s.wins+.5f*s.draws)/s.games;s.averageRemaining=s.totalPower/s.games;s.averageTime=s.totalTime/s.games;}s.stableMvp=s.mvps.OrderByDescending(x=>x.Value).ThenBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key).FirstOrDefault();}Report.ranking=Report.ranking.OrderByDescending(s=>s.scoreRate).ThenByDescending(s=>s.averageRemaining).ThenBy(s=>s.index).ToList();
        }
    }
}


