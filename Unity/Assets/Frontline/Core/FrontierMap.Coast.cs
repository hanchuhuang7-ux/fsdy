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

        // The island survey keeps the same 36 plots, approaches and dock.
        static readonly ShoreSlice[] Island={
            new ShoreSlice(IslandWest,-14,16),
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
        public static IReadOnlyList<ShoreSlice> ManorCoast {get;}=Array.AsReadOnly(Island);
        public static readonly double ManorArea=IslandArea();
        public const float PeaceMarchMultiplier=1.85f;
        public const float CastleHalfWidth=4.6f,CastleHalfDepth=3.82f;
        public static Wall CastleFootprint => new Wall{x=ManorX,z=0,hx=CastleHalfWidth,hz=CastleHalfDepth,hy=2.6f};

        static double IslandArea()
        {
            double area=0;
            for(int i=1;i<Island.Length;i++)
            {
                var a=Island[i-1];var b=Island[i];
                area+=(b.x-a.x)*(a.north-a.south+b.north-b.south)*.5;
            }
            return area;
        }

        public static bool ShoreAt(float x,out float south,out float north)
        {
            if(!(x>=IslandWest&&x<=BridgeStart)){south=north=0;return false;}
            int lower=0,upper=Island.Length-1;
            while(upper-lower>1)
            {
                int middle=(lower+upper)/2;
                if(Island[middle].x<=x)lower=middle;else upper=middle;
            }
            var a=Island[lower];var b=Island[upper];
            float mix=(x-a.x)/(b.x-a.x);
            south=a.south+(b.south-a.south)*mix;
            north=a.north+(b.north-a.north)*mix;
            return true;
        }

        public static bool IsIsland(float x,float z) =>
            ShoreAt(x,out float south,out float north)&&z>=south&&z<=north;

        static bool OnBridge(float x,float z) => x>=BridgeStart&&x<Boundary&&Math.Abs(z)<=BridgeHalfWidth;

        public static bool HasGround(float x,float z) =>
            OnBridge(x,z)||IsIsland(x,z)||OnMainland(x,z);

        static V3 KeepOnIsland(float x,float z,float margin)
        {
            x=Clamp(x,IslandWest+margin,BridgeStart-margin);
            ShoreAt(x-margin,out float southA,out float northA);
            ShoreAt(x+margin,out float southB,out float northB);
            ShoreAt(x,out float south,out float north);
            return new V3(x,0,Clamp(z,Math.Max(south,Math.Max(southA,southB))+margin,
                Math.Min(north,Math.Min(northA,northB))-margin));
        }

        static bool ClearsIslandBank(V3 from,V3 to)
        {
            float left=Math.Min(from.x,to.x),right=Math.Max(from.x,to.x);
            if(left<IslandWest||right>BridgeStart)return false;
            foreach(var bank in Island)
            {
                if(bank.x<=left)continue;
                if(bank.x>=right)break;
                float z=from.z+(to.z-from.z)*(bank.x-from.x)/(to.x-from.x);
                if(z<bank.south+2||z>bank.north-2)return false;
            }
            return true;
        }

        static V3 IslandWaypoint(V3 from,V3 goal)
        {
            if(ClearsIslandBank(from,goal))return goal;
            var alongSpine=new V3(goal.x,0,0);
            return ClearsIslandBank(from,alongSpine)?alongSpine:new V3(from.x,0,0);
        }
    }
}
