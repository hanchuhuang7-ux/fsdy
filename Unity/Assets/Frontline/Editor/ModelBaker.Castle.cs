using Frontline.Core;
using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static readonly Color KeepStone=new Color(.68f,.66f,.56f,0);
        static readonly Color KeepEdge=new Color(.78f,.74f,.61f,0);
        static readonly Color KeepRoof=new Color(.27f,.31f,.40f,0);
        static readonly Color KeepRed=new Color(.56f,.19f,.13f,0);

        static Geometry Castle(int level,int lod)
        {
            if(lod==1)return CastleSilhouette(level);
            var g=new Geometry();
            bool fine=lod==0;
            g.Box(V(0,.22f,0),V(9.12f,.44f,7.5f),Stonework);
            if(level==1)
            {
                g.Box(V(-.52f,1.07f,.48f),V(6.5f,1.5f,4.95f),KeepStone);
                House(g,-.52f,.51f,5.82f,4.40f,3.05f,1.42f,Tile,lod);
                CastleTower(g,V(2.96f,0,1.83f),1.16f,4.44f,1.8f,true,fine);
                CastleGate(g,1.86f,true,fine);
                SquareWard(g,V(-2.90f,0,-1.82f),1.52f,2.91f,true);
            }
            else
            {
                bool duke=level==3;
                float curtain=duke?3.05f:2.28f,citadel=duke?8.8f:5.35f;
                var court=g;
                g=new Geometry();
                g.Box(V(0,citadel*.5f,.50f),V(duke?4.45f:4.0f,citadel,4.15f),KeepStone);
                MasonryFace(g,V(0,.47f,-1.587f),Quaternion.identity,duke?4.42f:3.98f,citadel-.96f,.43f);
                g.Box(V(0,citadel-.32f,.5f),V(4.58f,.39f,4.37f),KeepEdge);
                if(duke)
                {
                    Gable(g,V(0,citadel,.51f),4.52f,4.45f,1.55f,KeepRoof,KeepStone,lod);
                    if(fine)
                    {
                        g.Box(V(0,6.41f,-1.612f),V(4.50f,.15f,.17f),KeepEdge);
                        g.Box(V(0,4.62f,-1.612f),V(4.50f,.15f,.17f),KeepEdge);
                        for(int x=-1;x<=1;x++)
                        {
                            ArrowLoop(g,x*1.19f,7.31f,-1.615f,.83f);
                            ArrowLoop(g,x*1.19f,5.45f,-1.615f,.73f);
                        }
                    }
                }
                else
                {
                    g.Box(V(0,6.04f,.5f),V(2.72f,1.55f,2.63f),KeepStone);
                    Gable(g,V(0,6.82f,.5f),2.92f,2.98f,.85f,KeepRoof,KeepStone,lod);
                }
                DonjonCorbels(g,citadel,duke);
                court.Append(g,Matrix4x4.Translate(V(-.74f,0,.25f)));
                g=court;
                // A lower east hall gives the central keep a stepped silhouette facing the lake.
                SquareWard(g,V(1.82f,0,.57f),2.20f,duke?5.71f:3.94f,false);
                CastleWall(g,V(0,0,2.78f),8.15f,curtain,1,false,fine);
                CastleWall(g,V(-3.39f,0,0),5.85f,curtain,1,true,fine);
                CastleWall(g,V(3.39f,0,0),5.85f,curtain,1,true,fine);
                for(int side=-1;side<=1;side+=2)
                {
                    CastleWall(g,V(side*2.24f,0,-2.92f),2.52f,curtain,1,false,fine);
                    CastleTower(g,V(side*3.34f,0,-2.53f),side<0?.91f:1.05f,
                        (duke?4.67f:3.31f)+(side>0?.92f:0),duke?1.73f:1.16f,false,fine);
                    if(side>0)CastleTower(g,V(3.20f,0,2.50f),1.09f,duke?7.10f:4.61f,duke?2.11f:1.42f,false,fine);
                    else SquareWard(g,V(-3.16f,0,2.38f),1.83f,duke?5.62f:3.71f,false);
                }
                CastleGate(g,curtain,false,fine);
                HangingStandard(g,V(-1.50f,citadel+.58f,-1.53f),duke?2.67f:1.79f);
                if(duke)HangingStandard(g,V(1.92f,4.72f,-.57f),1.46f);
            }
            for(int side=-1;side<=1;side+=2)
            {
                // Wall-mounted iron baskets flank the gate above the walking clearance.
                var mount=V(side*1.31f,1.26f,-3.58f);
                g.surface=Surface.Metal;
                g.Cylinder(mount,mount+V(0,.09f,-.19f),.032f,WroughtIron,6);
                var bowl=mount+V(0,.085f,-.18f);
                g.Frustum(bowl,V(.19f,0,.19f),bowl+Vector3.up*.13f,V(.34f,0,.34f),WroughtIron,9);
                g.surface=Surface.Ember;
                g.Ellipsoid(bowl+Vector3.up*.138f,V(.136f,.048f,.132f),Amber,8,3);
                g.Cone(bowl+V(.024f,.14f,0),.085f,.22f,new Color(1,.47f,.13f,0),7);
                g.surface=Surface.Auto;
            }
            for(int step=0;step<3;step++)
                g.Box(V(0,.058f+step*.059f,-3.61f+step*.168f),V(2.21f-step*.063f,.116f+step*.118f,.35f),KeepEdge);
            g.FitFootprint(FrontierMap.CastleHalfWidth,FrontierMap.CastleHalfDepth);
            return g;
        }

        static void SquareWard(Geometry g,Vector3 at,float width,float top,bool timber)
        {
            g.Box(at+Vector3.up*top*.50f,V(width,top,width*1.16f),KeepStone);
            g.Box(at+Vector3.up*(top-.24f),V(width+.16f,.25f,width*1.16f+.16f),timber?Walnut:KeepEdge);
            Gable(g,at+Vector3.up*top,width+.24f,width*1.16f+.23f,width*.54f,timber?FiredClay:KeepRoof,KeepStone,0);
            ArrowLoop(g,at.x,top-.83f,at.z-width*.58f-.01f,.62f);
            if(!timber)UpperWindow(g,at+V(width*.5f,0,0),Quaternion.Euler(0,-90,0),top-1.49f,.018f,.28f);
        }

        static void DonjonCorbels(Geometry g,float height,bool duke)
        {
            float side=duke?2.225f:2f;
            foreach(int flank in new[]{-1,1})
            {
                float x=flank*(side-.17f);
                g.Frustum(V(x,.43f,-1.73f),V(.47f,0,.69f),V(x,height*.64f,-1.57f),
                    V(.30f,0,.31f),Stonework,4);
                g.Box(V(x,height*.64f,-1.61f),V(.37f,.12f,.37f),KeepEdge);
            }
            for(int i=0;i<7;i++)
            {
                float x=(i-3)*side*.275f;
                g.Bevel(V(x,height-.57f,-1.63f),V(.23f,.48f,.31f),.058f,Stonework);
            }
            g.Box(V(0,height-.23f,-1.69f),V(2*side+.30f,.31f,.53f),KeepEdge);
        }

        static void HangingStandard(Geometry g,Vector3 top,float length)
        {
            float w=length*.32f;
            g.Cylinder(top+V(-w*.69f,0,.02f),top+V(w*.69f,0,.02f),.038f,OldGold,8);
            int cloth=g.Mark;
            Vector3 Point(int i,int row)
            {
                float u=i/8f,v=row/6f;
                return top+V((u-.5f)*w+.09f*v*v,v*(-length)+v*v*Mathf.Abs(u-.5f)*.25f,
                    -.056f-.13f*v-.065f*Mathf.Sin(u*10+v*2.2f)*(.3f+.7f*v));
            }
            g.surface=Surface.Cloth;
            for(int row=0;row<6;row++)for(int i=0;i<8;i++)
            {
                var dye=i==0||i==7||row==5||row==2&&i>=3&&i<=4||row==3&&i==4?OldGold:KeepRed;
                g.Quad(Point(i,row),Point(i+1,row),Point(i+1,row+1),Point(i,row+1),dye,true);
            }
            g.WeightSince(cloth,top,top+Vector3.down*length);
            g.surface=Surface.Auto;
        }

        static Geometry CastleSilhouette(int level)
        {
            var g=new Geometry();
            g.Box(V(0,.70f,0),V(9.12f,1.4f,7.50f),Stonework);
            float height=level==1?3.05f:level==2?6.82f:8.8f;
            float keepWidth=level==1?5.82f:level==2?3.70f:4.45f;
            float keepDepth=level==1?4.40f:4.15f;
            g.Box(V(0,height*.50f,.50f),V(keepWidth,height,keepDepth),KeepStone);
            Gable(g,V(0,height,.50f),keepWidth+.12f,keepDepth+.16f,level==2?.85f:1.49f,
                level==1?Tile:KeepRoof,KeepStone,1);
            if(level==1)
            {
                g.Box(V(2.96f,2.22f,1.83f),V(1.62f,4.44f,1.62f),KeepStone);
                g.Cone(V(2.96f,4.44f,1.83f),1.23f,1.8f,Tile,4);
            }
            else
            {
                bool duke=level==3;
                float curtain=duke?3.05f:2.28f;
                g.Box(V(0,curtain*.50f,-2.93f),V(8.16f,curtain,1.11f),KeepStone);
                g.Box(V(0,curtain*.50f,2.75f),V(8.16f,curtain,1.11f),KeepStone);
                for(int side=-1;side<=1;side+=2)
                {
                    for(int rear=-1;rear<=1;rear+=2)
                    {
                        var point=V(side*3.34f,0,rear*2.52f);
                        float tower=duke?(rear<0?5.65f:6.43f):(rear<0?3.64f:3.88f);
                        g.Box(point+Vector3.up*tower*.50f,V(1.57f,tower,1.57f),KeepStone);
                        g.Cone(point+Vector3.up*tower,1.16f,duke?2.17f:1.29f,KeepRoof,4);
                    }
                }
                g.Box(V(0,.83f,-3.525f),V(1.89f,1.56f,.065f),Dark);
            }
            return g;
        }

        static void CastleWall(Geometry g,Vector3 at,float span,float height,float depth,bool turn,bool fine)
        {
            var wall=new Geometry();
            wall.Box(V(0,height*.5f,0),V(span,height,depth),KeepStone);
            wall.Box(V(0,height-.14f,0),V(span+.12f,.23f,depth+.17f),KeepEdge);
            if(fine)
            {
                wall.Box(V(0,height+.025f,depth*.18f),V(span,.11f,depth*.76f),Stonework);
                wall.Box(V(0,height+.14f,depth*.48f),V(span,.30f,.12f),KeepEdge);
                int merlons=Mathf.Max(3,Mathf.RoundToInt(span/.67f));
                for(int tooth=0;tooth<merlons;tooth++)
                {
                    float x=span*((tooth+.5f)/merlons-.5f);
                    wall.Box(V(x,height+.18f,-depth*.31f),V(.33f,.38f,depth*.43f),KeepEdge);
                }
                MasonryFace(wall,V(0,.041f,-depth*.505f),Quaternion.identity,span-.043f,height-.195f,.35f);
            }
            g.Append(wall,Matrix4x4.TRS(at,Quaternion.Euler(0,turn?90:0,0),Vector3.one));
        }

        static void CastleTower(Geometry g,Vector3 at,float radius,float height,float roof,bool timber,bool fine)
        {
            int sides=fine?14:4;
            var baseShape=V(radius*2,0,radius*2);
            var turret=V(radius*1.82f,0,radius*1.82f);
            g.Frustum(at+Vector3.up*.40f,baseShape,at+Vector3.up*height,turret,KeepStone,sides);
            if(fine)
            {
                g.Frustum(at+Vector3.up*(height-.28f),turret,at+Vector3.up*height,baseShape,KeepEdge,8);
                g.Frustum(at+Vector3.up*(height-1.53f),turret*1.012f,at+Vector3.up*(height-1.35f),turret*1.036f,KeepEdge,8);
                ArrowLoop(g,at.x,height-1.25f,at.z-radius*.93f,.63f);
                for(int index=0;index<7;index++)
                {
                    float y=.77f+index*.41f;
                    if(y>height-1.1f)break;
                    float x=at.x+((index&1)==0?.21f:-.21f);
                    g.Box(V(x,y,at.z-radius*(1-y*.09f/height)+.014f),V(.40f,.09f,.027f),
                        Color.Lerp(KeepStone,Rock,.35f+.1f*(index%3)));
                }
            }
            g.Cone(at+Vector3.up*height,radius*1.08f,roof,timber?Tile:KeepRoof,sides);
            if(fine)g.Spear(at+Vector3.up*(height+roof*.80f),at+Vector3.up*(height+roof+.24f),.093f,Gold);
        }

        static void CastleGate(Geometry g,float top,bool timber,bool fine)
        {
            const float face=-3.39f,doorWidth=1.05f;
            float spring=top*.56f;
            for(int side=-1;side<=1;side+=2)
                g.Box(V(side*1.22f,top*.5f,-3.14f),V(.63f,top,.94f),timber?Wood:KeepStone);
            g.Box(V(0,top-.16f,-3.15f),V(2.96f,.32f,.89f),timber?WoodLight:KeepEdge);
            g.Box(V(0,spring*.57f,face+.18f),V(doorWidth*2,spring*1.14f,.21f),Dark);
            if(!fine)return;
            var topOfDoor=V(0,spring,face-.046f);
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;
                g.Triangle(topOfDoor,topOfDoor+V(Mathf.Cos(b)*doorWidth,Mathf.Sin(b)*doorWidth,0),
                    topOfDoor+V(Mathf.Cos(a)*doorWidth,Mathf.Sin(a)*doorWidth,0),Dark);
            }
            float outer=doorWidth+.26f;
            for(int tooth=0;tooth<9;tooth++)
            {
                float a=(tooth+.027f)*Mathf.PI/9,b=(tooth+.973f)*Mathf.PI/9;
                Vector3 At(float angle,float r)=>V(Mathf.Cos(angle)*r,spring+Mathf.Sin(angle)*r,face-.11f);
                g.Quad(At(b,doorWidth),At(b,outer),At(a,outer),At(a,doorWidth),tooth%2==0?KeepEdge:KeepStone,true);
            }
            for(int bar=-4;bar<=4;bar++)
            {
                float x=bar*.214f;
                float rise=spring+Mathf.Sqrt(doorWidth*doorWidth-x*x)*.83f;
                g.Box(V(x,rise*.5f,face),V(.091f,rise,.13f),timber?WoodLight:Steel);
                if(!timber)g.Spear(V(x,.22f,face),V(x,.07f,face),.075f,LightSteel);
            }
            g.Box(V(0,.62f,face-.045f),V(2.00f,.13f,.15f),timber?Wood:Steel);
            g.Box(V(0,1.0f,face-.045f),V(2.00f,.13f,.15f),timber?Wood:Steel);
            g.Box(V(0,top+.14f,face-.08f),V(.54f,.54f,.12f),KeepRed);
            g.Box(V(0,top+.14f,face-.16f),V(.09f,.33f,.055f),Gold);
            g.Box(V(0,top+.18f,face-.17f),V(.32f,.09f,.055f),Gold);
        }

        static void ArrowLoop(Geometry g,float x,float y,float face,float height)
        {
            float half=height*.5f;
            g.Box(V(x,y,face-.037f),V(.238f,height+.17f,.081f),KeepEdge);
            g.Box(V(x,y,face-.085f),V(.119f,height,.023f),Dark);
            for(int side=-1;side<=1;side+=2)
            {
                g.Quad(V(x+side*.096f,y+half+.04f,face-.089f),V(x+side*.039f,y+half,face-.101f),
                    V(x+side*.039f,y-half,face-.101f),V(x+side*.096f,y-half-.04f,face-.089f),
                    side<0?Stonework:Pigment(KeepStone,.67f),true);
            }
            g.Box(V(x,y-.06f,face-.105f),V(.146f,.06f,.015f),Dark);
        }
    }
}
