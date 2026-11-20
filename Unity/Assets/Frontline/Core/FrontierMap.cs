using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontline.Core
{
    public static partial class FrontierMap
    {
        public const string Id = "frontier";
        public const float ManorX = -64, BridgeStart = ManorX + Manor.PlateauHalfWidth, Boundary = -10, BridgeHalfWidth = 3.6f;
        public const float West = ManorX - Manor.PlateauHalfWidth, East = 290, HalfDepth = 122, EnemyStop = Boundary + 1.15f;
        public const int MaxParty = 16, GroupSize = 5, MaxTier = 3;
        public const float AlertRadius = 18;
        public static V3 Rally => new V3(BridgeStart - 4,0,0);
        public static bool Safe(float x) => x < Boundary;
        public static bool AtManor(float x) => x <= BridgeStart - .65f;
        public static bool NearBridge(float x) => x > BridgeStart - 2 && x < Boundary + 2;
        public static bool Fell(V3 p) => p.y < -4 || p.x < West - 6 || p.x > East + 6 || Math.Abs(p.z) > HalfDepth + 6;

        static Platform Surface(float west,float east,float halfDepth,float thickness) =>
            new Platform { x = (west + east) * .5f, y = -thickness * .5f,
                hx = (east - west) * .5f, hy = thickness * .5f, hz = halfDepth };

        internal static MapDef Create() => new MapDef {
            id = Id, name = "半岛边疆",
            platforms = new List<Platform> {
                Surface(BridgeStart-.13f,Boundary+.13f,BridgeHalfWidth,.68f)
            },
            walls = new List<Wall> {
                new Wall { x=34,z=15,hx=1.85f,hy=.64f,hz=.30f },
                new Wall { x=168,z=-36,hx=.35f,hy=.78f,hz=2.0f }
            },
            rocks = new List<Rock> {
                new Rock { x=12,z=-14,r=1.1f }, new Rock { x=33,z=-16,r=1.15f },
                new Rock { x=82,z=31,r=1.35f }, new Rock { x=125,z=40,r=1.65f },
                new Rock { x=195,z=-52,r=1.0f }, new Rock { x=230,z=-73,r=1.3f }
            }
        };

        public readonly struct Guard
        {
            public readonly string unit;
            public readonly V3 camp, spawn;
            readonly float phase;
            readonly double startCos,startSin;
            internal Guard(string unit,V3 camp,V3 offset,float phase)
            {
                this.unit=unit;this.camp=camp;spawn=camp+offset;this.phase=phase;
                startCos=Math.Cos(phase);startSin=Math.Sin(phase);
            }
            public V3 PatrolOffset(float time,out float heading)
            {
                float angle=time*.25f+phase;
                double cos=Math.Cos(angle),sin=Math.Sin(angle);
                heading=(float)Math.Atan2(-1.7*sin,1.2*cos);
                return new V3((float)(cos-startCos)*1.7f,0,(float)(sin-startSin)*1.2f);
            }
        }

        static readonly IReadOnlyList<Guard> guards=Array.AsReadOnly(new[] {
            new Guard("militia",WestCamp,new V3(-2,0,-2),0),
            new Guard("shield",WestCamp,new V3(-2,0,1.5f),0),
            new Guard("spear",WestCamp,new V3(.1f,0,.2f),0),
            new Guard("militia",WestCamp,new V3(1,0,-2.4f),0),
            new Guard("archer",WestCamp,new V3(2,0,2.2f),0),
            new Guard("shield",NorthCamp,new V3(-2,0,-2),2),
            new Guard("spear",NorthCamp,new V3(-2,0,1.5f),2),
            new Guard("archer",NorthCamp,new V3(.1f,0,.2f),2),
            new Guard("archer",NorthCamp,new V3(1,0,-2.4f),2),
            new Guard("medic",NorthCamp,new V3(2,0,2.2f),2),
            new Guard("militia",EastCamp,new V3(-2,0,-2),4),
            new Guard("spear",EastCamp,new V3(-2,0,1.5f),4),
            new Guard("shield",EastCamp,new V3(.1f,0,.2f),4),
            new Guard("crossbow",EastCamp,new V3(1,0,-2.4f),4),
            new Guard("cavalry",EastCamp,new V3(2,0,2.2f),4)
        });
        public static IReadOnlyList<Guard> Guards => guards;
        static readonly IReadOnlyList<string> RegularRoster = Array.AsReadOnly(guards.Select(guard=>guard.unit).ToArray());
        static readonly IReadOnlyList<string> EliteRoster = Array.AsReadOnly(new[] {
            "shield","shield","spear","giant","crossbow",
            "shield","spear","crossbow","archer","medic",
            "giant","spear","shield","crossbow","cavalry"
        });

        public static IReadOnlyList<string> Roster(int tier) => tier switch
        {
            1 or 2 => RegularRoster,
            3 => EliteRoster,
            _ => throw new ArgumentOutOfRangeException(nameof(tier))
        };

        public static int RewardPercent(int tier) => 100+(tier-1)*50;
        public static int ApprovalPerGroup(int tier) => tier+1;

        internal static int ValidateGuardLevels(IReadOnlyList<UnitPlacement> army)
        {
            int level=0;
            foreach(var unit in army)
                if(unit!=null&&unit.level!=0){level=unit.level;break;}
            // Unleveled patrols remain valid for existing deployments and replays.
            if(level==0)return 0;
            if(level<2||level>MaxTier||army.Count!=guards.Count)throw new FormatException("外域警戒阶位无效");
            var roster=Roster(level);
            for(int i=0;i<army.Count;i++)
            {
                var unit=army[i];
                var spawn=guards[i].spawn;
                if(unit==null||unit.level!=level||unit.type!=roster[i]||unit.x!=spawn.x||unit.z!=spawn.z)
                    throw new FormatException("精锐守军与警戒阶位不符");
            }
            return level;
        }

        static bool Inside(V3 a,V3 b,float radius)
        {
            float x=a.x-b.x,z=a.z-b.z;
            return x*x+z*z<=radius*radius;
        }
        internal static bool EnemyAlert(Guard guard,V3 enemy,V3 ally,bool pursuing) =>
            !Safe(ally.x) && !Safe(enemy.x) && Inside(enemy,ally,pursuing?23:AlertRadius) &&
            Inside(guard.camp,ally,27) && Inside(guard.camp,enemy,28);

        internal static int Columns(int count) => count>=10?4:Math.Min(3,Math.Max(1,count));
        internal static float Lane(int index,int count)
        {
            int columns=Columns(count);
            return (index%columns-(columns-1)*.5f)*(columns==4?1.45f:1.7f);
        }
        internal static V3 Formation(int index,int count,V3 center,int direction)
        {
            int columns=Columns(count);
            float rank=(count-1)/columns*.5f-index/columns;
            float x=center.x+rank*1.65f*direction;
            float lane=Lane(index,count);
            float z=center.z+lane*1.16f;
            if(center.x<BridgeStart)
            {
                var goal=KeepInland(Peninsula,x,z,1.9f);
                if(Math.Abs(goal.x-ManorX)<CastleHalfWidth+1.05f&&Math.Abs(goal.z)<CastleHalfDepth+1.12f)
                    goal.z=(center.z<0?-1:1)*(CastleHalfDepth+1.12f);
                return goal;
            }
            if(Safe(center.x))
                return new V3(Clamp(x,BridgeStart+.2f,Boundary-.2f),0,lane);
            return KeepInland(Mainland,x,z,2.15f);
        }

        internal static V3 Waypoint(V3 from,V3 goal,float lane)
        {
            if(from.x<BridgeStart-1.6f&&goal.x<BridgeStart)return InlandWaypoint(Peninsula,from,goal);
            if(from.x>Boundary+1.6f&&goal.x>=Boundary)return InlandWaypoint(Mainland,from,goal);
            int direction=goal.x>=from.x?1:-1;
            float entry=direction>0?BridgeStart:Boundary;
            float at=(from.x-entry)*direction,end=(goal.x-entry)*direction;
            float span=Boundary-BridgeStart;
            if(end<0||at>=span+4)return goal;
            // Align on dry land. A moving look-ahead avoids braking on the same exit point in each file.
            if(at<-1.6f && Math.Abs(from.z-lane)>.65f)
                return InlandWaypoint(direction>0?Peninsula:Mainland,from,new V3(entry-2*direction,0,lane));
            float look=Math.Min(end,Math.Max(-1.3f,at+5.0f));
            float fanDistance=Clamp(end-span-.55f,.20f,3.0f);
            float spread=Clamp((at-span-.55f)/fanDistance,0,1);
            spread=spread*spread*(3-2*spread);
            return new V3(entry+look*direction,0,lane+(goal.z-lane)*spread);
        }

        internal static V3 AroundFence(V3 from,V3 goal,Wall wall)
        {
            bool horizontal=wall.hx>wall.hz;
            float length=Math.Max(wall.hx,wall.hz)+.85f, depth=Math.Min(wall.hx,wall.hz)+.85f;
            float u=horizontal?from.x-wall.x:from.z-wall.z, v=horizontal?from.z-wall.z:from.x-wall.x;
            float endU=horizontal?goal.x-wall.x:goal.z-wall.z, endV=horizontal?goal.z-wall.z:goal.x-wall.x;
            float side=v>=0?1:-1;
            V3 Point(float a,float b)=>horizontal?new V3(wall.x+a,0,wall.z+b):new V3(wall.x+b,0,wall.z+a);
            if(Math.Abs(endV)<depth && Math.Abs(endU)<length)
            {
                endV=side*(depth+.20f);
                goal=Point(endU,endV);
            }
            if(side*endV>=depth || Math.Abs(endV-v)<.001f)return goal;
            float t=Clamp((side*depth-v)/(endV-v),0,1);
            if(Math.Abs(u+(endU-u)*t)>length && Math.Abs(endU)>length && Math.Sign(u)==Math.Sign(endU))return goal;
            float crossing=Clamp(-v/(endV-v),0,1);
            if(Math.Abs(u+(endU-u)*crossing)>length)return goal;
            float end=(u+endU>=0?1:-1)*(length+.6f);
            if(Math.Abs(u)<length+.15f)return Point(end,side*(depth+.3f));
            return Point(u,-side*(depth+.5f));
        }

        static float Clamp(float value,float min,float max) => Math.Min(max,Math.Max(min,value));
    }
}
