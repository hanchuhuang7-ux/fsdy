using System;
using System.Collections.Generic;

namespace Frontline.Core
{
    public static partial class FrontierMap
    {
        public readonly struct ShoreSlice
        {
            public readonly float x,south,north;
            public ShoreSlice(float x,float south,float north){this.x=x;this.south=south;this.north=north;}
        }

        // The opposing banks are linear between survey stations. This is the polygon used
        // by terrain baking, movement, ground picking, and the PhysX surface.
        static readonly ShoreSlice[] Peninsula={
            new ShoreSlice(West,-14,16),
            new ShoreSlice(-103,-24,22),
            new ShoreSlice(-99,-25,26),
            new ShoreSlice(-92,-28,27),
            new ShoreSlice(-85,-28,28),
            new ShoreSlice(-77,-27,27),
            new ShoreSlice(-68,-27,28),
            new ShoreSlice(-57,-28,27),
            new ShoreSlice(-48,-26,28),
            new ShoreSlice(-39,-27,27),
            new ShoreSlice(-33,-25,25),
            new ShoreSlice(-28,-23,24),
            new ShoreSlice(-24,-14,15),
            new ShoreSlice(BridgeStart,-5,5),
        };
        static readonly ShoreSlice[] Mainland={
            new ShoreSlice(Boundary,-5,5),
            new ShoreSlice(-4,-26,31),
            new ShoreSlice(6,-47,56),
            new ShoreSlice(20,-66,75),
            new ShoreSlice(37,-89,88),
            new ShoreSlice(53,-111,101),
            new ShoreSlice(70,-114,116),
            new ShoreSlice(89,-103,111),
            new ShoreSlice(110,-91,82),
            new ShoreSlice(129,-112,64),
            new ShoreSlice(148,-120,90),
            new ShoreSlice(166,-102,116),
            new ShoreSlice(184,-83,122),
            new ShoreSlice(202,-109,108),
            new ShoreSlice(224,-121,94),
            new ShoreSlice(245,-108,106),
            new ShoreSlice(261,-80,89),
            new ShoreSlice(276,-64,69),
            new ShoreSlice(286,-41,48),
            new ShoreSlice(East,-11,20),
        };
        public static IReadOnlyList<ShoreSlice> ManorCoast {get;}=Array.AsReadOnly(Peninsula);
        public static IReadOnlyList<ShoreSlice> FrontierCoast {get;}=Array.AsReadOnly(Mainland);
        public static readonly double ManorArea=Area(Peninsula),FrontierArea=Area(Mainland);
        public const int TerrainBands=10;
        public const float TerrainCeiling=11;
        static readonly V3[] MainlandVertices=BuildTerrainStations();

        static double Area(ShoreSlice[] slices)
        {
            double result=0;
            for(int i=1;i<slices.Length;i++)
            {
                var a=slices[i-1];var b=slices[i];
                result+=(b.x-a.x)*(a.north-a.south+b.north-b.south)*.5;
            }
            return result;
        }

        public static bool ShoreAt(float x,out float south,out float north)
        {
            if(x>=West&&x<=BridgeStart){ShoreAt(Peninsula,x,out south,out north);return true;}
            if(x>=Boundary&&x<=East){ShoreAt(Mainland,x,out south,out north);return true;}
            south=north=0;
            return false;
        }

        static int FindStation(ShoreSlice[] coast,float x)
        {
            int lower=0,upper=coast.Length-1;
            while(upper-lower>1)
            {
                int middle=(lower+upper)/2;
                if(coast[middle].x<=x)lower=middle;else upper=middle;
            }
            return lower;
        }

        static void ShoreAt(ShoreSlice[] coast,float x,out float south,out float north)
        {
            int lower=FindStation(coast,x);
            var a=coast[lower];var b=coast[lower+1];
            float mix=(x-a.x)/(b.x-a.x);
            south=a.south+(b.south-a.south)*mix;
            north=a.north+(b.north-a.north)*mix;
        }

        public static bool HasGround(float x,float z)
        {
            if(x>=BridgeStart&&x<=Boundary)return Math.Abs(z)<=BridgeHalfWidth||
                ShoreAt(x,out float bankSouth,out float bankNorth)&&z>=bankSouth&&z<=bankNorth;
            return ShoreAt(x,out float south,out float north)&&z>=south&&z<=north;
        }

        static V3[] BuildTerrainStations()
        {
            var points=new V3[Mainland.Length*(TerrainBands+1)];
            for(int station=0;station<Mainland.Length;station++)
            {
                var shore=Mainland[station];
                for(int band=0;band<=TerrainBands;band++)
                {
                    float z=shore.south+(shore.north-shore.south)*band/TerrainBands;
                    float x=shore.x;
                    float y=band==0||band==TerrainBands?0:
                        Math.Max(Math.Max(Knoll(x,z,62,-69,43,38,8),Knoll(x,z,102,93,43,25,7.8f)),
                            Math.Max(Knoll(x,z,223,25,66,53,10.8f),Knoll(x,z,182,-91,41,29,6.6f)));
                    points[station*(TerrainBands+1)+band]=new V3(x,y,z);
                }
            }
            return points;
        }

        static float Knoll(float x,float z,float cx,float cz,float width,float depth,float height)
        {
            float sx=(x-cx)/width,sz=(z-cz)/depth;
            float fill=Math.Max(0,1-sx*sx-sz*sz);
            return height*fill*fill;
        }

        public static V3 TerrainPoint(int station,int band) => MainlandVertices[station*(TerrainBands+1)+band];

        public static float SurfaceHeight(float x,float z)
        {
            if(!(x>=Boundary&&x<=East))return HasGround(x,z)?0:float.NegativeInfinity;
            int i=FindStation(Mainland,x);
            var left=Mainland[i];var right=Mainland[i+1];
            float along=(x-left.x)/(right.x-left.x);
            float south=left.south+(right.south-left.south)*along;
            float north=left.north+(right.north-left.north)*along;
            if(!(z>=south&&z<=north))return float.NegativeInfinity;
            int band=Math.Min(TerrainBands-1,(int)((z-south)/(north-south)*TerrainBands));
            var a=TerrainPoint(i,band);var b=TerrainPoint(i,band+1);
            var c=TerrainPoint(i+1,band+1);var d=TerrainPoint(i+1,band);
            float diagonal=a.z+(c.z-a.z)*along;
            float middle=a.y+(c.y-a.y)*along;
            if(z==diagonal)return middle;
            float edge,mix;
            if(z<diagonal)
            {
                edge=a.z+(d.z-a.z)*along;
                mix=a.y+(d.y-a.y)*along;
            }
            else
            {
                edge=b.z+(c.z-b.z)*along;
                mix=b.y+(c.y-b.y)*along;
            }
            return edge==diagonal?middle:middle+(mix-middle)*(z-diagonal)/(edge-diagonal);
        }

        static V3 WestCamp => new V3(25,0,3);
        static V3 NorthCamp => new V3(100,0,53);
        static V3 EastCamp => new V3(220,0,-63);
        public static V3 CampCenter(int sector) => guards[sector*GroupSize].camp;
        public const float PeaceMarchMultiplier=1.85f;
        public const float CastleHalfWidth=4.6f,CastleHalfDepth=3.82f;
        public static Wall CastleFootprint => new Wall{x=ManorX,z=0,hx=CastleHalfWidth,hz=CastleHalfDepth,hy=2.6f};

        static V3 KeepInland(ShoreSlice[] coast,float x,float z,float margin)
        {
            x=Clamp(x,coast[0].x+margin,coast[coast.Length-1].x-margin);
            ShoreAt(coast,x-margin,out float southA,out float northA);
            ShoreAt(coast,x+margin,out float southB,out float northB);
            ShoreAt(coast,x,out float south,out float north);
            return new V3(x,0,Clamp(z,Math.Max(south,Math.Max(southA,southB))+margin,
                Math.Min(north,Math.Min(northA,northB))-margin));
        }

        static bool ClearsBank(ShoreSlice[] coast,V3 from,V3 to,float margin)
        {
            float left=Math.Min(from.x,to.x),right=Math.Max(from.x,to.x);
            if(left<coast[0].x||right>coast[coast.Length-1].x)return false;
            for(int i=0;i<coast.Length;i++)
            {
                var bank=coast[i];
                if(bank.x<=left)continue;
                if(bank.x>=right)break;
                float z=from.z+(to.z-from.z)*(bank.x-from.x)/(to.x-from.x);
                if(z<bank.south+margin||z>bank.north-margin)return false;
            }
            return true;
        }

        // The coast has an unbroken inland spine. Use it to skirt a bay, then fan back
        // toward the formation's dry destination as soon as that line is unobstructed.
        static V3 InlandWaypoint(ShoreSlice[] coast,V3 from,V3 goal)
        {
            if(ClearsBank(coast,from,goal,2f))return goal;
            var alongSpine=new V3(goal.x,0,0);
            if(ClearsBank(coast,from,alongSpine,2f))return alongSpine;
            return new V3(from.x,0,0);
        }
    }
}
