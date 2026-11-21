using System;
using System.Collections.Generic;

namespace Frontline.Core
{
    public static partial class FrontierMap
    {
        public const int ShoreStations=64,TerrainBands=9;
        public const float TerrainCeiling=25;
        const double FullTurn=Math.PI*2,StationAngle=FullTurn/ShoreStations;
        const float InlandMargin=2.05f;
        static V3 WestCamp => new V3(71,0,5);
        static V3 NorthCamp => new V3(-55,0,115);
        static V3 EastCamp => new V3(28,0,-121);
        public static V3 CampCenter(int sector) => guards[sector*GroupSize].camp;

        static readonly V3[] Inner=SurveyBank(true);
        static readonly V3[] Outer=SurveyBank(false);
        static readonly V3[] InnerRoute=MoveBank(Inner,InlandMargin);
        static readonly V3[] OuterRoute=MoveBank(Outer,-InlandMargin);
        static readonly V3[] Terrain=BuildTerrain();
        public static IReadOnlyList<V3> LakeBank {get;}=Array.AsReadOnly(Inner);
        public static IReadOnlyList<V3> OuterBank {get;}=Array.AsReadOnly(Outer);
        public static readonly double FrontierArea=PolygonArea(Outer)-PolygonArea(Inner);
        public static double LakeArea => PolygonArea(Inner)-ManorArea;

        static int NextStation(int station) => (station+1)%ShoreStations;

        static V3[] SurveyBank(bool lake)
        {
            var bank=new V3[ShoreStations];
            for(int i=0;i<bank.Length;i++)
            {
                double a=i*StationAngle;
                double x=Math.Cos(a),z=Math.Sin(a);
                // Different wavelengths form headlands and coves without overhanging
                // the radial survey. The three east stations form the bridge abutment.
                double profile=lake?1+.079*Math.Sin(a*3+.35)+.061*Math.Cos(a*5+.81)+.037*Math.Sin(a*9+.6)
                    :1+.082*Math.Sin(a*3-.6)+.047*Math.Cos(a*5+1.1)+.024*Math.Sin(a*9);
                double width=lake?106:235,depth=lake?83:171;
                double radius=profile/Math.Sqrt(x*x/(width*width)+z*z/(depth*depth));
                if(lake&&(i<=1||i==bank.Length-1))radius=(Boundary-ManorX)/x;
                float easting=ManorX+(float)(x*radius),northing=(float)(z*radius);
                bank[i]=new V3(easting,0,northing);
            }
            return bank;
        }

        static V3[] MoveBank(V3[] bank,float metres)
        {
            var shifted=new V3[bank.Length];
            var center=new V3(ManorX,0,0);
            for(int i=0;i<shifted.Length;i++)shifted[i]=bank[i]+(bank[i]-center).Normalized*metres;
            return shifted;
        }

        static double PolygonArea(V3[] ring)
        {
            double twice=0;
            for(int i=0;i<ring.Length;i++)twice+=Cross(ring[i],ring[NextStation(i)]);
            return twice*.5;
        }

        static float Cross(V3 a,V3 b) => a.x*b.z-a.z*b.x;

        static double Bearing(float x,float z)
        {
            double angle=Math.Atan2(z,x-ManorX);
            return angle<0?angle+FullTurn:angle;
        }

        static int Sector(double bearing) => Math.Min(ShoreStations-1,(int)(bearing/StationAngle));

        static bool BetweenBanks(V3 p,int sector,V3[] inner,V3[] outer)
        {
            int next=NextStation(sector);
            var a=inner[sector];var b=outer[sector];
            return Cross(inner[next]-a,p-a)<=.0005f&&Cross(outer[next]-b,p-b)>=-.0005f;
        }

        public static bool OnMainland(float x,float z)
        {
            if(!(x>=West&&x<=East&&Math.Abs(z)<=HalfDepth)||Math.Abs(x-ManorX)<42&&Math.Abs(z)<42)return false;
            return BetweenBanks(new V3(x,0,z),Sector(Bearing(x,z)),Inner,Outer);
        }

        public static V3 TerrainPoint(int station,int band) => Terrain[station*(TerrainBands+1)+band];

        static V3[] BuildTerrain()
        {
            var points=new V3[ShoreStations*(TerrainBands+1)];
            for(int station=0;station<ShoreStations;station++)
                for(int band=0;band<=TerrainBands;band++)
                {
                    float depth=band/(float)TerrainBands;
                    var p=Inner[station]+(Outer[station]-Inner[station])*depth;
                    // Start each shelf at grade; keep the bridge throat and camp yards
                    // open, and build the ridges further back from the working roads.
                    float relief=(float)Math.Sin(Math.PI*depth);
                    float roll=(float)(2.7+1.75*Math.Sin(p.x*.038+p.z*.028)+1.1*Math.Cos(p.z*.048-p.x*.031));
                    float ridge=Math.Max(Knoll(p,-217,84,94,73,19),Knoll(p,-217,-92,88,69,20));
                    ridge=Math.Max(ridge,Math.Max(Knoll(p,99,91,83,71,21),Knoll(p,116,-69,75,65,18)));
                    float grading=Math.Min(Grade(p,WestCamp,20),Math.Min(Grade(p,NorthCamp,20),Grade(p,EastCamp,20)));
                    grading=Math.Min(grading,Grade(p,new V3(Boundary,0,0),20));
                    p.y=band==0||band==TerrainBands?0:Math.Max(0,roll+ridge)*relief*grading;
                    points[station*(TerrainBands+1)+band]=p;
                }
            return points;
        }

        static float Grade(V3 p,V3 center,float pad) => Clamp((V3.DistanceXZ(p,center)-pad)/13,0,1);

        static float Knoll(V3 p,float x,float z,float width,float depth,float height)
        {
            float sx=(p.x-x)/width,sz=(p.z-z)/depth;
            float fill=Math.Max(0,1-sx*sx-sz*sz);
            return height*fill*fill;
        }

        public static float SurfaceHeight(float x,float z)
        {
            if(OnBridge(x,z)||IsIsland(x,z))return 0;
            if(!(x>=West&&x<=East&&Math.Abs(z)<=HalfDepth))return float.NegativeInfinity;
            int i=Sector(Bearing(x,z)),j=NextStation(i);
            var p=new V3(x,0,z);
            if(!BetweenBanks(p,i,Inner,Outer))return float.NegativeInfinity;
            for(int band=0;band<TerrainBands;band++)
            {
                var a=TerrainPoint(i,band);var b=TerrainPoint(j,band);
                var c=TerrainPoint(j,band+1);var d=TerrainPoint(i,band+1);
                if(TriangleHeight(p,a,b,c,out float y)||TriangleHeight(p,a,c,d,out y))return y;
            }
            return float.NegativeInfinity;
        }

        static bool TriangleHeight(V3 p,V3 a,V3 b,V3 c,out float height)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;
            float determinant=Cross(ab,ac);
            float u=Cross(ap,ac)/determinant,v=Cross(ab,ap)/determinant;
            height=a.y+ab.y*u+ac.y*v;
            return u>=-.000001f&&v>=-.000001f&&u+v<=1.000001f;
        }

        static float BankRadius(V3[] bank,int sector,V3 ray)
        {
            var a=bank[sector]-new V3(ManorX,0,0);
            var edge=bank[NextStation(sector)]-bank[sector];
            return Cross(a,edge)/Cross(ray,edge);
        }

        static V3 KeepOnMainland(float x,float z,float margin)
        {
            var leg=new V3(x-ManorX,0,z);
            float length=leg.Planar;
            var direction=length>.0001f?leg*(1/length):new V3(1,0,0);
            int sector=Sector(Bearing(x,z));
            float radius=Clamp(length,BankRadius(Inner,sector,direction)+margin,BankRadius(Outer,sector,direction)-margin);
            return new V3(ManorX,0,0)+direction*radius;
        }

        public static V3 RingRoad(double radians,float depth)
        {
            radians=(radians+FullTurn)%FullTurn;
            var ray=new V3((float)Math.Cos(radians),0,(float)Math.Sin(radians));
            int station=Sector(radians);
            float inner=BankRadius(Inner,station,ray),outer=BankRadius(Outer,station,ray);
            return new V3(ManorX,0,0)+ray*(inner+(outer-inner)*depth);
        }

        public static V3 SurveyRoad(double bearing,bool ridge) => RingRoad(bearing,RoadDepth(bearing,ridge));
        public static V3 RoadJunction(V3 site) => SurveyRoad(Bearing(site.x,site.z),false);

        static float RoadDepth(double bearing,bool ridge) => ridge
            ?.65f+(float)(.076*Math.Sin(bearing*4+.6)+.064*Math.Cos(bearing*9+.14))
            :.27f+(float)(.045*Math.Sin(bearing*5+.61)+.046*Math.Cos(bearing*8-1.1));

        public static bool BesideRingRoad(float x,float z,float depth,float margin)
        {
            double bearing=Bearing(x,z);
            return Math.Abs(depth-RoadDepth(bearing,false))<margin||Math.Abs(depth-RoadDepth(bearing,true))<margin;
        }

        public static float MainlandDepth(float x,float z,out float bankDistance)
        {
            var ray=new V3(x-ManorX,0,z);
            float radius=ray.Planar;
            if(radius<.01f){bankDistance=0;return -1;}
            int station=Sector(Bearing(x,z));
            ray*=1/radius;
            float inner=BankRadius(Inner,station,ray),outer=BankRadius(Outer,station,ray);
            bankDistance=Math.Min(radius-inner,outer-radius);
            return (radius-inner)/(outer-inner);
        }

        static bool CrossesBank(V3 from,V3 to,V3[] bank)
        {
            float west=Math.Min(from.x,to.x),east=Math.Max(from.x,to.x);
            float south=Math.Min(from.z,to.z),north=Math.Max(from.z,to.z);
            var leg=to-from;
            for(int i=0;i<bank.Length;i++)
            {
                var a=bank[i];var b=bank[NextStation(i)];
                if(Math.Max(a.x,b.x)<west||Math.Min(a.x,b.x)>east||
                    Math.Max(a.z,b.z)<south||Math.Min(a.z,b.z)>north)continue;
                if(Cross(leg,a-from)*Cross(leg,b-from)>0)continue;
                var edge=b-a;
                if(Cross(edge,from-a)*Cross(edge,to-a)<=0)return true;
            }
            return false;
        }

        static bool ClearsMainland(V3 from,V3 to) =>
            !CrossesBank(from,to,InnerRoute)&&!CrossesBank(from,to,OuterRoute);

        static V3 MainlandWaypoint(V3 from,V3 goal)
        {
            double bearing=Bearing(from.x,from.z);
            if(!BetweenBanks(from,Sector(bearing),InnerRoute,OuterRoute))
                return KeepOnMainland(from.x,from.z,3.5f);
            if(ClearsMainland(from,goal))return goal;
            double turn=Bearing(goal.x,goal.z)-bearing;
            if(turn>Math.PI)turn-=FullTurn;
            if(turn< -Math.PI)turn+=FullTurn;
            var next=RingRoad(bearing+Math.Sign(turn)*.19,.34f);
            return ClearsMainland(from,next)?next:RingRoad(bearing,.34f);
        }
    }
}
