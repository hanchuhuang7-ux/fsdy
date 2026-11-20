using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static void FacetedCanopy(Geometry g,bool pine)
        {
            g.Cylinder(Vector3.zero,V(.06f,2.71f,.01f),.13f,Wood,6);
            if(pine)
            {
                for(int tier=0;tier<4;tier++)
                {
                    const int tips=7;
                    float y=.88f+tier*.55f,r=1-tier*.15f,turn=tier*.72f;
                    Color shade=Color.Lerp(Green,Leaf,tier*.26f);
                    Vector3 Ring(int i,bool inner)
                    {
                        float a=i*Mathf.PI*2/tips+turn;
                        float ripple=.86f+.12f*Mathf.Cos(i*2.71f+tier);
                        return V(Mathf.Sin(a)*r*ripple*(inner?.64f:1),y+(inner?.48f:i%2*.073f),
                            Mathf.Cos(a)*r*ripple*(inner?.64f:1));
                    }
                    for(int i=0;i<tips;i++)
                    {
                        int next=(i+1)%tips;
                        var a=Ring(i,false); var b=Ring(next,false); var c=Ring(next,true); var d=Ring(i,true);
                        g.Triangle(a,b,d,shade*(i%2==0?.86f:1.02f));
                        g.Triangle(b,c,d,shade*(i%2==0?1.13f:.97f));
                        g.Triangle(d,c,V(.038f*tier,y+1.20f,-.05f),Color.Lerp(shade,Snow,.39f+.10f*tier));
                        g.Triangle(V(0,y+.14f,0),b,a,shade*.71f);
                    }
                }
                return;
            }

            for(int branch=0;branch<4;branch++)
            {
                float a=branch*2.18f;
                var tip=V(Mathf.Sin(a)*.77f,1.76f+branch*.21f,Mathf.Cos(a)*.79f);
                g.Cylinder(V(0,1.15f+branch*.22f,0),tip,.060f,WoodLight,4);
                LeafCluster(g,tip+Vector3.up*.38f,.73f,branch,Color.Lerp(Moss,Leaf,branch*.25f));
            }
            LeafCluster(g,V(-.11f,2.73f,.075f),1.06f,5,new Color(.35f,.51f,.27f,0));
        }

        static void LeafCluster(Geometry g,Vector3 center,float radius,int seed,Color leaf)
        {
            const int sides=6;
            Vector3 Ring(int i,bool high)
            {
                float angle=i*Mathf.PI/3+seed*.68f+(high?.18f:0);
                float r=radius*(.81f+.19f*Mathf.Sin(i*2.39f+seed))*(high?.87f:1);
                return center+V(Mathf.Sin(angle)*r,radius*((high?.30f:-.22f)+.20f*Mathf.Cos(i*2.09f+seed)),Mathf.Cos(angle)*r);
            }
            var top=center+V(radius*.12f,radius*.78f,-radius*.24f);
            var bottom=center+V(-radius*.12f,-radius*.62f,radius*.06f);
            for(int i=0;i<sides;i++)
            {
                int next=(i+1)%sides;
                var a=Ring(i,false); var b=Ring(next,false); var c=Ring(next,true); var d=Ring(i,true);
                g.Triangle(a,b,d,leaf*(.75f+i*.06f));
                g.Triangle(b,c,d,leaf*(1.08f-i*.045f));
                g.Triangle(d,c,top,leaf*(1.02f+i*.047f));
                g.Triangle(bottom,b,a,leaf*.73f);
            }
        }

        static Geometry DistantCatapult()
        {
            var g=new Geometry();
            foreach(float side in new[]{-.52f,.52f})
            {
                g.Box(V(side,-.47f,0),V(.18f,.22f,1.70f),Wood);
                g.Cylinder(V(side*.74f,-.38f,-.43f),V(side*.74f,.60f,0),.067f,WoodLight,4);
                g.Cylinder(V(side*.74f,-.38f,.55f),V(side*.74f,.60f,0),.067f,WoodLight,4);
                foreach(float z in new[]{-.55f,.55f})
                    g.Cylinder(V(side-.07f,-.43f,z),V(side+.07f,-.43f,z),.32f,Dark,6);
            }
            g.Box(V(0,-.42f,0),V(.91f,.15f,.92f),Wood);
            g.Box(V(0,.60f,0),V(1.02f,.13f,.13f),Steel);
            return g;
        }

        static void SiegeJoinery(Geometry g)
        {
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*.47f;
                g.Box(V(x,-.216f,.055f),V(.125f,.276f,.49f),Wood);
                g.Box(V(x,-.114f,.061f),V(.143f,.034f,.50f),Copper);
                for(int slat=0;slat<3;slat++)
                {
                    float z=.23f-slat*.162f;
                    g.Quad(V(side*.536f,-.13f,z),V(side*.537f,-.31f,z-.016f),V(side*.537f,-.31f,z+.012f),
                        V(side*.537f,-.13f,z+.027f),WoodLight,true);
                }
                g.Cylinder(V(side*.48f,.36f,.018f),V(side*.60f,.36f,.018f),.103f,Steel,6);
                g.Cylinder(V(side*.595f,.36f,.018f),V(side*.62f,.54f,-.078f),.023f,WoodLight,4);
            }
        }

        static void QuaySkiff(Geometry g)
        {
            Vector3 at=V(1.83f,-.85f,-2.68f);
            // Bow and stern converge independently of the quay's collision and walking lane.
            float[] z={-1.32f,-.88f,-.13f,.68f,1.21f};
            float[] width={0,.37f,.54f,.43f,0};
            float[] sheer={.27f,.13f,.083f,.13f,.21f};
            for(int side=-1;side<=1;side+=2)for(int i=0;i<z.Length-1;i++)
            {
                Vector3 At(int j,float t)=>at+V(side*width[j]*(.52f+.48f*t),-.21f+(sheer[j]+.21f)*t,z[j]);
                for(int strake=0;strake<3;strake++)
                {
                    float low=strake/3f,high=(strake+1)/3f;
                    g.Quad(At(i,low),At(i+1,low),At(i+1,high),At(i,high),
                        strake==2?WoodLight:strake==1?Copper:Wood,true);
                }
                g.Cylinder(At(i,1),At(i+1,1),.031f,Wood,4);
            }
            for(int i=0;i<z.Length-1;i++)
            {
                float left=width[i]*.69f,right=width[i+1]*.69f;
                g.Quad(at+V(-left,-.09f,z[i]),at+V(-right,-.09f,z[i+1]),
                    at+V(right,-.09f,z[i+1]),at+V(left,-.09f,z[i]),i%2==0?Wood:WoodLight);
            }
            g.Box(at+V(0,.028f,-.55f),V(.79f,.052f,.18f),WoodLight);
            g.Box(at+V(0,.027f,.55f),V(.80f,.052f,.17f),WoodLight);
            g.Cylinder(at+V(-.19f,.072f,.88f),at+V(.34f,.118f,-.92f),.025f,Linen,4);
            g.Box(at+V(.36f,.119f,-.95f),V(.14f,.036f,.34f),WoodLight);
            var bollard=V(1.25f,.16f,-1.58f);
            g.Cylinder(bollard,bollard+Vector3.up*.27f,.095f,Wood,6);
            g.Cylinder(bollard+Vector3.up*.23f,bollard+Vector3.up*.282f,.121f,Steel,6);
            var slack=V(1.41f,-.25f,-1.54f);
            g.Cylinder(bollard+Vector3.up*.22f,slack,.024f,Linen,4);
            var mooring=at+V(0,.238f,1.167f);
            g.Cylinder(slack,mooring,.024f,Linen,4);
            g.Cylinder(mooring-Vector3.up*.068f,mooring+Vector3.up*.026f,.046f,Wood,5);
        }
    }
}
