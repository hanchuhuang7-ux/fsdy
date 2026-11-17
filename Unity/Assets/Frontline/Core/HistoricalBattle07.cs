using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontline.Core.History07
{
    public sealed class Entity
    {
        public int id;public string type,team;public bool alive=true;public float hp,maxHp;public V3 position,spawn;
        public readonly Contribution stats=new Contribution();public IBody body;
    }
    public sealed class Soldier
    {
        public Entity entity;public UnitDef definition;public string stance;public float angle,cooldown,attack,stun,charge,floor;public V3 anchor,progress;
        public int inspireUntil,fortifyUntil,lastHitTick=-99999,escapeUntil,blocked,escapes,routeStage;public float routeLane;public bool hasRoute;
        public Entity lastSource;public Soldier target;public OrderEntry focus;
    }
    public sealed class PropState {public Entity entity;public float floor,fuse=-1,cooldown;}
    public sealed class Shot {public int id;public string type;public Entity owner;public IBody body;public float life,damage,knock,pierce;public bool removed;}
    public sealed class Seeded
    {
        public uint state;public Seeded(uint seed){state=seed;}
        public float Next(){state=unchecked(state*1664525u+1013904223u);return (state>>8)*(1f/16777216f);}
    }
    public sealed class Battle:IRecordedBattle
    {
        public readonly Catalog Data;public readonly Deployment Config;public readonly IPhysicsWorld World;public readonly Seeded Random;
        public readonly List<Soldier> Units=new List<Soldier>();public readonly List<PropState> Props=new List<PropState>();public readonly List<Shot> Shots=new List<Shot>();
        public readonly CommanderState Commander=new CommanderState();public readonly List<Highlight> Highlights=new List<Highlight>();public readonly List<Cue> Cues=new List<Cue>();public readonly List<EffectFrame> Effects=new List<EffectFrame>();
        public int Tick {get;private set;}public float Time {get{return Tick*Catalog.Step;}}public Result Result {get;private set;}
        readonly List<Soldier>[] grid=new List<Soldier>[128*128];readonly List<List<Soldier>> gridBuckets=new List<List<Soldier>>();readonly Dictionary<int,List<Soldier>> overflowGrid=new Dictionary<int,List<Soldier>>();readonly Dictionary<int,Entity> entities=new Dictionary<int,Entity>();readonly Dictionary<int,Soldier> soldiersById=new Dictionary<int,Soldier>();readonly Dictionary<int,PropState> propsById=new Dictionary<int,PropState>();readonly Dictionary<int,Shot> shotsById=new Dictionary<int,Shot>();
        readonly Dictionary<string,int> cueTimes=new Dictionary<string,int>(),highlightTimes=new Dictionary<string,int>();
        readonly bool legacyMath;readonly GroundSample[] groundSamples;
        struct GroundSample {public float x,z,hz,extent,top,slope;}
        int nextId=1,nextCue=1,nextEffect=1,leader,replayIndex;bool firstDeath,disposed;List<OrderEntry> replayOrders;
        public Battle(Catalog catalog,Deployment input,IWorldFactory factory)
        {
            Data=catalog;Config=catalog.Validate(input);Random=new Seeded(Config.seed);World=factory.Create(catalog,Config);legacyMath=World is ILegacyCompatibilityWorld;var platforms=Data.Maps[Config.map].platforms;groundSamples=new GroundSample[platforms.Count];for(int i=0;i<platforms.Count;i++){var p=platforms[i];float cos=(float)Math.Cos(p.angle);groundSamples[i]=new GroundSample{x=p.x,z=p.z,hz=p.hz,extent=p.hx*cos+p.hy*Math.Abs((float)Math.Sin(p.angle)),top=p.y+p.hy/cos,slope=(float)Math.Tan(p.angle)};}
            try{
                foreach(var team in new[]{"red","blue"})foreach(var p in Config.Army(team))
                {
                    var d=catalog.Units[p.type];var e=new Entity{id=nextId++,type=p.type,team=team,hp=d.hp,maxHp=d.hp,spawn=new V3(p.x,0,p.z)};e.body=World.Unit(e.id,p,team,d);e.position=e.body.Position;entities.Add(e.id,e);
                    Units.Add(new Soldier{entity=e,definition=d,stance=p.stance,cooldown=Random.Next()*.6f,angle=team=="red"?(float)Math.PI/2:-(float)Math.PI/2,anchor=e.spawn,progress=e.position});
                }
                foreach(var placement in catalog.Maps[Config.map].props.Concat(Config.props))
                {
                    var p=Json.Clone(placement);if(!Catalog.Team(p.team))p.team="neutral";var d=catalog.Props[p.type];var e=new Entity{id=nextId++,type=p.type,team=p.team,hp=d.hp,maxHp=d.hp,spawn=new V3(p.x,0,p.z)};e.body=World.Prop(e.id,p,d);e.position=e.body.Position;entities.Add(e.id,e);Props.Add(new PropState{entity=e,floor=catalog.Height(Config.map,p.x,p.z)});
                }
                foreach(var u in Units)soldiersById.Add(u.entity.id,u);foreach(var p in Props)propsById.Add(p.entity.id,p);
                Cache();
            }catch{World.Dispose();throw;}
        }
        static readonly Passage MarshPassage=new Passage{approach=5.3f,lanes=new[]{-9.6f,9.6f}};
        static readonly float[] EscapeAngles={1.047f,1.571f,-1.047f,-1.571f,3.14159f};
        static int Key(float x,float z){return ((int)Math.Floor(x/3)+64)*128+(int)Math.Floor(z/3)+64;}
        List<Soldier> GridBucket(int key){if((uint)key<(uint)grid.Length)return grid[key];overflowGrid.TryGetValue(key,out var value);return value;}
        float GroundAt(float x,float z)
        {
            if(legacyMath)return Data.Height(Config.map,x,z);if(!Catalog.Finite(x)||!Catalog.Finite(z))return float.NegativeInfinity;float height=float.NegativeInfinity;
            for(int i=0;i<groundSamples.Length;i++){var p=groundSamples[i];if(Math.Abs(x-p.x)<=p.extent-.01f&&Math.Abs(z-p.z)<=p.hz-.01f)height=Math.Max(height,p.top+p.slope*(x-p.x));}return height;
        }
        bool Within(V3 a,V3 b,float radius){if(legacyMath)return V3.DistanceXZ(a,b)<=radius;float x=a.x-b.x,z=a.z-b.z;return x*x+z*z<=radius*radius;}
        void Cache()
        {
            foreach(var bucket in gridBuckets)bucket.Clear();foreach(var u in Units)if(u.entity.alive){u.entity.position=u.entity.body.Position;u.floor=GroundAt(u.entity.position.x,u.entity.position.z);int k=Key(u.entity.position.x,u.entity.position.z);var bucket=GridBucket(k);if(bucket==null){bucket=new List<Soldier>(8);if((uint)k<(uint)grid.Length)grid[k]=bucket;else overflowGrid.Add(k,bucket);gridBuckets.Add(bucket);}bucket.Add(u);}
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
                readonly Battle battle;readonly V3 p;readonly float radius;readonly int maxX,minZ,maxZ;int x,z,index;List<Soldier> bucket;
                public Soldier Current {get;private set;}object System.Collections.IEnumerator.Current {get{return Current;}}
                public Enumerator(Battle battle,V3 p,float radius){this.battle=battle;this.p=p;this.radius=radius;x=(int)Math.Floor((p.x-radius)/3);maxX=(int)Math.Floor((p.x+radius)/3);minZ=(int)Math.Floor((p.z-radius)/3);maxZ=(int)Math.Floor((p.z+radius)/3);z=minZ;index=0;bucket=null;Current=null;}
                public bool MoveNext(){while(true){if(bucket!=null){while(index<bucket.Count){var u=bucket[index++];if(u.entity.alive&&battle.Within(p,u.entity.position,radius)){Current=u;return true;}}bucket=null;}if(x>maxX)return false;bucket=battle.GridBucket((x+64)*128+z+64);index=0;if(++z>maxZ){z=minZ;x++;}}}
                public void Dispose(){}public void Reset(){throw new NotSupportedException();}
            }
        }
        Soldier Target(Soldier own)
        {
            Soldier best=null;float score=float.PositiveInfinity;foreach(var foe in Units)if(foe.entity.alive&&foe.entity.team!=own.entity.team)
            {
                float s;if(legacyMath){s=V3.DistanceXZ(own.entity.position,foe.entity.position);s*=s;}else{float dx=own.entity.position.x-foe.entity.position.x,dz=own.entity.position.z-foe.entity.position.z;s=dx*dx+dz*dz;}var def=foe.definition;
                if(own.focus!=null&&own.focus.endTick>Tick&&Catalog.Distance(foe.entity.position.x,foe.entity.position.z,own.focus.x,own.focus.z)<4)s*=.08f;
                if(own.stance=="flank"&&def.ranged)s*=.42f;if(own.stance=="hunt")s*=def.ranged?.12f*(160/def.cost):foe.entity.type=="medic"?.2f:1.5f;
                if(s<score){best=foe;score=s;}
            }return best;
        }
        public float Power(string team){float value=0;foreach(var u in Units)if(u.entity.alive&&u.entity.team==team)value+=Math.Max(0,u.entity.hp)/u.entity.maxHp*Data.Units[u.entity.type].cost;return value;}
        public bool IssueOrder(string team,Order order,out string reason)
        {
            reason=null;if(!Config.commander.enabled)reason="军令模式未开启";else if(Result!=null)reason="本局已经结束";else if(!Catalog.Team(team)||order==null||!Catalog.Orders.Contains(order.type))reason="军令或阵营无效";
            else if(!Catalog.Finite(order.x)||!Catalog.Finite(order.z)||Math.Abs(order.x)>18||Math.Abs(order.z)>12||!Catalog.Finite(Data.Height(Config.map,order.x,order.z)))reason="请选择战场地面";
            else if(Commander.points[team]<1)reason="军令点数不足";else if(Tick<Commander.readyAt[team])reason="军令正在冷却";
            if(reason!=null)return false;Cache();var entry=new OrderEntry{id=Commander.log.Count+1,team=team,type=order.type,x=order.x,z=order.z,tick=Tick,endTick=Tick+(order.type=="focus"?360:300)};
            Commander.points[team]--;Commander.readyAt[team]=Tick+480;Commander.log.Add(entry);Commander.active.Add(entry);
            foreach(var u in Nearby(new V3(order.x,0,order.z),order.type=="focus"?8:6))if(u.entity.team==team){if(order.type=="inspire")u.inspireUntil=entry.endTick;else if(order.type=="fortify"&&new[]{"shield","spear","giant","militia"}.Contains(u.entity.type))u.fortifyUntil=entry.endTick;else if(order.type=="focus")u.focus=entry;}
            Cue("order",new V3(order.x,Data.Height(Config.map,order.x,order.z),order.z));Highlight("order",team);return true;
        }
        public void UseOrderLog(List<OrderEntry> log)
        {
            if(Tick!=0||Commander.log.Count>0)throw new InvalidOperationException("重演必须从初始状态开始");Replay.ValidateOrders(Data,Config,log);replayOrders=Json.Clone(log);replayIndex=0;
        }
        void TickOrders()
        {
            Commander.active.RemoveAll(e=>e.endTick<=Tick);
            if(replayOrders!=null){while(replayIndex<replayOrders.Count&&replayOrders[replayIndex].tick==Tick){var e=replayOrders[replayIndex++];if(!IssueOrder(e.team,e,out var reason))throw new InvalidOperationException("军令日志无法重演："+reason);}return;}
            if(Tick%60!=0)return;
            foreach(var team in Config.commander.aiTeams)
            {
                if(Commander.points[team]<=0||Tick<Commander.readyAt[team])continue;Soldier front=null;foreach(var u in Units)if(u.entity.alive&&u.entity.team==team&&(front==null||(team=="red"?u.entity.position.x>front.entity.position.x:u.entity.position.x<front.entity.position.x)))front=u;if(front==null)continue;
                var enemies=Nearby(front.entity.position,8).Where(u=>u.entity.team!=team).ToList();string type=front.entity.hp/front.entity.maxHp<.65f?"fortify":enemies.Count>=3?"focus":"inspire";var p=front.entity.position;if(type=="focus"){p=new V3();foreach(var e in enemies)p+=e.entity.position/enemies.Count;}IssueOrder(team,new Order(type,p.x,p.z),out _);
            }
        }
        void Heal(Entity target,float amount,Entity source)
        {
            if(!target.alive)return;float actual=Math.Max(0,Math.Min(target.maxHp-target.hp,amount));target.hp+=actual;if(source!=null)source.stats.healing+=actual;if(actual>0&&(Tick+target.id)%12==0){Cue("heal",target.position);Effect("heal",target.position,.4f);}
        }
        void Command(Soldier u)
        {
            var e=u.entity;var p=e.position;var d=u.definition;if(p.y< -4||Math.Abs(p.x)>23||Math.Abs(p.z)>19){Kill(u,new V3(),"fall",null);return;}
            bool inspired=u.inspireUntil>Tick;u.cooldown-=Catalog.Step*(inspired?1.15f:1);u.attack=Math.Max(0,u.attack-Catalog.Step*3);u.stun=Math.Max(0,u.stun-Catalog.Step);
            bool ai=(Tick+e.id)%6==0;if(ai||u.target==null||!u.target.entity.alive)u.target=Target(u);
            if(e.type=="medic"&&ai){Soldier ally=null;float ratio=1;foreach(var a in Nearby(p,6))if(a.entity.team==e.team&&a.entity.hp/a.entity.maxHp<ratio){ally=a;ratio=a.entity.hp/a.entity.maxHp;}if(ally!=null)Heal(ally.entity,d.heal*.1f,e);}
            var target=u.target;if(target==null)return;var tp=target.entity.position;var diff=tp-p;float dist=diff.Planar,range=d.range*(d.ranged&&u.floor>target.floor+1?1.18f:1),min=Data.Units[target.entity.type].scale*.25f;
            u.angle=(float)Math.Atan2(diff.x,diff.z);V3 move=new V3(diff.x/(dist>.001f?dist:1),0,diff.z/(dist>.001f?dist:1));float speed=d.speed;bool firing=d.ranged&&dist<range;
            if(e.type=="crossbow"&&p.x*tp.x<0&&Config.map=="ruins"){float crossing=p.z+(tp.z-p.z)*(-p.x/(tp.x-p.x));if(Math.Abs(crossing)>2.1f&&Math.Abs(crossing)<9.1f)firing=false;}
            if((d.ranged||e.type=="medic")&&dist<4&&u.stance!="hold"){move*=-1;speed*=.7f;}else if(firing||(!d.ranged&&dist<range+min-.15f)||u.stance=="hold")speed=0;
            if(e.type=="medic"&&dist>5&&dist<9)speed=0;
            if(u.stance=="hold"&&!firing&&dist>range+min&&V3.DistanceXZ(p,u.anchor)>1.4f){move=(u.anchor-p);move.y=0;move=move.Normalized;speed=d.speed*.7f;}
            var passage=Data.Maps[Config.map].passage;if(passage==null&&u.stance=="flank"&&Config.map=="marsh")passage=MarshPassage;
            if(passage!=null&&u.stance!="hold"&&!firing&&dist>range+min-.15f)
            {
                if(!u.hasRoute&&p.x*tp.x<0){u.hasRoute=true;u.routeLane=u.stance=="flank"&&passage.lanes.Length>1?(e.spawn.z>=0?passage.lanes.Max():passage.lanes.Min()):passage.lanes[0];u.routeStage=Math.Abs(p.z-u.routeLane)>1?0:1;}
                if(u.hasRoute){float sign=tp.x>=0?1:-1;var entry=new V3(-sign*passage.approach,0,u.routeLane);if(u.routeStage==0&&V3.DistanceXZ(p,entry)<.85f)u.routeStage=1;if(u.routeStage==1&&sign*p.x>passage.approach+.5f)u.hasRoute=false;else{move=new V3(u.routeStage==0?entry.x:sign*(passage.approach+.8f),0,u.routeLane)-p;move.y=0;move=move.Normalized;speed=d.speed;}}
            }else if(u.stance=="flank"&&!d.ranged&&dist>6&&passage==null){move.z=(tp.z+Math.Sign(e.spawn.z==0?1:e.spawn.z)*5-p.z)/(dist+3);move=move.Normalized;}
            string terrain=Data.Terrain(Config.map,p.x,p.z);if(inspired)speed*=1.12f;if(terrain=="mud")speed*=.45f;
            if(speed>0&&u.stance!="hold")
            {
                if((Tick+e.id)%30==0){if(V3.DistanceXZ(p,u.progress)<.18f&&u.stun<=0)u.blocked++;else u.blocked=0;u.progress=p;if(u.blocked>=3){u.blocked=0;u.escapes++;u.escapeUntil=Tick+90;}}
                if(u.escapeUntil>Tick)
                {
                    float side=(e.id%2==0?-1:1)*(u.escapes%2==0?-1:1);foreach(float baseAngle in EscapeAngles){float angle=baseAngle==3.14159f?baseAngle:baseAngle*side;var direction=new V3(move.x*(float)Math.Cos(angle)-move.z*(float)Math.Sin(angle),0,move.x*(float)Math.Sin(angle)+move.z*(float)Math.Cos(angle));var q=p+direction*1.2f;if(Catalog.Finite(Data.Height(Config.map,q.x,q.z))&&!Data.Maps[Config.map].walls.Any(w=>Math.Abs(q.x-w.x)<w.hx+.5f&&Math.Abs(q.z-w.z)<w.hz+.5f)){move=direction;break;}}
                }
                foreach(var a in Nearby(p,3))if(a!=u&&a.entity.team==e.team){float dd=V3.DistanceXZ(p,a.entity.position),radius=.65f*(d.scale+Data.Units[a.entity.type].scale);if(dd<radius&&dd>.01f)move+=(p-a.entity.position)*((radius-dd)*.7f/dd);}
            }
            var velocity=e.body.Velocity;bool grounded=Catalog.Finite(u.floor)&&p.y<u.floor+d.scale*1.12f;
            if(grounded){var desired=move/Math.Max(1,move.Planar)*speed;float response=u.stun>0?.015f:terrain=="ice"?.035f:.16f;e.body.Impulse(new V3((desired.x-velocity.x)*d.mass*response,0,(desired.z-velocity.z)*d.mass*response));}
            if(e.type=="cavalry")u.charge=Math.Min(1.5f,Math.Max(0,u.charge+(velocity.Planar>3.8f&&terrain!="mud"?Catalog.Step:-Catalog.Step*.5f)));
            if(u.cooldown>0||!grounded)return;
            if(!d.ranged)
            {
                foreach(var prop in Props)if(prop.entity.alive&&((prop.entity.type=="barricade"&&prop.entity.team!=e.team)||(prop.entity.type=="banner"&&prop.entity.team!="neutral"&&prop.entity.team!=e.team))&&Math.Abs(p.y-prop.floor)<2.8f&&V3.DistanceXZ(p,prop.entity.position)<range+Data.Props[prop.entity.type].radius){DamageProp(prop,d.damage,e);u.cooldown=d.cooldown;u.attack=1;return;}
            }
            if(dist>=range+min||Math.Abs(p.y-tp.y)>=(d.ranged?20:2.2f))return;u.cooldown=d.cooldown;u.attack=1;
            if(d.ranged)Shoot(u,target);
            else if(e.type=="giant"){foreach(var a in Nearby(p,range+min))if(a.entity.team!=e.team)Hit(a,d.damage,e,d.knock);Effect("slam",p,.6f);Cue("slam",p);}
            else{bool charge=e.type=="cavalry"&&u.charge>.55f,brace=e.type=="spear"&&target.entity.type=="cavalry"&&target.entity.body.Velocity.Planar>3;Hit(target,d.damage*(charge?2.5f:brace?1.8f:1),e,d.knock*(charge?3.5f:brace?1.4f:1));if(charge){u.charge=0;Effect("charge",p,.45f);Cue("charge",p);}}
        }
        void Shoot(Soldier u,Soldier target)
        {
            if(Shots.Count>=1024)return;var e=u.entity;var d=Data.Units[e.type];var p=e.body.Position;var t=target.entity.body.Position;var delta=t-p;float dist=Math.Max(.01f,delta.Planar);
            string type=e.type=="bomber"?"bomb":e.type=="catapult"?"stone":e.type=="crossbow"?"bolt":"arrow";bool bomb=type=="bomb",stone=type=="stone",bolt=type=="bolt";
            float flight=Math.Max(.22f,dist/(bomb?10:stone?12:bolt?28:19)),gravity=bolt?.2f:1,spread=(Random.Next()-.5f)*(bomb?.9f:stone?1.1f:bolt?.2f:.55f);
            var spawn=p+new V3(delta.x/dist*(stone?.9f:.7f),.5f*d.scale,delta.z/dist*(stone?.9f:.7f));var tv=target.entity.body.Velocity;var velocity=new V3((t.x+tv.x*flight*.6f-spawn.x+spread)/flight,(t.y+.15f-spawn.y+9*gravity*flight*flight)/flight,(t.z+tv.z*flight*.6f-spawn.z+spread)/flight);
            var shot=new Shot{id=nextId++,type=type,owner=e,life=bomb?1.8f:6,damage=d.damage,knock=d.knock,pierce=bolt?.6f:0};shot.body=World.Projectile(shot.id,type,spawn,velocity,gravity,e.id);Shots.Add(shot);shotsById.Add(shot.id,shot);Cue(e.type=="archer"?"bow":e.type,p);
        }
        public void Hit(Soldier target,float damage,Entity source,float knock,V3? origin=null,float pierce=0)
        {
            var e=target.entity;if(!e.alive)return;var p=e.body.Position;var from=origin??(source==null?p+new V3(-1,0,0):source.alive?source.body.Position:source.position);var diff=p-from;float dist=Math.Max(.01f,diff.Planar);bool block=e.type=="shield"&&((float)Math.Sin(target.angle)*-diff.x+(float)Math.Cos(target.angle)*-diff.z)/dist>.25f;
            if(block)damage*=.4f+.6f*pierce;if(target.fortifyUntil>Tick){damage*=.75f;knock*=.5f;}float actual=Math.Min(e.hp,Math.Max(0,damage));e.stats.damageTaken+=actual;
            if(source!=null){source.stats.damageDealt+=actual;if(source.team==e.team)source.stats.friendlyFire+=actual;target.lastSource=source;target.lastHitTick=Tick;}
            e.hp=Math.Max(0,e.hp-damage);target.stun=.25f;var impulse=new V3(diff.x/dist*knock,knock*.32f,diff.z/dist*knock);e.body.Impulse(impulse);Cue(block?"block":"melee",p);if((Tick+e.id)%6==0)Effect(block?"block":"hit",p,.18f);if(e.hp<=0)Kill(target,impulse,"combat",source);
        }
        void Kill(Soldier u,V3 impulse,string reason,Entity source)
        {
            var e=u.entity;if(!e.alive)return;if(reason=="fall"){e.stats.falls++;if(Tick-u.lastHitTick<=180)source=u.lastSource;}
            if(source!=null){if(source.team==e.team)source.stats.friendlyKills++;else{source.stats.kills++;if(reason=="fall")source.stats.fallKills++;}}
            if(!firstDeath){firstDeath=true;Highlight("firstKill",source==null?"neutral":source.team);}
            var p=e.body.Position;var v=e.body.Velocity;e.position=p;e.alive=false;e.hp=0;World.Remove(e.body);World.Corpse(e.id,e.type,e.team,Data.Units[e.type],p,v,impulse);
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
            var e=prop.entity;if(!e.alive)return;float actual=Math.Min(e.hp,Math.Max(0,damage));e.stats.damageTaken+=actual;if(source!=null){source.stats.damageDealt+=actual;if(source.team==e.team)source.stats.friendlyFire+=actual;}e.hp=Math.Max(0,e.hp-damage);if(e.hp>0)return;e.alive=false;World.Remove(e.body);if(e.type=="barrel")Blast(new V3(e.spawn.x,prop.floor+.45f,e.spawn.z),4.2f,95,28,e);else Effect("slam",e.position,.6f);
        }
        void TickProps()
        {
            foreach(var prop in Props)if(prop.entity.alive)
            {
                var e=prop.entity;prop.cooldown=Math.Max(0,prop.cooldown-.1f);var p=e.position;
                if(e.type=="barrel"){if(prop.fuse<0&&Nearby(p,1.5f).Any(u=>(e.team=="neutral"||u.entity.team!=e.team)&&Math.Abs(u.entity.position.y-prop.floor)<2.3f))prop.fuse=.7f;if(prop.fuse>=0){prop.fuse-=.1f;if(prop.fuse<=0)DamageProp(prop,9999,null);}}
                else if(e.type=="banner"){foreach(var u in Nearby(p,3.2f))if((e.team=="neutral"||u.entity.team==e.team)&&Math.Abs(u.entity.position.y-prop.floor)<3)Heal(u.entity,1,e);}
                else if(e.type=="spikes"&&prop.cooldown<=0){bool fired=false;foreach(var u in Nearby(p,1.15f))if((e.team=="neutral"||u.entity.team!=e.team)&&u.entity.position.y<prop.floor+1.7f){Hit(u,14,e,.2f,new V3(e.spawn.x,prop.floor,e.spawn.z));fired=true;}if(fired){prop.cooldown=.6f;Effect("spikes",p,.4f);}}
                else if(e.type=="spring"&&prop.cooldown<=0){foreach(var u in Nearby(p,.95f))if(u.entity.position.y<=prop.floor+1.5f){float sign=(e.team=="neutral"?u.entity.team:e.team)=="red"?1:-1,mass=Data.Units[u.entity.type].mass;u.entity.body.Impulse(new V3(sign*mass*7,mass*8,0));u.stun=.7f;prop.cooldown=1.5f;Effect("spring",p,.5f);break;}}
            }
        }
        void Highlight(string type,string team,float value=0)
        {
            if(Highlights.Count>=24)return;int interval=type=="blast"?180:type=="reversal"?600:0;if(highlightTimes.TryGetValue(type,out var previous)&&Tick-previous<interval)return;highlightTimes[type]=Tick;Highlights.Add(new Highlight{id=Highlights.Count+1,tick=Tick,type=type,team=team,value=value});
        }
        void Cue(string type,V3 p)
        {
            if(cueTimes.TryGetValue(type,out var previous)&&Tick-previous<8)return;cueTimes[type]=Tick;if(Cues.Count>=24)Cues.RemoveAt(0);Cues.Add(new Cue{id=nextCue++,tick=Tick,type=type,x=p.x,y=p.y,z=p.z});
        }
        void Effect(string type,V3 p,float life){if(Effects.Count>=41)Effects.RemoveAt(0);Effects.Add(new EffectFrame{id=nextEffect++,type=type,x=p.x,y=p.y,z=p.z,life=life,maxLife=life});}
        public void Step()
        {
            if(disposed)throw new ObjectDisposedException("Battle");if(Result!=null)return;Cache();if(Config.commander.enabled)TickOrders();if(Tick%6==0)TickProps();foreach(var u in Units)if(u.entity.alive)Command(u);World.Step(Catalog.Step);
            foreach(var contact in World.Contacts())
            {
                shotsById.TryGetValue(contact.a,out var shot);if(shot==null)shotsById.TryGetValue(contact.b,out shot);if(shot==null||shot.type=="bomb")continue;int other=contact.a==shot.id?contact.b:contact.a;if(other==shot.owner.id)continue;if(shot.type=="stone"){Explode(shot);continue;}if(entities.TryGetValue(other,out var entity)&&entity.alive){soldiersById.TryGetValue(entity.id,out var u);if(u!=null)Hit(u,shot.damage,shot.owner,shot.knock,shot.body.Position,shot.pierce);else{propsById.TryGetValue(entity.id,out var prop);if(prop!=null)DamageProp(prop,shot.damage,shot.owner);}}RemoveShot(shot);
            }
            foreach(var shot in Shots)if(!shot.removed){shot.life-=Catalog.Step;if(shot.body.Position.y< -5)RemoveShot(shot);else if(shot.life<=0){if(shot.type=="bomb"||shot.type=="stone")Explode(shot);else RemoveShot(shot);}}Shots.RemoveAll(s=>s.removed);
            foreach(var effect in Effects)effect.life-=Catalog.Step;Effects.RemoveAll(e=>e.life<=0);World.AgeCorpses(Catalog.Step);Tick++;
            int red=0,blue=0;float rp=0,bp=0;foreach(var u in Units)if(u.entity.alive){float power=Math.Max(0,u.entity.hp)/u.entity.maxHp*u.definition.cost;if(u.entity.team=="red"){red++;rp+=power;}else{blue++;bp+=power;}}
            if(Tick%60==0&&Math.Abs(rp-bp)>=80){int next=rp>bp?1:-1;if(leader!=0&&next!=leader)Highlight("reversal",next==1?"red":"blue",Math.Abs(rp-bp));leader=next;}
            if(red==0||blue==0||Tick>=5400)Result=new Result{winner=red==0&&blue==0?"draw":red==0?"blue":blue==0?"red":Math.Abs(rp-bp)<1?"draw":rp>bp?"red":"blue",reason=Tick>=5400?"timeout":"elimination",time=Time,red=red,blue=blue,remainingValue=new Dictionary<string,float>{{"red",rp},{"blue",bp}},report=Report(),orders=Json.Clone(Commander.log),highlights=Json.Clone(Highlights)};
        }
        public BattleReport Report()
        {
            var report=new BattleReport();foreach(var u in Units){var e=u.entity;var r=new UnitReport{id=e.id,type=e.type,team=e.team,alive=e.alive,hp=e.hp,spawn=e.spawn};r.Add(e.stats);r.score=r.Score;report.units.Add(r);}foreach(var p in Props){var e=p.entity;var r=new UnitReport{id=e.id,type=e.type,team=e.team,alive=e.alive,hp=e.hp};r.Add(e.stats);report.props.Add(r);}
            foreach(var team in new[]{"red","blue"}){var sum=new TeamReport();foreach(var u in report.units.Where(u=>u.team==team)){sum.Add(u);sum.initial++;if(u.alive)sum.survived++;else sum.lost++;if(!sum.byType.TryGetValue(u.type,out var row)){row=new TypeReport();sum.byType.Add(u.type,row);}row.Add(u);row.initial++;if(u.alive)row.survived++;else row.lost++;sum.remainingValue+=Math.Max(0,u.hp)/Data.Units[u.type].hp*Data.Units[u.type].cost;}foreach(var p in report.props.Where(p=>p.team==team))sum.Add(p);report.teams.Add(team,sum);}report.mvp=report.units.OrderByDescending(u=>u.score).ThenBy(u=>u.id).FirstOrDefault();return report;
        }
        public int SnapshotShotCapacity {get {int total=0;foreach(var u in Units)if(u.definition.ranged)total+=(int)Math.Ceiling(6f*1.15f/u.definition.cooldown)+2;return Math.Min(1024,total);}}
        public Frame PrepareSnapshot()
        {
            var f=new Frame();f.storage=new SnapshotStorage();f.storage.Prepare(f,Units.Count,SnapshotShotCapacity,Props.Count);return f;
        }
        public Frame Snapshot(Frame reuse=null)
        {
            var f=reuse??new Frame();if(f.storage==null)f.storage=new SnapshotStorage();var storage=f.storage;f.tick=Tick;f.time=Time;f.result=Result;
            f.commander.points["red"]=Commander.points["red"];f.commander.points["blue"]=Commander.points["blue"];f.commander.readyAt["red"]=Commander.readyAt["red"];f.commander.readyAt["blue"]=Commander.readyAt["blue"];
            SnapshotStorage.Size(f.commander.log,storage.log,Commander.log.Count);for(int i=0;i<Commander.log.Count;i++)CopyOrder(Commander.log[i],f.commander.log[i]);
            SnapshotStorage.Size(f.commander.active,storage.active,Commander.active.Count);for(int i=0;i<Commander.active.Count;i++)CopyOrder(Commander.active[i],f.commander.active[i]);
            SnapshotStorage.Size(f.cues,storage.cues,Cues.Count);for(int i=0;i<Cues.Count;i++){var a=Cues[i];var b=f.cues[i];b.id=a.id;b.tick=a.tick;b.type=a.type;b.x=a.x;b.y=a.y;b.z=a.z;b.strength=a.strength;}
            SnapshotStorage.Size(f.effects,storage.effects,Effects.Count);for(int i=0;i<Effects.Count;i++){var a=Effects[i];var b=f.effects[i];b.id=a.id;b.type=a.type;b.x=a.x;b.y=a.y;b.z=a.z;b.life=a.life;b.maxLife=a.maxLife;}
            // Restore the retained slots after a previous frame had fewer bodies.
            SnapshotStorage.Size(f.ragdolls,storage.corpses,Math.Max(storage.corpses.Count,24));World.Corpses(f.ragdolls);
            while(f.units.Count<Units.Count)f.units.Add(new UnitFrame());for(int i=0;i<Units.Count;i++){var u=Units[i];var e=u.entity;var v=f.units[i];v.id=e.id;v.type=e.type;v.team=e.team;v.stance=u.stance;v.hp=e.hp;v.alive=e.alive;v.angle=u.angle;v.attack=u.attack;v.charge=u.charge;v.inspired=u.inspireUntil>Tick;v.fortified=u.fortifyUntil>Tick;v.t=e.alive?e.body.Transform(v.t):null;}
            SnapshotStorage.Size(f.projectiles,storage.projectiles,Shots.Count);for(int i=0;i<Shots.Count;i++){var s=Shots[i];var p=f.projectiles[i];p.id=s.id;p.type=s.type;p.t=s.body.Transform(p.t);p.v=s.body.Velocity;}
            SnapshotStorage.Size(f.props,storage.props,Props.Count);for(int i=0;i<Props.Count;i++){var p=Props[i];var v=f.props[i];v.id=p.entity.id;v.type=p.entity.type;v.team=p.entity.team;v.x=p.entity.spawn.x;v.z=p.entity.spawn.z;v.floor=p.floor;v.hp=p.entity.hp;v.alive=p.entity.alive;v.active=p.fuse>=0||p.cooldown>0;}return f;
        }
        static void CopyOrder(OrderEntry a,OrderEntry b){b.id=a.id;b.tick=a.tick;b.endTick=a.endTick;b.team=a.team;b.type=a.type;b.x=a.x;b.z=a.z;}
        static OrderEntry CopyOrder(OrderEntry o){return new OrderEntry{id=o.id,tick=o.tick,endTick=o.endTick,team=o.team,type=o.type,x=o.x,z=o.z};}
        public void Dispose(){if(disposed)return;disposed=true;World.Dispose();}
    }
}

