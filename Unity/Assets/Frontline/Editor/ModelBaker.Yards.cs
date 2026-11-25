using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static Geometry PavedCourt()
        {
            var g=new Geometry{surface=Surface.Earth};
            g.Bevel(V(0,.017f,0),V(13.14f,.030f,6.46f),.014f,Pigment(Earth,.84f));
            g.surface=Surface.Outcrop;
            const int rows=13,columns=22;
            const float half=6.54f,d=6.41f/rows,w=2*half/columns;
            for(int row=0;row<rows;row++)for(int col=-1;col<columns;col++)
            {
                float edge=col*w-half+(row%2)*w*.50f;
                float l=Mathf.Max(edge+.014f,-half),r=Mathf.Min(edge+w-.016f,half);
                if(r-l<.036f)continue;
                float b=-3.205f+row*d+.013f,t=b+d-.024f;
                float h=.043f+(row*17+col*7+23)%4*.002f;
                var tone=Color.Lerp(new Color(.36f,.40f,.395f,0),new Color(.60f,.59f,.529f,0),
                    ((row*37+col*13+33)%11)*.096f);
                var a=V(l+.024f,h,b);var c=V(r-.019f,h,t);
                g.Quad(a,V(l,h,t-.023f),c,V(r,h,b+.018f),tone);
                g.Quad(V(l,h-.014f,t-.023f),V(l,h,t-.023f),a,a-Vector3.up*.014f,Pigment(tone,.72f),true);
            }
            for(int side=-1;side<=1;side+=2)for(int i=0;i<21;i++)
                g.Bevel(V(-6.23f+i*.624f,.045f,side*3.22f),V(.60f,.074f,.16f),.018f,Stonework);
            return g;
        }

        static Geometry CanvasSails()
        {
            var g=new Geometry();
            for(int blade=0;blade<4;blade++)
            {
                var wing=new Geometry();
                wing.surface=Surface.Oak;
                wing.Cylinder(Vector3.zero,V(.008f,1.053f,-.022f),.030f,Walnut,7);
                wing.Cylinder(V(-.19f,.28f,-.021f),V(-.26f,1.04f,-.060f),.017f,Wood,6);
                const int panels=6;
                for(int j=0;j<panels;j++)
                {
                    float a=j/(float)panels,b=(j+1)/(float)panels;
                    Vector3 P(float u,float t)=>V(-u*(.19f+.071f*t),.28f+.75f*t,
                        -.022f-.046f*t*u-.091f*Mathf.Sin(u*Mathf.PI)*(.46f+.54f*Mathf.Sin(t*Mathf.PI)));
                    wing.surface=Surface.Cloth;
                    for(int k=0;k<3;k++)wing.Quad(P(k/3f,a),P((k+1)/3f,a),P((k+1)/3f,b),P(k/3f,b),
                        Pigment(UndyedLinen,.89f+.071f*(j%2)),true);
                    wing.surface=Surface.Oak;wing.Cylinder(P(0,b)+Vector3.forward*.016f,P(1,b)+Vector3.forward*.016f,.013f,WoodLight,5);
                }
                g.Append(wing,Matrix4x4.Rotate(Quaternion.AngleAxis(90*blade,Vector3.forward)));
            }
            g.Cylinder(V(0,0,-.086f),V(0,0,.064f),.123f,WroughtIron,12);
            return g;
        }

        static void BackyardFinish(Geometry g,string kind)
        {
            switch(kind)
            {
                case "garden":
                    for(int i=0;i<5;i++)
                    {
                        var p=V(-.23f+i*.119f,.09f,.15f);
                        for(int j=0;j<4;j++)
                        {
                            float a=j*1.57f+i;
                            var side=V(Mathf.Sin(a),0,Mathf.Cos(a));
                            var tangent=V(side.z,0,-side.x)*.019f;
                            g.Quad(p,p+tangent+side*.051f+Vector3.up*.046f,p+side*.096f+Vector3.up*.039f,
                                p-tangent+side*.051f+Vector3.up*.046f,Leaf,true);
                        }
                    }
                    break;
                case "poultry":
                    for(int i=0;i<4;i++)g.Box(V(-.111f,.101f+i*.022f,-.222f+i*.018f),V(.144f,.017f,.030f),Walnut);
                    g.Torus(V(.22f,.128f,.121f),.077f,.018f,StrawLight,8);
                    break;
                case "workshop":
                    var shuttle=V(.052f,.205f,-.069f);
                    g.Loft(new[]{shuttle+V(-.14f,0,0),shuttle,shuttle+V(.14f,.014f,0)},
                        new[]{Section(.008f,.011f),Section(.038f,.030f),Section(.009f,.009f)},6,2,Walnut);
                    g.Torus(V(-.224f,.102f,-.13f),.056f,.025f,Indigo,9);
                    break;
                case "militia_yard":
                    var buckle=V(-.18f,.25f,-.135f);
                    g.Ellipsoid(buckle,V(.053f,.054f,.028f),Copper,8,3);
                    for(int k=0;k<7;k++)
                    {
                        float a=k*Mathf.PI*2/7;
                        g.Ellipsoid(buckle+V(.132f*Mathf.Cos(a),.132f*Mathf.Sin(a),.030f),Vector3.one*.010f,OldGold,5,2);
                    }
                    break;
            }
        }

        static void MarketGoods(Geometry g)
        {
            for(int box=0;box<2;box++)
            {
                var at=V(-.04f+box*.49f,.819f,-.39f);
                foreach(int side in new[]{-1,1})
                {
                    g.Box(at+V(0,.067f,side*.105f),V(.42f,.117f,.029f),WoodLight);
                    g.Box(at+V(side*.202f,.067f,0),V(.032f,.116f,.23f),Walnut);
                }
                for(int j=0;j<7;j++)
                {
                    var fruit=at+V((j%4-1.5f)*.092f,.135f+(j/4)*.018f,(j/4-.5f)*.094f);
                    g.Ellipsoid(fruit,box==0?V(.056f,.055f,.055f):V(.062f,.071f,.053f),
                        box==0?Madder:Leaf,7,3);
                }
            }
        }
    }
}
