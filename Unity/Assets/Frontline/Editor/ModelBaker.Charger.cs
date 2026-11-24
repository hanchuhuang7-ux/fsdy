using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static Vector2 Section(float width,float depth)=>new Vector2(width,depth);

        static Geometry Charger()
        {
            var g=new Geometry {surface=Surface.Hide};
            var bay=new Color(.425f,.239f,.148f,0);
            g.Loft(new[]{V(0,-.54f,-.81f),V(0,-.51f,-.64f),V(0,-.50f,-.35f),V(0,-.54f,.04f),
                    V(0,-.52f,.32f),V(0,-.43f,.56f),V(0,-.34f,.61f)},
                new[]{Section(.08f,.14f),Section(.29f,.34f),Section(.36f,.37f),Section(.338f,.37f),
                    Section(.31f,.395f),Section(.274f,.305f),Section(.18f,.20f)},14,2,bay);
            g.Loft(new[]{V(0,-.41f,.38f),V(0,-.19f,.535f),V(0,.015f,.59f),V(0,.21f,.689f),V(0,.239f,.799f)},
                new[]{Section(.26f,.29f),Section(.214f,.275f),Section(.161f,.244f),Section(.134f,.173f),Section(.129f,.13f)},12,3,bay);
            g.Loft(new[]{V(0,.229f,.66f),V(0,.252f,.746f),V(0,.218f,.878f),V(0,.121f,.975f),V(0,.035f,1.13f),V(0,.029f,1.21f)},
                new[]{Section(.087f,.097f),Section(.139f,.148f),Section(.153f,.133f),Section(.118f,.124f),
                    Section(.093f,.089f),Section(.083f,.062f)},12,2,Pigment(bay,1.08f));
            for(int side=-1;side<=1;side+=2)
            {
                var ear=V(side*.098f,.338f,.705f);
                g.Loft(new[]{ear,ear+V(side*.017f,.097f,-.018f),ear+V(side*.043f,.202f,-.034f)},
                    new[]{Section(.044f,.055f),Section(.043f,.036f),Section(.004f,.006f)},7,2,bay);
                g.Ellipsoid(V(side*.147f,.245f,.834f),V(.017f,.027f,.035f),Dark,8,3);
                g.Ellipsoid(V(side*.091f,.049f,1.153f),V(.008f,.022f,.035f),Dark,7,3);
                var rein=new[]{V(side*.101f,.071f,1.086f),V(side*.164f,.026f,.875f),
                    V(side*.219f,-.067f,.40f),V(side*.159f,.097f,.037f)};
                g.Loft(rein,new[]{Section(.012f,.014f),Section(.012f,.014f),Section(.009f,.012f),Section(.009f,.012f)},5,2,Walnut);
            }
            var hair=new Color(.19f,.14f,.122f,0);
            g.Loft(new[]{V(0,-.20f,.18f),V(-.031f,-.02f,.31f),V(-.044f,.183f,.42f),
                    V(-.041f,.323f,.566f),V(0,.379f,.72f),V(.032f,.319f,.817f)},
                new[]{Section(.05f,.05f),Section(.072f,.084f),Section(.062f,.094f),Section(.055f,.074f),
                    Section(.042f,.041f),Section(.012f,.019f)},8,2,hair);
            g.Loft(new[]{V(0,-.34f,-.66f),V(.027f,-.45f,-.84f),V(.063f,-.71f,-.94f),V(.109f,-.96f,-.986f)},
                new[]{Section(.045f,.048f),Section(.091f,.12f),Section(.102f,.125f),Section(.029f,.035f)},8,2,hair);
            // Caparison panels drape from the spine over the flank and clear the four animated hocks.
            g.surface=Surface.Cloth;
            for(int side=-1;side<=1;side+=2)for(int j=0;j<8;j++)for(int k=0;k<3;k++)
            {
                Vector3 ClothPoint(int col,int row)
                {
                    float u=col/8f,v=row/3f;
                    float z=Mathf.Lerp(-.68f,.37f,u);
                    float barrel=.81f+.19f*Mathf.Sin(u*Mathf.PI);
                    return V(side*(.145f+.256f*Mathf.Sin(v*2.04f)+.015f*Mathf.Cos(u*17)*v)*barrel,
                        -.095f-.554f*v+.07f*(u-.5f)*(u-.5f)+.049f*Mathf.Sin(u*9)*v*v,z);
                }
                var dye=k==2&&(j%4==0||j%4==3)?OldGold:DyedWool;
                g.Quad(ClothPoint(j,k),ClothPoint(j+1,k),ClothPoint(j+1,k+1),ClothPoint(j,k+1),dye,true);
            }
            g.surface=Surface.Hide;
            g.Bevel(V(0,-.143f,-.045f),V(.46f,.126f,.49f),.051f,Walnut);
            g.Ellipsoid(V(0,-.089f,-.233f),V(.219f,.093f,.078f),Walnut,10,4);
            g.surface=Surface.Auto;
            for(int side=-1;side<=1;side+=2)
            {
                g.surface=Surface.Hide;
                g.Cylinder(V(side*.20f,-.17f,.064f),V(side*.385f,-.652f,.036f),.018f,Walnut,6);
                g.surface=Surface.Metal;
                g.Cylinder(V(side*.385f,-.652f,-.049f),V(side*.385f,-.652f,.113f),.022f,Steel,7);
            }
            g.surface=Surface.Auto;
            for(int side=-1;side<=1;side+=2)
                g.Cylinder(V(side*.113f,.11f,.961f),V(side*.122f,.22f,.797f),.023f,OldGold,6);
            return g;
        }

        static Geometry ChargerLeg()
        {
            var g=new Geometry {surface=Surface.Hide};
            g.Loft(new[]{V(0,.174f,-.008f),V(0,.028f,0),V(0,-.136f,-.018f),V(0,-.283f,.021f),
                    V(0,-.35f,.039f),V(0,-.48f,.029f),V(0,-.557f,.058f),V(0,-.626f,.084f)},
                new[]{Section(.113f,.131f),Section(.096f,.106f),Section(.068f,.093f),Section(.064f,.073f),
                    Section(.072f,.084f),Section(.047f,.052f),Section(.061f,.062f),Section(.059f,.062f)},8,2,Wood);
            g.Frustum(V(0,-.699f,.084f),V(.179f,0,.195f),V(0,-.598f,.065f),V(.119f,0,.131f),Dark,8);
            return g;
        }
    }
}
