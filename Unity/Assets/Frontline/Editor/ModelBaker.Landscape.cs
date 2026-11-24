using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {

        static void WoodlandTree(Geometry g,bool conifer)
        {
            var bark=Pigment(Walnut,1.14f);
            var basePoint=V(-.041f,0,0);
            var fork=conifer?V(.038f,1.58f,.04f):V(.12f,1.38f,-.06f);
            var crown=conifer?V(.05f,3.66f,.09f):V(-.21f,2.97f,.10f);
            g.surface=Surface.Oak;
            g.Loft(new[]{basePoint,V(-.08f,.58f,.045f),fork,V(.033f,2.13f,.07f),crown},
                new[]{new Vector2(.222f,.20f),new Vector2(.143f,.146f),new Vector2(.115f,.10f),
                    new Vector2(.079f,.066f),new Vector2(.016f,.018f)},8,2,bark);
            for(int k=0;k<5;k++)
            {
                float a=k*2.4f;
                var outward=V(Mathf.Cos(a),0,Mathf.Sin(a));
                g.Loft(new[]{basePoint+Vector3.up*.25f,basePoint+outward*.21f+Vector3.up*.091f,basePoint+outward*.46f},
                    new[]{new Vector2(.125f,.095f),new Vector2(.093f,.065f),new Vector2(.015f,.011f)},5,1,bark);
            }
            int branches=conifer?12:7;
            for(int k=0;k<branches;k++)
            {
                float t=k/(float)branches,a=k*2.39996f+.34f;
                float reach=conifer?.81f*(1-.63f*t):.88f*(1-.35f*t);
                var outwards=V(Mathf.Sin(a),0,Mathf.Cos(a));
                var axil=Vector3.Lerp(fork,crown,t*.86f);
                var end=axil+outwards*reach+Vector3.up*(conifer?-.17f:.49f);
                g.surface=Surface.Oak;
                g.Loft(new[]{axil,axil+outwards*(reach*.37f)+Vector3.up*.08f,end},
                    new[]{new Vector2(.076f,.073f),new Vector2(.049f,.041f),new Vector2(.020f,.015f)},5,1,bark);
                g.surface=Surface.Leaf;
                var dye=conifer?Color.Lerp(new Color(.19f,.31f,.215f,.4f),new Color(.39f,.47f,.30f,.47f),t):
                    Color.Lerp(new Color(.295f,.405f,.195f,.51f),new Color(.53f,.57f,.302f,.58f),t);
                var radii=conifer?V(.60f,.43f,.61f)*(1-.49f*t):V(.71f,.61f,.70f)*(1-.17f*t);
                LeafCrown(g,end+Vector3.up*(conifer?.20f:.14f),radii,k,dye);
            }
            g.surface=Surface.Leaf;
            LeafCrown(g,crown,conifer?V(.30f,.51f,.31f):V(.69f,.53f,.68f),18,
                conifer?new Color(.38f,.47f,.31f,.5f):new Color(.49f,.58f,.32f,.56f));
            g.surface=Surface.Auto;
        }

        // Shading normals come from the surrounding botanical crown, not the tiny boundary lobes.
        static void LeafCrown(Geometry g,Vector3 center,Vector3 extent,int seed,Color leaf)
        {
            const int sides=9,bands=4;
            Vector3 Point(int k,int j)
            {
                k%=sides;
                float lat=(j/(float)bands-.5f)*Mathf.PI;
                float a=k*(2*Mathf.PI/sides)+seed*.72f+j*.27f;
                float r=(1+.16f*Mathf.Sin(k*4.01f+j*2.68f+seed))*Mathf.Cos(lat);
                return center+Vector3.Scale(extent,V(r*Mathf.Sin(a),Mathf.Sin(lat)+.074f*r*Mathf.Sin(a*3+seed),r*Mathf.Cos(a)));
            }
            Vector3 Normal(Vector3 p)
            {
                var n=p-center;
                return V(n.x/extent.x,n.y/extent.y+.22f,n.z/extent.z).normalized;
            }
            Color Tone(Vector3 p)=>Pigment(leaf,.88f+.16f*Normal(p).y+.032f*Mathf.Sin(7*p.x+4*p.z));
            void Face(Vector3 a,Vector3 b,Vector3 c)=>g.SmoothFace(a,b,c,Tone(a),Tone(b),Tone(c),Normal(a),Normal(b),Normal(c));
            for(int j=0;j<bands;j++)for(int k=0;k<sides;k++)
            {
                var a=Point(k,j);var b=Point(k+1,j);var c=Point(k+1,j+1);var d=Point(k,j+1);
                if(j>0)Face(a,b,c);
                if(j+1<bands)Face(a,c,d);
            }

            // Leaf sprays break the silvery outline into individual leaves. Their normals continue
            // the crown lighting so that strips at the silhouette never turn into black cutouts.
            for(int spray=0;spray<14;spray++)
            {
                float a=spray*2.39996f+seed*.61f,level=-.36f+.13f*(spray%7);
                var radial=V(Mathf.Cos(a),level,Mathf.Sin(a)).normalized;
                var tip=center+Vector3.Scale(extent,radial)*(1.04f+.10f*Mathf.Sin(spray+seed));
                var tangent=V(-radial.z,0,radial.x);
                for(int blade=0;blade<5;blade++)
                {
                    float s=blade/4f;
                    var foot=tip+Vector3.Scale(extent,tangent)*((s-.5f)*.64f)-radial*.065f;
                    var direction=(radial+tangent*(s-.5f)*1.8f+Vector3.down*.48f).normalized;
                    float length=(.13f+.05f*Mathf.Sin(spray*3f+blade))*Mathf.Min(extent.x,extent.z)+.051f;
                    var width=(tangent+Vector3.up*(.41f-.13f*blade)).normalized*(length*.38f);
                    var end=foot+direction*length*1.53f;
                    var ridge=foot+direction*length*.60f+Vector3.up*.017f;
                    var lo=ridge-width;var hi=ridge+width;
                    Face(foot,lo,end);Face(foot,end,hi);Face(end,lo,foot);Face(hi,end,foot);
                }
            }
        }

        static void MeadowTuft(Geometry g,bool reed)
        {
            g.surface=Surface.Leaf;
            int count=reed?7:10;
            for(int i=0;i<count;i++)
            {
                float a=i*2.39996f,r=.034f+(i%4)*.039f;
                var direction=V(Mathf.Sin(a),0,Mathf.Cos(a));var across=V(direction.z,0,-direction.x)*(.018f+(i%3)*.006f);
                var root=direction*r;
                float h=reed?.74f+.081f*(i%5):.23f+.047f*(i%4);
                var mid=root+direction*h*.24f+Vector3.up*h*.59f;
                var tip=root+direction*h*.69f+Vector3.up*h;
                var tint=Color.Lerp(Moss,new Color(.53f,.57f,.29f,.3f),i/(float)count);
                var normal=(direction+Vector3.up*1.7f).normalized;
                void Blade(Vector3 p,Vector3 q,Vector3 s)
                {
                    g.SmoothFace(p,q,s,tint,tint,tint,normal,normal,normal);
                    g.SmoothFace(s,q,p,tint,tint,tint,normal,normal,normal);
                }
                Blade(root-across,root+across,mid+across*.66f);
                Blade(root-across,mid+across*.66f,mid-across*.66f);
                Blade(mid-across*.66f,mid+across*.66f,tip);
                if(reed&&i%3==0)g.Cylinder(tip,tip+Vector3.up*.12f,.023f,Walnut,5);
            }
            g.surface=Surface.Auto;
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
