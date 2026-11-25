using System;
using Frontline.Core;
using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static void WeaponFinish(Geometry g,string type,float x)
        {
            if(type=="spear"||type=="cavalry")
            {
                int tassel=g.Mark;
                g.Quad(V(x,.052f,1.34f),V(x-.035f,.043f,1.25f),V(x-.042f,-.18f,1.14f),V(x,-.13f,1.29f),Gold,true);
                g.Quad(V(x,.05f,1.34f),V(x+.039f,.033f,1.24f),V(x+.081f,-.13f,1.17f),V(x+.029f,-.17f,1.23f),Cloth,true);
                g.WeightSince(tassel,V(x,.042f,0),V(x,-.18f,0));
            }
            else if(type=="archer"||type=="crossbow")
            {
                float tip=type=="archer"?.67f:.59f;
                g.Quad(V(x,.115f,tip-.41f),V(x+.049f,.118f,tip-.51f),V(x+.013f,.118f,tip-.37f),V(x,.115f,tip-.33f),Linen,true);
                g.Quad(V(x,.12f,tip-.41f),V(x-.045f,.125f,tip-.51f),V(x-.013f,.125f,tip-.37f),V(x,.12f,tip-.33f),Copper,true);
            }
            else if(type=="giant")
            {
                foreach(float side in new[]{-1f,1f})
                {
                    g.Quad(V(x+.18f*side,.744f,.556f),V(x+.234f*side,.743f,.551f),
                        V(x+.234f*side,.483f,.551f),V(x+.18f*side,.486f,.556f),Gold,true);
                }
            }
            else
            {
                g.Quad(V(x-.067f,-.061f,.18f),V(x+.067f,-.061f,.18f),V(x+.063f,-.08f,.18f),V(x-.063f,-.08f,.18f),Copper,true);
            }
        }

        static Geometry DistantHorse()
        {
            var g=new Geometry();
            g.Ellipsoid(V(0,-.53f,-.082f),V(.37f,.40f,.76f),Wood,8,2);
            g.Cylinder(V(0,-.50f,.36f),V(0,.10f,.72f),.22f,Wood,5);
            g.Ellipsoid(V(0,.115f,.87f),V(.19f,.25f,.36f),WoodLight,6,2);
            g.Box(V(0,-.018f,1.063f),V(.26f,.20f,.28f),Wood);
            for(int side=-1;side<=1;side+=2)
                g.Triangle(V(side*.16f,.34f,.64f),V(side*.12f,.54f,.72f),V(side*.055f,.35f,.82f),Dark);
            g.Box(V(0,-.231f,-.06f),V(.62f,.112f,.61f),Cloth);
            g.Cylinder(V(0,-.31f,-.68f),V(0,-.87f,-.98f),.076f,Dark,4);
            return g;
        }

        static void EngineIronwork(Geometry g)
        {
            foreach(float side in new[]{-1f,1f})
            {
                g.Box(V(side*.48f,.493f,.092f),V(.052f,.215f,.205f),Copper);
                g.Quad(V(side*.532f,-.28f,-.38f),V(side*.532f,-.28f,-.52f),V(side*.532f,.10f,-.18f),V(side*.532f,.113f,-.08f),Steel,true);
            }
        }

        static void RoofCourses(Geometry g,Vector3 eave,float width,float depth,float rise,Color pigment)
        {
            float half=width*.5f;
            for(int side=-1;side<=1;side+=2)
            {
                float edge=side*depth*.5f;
                for(int row=1;row<5;row++)
                {
                    float t=row*.19f,y=rise*t+.012f,z=edge*(1-t);
                    g.Quad(eave+V(-half,y,z),eave+V(half,y,z),eave+V(half,y+.024f,z-edge*.023f),
                        eave+V(-half,y+.024f,z-edge*.023f),pigment*(row%2==0?.76f:1.16f),true);
                }
                for(int tile=1;tile<7;tile++)
                {
                    float x=width*(tile/7f-.5f),t=(tile%2)*.09f;
                    g.Quad(eave+V(x,rise*t+.014f,edge*(1-t)),eave+V(x+.017f,rise*t+.014f,edge*(1-t)),
                        eave+V(x+.017f,rise*.76f+.014f,edge*.24f),eave+V(x,rise*.76f+.014f,edge*.24f),pigment*.81f,true);
                }
            }
        }

        static void GateStoneFinish(Geometry g)
        {
            foreach(int side in new[]{-1,1})
                for(int i=0;i<5;i++)
                {
                    float y=.67f+i*.42f;
                    g.Quad(V(.235f,y,side*4.133f),V(-.25f,y,side*4.133f),
                        V(-.25f,y+.028f,side*4.133f),V(.235f,y+.028f,side*4.133f),Dark,true);
                }
        }

        static Geometry BridgeFittings(int lod)
        {
            var g=new Geometry();
            float length=FrontierMap.Boundary-FrontierMap.BridgeStart;
            float width=FrontierMap.BridgeHalfWidth;
            const int spans=9;
            g.Box(V(0,-.24f,0),V(length,.48f,width*2),Wood);
            if(lod==0)
            {
                int boards=Mathf.RoundToInt(length/.50f);
                for(int i=0;i<boards;i++)
                {
                    float step=length/boards,x=(i+.5f)*step-length*.5f;
                    var stain=Color.Lerp(new Color(.42f,.29f,.19f,0),new Color(.66f,.50f,.33f,0),.4f+.39f*Mathf.Sin(i*3.13f+.8f));
                    g.Quad(V(x-step*.477f,.014f,-width),V(x-step*.477f,.014f,width),
                        V(x+step*.477f,.014f,width),V(x+step*.477f,.014f,-width),stain);
                }
            }
            for(int side=-1;side<=1;side+=2)
            {
                float z=side*(width+.075f);
                g.Box(V(0,.64f,z),V(length,.14f,.17f),WoodLight);
                g.Box(V(0,.105f,z),V(length,.19f,.29f),Wood);
                for(int i=0;i<=spans;i++)
                {
                    float x=(i/(float)spans-.5f)*(length-1.2f);
                    g.Box(V(x,-.66f,z*.91f),V(.36f,2.54f,.40f),Wood);
                    if(lod!=0)continue;
                    g.Box(V(x,.72f,z),V(.27f,.16f,.29f),WoodLight);
                    g.Quad(V(x-.139f,.49f,z-side*.18f),V(x+.139f,.49f,z-side*.18f),V(x+.139f,.63f,z-side*.18f),
                        V(x-.139f,.63f,z-side*.18f),Steel,true);
                    if(i<spans)
                    {
                        float end=x+(length-1.2f)/spans;
                        g.Box(V((x+end)*.5f,.32f,z),V(.14f,.74f,.15f),Wood);
                    }
                    if(i%3==1)
                    {
                        g.Box(V(x,-1.20f,0),V(.58f,.38f,width*1.9f),WoodLight);
                        g.Cylinder(V(x,-1.58f,0),V(x,-.33f,z*.88f),.136f,Wood,4);
                    }
                }
            }
            return g;
        }
    }
}
