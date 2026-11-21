using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;

namespace Frontline.Core
{
    public sealed class Entity
    {
        public int id;public string type,team;public bool alive=true;public float hp,maxHp;public V3 position,spawn;
        public readonly Contribution stats=new Contribution();public IBody body;
    }
    public sealed class Soldier
    {
        public readonly Entity entity;
        public readonly UnitDef definition;
        public readonly string stance;
        internal readonly bool isRed,isRanged,isCavalry,canHeal,keepsMedicDistance,isSpear,isGiant,isCrossbow,isCatapult,isHold,isFlank,isHunt,canFortify;
        internal readonly bool isVanguard,isMarksman;
        internal readonly int armamentReduction,armamentStability,armamentPierce;
        internal readonly float turnRateAtRest,windupDuration;
        internal Soldier(Entity entity,UnitDef definition,string stance,bool legacyManorMotion,HeroGear gear=default)
        {
            this.entity=entity;this.definition=definition;this.stance=stance;
            string type=entity.type;
            string ability=(definition as HeroDef)?.ability;
            isVanguard=ability=="vanguard";isMarksman=ability=="marksman";
            armamentReduction=gear.reduction;armamentStability=gear.stability;armamentPierce=gear.pierce;
            isRed=entity.team=="red";isRanged=definition.ranged;isCavalry=type=="cavalry";
            canHeal=type=="medic"||ability=="field_healer";keepsMedicDistance=type=="medic"||legacyManorMotion&&canHeal;
            isSpear=type=="spear";isGiant=type=="giant"||isVanguard;isCrossbow=type=="crossbow"||isMarksman;isCatapult=type=="catapult";
            isHold=stance=="hold";isFlank=stance=="flank";isHunt=stance=="hunt";
            canFortify=type=="shield"||isSpear||isGiant||type=="militia";
            turnRateAtRest=CombatMotion.TurnRate(isVanguard?"giant":type,0);windupDuration=CombatMotion.Windup(isVanguard?"giant":type);
            collisionFast=isCavalry;combatBody=entity.body as ICombatBody;
        }
        public float angle,cooldown,attack,stun,charge,floor;public V3 anchor,progress,avoidance,avoidanceTarget;
        public int inspireUntil,fortifyUntil,lastHitTick=-99999,escapeUntil,blocked,escapes,routeStage;public float routeLane;public bool hasRoute;
        public Entity lastSource;public Soldier target;public OrderEntry focus;
        public ICombatBody combatBody;public float slope,hitFlash,hitTiltX,hitTiltZ,moveSpeed,moveForward,gait,windup;public float guard;public int lastWallTick=-999;public bool preparing,collisionFast;public int lastVisualTick=-9999;
    }
    public sealed class PropState {public Entity entity;internal PropDef definition;public float floor,fuse=-1,cooldown;}
    public sealed class Shot {public int id;public string type;public Entity owner;public IBody body;public float life,damage,knock,pierce;public bool removed;}
    public sealed class Seeded
    {
        public uint state;public Seeded(uint seed){state=seed;}
        public float Next(){state=unchecked(state*1664525u+1013904223u);return (state>>8)*(1f/16777216f);}
    }
    public sealed partial class Battle:IRecordedBattle
    {
        public readonly Catalog Data;public readonly Deployment Config;public readonly IPhysicsWorld World;public readonly Seeded Random;
        public readonly List<Soldier> Units=new List<Soldier>();public readonly List<PropState> Props=new List<PropState>();public readonly List<Shot> Shots=new List<Shot>();
        public readonly CommanderState Commander=new CommanderState();public readonly List<Highlight> Highlights=new List<Highlight>();public readonly List<Cue> Cues=new List<Cue>(24);public readonly List<EffectFrame> Effects=new List<EffectFrame>(CombatMotion.EffectCapacity);
        public int Tick {get;private set;}public float Time {get{return Tick*Catalog.Step;}}public Result Result {get;private set;}
        struct GridCell {public int first,last;}
        readonly GridCell[] grid=new GridCell[128*128];
        readonly int[] occupied,nextInCell;
        int occupiedCount;
        readonly Dictionary<int,GridCell> overflowGrid=new Dictionary<int,GridCell>();
        readonly List<Soldier> redTargets=new List<Soldier>(60),blueTargets=new List<Soldier>(60);
        readonly Dictionary<int,Entity> entities=new Dictionary<int,Entity>();readonly Dictionary<int,Soldier> soldiersById=new Dictionary<int,Soldier>();readonly Dictionary<int,PropState> propsById=new Dictionary<int,PropState>();readonly Dictionary<int,Shot> shotsById=new Dictionary<int,Shot>();
        readonly Dictionary<string,int> cueTimes=new Dictionary<string,int>(),highlightTimes=new Dictionary<string,int>();
        readonly Stack<Cue> cuePool=new Stack<Cue>(24);readonly Stack<EffectFrame> effectPool=new Stack<EffectFrame>(CombatMotion.EffectCapacity);readonly bool legacyMath;readonly MapDef map;readonly GroundSample[] groundSamples;
        readonly ZoneSample[] zoneSamples;
        readonly List<PropState> attackablePropsForRed=new List<PropState>(),attackablePropsForBlue=new List<PropState>();
        readonly List<Soldier> orderTargets=new List<Soldier>(120);
        struct GroundSample {public float x,z,hz,extent,top,slope;}
        enum Terrain { Ground, Mud, Ice }
        struct ZoneSample {public float x,z,hx,hz;public Terrain terrain;}
        int nextId=1,nextCue=1,nextEffect=1,leader,replayIndex;bool firstDeath,disposed;List<OrderEntry> replayOrders;
        public Battle(Catalog catalog,Deployment input,IWorldFactory factory,bool expedition=false,
            bool legacyManorMotion=false,int targetScanWorkers=TargetWorkerCap)
            :this(catalog,catalog.Validate(input,expedition),factory,targetScanWorkers,legacyManorMotion) {}

        Battle(Catalog catalog,Deployment validated,IWorldFactory factory,int targetScanWorkers,bool legacyManorMotion)
        {
            Data=catalog;Config=validated;
            int unitCapacity=Math.Max(2*Catalog.MaxUnits,Config.red.Count+Config.blue.Count);
            occupied=new int[unitCapacity];nextInCell=new int[unitCapacity+1];
            IsFrontier=Config.map==FrontierMap.Id;Random=new Seeded(Config.seed);World=factory.Create(catalog,Config);legacyMath=World is ILegacyCompatibilityWorld;map=Data.Map(Config.map);var platforms=map.platforms;groundSamples=new GroundSample[platforms.Count];for(int i=0;i<platforms.Count;i++){var p=platforms[i];float cos=(float)Math.Cos(p.angle);groundSamples[i]=new GroundSample{x=p.x,z=p.z,hz=p.hz,extent=p.hx*cos+p.hy*Math.Abs((float)Math.Sin(p.angle)),top=p.y+p.hy/cos,slope=(float)Math.Tan(p.angle)};}
            if(IsFrontier)march=new MarchState(map);
#if !UNITY_WEBGL
            if(!IsFrontier&&!legacyMath&&!legacyManorMotion)
                TargetScanWorkers=Math.Max(1,Math.Min(Math.Min(TargetWorkerCap,Environment.ProcessorCount),targetScanWorkers));
#endif
            var zones=map.zones;
            zoneSamples=zones.Count==0?Array.Empty<ZoneSample>():new ZoneSample[zones.Count];
            for(int i=0;i<zones.Count;i++)
            {
                var zone=zones[i];
                zoneSamples[i]=new ZoneSample{x=zone.x,z=zone.z,hx=zone.hx,hz=zone.hz,terrain=zone.type=="mud"?Terrain.Mud:zone.type=="ice"?Terrain.Ice:Terrain.Ground};
            }
            for(int i=0;i<24;i++)cuePool.Push(new Cue());for(int i=0;i<CombatMotion.EffectCapacity;i++)effectPool.Push(new EffectFrame());
            try{
                foreach(var team in new[]{"red","blue"})foreach(var p in Config.Army(team))
                {
                    var d=catalog.Unit(p.type).AtLevel(p.level);
                    var gear=p.armament>0?Manor.Armament(p.type,p.armament):default;
                    var stats=p.armament>0?d.WithArmament(gear):d;
                    var e=new Entity{id=nextId++,type=p.type,team=team,hp=stats.hp,maxHp=stats.hp,spawn=new V3(p.x,0,p.z)};
                    e.body=World.Unit(e.id,p,team,d);e.position=e.body.Position;entities.Add(e.id,e);
                    if(IsFrontier&&team=="blue"&&!(e.body is IFrontierBody))throw new InvalidOperationException("边疆需要安全区物理约束");
                    Units.Add(new Soldier(e,stats,p.stance,legacyManorMotion,gear){cooldown=Random.Next()*.6f,angle=team=="red"?(float)Math.PI/2:-(float)Math.PI/2,anchor=e.spawn,progress=e.position});
                }
                foreach(var placement in map.props.Concat(Config.props))
                {
                    var p=Json.Clone(placement);if(!Catalog.Team(p.team))p.team="neutral";var d=catalog.Props[p.type];var e=new Entity{id=nextId++,type=p.type,team=p.team,hp=d.hp,maxHp=d.hp,spawn=new V3(p.x,0,p.z)};e.body=World.Prop(e.id,p,d);e.position=e.body.Position;entities.Add(e.id,e);var prop=new PropState{entity=e,definition=d,floor=catalog.Height(Config.map,p.x,p.z)};
                    Props.Add(prop);
                    if(IsAttackable(e))
                    {
                        // Preserve map then deployment order, including neutral barricades for both teams.
                        if(e.team!="red")attackablePropsForRed.Add(prop);
                        if(e.team!="blue")attackablePropsForBlue.Add(prop);
                    }
                }
                foreach(var u in Units)soldiersById.Add(u.entity.id,u);foreach(var p in Props)propsById.Add(p.entity.id,p);
                Cache();
            }catch{World.Dispose();throw;}
        }
        static bool IsAttackable(Entity e){return e.type=="barricade"||(e.type=="banner"&&e.team!="neutral");}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] Terrain TerrainAt(float x,float z)
        {
            // The first matching zone wins, even if it provides no movement modifier.
            for(int i=0;i<zoneSamples.Length;i++)
            {
                ref readonly var zone=ref zoneSamples[i];
                if(Math.Abs(x-zone.x)<zone.hx&&Math.Abs(z-zone.z)<zone.hz)return zone.terrain;
            }
            return Terrain.Ground;
        }
        static readonly Passage MarshPassage=new Passage{approach=5.3f,lanes=new[]{-9.6f,9.6f}};
        static readonly float[] EscapeAngles={1.047f,1.571f,-1.047f,-1.571f,3.14159f};
        [MethodImpl(MethodImplOptions.AggressiveInlining)] static int Key(float x,float z){return ((int)Math.Floor(x/3)+64)*128+(int)Math.Floor(z/3)+64;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] int FirstInCell(int key){if((uint)key<(uint)grid.Length)return grid[key].first;return overflowGrid.TryGetValue(key,out var cell)?cell.first:0;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] float GroundAt(float x,float z){return GroundAt(x,z,out _);}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] float GroundAt(float x,float z,out float slope)
        {
            slope=0;if(legacyMath||IsFrontier)return Data.Height(Config.map,x,z);if(!Catalog.Finite(x)||!Catalog.Finite(z))return float.NegativeInfinity;float height=float.NegativeInfinity;
            for(int i=0;i<groundSamples.Length;i++){ref readonly var p=ref groundSamples[i];if(Math.Abs(x-p.x)<=p.extent-.01f&&Math.Abs(z-p.z)<=p.hz-.01f){float h=p.slope==0?p.top:p.top+p.slope*(x-p.x);if(h>height){height=h;slope=p.slope;}}}return height;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] bool Within(V3 a,V3 b,float radius){if(legacyMath)return V3.DistanceXZ(a,b)<=radius;float x=a.x-b.x,z=a.z-b.z;return x*x+z*z<=radius*radius;}
        void Cache()
        {
            for(int i=0;i<occupiedCount;i++)grid[occupied[i]]=default;
            occupiedCount=0;overflowGrid.Clear();redTargets.Clear();blueTargets.Clear();exposedParty=false;
            for(int i=0;i<Units.Count;i++)
            {
                var u=Units[i];if(!u.entity.alive)continue;
                var p=u.entity.body.Position;u.entity.position=p;
                (u.isRed?redTargets:blueTargets).Add(u);
                if(IsFrontier&&u.isRed&&!FrontierMap.Safe(p))exposedParty=true;
                u.floor=GroundAt(p.x,p.z,out u.slope);
                int key=Key(p.x,p.z),index=i+1;
                nextInCell[index]=0;
                if((uint)key<(uint)grid.Length)
                {
                    ref var cell=ref grid[key];
                    if(cell.first==0){occupied[occupiedCount++]=key;cell.first=index;}
                    else nextInCell[cell.last]=index;
                    cell.last=index;
                }
                else
                {
                    overflowGrid.TryGetValue(key,out var cell);
                    if(cell.first==0)cell.first=index;else nextInCell[cell.last]=index;
                    cell.last=index;overflowGrid[key]=cell;
                }
            }
        }
        Neighborhood Nearby(V3 p,float radius){return new Neighborhood(this,p,radius);}
        // foreach in the per-soldier avoidance path stays on the stack. LINQ consumers
        // in infrequent tactical checks can still use the same ordered neighborhood.
        readonly struct Neighborhood:IEnumerable<Soldier>
        {
            readonly Battle battle;readonly V3 p;readonly float radius;
            public Neighborhood(Battle battle,V3 p,float radius){this.battle=battle;this.p=p;this.radius=radius;}
            public Enumerator GetEnumerator(){return new Enumerator(battle,p,radius);}
            IEnumerator<Soldier> IEnumerable<Soldier>.GetEnumerator(){return GetEnumerator();}System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator(){return GetEnumerator();}
            public struct Enumerator:IEnumerator<Soldier>
            {
                readonly Battle battle;readonly V3 p;readonly float radius;readonly int maxX,minZ,maxZ;int x,z,index;
                public Soldier Current {get;private set;}object System.Collections.IEnumerator.Current {get{return Current;}}
                public Enumerator(Battle battle,V3 p,float radius){this.battle=battle;this.p=p;this.radius=radius;x=(int)Math.Floor((p.x-radius)/3);maxX=(int)Math.Floor((p.x+radius)/3);minZ=(int)Math.Floor((p.z-radius)/3);maxZ=(int)Math.Floor((p.z+radius)/3);z=minZ;index=0;Current=null;}
                public bool MoveNext()
                {
                    while(true)
                    {
                        while(index!=0)
                        {
                            var u=battle.Units[index-1];index=battle.nextInCell[index];
                            if(u.entity.alive&&battle.Within(p,u.entity.position,radius)){Current=u;return true;}
                        }
                        if(x>maxX)return false;
                        index=battle.FirstInCell((x+64)*128+z+64);
                        if(++z>maxZ){z=minZ;x++;}
                    }
                }
                public void Dispose(){}public void Reset(){throw new NotSupportedException();}
            }
        }
        Soldier Target(Soldier own)
        {
            if(IsFrontier&&(!exposedParty||FrontierMap.Safe(own.entity.position)))return null;
            Soldier best=null;float score=float.PositiveInfinity;
            var query=new TargetQuery(own,Tick);
            var targets=own.isRed?blueTargets:redTargets;
            for(int targetIndex=0;targetIndex<targets.Count;targetIndex++)
            {
                var foe=targets[targetIndex];if(!foe.entity.alive||IsFrontier&&!FrontierTarget(own,foe))continue;
                var position=foe.entity.position;
                float s;
                if(legacyMath){s=V3.DistanceXZ(query.position,position);s*=s;}
                else s=query.DistanceSquared(position);
                s=query.Weigh(s,position,foe.isRanged,foe.canHeal,foe.definition.cost);
                if(s<score){best=foe;score=s;}
            }return best;
        }
        public float Power(string team){float value=0;foreach(var u in Units)if(u.entity.alive&&u.entity.team==team)value+=Math.Max(0,u.entity.hp)/u.entity.maxHp*u.definition.cost;return value;}
        public bool IssueOrder(string team,Order order,out string reason)
        {
            reason=null;if(!Config.commander.enabled)reason="军令模式未开启";else if(Result!=null)reason="本局已经结束";else if(!Catalog.Team(team)||order==null||!Catalog.Orders.Contains(order.type))reason="军令或阵营无效";
            else if(team=="red"&&Config.manorHall>0&&Config.manorHall<Manor.OrderLevel(order.type))reason="宅邸等级尚未解锁此军令";
            else if(!Data.OrderGround(Config.map,order.x,order.z))reason="请选择战场地面";
            else if(Commander.points[team]<1)reason="军令点数不足";else if(Tick<Commander.readyAt[team])reason="军令正在冷却";
            if(reason!=null)return false;Cache();var entry=new OrderEntry{id=Commander.log.Count+1,team=team,type=order.type,x=order.x,z=order.z,tick=Tick,endTick=Tick+(order.type=="focus"?360:300)};
            Commander.points[team]--;Commander.readyAt[team]=Tick+480;Commander.log.Add(entry);Commander.active.Add(entry);
            bool inspire=order.type=="inspire",fortify=order.type=="fortify",focus=order.type=="focus";
            foreach(var u in Nearby(new V3(order.x,0,order.z),focus?8:6))if(u.entity.team==team)
            {
                if(inspire)u.inspireUntil=entry.endTick;
                else if(fortify&&u.canFortify)u.fortifyUntil=entry.endTick;
                else if(focus)u.focus=entry;
            }
            Cue("order",new V3(order.x,Data.Height(Config.map,order.x,order.z),order.z));Highlight("order",team);return true;
        }
        public void UseOrderLog(List<OrderEntry> log)
        {
            if(Tick!=0||Commander.log.Count>0)throw new InvalidOperationException("重演必须从初始状态开始");Replay.ValidateOrders(Data,Config,log);replayOrders=Json.Clone(log);replayIndex=0;
        }
        void TickOrders()
        {
            for(int i=Commander.active.Count-1;i>=0;i--)if(Commander.active[i].endTick<=Tick)Commander.active.RemoveAt(i);
            if(replayOrders!=null){while(replayIndex<replayOrders.Count&&replayOrders[replayIndex].tick==Tick){var e=replayOrders[replayIndex++];if(!IssueOrder(e.team,e,out var reason))throw new InvalidOperationException("军令日志无法重演："+reason);}return;}
            if(Tick%60!=0)return;
            foreach(var team in Config.commander.aiTeams)
            {
                if(Commander.points[team]<=0||Tick<Commander.readyAt[team])continue;Soldier front=null;foreach(var u in Units)if(u.entity.alive&&u.entity.team==team&&(front==null||(team=="red"?u.entity.position.x>front.entity.position.x:u.entity.position.x<front.entity.position.x)))front=u;if(front==null)continue;
                orderTargets.Clear();
                foreach(var u in Nearby(front.entity.position,8))if(u.entity.team!=team)orderTargets.Add(u);
                int count=orderTargets.Count;
                string type=front.entity.hp/front.entity.maxHp<.65f?"fortify":count>=3?"focus":"inspire";
                var p=front.entity.position;
                if(type=="focus"){p=new V3();foreach(var e in orderTargets)p+=e.entity.position/count;}
                IssueOrder(team,new Order(type,p.x,p.z),out _);
            }
        }
        bool EscapeClear(V3 q){foreach(var wall in map.walls)if(Math.Abs(q.x-wall.x)<wall.hx+.5f&&Math.Abs(q.z-wall.z)<wall.hz+.5f)return false;return true;}
        void Heal(Entity target,float amount,Entity source)
        {
            if(!target.alive)return;float actual=Math.Max(0,Math.Min(target.maxHp-target.hp,amount));target.hp+=actual;if(source!=null)source.stats.healing+=actual;if(actual>0&&(Tick+target.id)%12==0){Cue("heal",target.position);Effect("heal",target.position,.4f);}
        }
        void Command(Soldier u,int index,bool scanPrepared)
        {
            var e=u.entity;var p=e.position;var d=u.definition;
            if(IsFrontier?FrontierMap.Fell(p):p.y< -4||Math.Abs(p.x)>23||Math.Abs(p.z)>19){Kill(u,new V3(),"fall",null);return;}
            bool inspired=u.inspireUntil>Tick;u.cooldown-=Catalog.Step*(inspired?1.15f:1);u.attack=Math.Max(0,u.attack-Catalog.Step*3);u.stun=Math.Max(0,u.stun-Catalog.Step);u.hitFlash=Math.Max(0,u.hitFlash-Catalog.Step*7.5f);u.guard=Math.Max(0,u.guard-Catalog.Step*5);u.hitTiltX*=.83f;u.hitTiltZ*=.83f;
            bool ai=(Tick+e.id)%6==0;
            if(ai||u.target==null||!u.target.entity.alive||IsFrontier&&!FrontierTarget(u,u.target))
                u.target=ResolveTarget(u,index,scanPrepared);
            if(u.canHeal&&ai){Soldier ally=null;float ratio=1;foreach(var a in Nearby(p,6))if(a.entity.team==e.team&&a.entity.hp/a.entity.maxHp<ratio){ally=a;ratio=a.entity.hp/a.entity.maxHp;}if(ally!=null)Heal(ally.entity,d.heal*.1f,e);}
            var target=u.target;if(target==null){if(IsFrontier)WalkFrontier(u,inspired);return;}var tp=target.entity.position;var diff=tp-p;float dist=diff.Planar,range=d.range*(u.isRanged&&u.floor>target.floor+1?1.18f:1),min=target.definition.scale*.25f;
            V3 move=new V3(diff.x/(dist>.001f?dist:1),0,diff.z/(dist>.001f?dist:1));float speed=d.speed;bool firing=u.isRanged&&dist<range;
            if(u.isCrossbow&&p.x*tp.x<0&&Config.map=="ruins"){float crossing=p.z+(tp.z-p.z)*(-p.x/(tp.x-p.x));if(Math.Abs(crossing)>2.1f&&Math.Abs(crossing)<9.1f)firing=false;}
            if((u.isRanged||u.keepsMedicDistance)&&dist<4&&!u.isHold){move*=-1;speed*=.7f;}else if(firing||(!u.isRanged&&dist<range+min-.15f)||u.isHold)speed=0;
            if(u.keepsMedicDistance&&dist>5&&dist<9)speed=0;
            if(u.isHold&&!firing&&dist>range+min&&V3.DistanceXZ(p,u.anchor)>1.4f){move=(u.anchor-p);move.y=0;move=move.Normalized;speed=d.speed*.7f;}
            bool narrow=CombatMotion.Narrow(Config.map,p.x,p.z);var passage=map.passage;if(passage==null&&u.isFlank&&Config.map=="marsh")passage=MarshPassage;
            if(passage!=null&&!u.isHold&&!firing&&dist>range+min-.15f)
            {
                if(!u.hasRoute&&p.x*tp.x<0){u.hasRoute=true;u.routeLane=u.isFlank&&passage.lanes.Length>1?(e.spawn.z>=0?passage.lanes.Max():passage.lanes.Min()):passage.lanes[0];u.routeStage=Math.Abs(p.z-u.routeLane)>1?0:1;}
                if(u.hasRoute){float sign=tp.x>=0?1:-1;var entry=new V3(-sign*passage.approach,0,u.routeLane);if(u.routeStage==0&&V3.DistanceXZ(p,entry)<.85f)u.routeStage=1;if(u.routeStage==1&&sign*p.x>passage.approach+.5f)u.hasRoute=false;else{move=new V3(u.routeStage==0?entry.x:sign*(passage.approach+.8f),0,u.routeLane)-p;move.y=0;move=move.Normalized;speed=d.speed;}}
            }else if(u.isFlank&&!u.isRanged&&dist>6&&passage==null){move.z=(tp.z+Math.Sign(e.spawn.z==0?1:e.spawn.z)*5-p.z)/(dist+3);move=move.Normalized;}
            Terrain terrain=TerrainAt(p.x,p.z);if(inspired)speed*=1.12f;if(terrain==Terrain.Mud)speed*=.45f;
            if(speed>0&&!u.isHold)
            {
                if((Tick+e.id)%30==0){if(V3.DistanceXZ(p,u.progress)<.18f&&u.stun<=0)u.blocked++;else u.blocked=0;u.progress=p;if(u.blocked>=3){u.blocked=0;u.escapes++;u.escapeUntil=Tick+90;}}
                if(u.escapeUntil>Tick)
                {
                    if(narrow){float sign=tp.x>=p.x?1:-1;float side=(e.id%2==0?-1:1)*(u.escapes%2==0?-1:1);var trial=new V3(-sign*.65f,0,side*.45f);var q=p+trial*.55f;if(Catalog.Finite(GroundAt(q.x,q.z))&&EscapeClear(q)&&Math.Abs(q.z-u.routeLane)<1.35f){move=trial;speed*=.55f;}}
                    else {float side=(e.id%2==0?-1:1)*(u.escapes%2==0?-1:1);foreach(float baseAngle in EscapeAngles){float angle=baseAngle==3.14159f?baseAngle:baseAngle*side;var direction=new V3(move.x*(float)Math.Cos(angle)-move.z*(float)Math.Sin(angle),0,move.x*(float)Math.Sin(angle)+move.z*(float)Math.Cos(angle));var q=p+direction*1.2f;if(Catalog.Finite(Data.Height(Config.map,q.x,q.z))&&EscapeClear(q)){move=direction;break;}}}
                }
                // Stagger local perception at 10Hz; steering, forces and collision solving remain 60Hz.
                if(ai){u.avoidanceTarget=new V3();foreach(var a in Nearby(p,3))if(a!=u&&a.entity.team==e.team){var delta=p-a.entity.position;float squared=delta.x*delta.x+delta.z*delta.z,radius=.65f*(d.scale+a.definition.scale);if(squared<radius*radius&&squared>.0001f){float dd=(float)Math.Sqrt(squared);u.avoidanceTarget+=CombatMotion.Separation(delta,radius,narrow,tp.x>=p.x?1:-1,dd);}}}u.avoidance+=(u.avoidanceTarget-u.avoidance)*.35f;move+=u.avoidance;
            }
            var velocity=e.body.Velocity;u.moveSpeed=velocity.Planar;float moveLength=move.Planar;bool faceMotion=!u.isRanged&&speed>0&&moveLength>.001f;var facing=faceMotion?move:diff;float facingLength=faceMotion?moveLength:Math.Max(.001f,dist);float desiredAngle=(float)Math.Atan2(facing.x,facing.z);
            float turnRate=u.isCavalry?CombatMotion.CavalryTurnRate(u.turnRateAtRest,u.moveSpeed):u.turnRateAtRest;
            u.angle=CombatMotion.Turn(u.angle,desiredAngle,turnRate,Catalog.Step);var forward=u.angle==desiredAngle?new V3(facing.x/facingLength,0,facing.z/facingLength):CombatMotion.Forward(u.angle);
            u.moveForward=u.moveSpeed>.05f?(velocity.x*forward.x+velocity.z*forward.z)/u.moveSpeed:0;u.gait+=u.moveSpeed*Catalog.Step/Math.Max(.7f,d.scale);
            bool fast=(u.isCavalry&&u.moveSpeed>3.6f)||u.moveSpeed>8||Math.Abs(velocity.y)>6;if(u.isCavalry||u.isCatapult||fast!=u.collisionFast){u.combatBody?.Facing(u.angle,fast);u.collisionFast=fast;}
            bool grounded=Catalog.Finite(u.floor)&&p.y<u.floor+d.scale*1.12f+Math.Abs(u.slope)*.22f;
            if(grounded){
                var desired=move/Math.Max(1,moveLength)*speed;
                if(u.isCavalry){float alignment=Math.Max(0,move.x*forward.x+move.z*forward.z);desired=forward*(speed*(.22f+.78f*alignment));}
                desired=CombatMotion.SlopeVelocity(desired,u.slope);float response=u.stun>0?.008f:terrain==Terrain.Ice?.035f:u.isCavalry?.10f:.16f;
                float vertical=u.stun<=0&&Math.Abs(u.slope)>.001f&&velocity.y<1.5f?(desired.y-velocity.y)*d.mass*.18f:0;
                if(speed>0||Math.Abs(velocity.x)>.12f||Math.Abs(velocity.z)>.12f||Math.Abs(vertical)>.0001f)e.body.Impulse(new V3((desired.x-velocity.x)*d.mass*response,vertical,(desired.z-velocity.z)*d.mass*response));
            }
            if(u.isCavalry)u.charge=Math.Min(1.5f,Math.Max(0,u.charge+(u.moveSpeed>3.8f&&terrain!=Terrain.Mud?Catalog.Step:-Catalog.Step*.5f)));
            if(u.cooldown>0||!grounded||u.stun>0)return;
            if(!u.isRanged)
            {
                var attackableProps=u.isRed?attackablePropsForRed:attackablePropsForBlue;
                for(int i=0;i<attackableProps.Count;i++)
                {
                    var prop=attackableProps[i];
                    if(!(Math.Abs(p.y-prop.floor)<2.8f&&V3.DistanceXZ(p,prop.entity.position)<range+prop.definition.radius))continue;
                    DamageProp(prop,d.damage,e);u.cooldown=d.cooldown;u.attack=1;return;
                }
            }
            if(dist>=range+min||Math.Abs(p.y-tp.y)>=(u.isRanged?20:2.2f)){u.preparing=false;return;}
            if((forward.x*diff.x+forward.z*diff.z)/Math.Max(.01f,dist)<.6f)return;
            if(!u.preparing){u.preparing=true;u.windup=u.windupDuration;u.attack=.85f;return;}
            u.windup-=Catalog.Step;u.attack=.7f;if(u.windup>0)return;u.preparing=false;u.cooldown=d.cooldown;u.attack=1;
            if(u.isRanged)Shoot(u,target);
            else if(u.isGiant){foreach(var a in Nearby(p,range+min))if(a.entity.team!=e.team)Hit(a,d.damage,e,d.knock);Effect("sweep",p,.26f,u.angle);Effect("slam",p,.6f);Cue("heavy",p);}
            else{bool charge=u.isCavalry&&u.charge>.55f,brace=u.isSpear&&target.isCavalry&&target.entity.body.Velocity.Planar>3;Hit(target,d.damage*(charge?2.5f:brace?1.8f:1),e,d.knock*(charge?3.5f:brace?1.4f:1));if(u.isSpear)Effect("thrust",p,.20f,u.angle);if(charge){u.charge=0;Effect("charge",p,.45f,u.angle);Cue("charge",p);}}
        }
        void Shoot(Soldier u,Soldier target)
        {
            if(Shots.Count>=1024)return;var e=u.entity;var d=u.definition;var p=e.position;var t=target.entity.position;var delta=t-p;float dist=Math.Max(.01f,delta.Planar);
            string type=e.type=="bomber"?"bomb":e.type=="catapult"?"stone":u.isCrossbow?"bolt":"arrow";bool bomb=type=="bomb",stone=type=="stone",bolt=type=="bolt";
            float flight=Math.Max(.22f,dist/(bomb?10:stone?12:bolt?28:19)),gravity=bolt?.2f:1,spread=(Random.Next()-.5f)*(bomb?.9f:stone?1.1f:bolt?.2f:.55f);
            var spawn=CombatMotion.Launch(e.type,p,new V3(delta.x/dist,0,delta.z/dist),d.scale);var tv=target.entity.body.Velocity;var velocity=new V3((t.x+tv.x*flight*.6f-spawn.x+spread)/flight,(t.y+.15f-spawn.y+9*gravity*flight*flight)/flight,(t.z+tv.z*flight*.6f-spawn.z+spread)/flight);
            float pierce=u.isMarksman?.9f+u.armamentPierce*.01f:bolt?.6f:0;
            var shot=new Shot{id=nextId++,type=type,owner=e,life=bomb?1.8f:6,damage=d.damage,knock=d.knock,pierce=pierce};shot.body=World.Projectile(shot.id,type,spawn,velocity,gravity,e.id);Shots.Add(shot);shotsById.Add(shot.id,shot);Cue(e.type=="archer"?"bow":u.isMarksman?"crossbow":e.type,p);
        }
        public void Hit(Soldier target,float damage,Entity source,float knock,V3? origin=null,float pierce=0)
        {
            var e=target.entity;if(!e.alive||IsFrontier&&!FrontierCanHit(e,source))return;var p=e.body.Position;var from=origin??(source==null?p+new V3(-1,0,0):source.alive?source.body.Position:source.position);var diff=p-from;float dist=Math.Max(.01f,diff.Planar);bool block=e.type=="shield"&&((float)Math.Sin(target.angle)*-diff.x+(float)Math.Cos(target.angle)*-diff.z)/dist>.25f;
            if(target.armamentStability>0)knock*=(100-target.armamentStability)*.01f;
            if(target.armamentReduction>0)damage*=(100-target.armamentReduction)*.01f;
            int tier=CombatMotion.HitTier(knock,source?.type);if(block){damage*=.4f+.6f*pierce;knock*=CombatMotion.BlockImpulse(pierce);}if(target.isVanguard)damage*=.75f;if(target.fortifyUntil>Tick){damage*=.75f;knock*=.5f;}float actual=Math.Min(e.hp,Math.Max(0,damage));e.stats.damageTaken+=actual;
            if(source!=null){source.stats.damageDealt+=actual;if(source.team==e.team)source.stats.friendlyFire+=actual;target.lastSource=source;target.lastHitTick=Tick;}
            e.hp=Math.Max(0,e.hp-damage);target.stun=Math.Max(target.stun,CombatMotion.Stun(tier,block));
            if(tier>0&&target.preparing)target.windup=Math.Min(.42f,target.windup+(block?.025f:tier==2?.14f:.055f));
            if(block)target.guard=1;target.hitFlash=block?.38f:1;float tilt=(block?.025f:tier==2?.27f:.10f);target.hitTiltX=diff.x/dist*tilt;target.hitTiltZ=diff.z/dist*tilt;
            var impulse=new V3(diff.x/dist*knock,knock*(block?.06f:tier==2?.58f:.14f),diff.z/dist*knock);e.body.Impulse(impulse);if(knock/target.definition.mass>=2.5f)(e.body as IWallImpactBody)?.ArmWallImpact(60);
            Cue(block?"block":tier==2?"heavy":"melee",p);
            if(block||tier==2||e.hp<=0||Tick-target.lastVisualTick>=4){Effect(block?"block":tier==2?"heavy":"hit",p+new V3(block?-diff.x/dist*.55f:0,.23f,block?-diff.z/dist*.55f:0),block?.26f:tier==2?.32f:.18f);target.lastVisualTick=Tick;}
            if(e.hp<=0)Kill(target,impulse,"combat",source);
        }
        void Kill(Soldier u,V3 impulse,string reason,Entity source)
        {
            var e=u.entity;if(!e.alive)return;if(reason=="fall"){e.stats.falls++;if(Tick-u.lastHitTick<=180)source=u.lastSource;}
            if(IsFrontier){u.target=null;u.preparing=false;}
            if(source!=null){if(source.team==e.team)source.stats.friendlyKills++;else{source.stats.kills++;if(reason=="fall")source.stats.fallKills++;}}
            if(!firstDeath){firstDeath=true;Highlight("firstKill",source==null?"neutral":source.team);}
            var p=e.body.Position;var v=e.body.Velocity;e.position=p;e.alive=false;e.hp=0;World.Remove(e.body);World.Corpse(e.id,e.type,e.team,u.definition,p,v,impulse);
        }
        public void WallFeedback(WallImpact impact){if(impact.speed<5||!soldiersById.TryGetValue(impact.id,out var u)||!u.entity.alive||Tick-u.lastWallTick<12)return;u.lastWallTick=Tick;u.stun=Math.Max(u.stun,.10f);u.hitFlash=Math.Max(u.hitFlash,.4f);Effect("wall",impact.point,.38f,(float)Math.Atan2(impact.normal.x,impact.normal.z));Cue("wall",impact.point);}
        void ChargeContact(int attacker,int defender)
        {
            if(!soldiersById.TryGetValue(attacker,out var u)||!u.isCavalry||!u.entity.alive||u.charge<=.55f||u.moveSpeed<3.8f)return;
            if(!soldiersById.TryGetValue(defender,out var target)||!target.entity.alive||target.entity.team==u.entity.team||IsFrontier&&!FrontierCanHit(target.entity,u.entity))return;
            var delta=target.entity.position-u.entity.position;var forward=CombatMotion.Forward(u.angle);if((delta.x*forward.x+delta.z*forward.z)/Math.Max(.01f,delta.Planar)<.45f)return;
            Hit(target,u.definition.damage*2.5f,u.entity,Math.Max(u.definition.knock*2.8f,u.definition.mass*u.moveSpeed*.35f));u.charge=0;u.cooldown=u.definition.cooldown;u.preparing=false;u.attack=1;
            Effect("charge",u.entity.position,.45f,u.angle);Cue("heavy",u.entity.position);
        }
        void RemoveShot(Shot shot){if(shot.removed)return;shot.removed=true;shotsById.Remove(shot.id);World.Remove(shot.body);}
        void Explode(Shot shot){if(shot.removed)return;var p=shot.body.Position;RemoveShot(shot);Blast(p,shot.type=="stone"?2.1f:3.6f,shot.damage,shot.knock,shot.owner);}
        void Blast(V3 p,float radius,float damage,float knock,Entity source)
        {
            float before=source==null?0:source.stats.damageDealt;foreach(var u in Units)if(u.entity.alive){float distance=V3.Distance(u.entity.body.Position,p);if(distance<radius){float factor=1-distance/(radius*1.3f);Hit(u,damage*factor,source,knock*factor,p);}}
            foreach(var prop in Props)if(prop.entity.alive&&V3.Distance(new V3(prop.entity.spawn.x,prop.floor+.5f,prop.entity.spawn.z),p)<radius)DamageProp(prop,damage*.9f,source);
            World.PushCorpses(p,radius);Effect("explosion",p,.65f);Cue("explosion",p);float dealt=(source==null?0:source.stats.damageDealt)-before;if(dealt>=150&&source!=null&&(source.type=="barrel"||source.type=="catapult"))Highlight("blast",source.team,dealt);
        }
        void DamageProp(PropState prop,float damage,Entity source)
        {
            var e=prop.entity;if(!e.alive)return;float actual=Math.Min(e.hp,Math.Max(0,damage));e.stats.damageTaken+=actual;if(source!=null){source.stats.damageDealt+=actual;if(source.team==e.team)source.stats.friendlyFire+=actual;}e.hp=Math.Max(0,e.hp-damage);if(e.hp>0)return;e.alive=false;World.Remove(e.body);
            if(IsAttackable(e))
            {
                if(e.team!="red")attackablePropsForRed.Remove(prop);
                if(e.team!="blue")attackablePropsForBlue.Remove(prop);
            }
            if(e.type=="barrel")Blast(new V3(e.spawn.x,prop.floor+.45f,e.spawn.z),4.2f,95,28,e);else Effect("slam",e.position,.6f);
        }
        void TickProps()
        {
            foreach(var prop in Props)if(prop.entity.alive)
            {
                var e=prop.entity;prop.cooldown=Math.Max(0,prop.cooldown-.1f);var p=e.position;
                if(e.type=="barrel"){if(prop.fuse<0){foreach(var u in Nearby(p,1.5f))if((e.team=="neutral"||u.entity.team!=e.team)&&Math.Abs(u.entity.position.y-prop.floor)<2.3f){prop.fuse=.7f;break;}}if(prop.fuse>=0){prop.fuse-=.1f;if(prop.fuse<=0)DamageProp(prop,9999,null);}}
                else if(e.type=="banner"){foreach(var u in Nearby(p,3.2f))if((e.team=="neutral"||u.entity.team==e.team)&&Math.Abs(u.entity.position.y-prop.floor)<3)Heal(u.entity,1,e);}
                else if(e.type=="spikes"&&prop.cooldown<=0){bool fired=false;foreach(var u in Nearby(p,1.15f))if((e.team=="neutral"||u.entity.team!=e.team)&&u.entity.position.y<prop.floor+1.7f){Hit(u,14,e,.2f,new V3(e.spawn.x,prop.floor,e.spawn.z));fired=true;}if(fired){prop.cooldown=.6f;Effect("spikes",p,.4f);}}
                else if(e.type=="spring"&&prop.cooldown<=0){foreach(var u in Nearby(p,.95f))if(u.entity.position.y<=prop.floor+1.5f){float sign=(e.team=="neutral"?u.entity.team:e.team)=="red"?1:-1,mass=u.definition.mass;u.entity.body.Impulse(new V3(sign*mass*7,mass*8,0));u.stun=.7f;prop.cooldown=1.5f;Effect("spring",p,.5f);break;}}
            }
        }
        void Highlight(string type,string team,float value=0)
        {
            if(Highlights.Count>=24)return;int interval=type=="blast"?180:type=="reversal"?600:0;if(highlightTimes.TryGetValue(type,out var previous)&&Tick-previous<interval)return;highlightTimes[type]=Tick;Highlights.Add(new Highlight{id=Highlights.Count+1,tick=Tick,type=type,team=team,value=value});
        }
        void Cue(string type,V3 p)
        {
            if(cueTimes.TryGetValue(type,out var previous)&&Tick-previous<8)return;cueTimes[type]=Tick;if(Cues.Count>=24){cuePool.Push(Cues[0]);Cues.RemoveAt(0);}var cue=cuePool.Pop();cue.id=nextCue++;cue.tick=Tick;cue.type=type;cue.x=p.x;cue.y=p.y;cue.z=p.z;cue.strength=1;Cues.Add(cue);
        }
        void Effect(string type,V3 p,float life,float angle=0){if(Effects.Count>=CombatMotion.EffectCapacity){effectPool.Push(Effects[0]);Effects.RemoveAt(0);}var fx=effectPool.Pop();fx.id=nextEffect++;fx.type=type;fx.x=p.x;fx.y=p.y;fx.z=p.z;fx.life=fx.maxLife=life;fx.angle=angle;Effects.Add(fx);}
        public void Step()
        {
            if(disposed)throw new ObjectDisposedException("Battle");if(Result!=null)return;
            Cache();if(Config.commander.enabled)TickOrders();if(Tick%6==0)TickProps();
            bool scanPrepared=false;
#if !UNITY_WEBGL
            if(TargetScanWorkers>1&&redTargets.Count+blueTargets.Count>=ParallelTargetScanThreshold)
                scanPrepared=PrepareTargetScan();
#endif
            for(int i=0;i<Units.Count;i++)if(Units[i].entity.alive)Command(Units[i],i,scanPrepared);
            World.Step(Catalog.Step);if(World is IWallImpactWorld walls)foreach(var impact in walls.WallImpacts())WallFeedback(impact);
            if(IsFrontier)FrontlineBoundary();
            foreach(var contact in World.Contacts())
            {
                shotsById.TryGetValue(contact.a,out var shot);if(shot==null)shotsById.TryGetValue(contact.b,out shot);if(shot==null){ChargeContact(contact.a,contact.b);ChargeContact(contact.b,contact.a);continue;}if(shot.type=="bomb")continue;int other=contact.a==shot.id?contact.b:contact.a;if(other==shot.owner.id)continue;if(shot.type=="stone"){Explode(shot);continue;}if(entities.TryGetValue(other,out var entity)&&entity.alive){soldiersById.TryGetValue(entity.id,out var u);if(u!=null)Hit(u,shot.damage,shot.owner,shot.knock,shot.body.Position,shot.pierce);else{propsById.TryGetValue(entity.id,out var prop);if(prop!=null)DamageProp(prop,shot.damage,shot.owner);}}RemoveShot(shot);
            }
            foreach(var shot in Shots)if(!shot.removed){shot.life-=Catalog.Step;if(shot.body.Position.y< -5)RemoveShot(shot);else if(shot.life<=0){if(shot.type=="bomb"||shot.type=="stone")Explode(shot);else RemoveShot(shot);}}
            for(int i=Shots.Count-1;i>=0;i--)if(Shots[i].removed)Shots.RemoveAt(i);
            for(int i=Effects.Count-1;i>=0;i--){var effect=Effects[i];effect.life-=Catalog.Step;if(effect.life<=0){effectPool.Push(effect);Effects.RemoveAt(i);}}World.AgeCorpses(Catalog.Step);Tick++;
            int red=0,blue=0;
            for(int i=0;i<Units.Count;i++)if(Units[i].entity.alive){if(Units[i].isRed)red++;else blue++;}
            bool powerCheck=Tick%60==0,timeout=!IsFrontier&&Tick>=5400,finished=red==0||blue==0||timeout;
            if(!powerCheck&&!finished)return;
            // Power only affects reversal highlights and the result. Keep the original sum order.
            float rp=0,bp=0;
            foreach(var u in Units)if(u.entity.alive)
            {
                float power=Math.Max(0,u.entity.hp)/u.entity.maxHp*u.definition.cost;
                if(u.isRed)rp+=power;else bp+=power;
            }
            if(powerCheck&&Math.Abs(rp-bp)>=80){int next=rp>bp?1:-1;if(leader!=0&&next!=leader)Highlight("reversal",next==1?"red":"blue",Math.Abs(rp-bp));leader=next;}
            if(finished)Finish(red==0&&blue==0?"draw":red==0?"blue":blue==0?"red":Math.Abs(rp-bp)<1?"draw":rp>bp?"red":"blue",timeout?"timeout":"elimination",red,blue,rp,bp);
        }
        void Finish(string winner,string reason,int red,int blue,float rp,float bp)
        {
            Result=new Result{winner=winner,reason=reason,time=Time,red=red,blue=blue,remainingValue=new Dictionary<string,float>{{"red",rp},{"blue",bp}},report=Report(),orders=Json.Clone(Commander.log),highlights=Json.Clone(Highlights)};
        }
        public BattleReport Report()
        {
            var report=new BattleReport();foreach(var u in Units){var e=u.entity;var r=new UnitReport{id=e.id,type=e.type,team=e.team,alive=e.alive,hp=e.hp,spawn=e.spawn};r.Add(e.stats);r.score=r.Score;report.units.Add(r);}foreach(var p in Props){var e=p.entity;var r=new UnitReport{id=e.id,type=e.type,team=e.team,alive=e.alive,hp=e.hp};r.Add(e.stats);report.props.Add(r);}
            foreach(var team in new[]{"red","blue"}){var sum=new TeamReport();foreach(var u in report.units.Where(u=>u.team==team)){sum.Add(u);sum.initial++;if(u.alive)sum.survived++;else sum.lost++;if(!sum.byType.TryGetValue(u.type,out var row)){row=new TypeReport();sum.byType.Add(u.type,row);}row.Add(u);row.initial++;if(u.alive)row.survived++;else row.lost++;var definition=soldiersById[u.id].definition;sum.remainingValue+=Math.Max(0,u.hp)/definition.hp*definition.cost;}foreach(var p in report.props.Where(p=>p.team==team))sum.Add(p);report.teams.Add(team,sum);}report.mvp=report.units.OrderByDescending(u=>u.score).ThenBy(u=>u.id).FirstOrDefault();return report;
        }
        public int SnapshotShotCapacity {get {int total=0;foreach(var u in Units)if(u.isRanged)total+=(int)Math.Ceiling(6f*1.15f/u.definition.cooldown)+2;return Math.Min(1024,total);}}
        public Frame PrepareSnapshot()
        {
            var f=new Frame();f.storage=new SnapshotStorage();f.storage.Prepare(f,Units.Count,SnapshotShotCapacity,Props.Count,CombatMotion.EffectCapacity);for(int i=0;i<Units.Count;i++){var u=Units[i];var row=f.units[i];row.id=u.entity.id;row.type=u.entity.type;row.team=u.entity.team;}return f;
        }
        public Frame Snapshot(Frame reuse=null)
        {
            var f=reuse??new Frame();if(f.storage==null){f.storage=new SnapshotStorage(f);f.units.Capacity=Units.Count;}var storage=f.storage;f.tick=Tick;f.time=Time;f.result=Result;
            f.commander.points["red"]=Commander.points["red"];f.commander.points["blue"]=Commander.points["blue"];f.commander.readyAt["red"]=Commander.readyAt["red"];f.commander.readyAt["blue"]=Commander.readyAt["blue"];
            SnapshotStorage.Size(f.commander.log,storage.log,Commander.log.Count);for(int i=0;i<Commander.log.Count;i++)CopyOrder(Commander.log[i],f.commander.log[i]);
            SnapshotStorage.Size(f.commander.active,storage.active,Commander.active.Count);for(int i=0;i<Commander.active.Count;i++)CopyOrder(Commander.active[i],f.commander.active[i]);
            SnapshotStorage.Size(f.cues,storage.cues,Cues.Count);for(int i=0;i<Cues.Count;i++){var a=Cues[i];var b=f.cues[i];b.id=a.id;b.tick=a.tick;b.type=a.type;b.x=a.x;b.y=a.y;b.z=a.z;b.strength=a.strength;}
            SnapshotStorage.Size(f.effects,storage.effects,Effects.Count);for(int i=0;i<Effects.Count;i++){var a=Effects[i];var b=f.effects[i];b.id=a.id;b.type=a.type;b.x=a.x;b.y=a.y;b.z=a.z;b.life=a.life;b.maxLife=a.maxLife;b.angle=a.angle;}
            // Restore the retained slots after a previous frame had fewer bodies.
            SnapshotStorage.Size(f.ragdolls,storage.corpses,World is ICorpseCount count?count.CorpseCount:24);World.Corpses(f.ragdolls);
            while(f.units.Count<Units.Count)f.units.Add(new UnitFrame());for(int i=0;i<Units.Count;i++){var u=Units[i];var e=u.entity;var v=f.units[i];if(v.id!=e.id){v.id=e.id;v.type=e.type;v.team=e.team;}v.stance=u.stance;v.hp=e.hp;v.alive=e.alive;v.angle=u.angle;v.attack=u.attack;v.charge=u.charge;v.hitFlash=u.hitFlash;v.hitTiltX=u.hitTiltX;v.hitTiltZ=u.hitTiltZ;v.moveSpeed=u.moveSpeed;v.moveForward=u.moveForward;v.gait=u.gait;v.guard=u.guard;v.inspired=u.inspireUntil>Tick;v.fortified=u.fortifyUntil>Tick;v.t=e.alive?e.body.Transform(v.t):null;}
            SnapshotStorage.Size(f.projectiles,storage.projectiles,Shots.Count);for(int i=0;i<Shots.Count;i++){var s=Shots[i];var p=f.projectiles[i];p.id=s.id;p.type=s.type;p.t=s.body.Transform(p.t);p.v=s.body.Velocity;}
            SnapshotStorage.Size(f.props,storage.props,Props.Count);for(int i=0;i<Props.Count;i++){var p=Props[i];var v=f.props[i];v.id=p.entity.id;v.type=p.entity.type;v.team=p.entity.team;v.x=p.entity.spawn.x;v.z=p.entity.spawn.z;v.floor=p.floor;v.hp=p.entity.hp;v.alive=p.entity.alive;v.active=p.fuse>=0||p.cooldown>0;}return f;
        }
        static void CopyOrder(OrderEntry a,OrderEntry b){b.id=a.id;b.tick=a.tick;b.endTick=a.endTick;b.team=a.team;b.type=a.type;b.x=a.x;b.z=a.z;}
        static OrderEntry CopyOrder(OrderEntry o){return new OrderEntry{id=o.id,tick=o.tick,endTick=o.endTick,team=o.team,type=o.type,x=o.x,z=o.z};}
        public void Dispose()
        {
            if(disposed)return;
            disposed=true;
#if !UNITY_WEBGL
            targetScan?.Dispose();
#endif
            World.Dispose();
        }
    }
}
