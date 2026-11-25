using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static void BakeVillageRoads(int lod)
        {
            Save("frontier_palisade",lod,Palisade(lod));
            Save("road_path",lod,RoadRibbon(0,lod));
            Save("road_link",lod,RoadRibbon(.125f,lod));
            Save("road_setts",lod,RoadRibbon(.5f,lod));
            Save("road_grass",lod,RoadRibbon(1,lod));
            Save("plot_ropes",lod,SurveyRopes(lod));
            Save("market_cart",lod,MarketCart(lod));
            Save("verge_wall",lod,VergeWall(lod));
            Save("verge_flowers",lod,FlowerVerge(lod));
        }

        // Endpoints and end tangents coincide with the gameplay lane. The transverse
        // coordinate follows the bend so both wheel tracks follow the actual dirt ribbon.
        static Geometry RoadRibbon(float style,int lod)
        {
            var g=new Geometry{surface=Surface.Earth};
            int steps=lod==0?18:4;
            Vector3 P(float u,float t)
            {
                float envelope=Mathf.Sin(Mathf.PI*t);
                float bend=style>.8f?0:envelope*envelope*(style==.125f?.22f:1)*(.20f*Mathf.Sin(t*8.7f+.2f)+.13f);
                float verge=1+.043f*envelope*Mathf.Sin(t*17.1f+u*4.3f);
                return V((u-.5f)*verge+bend,0,t-.5f);
            }
            Color UV(float u,float t)=>new Color(u,t,style,0);
            for(int i=0;i<steps;i++)
            {
                float a=i/(float)steps,b=(i+1)/(float)steps;
                g.Gradient(P(0,a),P(0,b),P(1,b),UV(0,a),UV(0,b),UV(1,b));
                g.Gradient(P(0,a),P(1,b),P(1,a),UV(0,a),UV(1,b),UV(1,a));
            }
            return g;
        }

        static void SagLine(Geometry g,Vector3 a,Vector3 b,float sag,float radius,Color dye,int sides=5)
        {
            var mid=(a+b)*.5f-Vector3.up*sag;
            var size=Section(radius,radius);
            g.Loft(new[]{a,Vector3.Lerp(a,mid,.54f)-Vector3.up*sag*.17f,mid,
                Vector3.Lerp(mid,b,.46f)-Vector3.up*sag*.17f,b},new[]{size,size,size,size,size},sides,2,dye);
        }

        static Geometry SurveyRopes(int lod)
        {
            var g=new Geometry();
            var ends=new[]{V(-1.55f,0,-1.11f),V(-1.62f,0,1.13f),V(1.58f,0,1.08f),V(1.62f,0,-1.12f)};
            for(int i=0;i<4;i++)
            {
                var at=ends[i];var top=at+V((i%2-.5f)*.043f,.33f+i%2*.043f,.018f);
                if(lod!=0)
                {
                    var left=at+V(-.047f,0,-.029f);var right=at+V(.047f,0,-.029f);var rear=at+V(0,0,.057f);
                    g.Triangle(left,top,right,Walnut);g.Triangle(right,top,rear,Wood);g.Triangle(rear,top,left,WoodLight);
                    if(i<3)
                    {
                        var to=ends[i+1]+Vector3.up*.27f;var from=top-Vector3.up*.066f;
                        g.Quad(from,to,to+Vector3.up*.032f,from+Vector3.up*.032f,Linen,true);
                    }
                    continue;
                }
                g.surface=Surface.Oak;g.Cylinder(at,top,.040f,Walnut,6);g.Spear(top,top+Vector3.up*.052f,.042f,WoodLight);
                g.Cylinder(top-Vector3.up*.074f,top-Vector3.up*.038f,.045f,Linen,7);
                if(i==3)continue;
                g.surface=Surface.Cloth;
                var a=top-Vector3.up*.075f;var b=ends[i+1]+Vector3.up*(.255f+(i+1)%2*.043f);
                SagLine(g,a,b,.105f,.015f,Linen);
            }
            return g;
        }

        static Geometry Palisade(int lod)
        {
            var g=new Geometry();
            if(lod!=0){g.Box(V(0,.38f,0),V(1,.76f,.80f),Walnut);return g;}
            for(int i=0;i<15;i++)
            {
                float x=(i-7)/15f,y=.81f+.075f*Mathf.Sin(i*2.31f);
                var basePoint=V(x,0,.035f*Mathf.Sin(i*3));var top=V(x+.004f,y,basePoint.z);
                g.Frustum(basePoint,V(.065f,0,.69f),top,V(.058f,0,.64f),Pigment(Walnut,.95f+i%3*.08f),6);
                g.Frustum(top,V(.058f,0,.64f),top+V(.004f,.102f,-.06f),V(.007f,0,.052f),WoodLight,6);
            }
            for(int i=0;i<2;i++)g.Box(V(0,.32f+i*.37f,-.37f),V(1,.074f,.13f),Wood);
            return g;
        }

        static Geometry VergeWall(int lod)
        {
            var g=new Geometry{surface=Surface.Outcrop};
            if(lod!=0)
            {
                g.Frustum(V(0,0,0),V(1.56f,0,.38f),V(.012f,.34f,0),V(1.46f,0,.30f),Stonework,4);
                return g;
            }
            const int courses=3,count=5;
            for(int row=0;row<courses;row++)for(int i=0;i<count;i++)
            {
                float w=1.54f/count;
                float x=(i+.5f-count*.5f)*w+(row%2)*.039f;
                g.Bevel(V(x,(row+.5f)*.13f,(i%3-1)*.024f),V(w*.96f,.137f,.34f-row*.034f),.039f,
                    Color.Lerp(Stonework,Rock,(i*7+row)%5*.20f),Quaternion.Euler(0,i%2*7,0));
            }
            return g;
        }

        static Geometry MarketCart(int lod)
        {
            var g=new Geometry{surface=Surface.Oak};
            if(lod!=0)
            {
                g.Box(V(0,.52f,-.066f),V(.63f,.26f,.75f),Walnut);
                for(int side=-1;side<=1;side+=2)
                {
                    g.Cylinder(V(side*.30f,.27f,-.084f),V(side*.39f,.27f,-.084f),.261f,Walnut,6);
                    g.Quad(V(side*.22f,.41f,.23f),V(side*.26f,.58f,.84f),
                        V(side*.29f,.55f,.84f),V(side*.25f,.375f,.23f),WoodLight,true);
                }
                return g;
            }
            g.Box(V(0,.42f,-.066f),V(.63f,.10f,.75f),Walnut);
            for(int side=-1;side<=1;side+=2)
            {
                g.Cylinder(V(side*.255f,.38f,-.38f),V(side*.259f,.57f,.84f),.035f,Wood,5);
                for(int rail=0;rail<3;rail++)
                    g.Box(V(side*.31f,.48f+rail*.079f,-.067f),V(.052f,.064f,.75f),WoodLight);
                var wheel=new Geometry();
                wheel.Torus(Vector3.zero,.245f,.030f,Walnut,14);
                wheel.Torus(Vector3.zero,.264f,.012f,WroughtIron,14);
                const int spokes=8;
                for(int j=0;j<spokes;j++)
                {
                    float a=j*2*Mathf.PI/spokes;
                    wheel.Cylinder(Vector3.zero,V(.235f*Mathf.Cos(a),0,.235f*Mathf.Sin(a)),.015f,WoodLight,4);
                }
                g.surface=Surface.Auto;g.Append(wheel,Matrix4x4.TRS(V(side*.367f,.273f,-.084f),Quaternion.Euler(0,0,90),Vector3.one));
                g.surface=Surface.Oak;
            }
            g.surface=Surface.Cloth;
            g.Ellipsoid(V(-.11f,.59f,-.21f),V(.152f,.185f,.201f),Linen,8,3);
            g.Ellipsoid(V(.13f,.56f,.051f),V(.143f,.153f,.18f),Ochre,8,3);
            return g;
        }

        static Geometry FlowerVerge(int lod)
        {
            var g=new Geometry {surface=Surface.Leaf};
            int stems=lod==0?21:4;
            for(int j=0;j<stems;j++)
            {
                float a=j*2.39996f,r=.44f*Mathf.Sqrt((j+.5f)/stems);
                var basePoint=V(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r*.72f);
                var tip=basePoint+V(.043f*Mathf.Cos(a),.15f+.12f*Mathf.Sin(a*2)*Mathf.Sin(a*2),.029f);
                g.Triangle(basePoint-V(.014f,0,0),basePoint+V(.016f,0,0),tip,Leaf);
                int petals=lod==0?5:3;
                for(int k=0;k<petals;k++)
                {
                    float angle=2*Mathf.PI*k/petals;
                    var outwards=V(Mathf.Sin(angle),0,Mathf.Cos(angle));
                    var side=V(outwards.z,0,-outwards.x)*.014f;
                    g.Quad(tip-side,tip+outwards*.049f-side,tip+outwards*.054f+side,tip+side,
                        j%3==0?UndyedLinen:j%3==1?Ochre:new Color(.48f,.40f,.60f,0),true);
                }
            }
            return g;
        }
    }
}
