using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static void SiegeTackle(Geometry g)
        {
            foreach(float x in new[]{-.56f,.56f})foreach(float z in new[]{-.55f,.55f})
            {
                var wheel=new Geometry();
                wheel.surface=Surface.Metal;
                wheel.Torus(Vector3.zero,.313f,.018f,WroughtIron,16);
                wheel.Cylinder(V(0,-.081f,0),V(0,.08f,0),.089f,Steel,8);
                int studs=8;
                for(int j=0;j<studs;j++)
                {
                    float a=2*Mathf.PI*j/studs;
                    wheel.Ellipsoid(V(Mathf.Cos(a)*.275f,.032f,Mathf.Sin(a)*.275f),Vector3.one*.019f,OldGold,6,2);
                }
                g.Append(wheel,Matrix4x4.TRS(V(x,-.43f,z),Quaternion.Euler(0,0,90),Vector3.one));
            }
            var axle=V(0,-.155f,-.42f);
            g.Cylinder(axle-Vector3.right*.43f,axle+Vector3.right*.43f,.103f,Walnut,10);
            for(int j=0;j<8;j++)
            {
                var band=new Geometry();
                band.Torus(Vector3.zero,.111f,.013f,UndyedLinen,12);
                g.Append(band,Matrix4x4.TRS(axle+Vector3.right*(-.141f+j*.042f),Quaternion.Euler(0,0,90),Vector3.one));
            }
            foreach(int s in new[]{-1,1})
            {
                g.Box(V(s*.393f,.343f,.037f),V(.034f,.145f,.175f),WroughtIron);
                SagLine(g,axle+V(s*.145f,.088f,0),V(s*.28f,.485f,.053f),.042f,.015f,Linen);
            }
        }

        static void ThrowingPouch(Geometry g)
        {
            var center=V(0,.092f,-1.19f);
            g.surface=Surface.Hide;
            const int sides=12;
            for(int i=0;i<sides;i++)
            {
                Vector3 Rim(int k)
                {
                    float a=k*2*Mathf.PI/sides;
                    return center+V(Mathf.Cos(a)*.216f,.103f+.018f*Mathf.Sin(a*3),Mathf.Sin(a)*.238f);
                }
                var a=Rim(i);var b=Rim(i+1);var bottom=center-Vector3.up*.08f;
                g.Quad(a,b,Vector3.Lerp(bottom,b,.43f),Vector3.Lerp(bottom,a,.43f),Pigment(Walnut,.90f+i%2*.10f),true);
                g.Triangle(bottom,Vector3.Lerp(bottom,b,.44f),Vector3.Lerp(bottom,a,.44f),Walnut);
            }
            for(int s=-1;s<=1;s+=2)
                SagLine(g,V(s*.06f,.10f,-.71f),center+V(s*.183f,.109f,-.057f),.032f,.013f,UndyedLinen);
            g.surface=Surface.Auto;
        }

        static void PropFinish(Geometry g,string kind)
        {
            switch(kind)
            {
                case "barrel":
                    for(int i=0;i<8;i++)
                    {
                        float a=(i+.5f)*Mathf.PI*.25f;
                        var p=V(.419f*Mathf.Cos(a),.335f,.419f*Mathf.Sin(a));
                        g.Ellipsoid(p,Vector3.one*.012f,WroughtIron,6,2);
                    }
                    g.Torus(V(0,1.098f,0),.30f,.009f,Walnut,12);break;
                case "barricade":
                    for(int j=-1;j<=1;j++)
                    {
                        var joint=V(0,.69f,j*1.06f);
                        for(int i=0;i<3;i++)
                            g.Cylinder(joint+V(-.148f,-.092f+i*.034f,0),joint+V(.145f,.073f+i*.034f,.02f),.023f,Linen,5);
                    }
                    break;
                case "banner":
                    g.Torus(V(0,2.05f,0),.061f,.016f,OldGold,10);
                    g.Loft(new[]{V(.025f,2.18f,0),V(.098f,2.02f,.031f),V(.062f,1.89f,.064f)},
                        new[]{Section(.011f,.012f),Section(.012f,.012f),Section(.022f,.027f)},6,2,OldGold);
                    break;
                case "spikes":case "spring":
                    foreach(float x in new[]{-.64f,.64f})foreach(float z in new[]{-.64f,.64f})
                        g.Ellipsoid(V(x,kind=="spring"?.716f:.158f,z),V(.043f,.020f,.044f),WroughtIron,7,2);
                    break;
            }
        }

        static void HandFinish(Geometry g,string kind)
        {
            switch(kind)
            {
                case "hoe":case "sickle":
                    for(int i=0;i<3;i++)g.Torus(V(0,-.078f+i*.042f,0),.035f,.008f,Walnut,7);
                    g.Torus(V(0,kind=="hoe"?.655f:.348f,0),.037f,.012f,WroughtIron,8);break;
                case "jug":
                    var r=Section(.019f,.021f);
                    g.Loft(new[]{V(.085f,-.018f,0),V(.189f,-.059f,0),V(.193f,-.148f,0),V(.133f,-.201f,0)},new[]{r,r,r,r},7,2,Copper);
                    g.Torus(V(0,.090f,0),.075f,.012f,FiredClay,10);break;
                case "crate":
                    g.Cylinder(V(-.162f,-.23f,-.159f),V(.156f,-.018f,-.159f),.020f,Walnut,4);break;
                case "bucket":case "basket":
                    bool wicker=kind=="basket";
                    int bands=wicker?5:2;
                    for(int j=0;j<bands;j++)
                    {
                        float t=(j+.5f)/bands;
                        g.Torus(V(0,-.34f+.23f*t,0),Mathf.Lerp(wicker?.148f:.11f,wicker?.194f:.143f,t),.008f,
                            wicker?WoodLight:WroughtIron,12);
                    }
                    break;
            }
        }

        static void WorkerArms(Geometry g,Vector3 joint,Vector3 hand,Color coat)
        {
            var elbow=Vector3.Lerp(joint,hand,.55f)+V(Mathf.Sign(joint.x)*.054f,-.059f,-.042f);
            var cuff=Vector3.Lerp(elbow,hand,.78f);
            g.surface=Surface.Cloth;
            g.Loft(new[]{joint,Vector3.Lerp(joint,elbow,.47f)+V(0,.012f,0),elbow,cuff,hand},
                new[]{Section(.103f,.099f),Section(.088f,.084f),Section(.077f,.076f),
                    Section(.058f,.065f),Section(.044f,.046f)},8,2,coat);
            g.surface=Surface.Hide;
            g.Ellipsoid(cuff,V(.064f,.051f,.061f),Walnut,8,3);
            g.surface=Surface.Auto;
        }

        static void ApronPanel(Geometry g,bool farmer)
        {
            for(int k=0;k<7;k++)
            {
                float a=k/7f,b=(k+1)/7f;
                Vector3 P(float u,float v)=>V((u-.5f)*Mathf.Lerp(.28f,.381f,v),-.117f-.305f*v+.028f*Mathf.Abs(u-.5f),
                    .18f+v*.042f+.023f*Mathf.Sin(14*u)*v);
                g.surface=farmer?Surface.Hide:Surface.Cloth;
                g.Quad(P(a,0),P(b,0),P(b,1),P(a,1),farmer?Walnut:Indigo,true);
            }
            g.surface=Surface.Auto;
        }
    }
}
