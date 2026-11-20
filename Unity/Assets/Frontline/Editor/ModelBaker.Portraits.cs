using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static readonly Color[] Complexions={
            new Color(.87f,.66f,.48f,0),new Color(.74f,.50f,.33f,0),new Color(.63f,.42f,.29f,0),
            new Color(.90f,.72f,.56f,0)
        };
        static readonly Color[] HairDyes={
            new Color(.23f,.14f,.09f,0),new Color(.40f,.22f,.12f,0),new Color(.16f,.13f,.12f,0),
            new Color(.55f,.44f,.32f,0)
        };
        static readonly float[] FaceHeights={-.91f,-.49f,-.02f,.55f,.88f};
        static readonly float[] FaceWidths={.54f,.86f,1,.96f,.61f};
        static readonly float[] FaceDepths={.62f,.86f,1,.94f,.67f};

        static void FaceBust(Geometry g,Vector3 at,Vector3 size,int style,bool helmet,bool collar=true)
        {
            Color skin=Complexions[style%Complexions.Length],hair=HairDyes[style%HairDyes.Length];
            Vector3 P(float x,float y,float z)=>at+Vector3.Scale(V(x,y,z),size);
            Vector3 Rim(int side,int ring)
            {
                float a=(side%8)*Mathf.PI*.25f;
                return P(Mathf.Sin(a)*FaceWidths[ring],FaceHeights[ring],Mathf.Cos(a)*FaceDepths[ring]);
            }
            for(int side=0;side<8;side++)
            {
                for(int row=0;row<4;row++)
                {
                    Color shade=side==2||side==5?skin*.91f:skin;
                    if(row==3&&side!=0&&side!=7||row==2&&side>=2&&side<=5)shade=hair;
                    else if(row==0)shade=style%3==0?hair:skin*.84f;
                    g.Quad(Rim(side,row),Rim(side+1,row),Rim(side+1,row+1),Rim(side,row+1),shade);
                }
                g.Triangle(P(0,-.96f,.06f),Rim(side+1,0),Rim(side,0),skin*.87f);
                g.Triangle(P(0,.98f,-.045f),Rim(side,4),Rim(side+1,4),hair);
            }
            foreach(int side in new[]{-1,1})
            {
                // Eyes and brows sit just above the cheek facets, under the helmet or hairline.
                float x=side*.36f;
                Vector3 Inner(float y)=>P(x-side*.18f,y,.94f);
                Vector3 Outer(float y)=>P(x+side*.20f,y,.79f);
                g.Quad(Inner(.23f),Outer(.29f),Outer(.15f),Inner(.11f),hair,true);
                g.Quad(Inner(.072f),Outer(.12f),Outer(.008f),Inner(-.043f),skin*.45f,true);
                g.Triangle(P(x-side*.11f,.039f,.936f),P(x+side*.107f,.066f,.862f),P(x,-.014f,.910f),skin*1.17f);
                g.Triangle(P(x-side*.039f,.064f,.916f),P(x+side*.063f,.073f,.878f),P(x,-.016f,.918f),Dark);
                g.Triangle(P(side*.32f,-.20f,.891f),P(side*.689f,-.266f,.729f),
                    P(side*.48f,-.46f,.824f),skin*1.04f);
                if(!helmet)g.Triangle(P(side*.86f,.19f,.46f),P(side*1.07f,-.04f,.35f),
                    P(side*.78f,-.40f,.47f),skin*.91f);
            }
            g.Triangle(P(0,.255f,.99f),P(.18f,-.29f,1.015f),P(0,-.20f,1.265f),skin*1.10f);
            g.Triangle(P(0,.255f,.99f),P(0,-.20f,1.265f),P(-.18f,-.29f,1.015f),skin*.91f);
            g.Triangle(P(0,-.20f,1.265f),P(.18f,-.29f,1.015f),P(-.18f,-.29f,1.015f),skin*.76f);
            g.Quad(P(-.25f,-.49f,.822f),P(.25f,-.49f,.822f),P(.14f,-.57f,.84f),P(-.12f,-.57f,.84f),skin*.52f,true);
            if(style%3!=1)
            {
                g.Triangle(P(-.45f,-.38f,.75f),P(0,-.34f,.963f),P(-.14f,-.51f,.91f),hair);
                g.Triangle(P(0,-.34f,.963f),P(.46f,-.38f,.75f),P(.12f,-.52f,.912f),hair);
                g.Triangle(P(-.29f,-.61f,.734f),P(.29f,-.61f,.734f),P(0,-.85f,.77f),hair);
            }
            if(collar)
            {
                g.Frustum(P(0,-1.32f,0),V(.21f,0,.18f),P(0,-.76f,.02f),V(.19f,0,.16f),skin*.79f,6);
                g.Quad(P(-.63f,-1.19f,.44f),P(.64f,-1.19f,.44f),P(.82f,-1.44f,.58f),P(-.82f,-1.44f,.58f),
                    helmet?Steel:Linen,true);
            }
        }

        static void SpecialistRegalia(Geometry g,int role,Color coat)
        {
            if(role==0)
            {
                g.Quad(V(-.22f,.185f,.163f),V(-.019f,.061f,.226f),V(.21f,.19f,.17f),V(.002f,.025f,.235f),Gold,true);
                g.Quad(V(-.122f,.054f,.459f),V(.141f,.054f,.459f),V(.141f,.031f,.461f),V(-.122f,.031f,.461f),Gold,true);
                g.Box(V(.02f,.037f,.464f),V(.035f,.062f,.01f),Steel);
            }
            else if(role==1)
            {
                for(int side=-1;side<=1;side+=2)for(int tuft=0;tuft<5;tuft++)
                {
                    float x=side*(.078f+tuft*.034f),y=.18f+tuft*.023f;
                    g.Triangle(V(x,y,.211f),V(x+side*.043f,y+.025f,.175f),V(x+side*.019f,y-.071f,.206f),
                        tuft%2==0?Linen:Plaster*.78f);
                }
                g.Barrel(V(-.227f,-.24f,.202f),.08f,.16f,Ochre,5);
                g.Box(V(-.222f,-.19f,.255f),V(.067f,.045f,.025f),Gold);
            }
            else if(role==2)
            {
                g.Quad(V(-.13f,.28f,.17f),V(-.08f,.29f,.19f),V(.23f,-.17f,.205f),V(.203f,-.19f,.21f),Wood,true);
                g.Box(V(.26f,-.26f,.181f),V(.17f,.20f,.115f),Copper);
                for(int sprig=0;sprig<3;sprig++)
                {
                    float x=.22f+sprig*.029f;
                    g.Triangle(V(x,-.192f,.237f),V(x-.041f,-.086f+sprig*.012f,.238f),
                        V(x+.024f,-.127f+sprig*.018f,.261f),sprig==1?Moss:Leaf);
                }
            }
            else if(role==3)
            {
                g.Quad(V(-.15f,.225f,.216f),V(.15f,.225f,.216f),V(.114f,-.23f,.239f),V(-.12f,-.23f,.234f),Indigo,true);
                HeraldicKite(g,0,.045f,.246f,.09f,.124f,Gold);
            }
            else
            {
                g.Quad(V(-.19f,.255f,.17f),V(.14f,.262f,.18f),V(.18f,.143f,.21f),V(-.21f,.15f,.187f),coat*1.29f,true);
                g.Box(V(.17f,.178f,.213f),V(.057f,.053f,.021f),role==4?Copper:Gold);
            }
        }

        static void HeraldicKite(Geometry g,float x,float y,float z,float width,float height,Color metal)
        {
            g.Triangle(V(x,y+height,z),V(x-width,y+.021f,z),V(x,y-height,z+.006f),metal*.77f);
            g.Triangle(V(x,y+height,z),V(x,y-height,z+.006f),V(x+width,y+.021f,z),metal);
        }

        static Geometry ArmamentCrest(string hero,int lod)
        {
            var g=new Geometry();
            bool vanguard=hero=="hero_vanguard",ranger=hero=="hero_ranger";
            Color metal=new Color(.96f,.96f,.90f,1);
            float z=vanguard?.33f:ranger?.24f:.315f,y=vanguard?.13f:ranger?.152f:.21f;
            HeraldicKite(g,0,y,z,.077f,.095f,metal);
            if(lod!=0)return g;
            for(int side=-1;side<=1;side+=2)
            {
                float s=side;
                if(vanguard)
                {
                    g.Quad(V(s*.075f,y+.012f,z),V(s*.199f,y+.082f,z-.02f),V(s*.173f,y+.13f,z-.024f),
                        V(s*.068f,y+.052f,z+.003f),metal,true);
                    g.Quad(V(s*.263f,.353f,.245f),V(s*.397f,.361f,.245f),V(s*.373f,.305f,.246f),
                        V(s*.277f,.298f,.246f),metal,true);
                }
                else if(ranger)
                {
                    g.Triangle(V(s*.045f,y+.012f,z+.006f),V(s*.222f,y+.125f,z-.036f),V(s*.155f,y-.033f,z-.014f),metal);
                    g.Triangle(V(s*.055f,y-.027f,z+.006f),V(s*.192f,y-.006f,z-.032f),V(s*.128f,y-.071f,z-.02f),metal*.85f);
                }
                else
                {
                    g.Quad(V(s*.046f,y+.034f,z),V(s*.133f,y+.037f,z-.005f),V(s*.132f,y-.014f,z-.005f),
                        V(s*.044f,y-.014f,z+.001f),metal,true);
                    g.Triangle(V(s*.10f,.278f,z-.048f),V(s*.234f,.338f,.182f),V(s*.198f,.232f,.206f),metal);
                }
            }
            return g;
        }
    }
}
