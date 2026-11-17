// Persistence uses strict Frontline.Core.Json; Unity inspector serialization is intentionally unused.
#pragma warning disable UAC1015, UAC1009
using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace Frontline.Core
{
    [Serializable] public struct V3
    {
        public float x,y,z;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public V3(float x,float y=0,float z=0){this.x=x;this.y=y;this.z=z;}
        public float Length { [MethodImpl(MethodImplOptions.AggressiveInlining)] get {return (float)Math.Sqrt(x*x+y*y+z*z);} }
        public float Planar { [MethodImpl(MethodImplOptions.AggressiveInlining)] get {return (float)Math.Sqrt(x*x+z*z);} }
        public V3 Normalized { [MethodImpl(MethodImplOptions.AggressiveInlining)] get {var l=Length;return l>0.00001f?this/l:new V3();} }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static V3 operator +(V3 a,V3 b){return new V3(a.x+b.x,a.y+b.y,a.z+b.z);}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static V3 operator -(V3 a,V3 b){return new V3(a.x-b.x,a.y-b.y,a.z-b.z);}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static V3 operator *(V3 a,float f){return new V3(a.x*f,a.y*f,a.z*f);}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static V3 operator /(V3 a,float f){return a*(1/f);}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Distance(V3 a,V3 b){return (a-b).Length;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float DistanceXZ(V3 a,V3 b){return (a-b).Planar;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static float Dot(V3 a,V3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
    }
    [Serializable] public class UnitDef {public string id,name,subtitle,glyph,description;public float cost,hp,damage,range,speed,cooldown,mass,scale=1,knock,heal;public bool ranged;}
    [Serializable] public class PropDef {public string id,name,subtitle,glyph,description;public float cost,hp,radius;}
    [Serializable] public class Platform {public float x,y,z,hx,hy,hz,angle;public bool ramp,bridge;}
    [Serializable] public class Rock {public float x,z,r;}
    [Serializable] public class Wall {public float x,z,hx,hy,hz;}
    [Serializable] public class Zone {public string type;public float x,z,hx,hz;}
    [Serializable] public class Passage {public float approach;public float[] lanes=new float[0];}
    [Serializable] public class MapDef {public string id,name,description,hint;public int color;public List<Platform> platforms=new List<Platform>();public List<Rock> rocks=new List<Rock>();public List<Wall> walls=new List<Wall>();public List<Zone> zones=new List<Zone>();public List<PropPlacement> props=new List<PropPlacement>();public Passage passage;}
    [Serializable] public class Mission {public string name,description,map,enemy;public int budget;}
    [Serializable] public class FormationDef {public string id;public List<UnitPlacement> red=new List<UnitPlacement>(),blue=new List<UnitPlacement>();}
    [Serializable] public class CatalogData {public string version;public List<UnitDef> units=new List<UnitDef>();public List<PropDef> props=new List<PropDef>();public List<MapDef> maps=new List<MapDef>();public List<Mission> missions=new List<Mission>();public List<FormationDef> formations=new List<FormationDef>();}
    [Serializable] public class UnitPlacement {public string type,stance="advance";public float x,z;public UnitPlacement(){}public UnitPlacement(string type,float x,float z,string stance="advance"){this.type=type;this.x=x;this.z=z;this.stance=stance;}}
    [Serializable] public class PropPlacement {public string type,team="neutral";public float x,z;}
    [Serializable] public class CommanderConfig {public bool enabled;public List<string> aiTeams=new List<string>();}
    [Serializable] public class Deployment {public string version="0.6.0",map="meadow";public uint seed=42;public int budget=2200;public List<UnitPlacement> red=new List<UnitPlacement>(),blue=new List<UnitPlacement>();public List<PropPlacement> props=new List<PropPlacement>();public CommanderConfig commander=new CommanderConfig();public List<UnitPlacement> Army(string team){return team=="red"?red:blue;}}
    [Serializable] public class Order {public string type;public float x,z;public Order(){}public Order(string type,float x,float z){this.type=type;this.x=x;this.z=z;}}
    [Serializable] public class OrderEntry:Order {public int id,tick,endTick;public string team;}
    [Serializable] public class CommanderState {public Dictionary<string,int> points=new Dictionary<string,int>{{"red",3},{"blue",3}},readyAt=new Dictionary<string,int>{{"red",0},{"blue",0}};public List<OrderEntry> active=new List<OrderEntry>(),log=new List<OrderEntry>();}
    [Serializable] public class Contribution {public float damageDealt,damageTaken,healing,friendlyFire;public int kills,falls,fallKills,friendlyKills;public void Add(Contribution r){damageDealt+=r.damageDealt;damageTaken+=r.damageTaken;healing+=r.healing;friendlyFire+=r.friendlyFire;kills+=r.kills;falls+=r.falls;fallKills+=r.fallKills;friendlyKills+=r.friendlyKills;}public float Score {get{return damageDealt-2*friendlyFire+.75f*healing+30*kills+20*fallKills;}}}
    [Serializable] public class UnitReport:Contribution {public int id;public string type,team;public bool alive;public float hp,score;public V3 spawn;}
    [Serializable] public class TypeReport:Contribution {public int initial,survived,lost;}
    [Serializable] public class TeamReport:TypeReport {public float remainingValue;public Dictionary<string,TypeReport> byType=new Dictionary<string,TypeReport>();}
    [Serializable] public class BattleReport {public Dictionary<string,TeamReport> teams=new Dictionary<string,TeamReport>();public List<UnitReport> units=new List<UnitReport>(),props=new List<UnitReport>();public UnitReport mvp;}
    [Serializable] public class Highlight {public int id,tick;public string type,team;public float value;}
    [Serializable] public class Cue {public int id,tick;public string type;public float x,y,z,strength=1;}
    [Serializable] public class Result {public string winner,reason;public float time;public int red,blue;public Dictionary<string,float> remainingValue=new Dictionary<string,float>();public BattleReport report;public List<OrderEntry> orders=new List<OrderEntry>();public List<Highlight> highlights=new List<Highlight>();}
    [Serializable] public class UnitFrame {public int id;public string type,team,stance;public bool alive,inspired,fortified;public float hp,angle,attack,charge;[OmitZero] public float hitFlash,hitTiltX,hitTiltZ,moveSpeed,moveForward,gait,guard;public float[] t;}
    [Serializable] public class ProjectileFrame {public int id;public string type;public float[] t;public V3 v;}
    [Serializable] public class PropFrame {public int id;public string type,team;public float x,z,floor,hp;public bool alive,active;}
    [Serializable] public class CorpsePart {public string name;public float[] t;public static string Name(int i){switch(i){case 0:return "torso";case 1:return "head";case 2:return "armL";case 3:return "armR";case 4:return "legL";default:return "legR";}}}
    [Serializable] public class CorpseFrame {[NonSerialized]public float[] wreckStorage;public int id;public string type,team;public float age;public float[] wreck;public List<CorpsePart> parts=new List<CorpsePart>(6);}
    [Serializable] public class EffectFrame {public int id;public string type;public float x,y,z,life,maxLife;[OmitZero] public float angle;}
    [Serializable] public class Frame {internal SnapshotStorage storage;public int tick;public float time;public List<UnitFrame> units=new List<UnitFrame>();public List<ProjectileFrame> projectiles=new List<ProjectileFrame>();public List<CorpseFrame> ragdolls=new List<CorpseFrame>();public List<EffectFrame> effects=new List<EffectFrame>();public List<PropFrame> props=new List<PropFrame>();public CommanderState commander=new CommanderState();public List<Cue> cues=new List<Cue>();public Result result;}
    [Serializable] public class ReplayPackage {public string format="frontline-replay",engineVersion="unity-0.9.0",kind="trajectory";public int version=3;public Deployment config;public Result result;public List<Frame> frames=new List<Frame>();public List<OrderEntry> orderLog=new List<OrderEntry>();public List<Highlight> highlights=new List<Highlight>();}
    [Serializable] public class Favorite {public string name,map,team;public List<UnitPlacement> units=new List<UnitPlacement>();public List<PropPlacement> props=new List<PropPlacement>();}
    [Serializable] public class SeriesRound {public Deployment config;public Result result;}
    [Serializable] public class SeriesArchive {public string format="frontline-bo3";public int version=2;public Deployment initial;public List<SeriesRound> rounds=new List<SeriesRound>();public Dictionary<string,int> wins=new Dictionary<string,int>{{"red",0},{"blue",0},{"draw",0}};public string winner;}
}

