using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontline.Core
{
    public sealed class Catalog
    {
        public readonly CatalogData Data;
        public readonly Dictionary<string,UnitDef> Units;
        public readonly Dictionary<string,HeroDef> Heroes;
        public readonly Dictionary<string,PropDef> Props;
        public readonly Dictionary<string,MapDef> Maps;
        public readonly MapDef Frontier = FrontierMap.Create();
        public readonly Dictionary<string,FormationDef> Formations;
        public Catalog(string json)
        {
            Data=Json.Read<CatalogData>(json);Units=Data.units.ToDictionary(x=>x.id,StringComparer.Ordinal);Props=Data.props.ToDictionary(x=>x.id,StringComparer.Ordinal);Maps=Data.maps.ToDictionary(x=>x.id,StringComparer.Ordinal);Formations=Data.formations.ToDictionary(x=>x.id,StringComparer.Ordinal);
            Heroes=Data.heroes.ToDictionary(x=>x.id,StringComparer.Ordinal);
            if(Units.Count!=10||Props.Count!=5||Maps.Count!=6||Data.missions.Count!=6||
                Heroes.Count!=0&&!new HashSet<string>(Manor.HeroIds).SetEquals(Heroes.Keys))throw new FormatException("游戏目录不完整");
        }
        public UnitDef Unit(string id) => Units.TryGetValue(id,out var unit) ? unit : Heroes[id];
        public MapDef Map(string id) => id==FrontierMap.Id?Frontier:Maps[id];
        public static string Other(string team){return team=="red"?"blue":"red";}
        public static bool Team(string team){return team=="red"||team=="blue";}
        public static bool Finite(float n){return !float.IsNaN(n)&&!float.IsInfinity(n);}
        public static readonly string[] Stances={"advance","hold","flank","hunt"};
        public static readonly string[] Styles={"guardian","maneuver","counter","oracle"};
        public static readonly string[] Orders={"inspire","focus","fortify"};
        public const int MaxUnits=60,MaxProps=12,OrderCooldown=480;
        public const float Step=1f/60f;
        public float Height(string map,float x,float z)
        {
            if(map==FrontierMap.Id)return FrontierMap.HasGround(x,z)?0:float.NegativeInfinity;
            if(!Maps.ContainsKey(map)||!Finite(x)||!Finite(z))return float.NegativeInfinity;
            float height=float.NegativeInfinity;
            foreach(var p in Maps[map].platforms){float cos=(float)Math.Cos(p.angle),extent=p.hx*cos+p.hy*Math.Abs((float)Math.Sin(p.angle));if(Math.Abs(x-p.x)<=extent-.01f&&Math.Abs(z-p.z)<=p.hz-.01f)height=Math.Max(height,p.y+p.hy/cos+(float)Math.Tan(p.angle)*(x-p.x));}
            return height;
        }
        public string Terrain(string map,float x,float z){foreach(var p in Map(map).zones)if(Math.Abs(x-p.x)<p.hx&&Math.Abs(z-p.z)<p.hz)return p.type;return "ground";}
        public float Cost(Deployment c,string team){return c.Army(team).Sum(u=>Units.TryGetValue(u.type??"",out var unit)?unit.cost:c.manorHall>0&&team=="red"&&Heroes.TryGetValue(u.type??"",out var hero)?hero.cost:0)+c.props.Where(p=>p.team==team).Sum(p=>Props.ContainsKey(p.type??"")?Props[p.type].cost:0);}
        public Deployment Default(string map="meadow",int budget=2200){var c=new Deployment{map=map,budget=budget};ApplyFormation(c,"red","balanced");ApplyFormation(c,"blue","balanced");return Validate(c);}
        public void ApplyFormation(Deployment c,string team,string preset){c.Army(team).Clear();c.props.RemoveAll(p=>p.team==team);foreach(var u in team=="red"?Formations[preset].red:Formations[preset].blue)if(Cost(c,team)+Units[u.type].cost<=c.budget)c.Army(team).Add(Json.Clone(u));}
        public Deployment Import(string json){var raw=Json.Parse(json,1024*1024) as Dictionary<string,object>;if(raw==null||!new[]{"version","map","seed","budget","red","blue"}.All(raw.ContainsKey))throw new FormatException("阵型缺少必要字段");return Validate(Json.Read<Deployment>(json,1024*1024));}
        public Deployment Validate(Deployment input,bool expedition=false)
        {
            if(input==null||!new[]{"0.1.0","0.2.0","0.3.0","0.4.0","0.5.0","0.6.0"}.Contains(input.version))throw new FormatException("不支持的布阵版本");
            var c=Json.Clone(input);c.version="0.6.0";
            if(expedition ? c.manorHall<1||c.manorHall>Manor.BuildingMaxLevel : c.manorHall!=0)throw new FormatException("远征等级仅用于领地庄园");
            bool frontier=c.map==FrontierMap.Id;
            if(c.map==null||!(frontier?expedition:Maps.ContainsKey(c.map)))throw new FormatException("未知地图");
            if(c.budget<100||c.budget>10000)throw new FormatException("军费须为 100–10000");
            if(c.red==null||c.blue==null||c.props==null||c.props.Count>24||c.commander==null||c.commander.aiTeams==null||c.commander.aiTeams.Count>2||c.commander.aiTeams.Distinct().Count()!=c.commander.aiTeams.Count||c.commander.aiTeams.Any(t=>!Team(t)))throw new FormatException("编队或军令配置无效");
            if(frontier&&(c.red.Count>FrontierMap.MaxParty||c.blue.Count>FrontierMap.Guards.Count||c.props.Count!=0||c.commander.aiTeams.Count!=0))
                throw new FormatException("巡边名册无效");
            int guardLevel=frontier?FrontierMap.ValidateGuardLevels(c.blue):0;
            foreach(var team in new[]{"red","blue"})
            {
                if(c.Army(team).Count>60||c.props.Count(p=>p.team==team)>12)throw new FormatException("兵力或道具数量超限");
                bool manorArmy=expedition&&team=="red";
                var deployedHeroes=manorArmy?new HashSet<string>(StringComparer.Ordinal):null;
                foreach(var u in c.Army(team))
                {
                    if(u==null||u.type==null||!LegalPosition(c,team,u.x,u.z))throw new FormatException("兵种、半场或坐标无效");
                    if(manorArmy&&Heroes.ContainsKey(u.type))
                    {
                        if(u.level<1||u.level>Manor.HeroMaxLevel||!deployedHeroes.Add(u.type))throw new FormatException("英雄等级或名册无效");
                    }
                    else if(!Units.ContainsKey(u.type)||(manorArmy?u.level<1||u.level>Manor.UnitMaxLevel:u.level!=guardLevel))
                        throw new FormatException("兵种或等级无效");
                    if(string.IsNullOrEmpty(u.stance))u.stance="advance";if(!Stances.Contains(u.stance))throw new FormatException("未知姿态");
                }
            }
            foreach(var p in c.props)if(p==null||p.type==null||!Props.ContainsKey(p.type)||!Team(p.team)||!LegalPosition(c,p.team,p.x,p.z))throw new FormatException("道具坐标或类型无效");
            foreach(var team in new[]{"red","blue"})if(Cost(c,team)>c.budget)throw new FormatException("士兵与道具超出军费");
            return c;
        }
        bool LegalPosition(Deployment c,string team,float x,float z)
        {
            if(c.map==FrontierMap.Id)return (team=="red"?FrontierMap.AtManor(x):x>=FrontierMap.EnemyStop)&&FrontierMap.HasGround(x,z);
            return Finite(x)&&Finite(z)&&Math.Abs(x)<=16.8f&&Math.Abs(z)<=10.8f&&(team=="red"?x<=-2.5f:x>=2.5f)&&Finite(Height(c.map,x,z));
        }
        public bool OrderGround(string map,float x,float z) => (map==FrontierMap.Id||Math.Abs(x)<=18&&Math.Abs(z)<=12)&&Finite(Height(map,x,z));
        public bool CanPlace(Deployment c,string team,string type,float x,float z,bool prop=false)
        {
            if(!Team(team)||type==null||!(prop?Props.ContainsKey(type):Units.ContainsKey(type)||c.manorHall>0&&team=="red"&&Heroes.ContainsKey(type))||!LegalPosition(c,team,x,z))return false;
            var radius=prop?Props[type].radius:.62f*Unit(type).scale;var m=Map(c.map);
            if(m.walls.Any(w=>Math.Abs(x-w.x)<w.hx+radius&&Math.Abs(z-w.z)<w.hz+radius)||m.rocks.Any(r=>Distance(x,z,r.x,r.z)<r.r+radius))return false;
            if(m.props.Concat(c.props).Any(p=>!( !prop&&(p.type=="spring"||p.type=="spikes"))&&Distance(x,z,p.x,p.z)<radius+Props[p.type].radius))return false;
            return !c.red.Concat(c.blue).Any(u=>Distance(x,z,u.x,u.z)<radius+.62f*Unit(u.type).scale);
        }
        public static float Distance(float x,float z,float bx,float bz){float dx=x-bx,dz=z-bz;return (float)Math.Sqrt(dx*dx+dz*dz);}
        public Deployment Place(Deployment input,string team,IEnumerable<UnitPlacement> additions,IEnumerable<int> removing=null,int playerLimit=10000)
        {
            var c=Validate(input);var remove=new HashSet<int>(removing??new int[0]);var list=c.Army(team);for(int i=list.Count-1;i>=0;i--)if(remove.Contains(i))list.RemoveAt(i);
            foreach(var u in additions){if(list.Count>=60||!CanPlace(c,team,u.type,u.x,u.z))throw new FormatException("队伍超限、越界或重叠");list.Add(Json.Clone(u));}
            if(Cost(c,team)>playerLimit)throw new FormatException("超出玩家军费");return Validate(c);
        }
        public Deployment Move(Deployment c,string team,List<int> indices,float dx,float dz,int limit=10000)
        {
            if(indices==null||indices.Count==0||indices.Distinct().Count()!=indices.Count||indices.Any(i=>i<0||i>=c.Army(team).Count)||!Finite(dx)||!Finite(dz))throw new FormatException("没有有效选中部队");
            return Place(c,team,indices.Select(i=>{var u=Json.Clone(c.Army(team)[i]);u.x+=dx;u.z+=dz;return u;}).ToList(),indices,limit);
        }
        public static Deployment Visible(Deployment config,string stage)
        {
            var c=Json.Clone(config);if(stage!="revealed"){if(stage!="red")c.red.Clear();if(stage!="blue")c.blue.Clear();c.props.RemoveAll(p=>p.team!=stage);}return c;
        }
        public static List<UnitPlacement> Squad(List<UnitPlacement> roster,string team,string shape,float x,float z,bool mirror)
        {
            if(!Team(team)||!new[]{"square","line","wedge"}.Contains(shape)||roster.Count==0||roster.Count>60)throw new FormatException("整队参数无效");
            var list=Json.Clone(roster);int row=0,index=0;while(index<list.Count){int width=shape=="line"?list.Count:shape=="wedge"?row*2+1:(int)Math.Ceiling(Math.Sqrt(list.Count)),n=Math.Min(width,list.Count-index);for(int j=0;j<n;j++){var u=list[index++];u.x=x+(team=="red"?-1:1)*row*2.5f;u.z=z+(j-(n-1)/2f)*2.5f;}row++;}
            if(mirror)list.AddRange(list.Where(u=>Math.Abs(u.z)>.01f).Select(u=>new UnitPlacement(u.type,u.x,-u.z,u.stance)).ToArray());return list;
        }
        public Favorite SaveFavorite(Deployment c,string team,string name){return new Favorite{name=string.IsNullOrWhiteSpace(name)?"我的阵型":name.Trim().Substring(0,Math.Min(32,name.Trim().Length)),map=c.map,team=team,units=Json.Clone(c.Army(team)),props=Json.Clone(c.props.Where(p=>p.team==team).ToList())};}
        public Deployment LoadFavorite(Deployment input,string team,Favorite f,int limit=10000)
        {
            if(f==null||f.map!=input.map||!Team(f.team)||f.units==null||f.props==null)throw new FormatException("请先切换到收藏地图");var c=Validate(input);int flip=f.team==team?1:-1;var units=Json.Clone(f.units);foreach(var u in units)u.x*=flip;if(team=="red")c.red=units;else c.blue=units;c.props.RemoveAll(p=>p.team==team);foreach(var p in f.props){var q=Json.Clone(p);q.x*=flip;q.team=team;c.props.Add(q);}c=Validate(c);if(Cost(c,team)>limit)throw new FormatException("收藏超过玩家军费");return c;
        }
        public string Diagnose(Deployment visible,string team)
        {
            var own=visible.Army(team);if(own.Count==0)return "先招募前排与输出部队。";
            if(own.Count(u=>new[]{"shield","spear","giant","militia"}.Contains(u.type))<own.Count*.25f)return "前排承伤偏少，远程与军医容易暴露。";
            var back=own.Where(u=>Units[u.type].ranged||u.type=="medic").ToList();int pairs=0;for(int i=0;i<back.Count;i++)for(int j=i+1;j<back.Count;j++)if(Distance(back[i].x,back[i].z,back[j].x,back[j].z)<3.5f)pairs++;
            if(back.Count>=3&&pairs>=Math.Max(2,back.Count*.6f))return "远程站位密集，容易受到范围爆破重创。";
            if(visible.map=="highland"&&back.Count>0&&back.Where(u=>Units[u.type].ranged).All(u=>Height(visible.map,u.x,u.z)<1))return "高台射程优势尚未利用。";
            if(visible.Army(Other(team)).Count(u=>u.type=="cavalry")>=2&&!own.Any(u=>u.type=="spear"))return "公开敌军有骑兵，考虑长枪或拒马保护侧翼。";
            return "前后排基本齐备；留出冲锋距离与分散间距。";
        }
    }
    public sealed class BattleSeries
    {
        public readonly SeriesArchive Archive=new SeriesArchive(); readonly Catalog catalog;
        public BattleSeries(Catalog catalog,Deployment c){this.catalog=catalog;Archive.initial=catalog.Validate(c);}
        public bool Complete {get{return Archive.rounds.Count>=3||Archive.wins["red"]>=2||Archive.wins["blue"]>=2;}}
        public string Winner {get{return !Complete?null:Archive.wins["red"]==Archive.wins["blue"]?"draw":Archive.wins["red"]>Archive.wins["blue"]?"red":"blue";}}
        public SeriesRound Last {get{return Archive.rounds.LastOrDefault();}}
        public void Record(Deployment c,Result r)
        {
            c=catalog.Validate(c);if(Complete||r==null||r.report==null||!Archive.wins.ContainsKey(r.winner))throw new FormatException("系列赛状态或战报无效");
            if(c.map!=Archive.initial.map||c.budget!=Archive.initial.budget||c.commander.enabled!=Archive.initial.commander.enabled||Archive.rounds.Any(x=>x.config.seed==c.seed))throw new FormatException("系列赛规则已变更或小局已记分");
            Archive.rounds.Add(new SeriesRound{config=Json.Clone(c),result=Json.Clone(r)});Archive.wins[r.winner]++;Archive.winner=Winner;
        }
        public Deployment Next(){if(Last==null||Complete)throw new InvalidOperationException("没有下一局");var c=Json.Clone(Last.config);c.seed=unchecked(Archive.initial.seed+(uint)Archive.rounds.Count);return c;}
    }
}
