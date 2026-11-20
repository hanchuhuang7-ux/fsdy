using System;

namespace Frontline.Core
{
    public sealed partial class Battle
    {
        public bool IsFrontier { get; }
        public V3? MarchDestination { get; private set; }
        sealed class MarchState
        {
            public readonly int[] sorted=new int[FrontierMap.MaxParty];
            public readonly float[] lanes=new float[FrontierMap.MaxParty];
            public readonly V3[] goals=new V3[FrontierMap.MaxParty];
            public readonly int[] patrolTicks=new int[FrontierMap.Guards.Count/FrontierMap.GroupSize];
            public readonly V3[] offsets=new V3[FrontierMap.Guards.Count/FrontierMap.GroupSize];
            public readonly Wall[] reefs;
            public readonly Wall castle=FrontierMap.CastleFootprint;
            public MarchState(MapDef map)
            {
                reefs=new Wall[map.rocks.Count];
                for(int i=0;i<reefs.Length;i++)
                {
                    var rock=map.rocks[i];
                    float radius=rock.r+.18f;
                    reefs[i]=new Wall{x=rock.x,z=rock.z,hx=radius+.03f,hz=radius};
                }
            }
        }
        readonly MarchState march;
        bool exposedParty;

        public bool MarchFrontier(float x,float z)
        {
            if(!IsFrontier || Result!=null || !FrontierMap.HasGround(x,z)) return false;
            if(Math.Abs(x-FrontierMap.ManorX)<=FrontierMap.CastleHalfWidth&&Math.Abs(z)<=FrontierMap.CastleHalfDepth)return false;
            foreach(var wall in map.walls)
                if(Math.Abs(x-wall.x)<=wall.hx && Math.Abs(z-wall.z)<=wall.hz)return false;
            foreach(var rock in map.rocks)
                if(Catalog.Distance(x,z,rock.x,rock.z)<=rock.r)return false;
            MarchDestination=new V3(x,0,z);
            int count=0;
            float sumX=0;
            for(int i=0;i<Config.red.Count;i++)
            {
                Units[i].target=null;
                Units[i].preparing=false;
                if(!Units[i].entity.alive)continue;
                var pos=Units[i].entity.body.Position;
                int at=count++;
                while(at>0&&Units[march.sorted[at-1]].entity.body.Position.z>pos.z)
                {
                    march.sorted[at]=march.sorted[at-1];at--;
                }
                march.sorted[at]=i;sumX+=pos.x;
            }
            int columns=FrontierMap.Columns(count),first=0;
            int direction=x*count>=sumX?1:-1;
            // Match nearby soldiers to the same file; the nearest soldier leads in either direction.
            for(int col=0;col<columns&&col<count;col++)
            {
                int length=1+(count-1-col)/columns;
                for(int j=first+1;j<first+length;j++)
                {
                    int unit=march.sorted[j],k=j;
                    float ahead=direction*Units[unit].entity.body.Position.x;
                    while(k>first&&direction*Units[march.sorted[k-1]].entity.body.Position.x<ahead)
                    {march.sorted[k]=march.sorted[k-1];k--;}
                    march.sorted[k]=unit;
                }
                for(int row=0;row<length;row++)
                {
                    int index=march.sorted[first+row],slot=row*columns+col;
                    march.lanes[index]=FrontierMap.Lane(slot,count);
                    march.goals[index]=FrontierMap.Formation(slot,count,MarchDestination.Value,direction);
                }
                first+=length;
            }
            return true;
        }

        public void EndFrontier()
        {
            if(!IsFrontier || Result!=null) return;
            int red=0,blue=0;
            foreach(var u in Units)
                if(u.entity.alive)
                {
                    if(u.isRed) red++;
                    else blue++;
                }
            Finish("draw","withdraw",red,blue,Power("red"),Power("blue"));
        }

        FrontierMap.Guard GuardFor(Soldier u) => FrontierMap.Guards[u.entity.id-1-Config.red.Count];

        bool FrontierTarget(Soldier own,Soldier foe)
        {
            var from=own.entity.position;
            var to=foe.entity.position;
            if(FrontierMap.Safe(from.x)||FrontierMap.Safe(to.x)) return false;
            if(own.isRed)
                return MarchDestination.HasValue && !FrontierMap.Safe(MarchDestination.Value.x) && Within(from,to,FrontierMap.AlertRadius+3);
            return FrontierMap.EnemyAlert(GuardFor(own),from,to,own.target==foe);
        }

        bool FrontierCanHit(Entity target,Entity source) =>
            !FrontierMap.Safe(target.body.Position.x) &&
            (source==null || !FrontierMap.Safe((source.alive ? source.body.Position : source.position).x));

        void FrontlineBoundary()
        {
            for(int i=Config.red.Count;i<Units.Count;i++)
            {
                var unit=Units[i].entity;
                if(unit.alive && unit.body.Position.x<FrontierMap.EnemyStop)
                    ((IFrontierBody)unit.body).StopAtFrontier(FrontierMap.EnemyStop);
            }
            // A shot launched against an exposed unit can no longer cross the gate after its target retreats.
            foreach(var shot in Shots)
                if(!shot.removed && FrontierMap.Safe(shot.body.Position.x)) RemoveShot(shot);
        }

        void WalkFrontier(Soldier u,bool inspired)
        {
            var entity=u.entity;
            var p=entity.position;
            var d=u.definition;
            var goal=u.anchor;
            if(!u.isRed)
            {
                var guard=GuardFor(u);
                int group=(entity.id-1-Config.red.Count)/FrontierMap.GroupSize;
                if(march.patrolTicks[group]!=Tick+1)
                {
                    march.offsets[group]=guard.PatrolOffset(Time,out _);
                    march.patrolTicks[group]=Tick+1;
                }
                goal=guard.spawn+march.offsets[group];
            }
            else if(MarchDestination.HasValue)
            {
                int index=entity.id-1;
                goal=FrontierMap.Waypoint(p,march.goals[index],march.lanes[index]);
                if(p.x<FrontierMap.BridgeStart)goal=FrontierMap.AroundFence(p,goal,march.castle);
                foreach(var wall in map.walls)goal=FrontierMap.AroundFence(p,goal,wall);
                foreach(var rock in march.reefs)goal=FrontierMap.AroundFence(p,goal,rock);
            }
            var delta=goal-p;
            delta.y=0;
            float distance=delta.Planar;
            var velocity=entity.body.Velocity;
            u.moveSpeed=velocity.Planar;
            if(distance>.10f)
            {
                float desiredAngle=(float)Math.Atan2(delta.x,delta.z);
                u.angle=CombatMotion.Turn(u.angle,desiredAngle,u.turnRateAtRest,Catalog.Step);
            }
            var forward=CombatMotion.Forward(u.angle);
            u.moveForward=u.moveSpeed>.05f?(velocity.x*forward.x+velocity.z*forward.z)/u.moveSpeed:0;
            u.gait+=u.moveSpeed*Catalog.Step/Math.Max(.7f,d.scale);
            u.preparing=false;
            bool fast=(u.isCavalry&&u.moveSpeed>3.6f)||u.moveSpeed>8||Math.Abs(velocity.y)>6;
            if(u.isCavalry||u.isCatapult||fast!=u.collisionFast){u.combatBody?.Facing(u.angle,fast);u.collisionFast=fast;}
            if(!Catalog.Finite(u.floor)||p.y>=u.floor+d.scale*1.12f) return;
            float travel=u.isRed&&!FrontierMap.NearBridge(p.x)&&distance>4?FrontierMap.PeaceMarchMultiplier:1;
            float pace=Math.Min(d.speed*(u.isRed?travel:.75f)*(inspired?1.12f:1),Math.Max(0,(distance-.10f)*4));
            var move=delta*(pace/Math.Max(.01f,distance));
            if(pace>0 && !FrontierMap.NearBridge(p.x))
                foreach(var ally in Nearby(p,2))
                    if(ally!=u && ally.isRed==u.isRed)
                        move+=CombatMotion.Separation(p-ally.entity.position,.65f*(d.scale+ally.definition.scale),false,1);
            float response=u.stun>0?.008f:.16f;
            entity.body.Impulse(new V3((move.x-velocity.x)*d.mass*response,0,(move.z-velocity.z)*d.mass*response));
        }
    }
}
