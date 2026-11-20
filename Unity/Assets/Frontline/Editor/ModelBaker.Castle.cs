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
            }
            else
            {
                bool duke=level==3;
                float curtain=duke?3.05f:2.28f,citadel=duke?8.8f:5.35f;
                g.Box(V(0,citadel*.5f,.50f),V(duke?4.45f:4.0f,citadel,4.15f),KeepStone);
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
                CastleWall(g,V(0,0,2.78f),8.15f,curtain,1,false,fine);
                CastleWall(g,V(-3.39f,0,0),5.85f,curtain,1,true,fine);
                CastleWall(g,V(3.39f,0,0),5.85f,curtain,1,true,fine);
                for(int side=-1;side<=1;side+=2)
                {
                    CastleWall(g,V(side*2.24f,0,-2.92f),2.52f,curtain,1,false,fine);
                    CastleTower(g,V(side*3.34f,0,-2.53f),1.08f,duke?5.65f:3.64f,duke?2.1f:1.22f,false,fine);
                    CastleTower(g,V(side*3.34f,0,2.50f),1.08f,duke?6.43f:3.88f,duke?2.25f:1.35f,false,fine);
                }
                CastleGate(g,curtain,false,fine);
            }
            g.FitFootprint(FrontierMap.CastleHalfWidth,FrontierMap.CastleHalfDepth);
            return g;
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
                int merlons=Mathf.Max(3,Mathf.RoundToInt(span/.67f));
                for(int tooth=0;tooth<merlons;tooth++)
                {
                    float x=span*((tooth+.5f)/merlons-.5f);
                    wall.Box(V(x,height+.18f,-depth*.31f),V(.33f,.38f,depth*.43f),KeepEdge);
                }
                StoneCourses(wall,span,height,-depth*.505f);
            }
            g.Append(wall,Matrix4x4.TRS(at,Quaternion.Euler(0,turn?90:0,0),Vector3.one));
        }

        static void CastleTower(Geometry g,Vector3 at,float radius,float height,float roof,bool timber,bool fine)
        {
            int sides=fine?8:4;
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
            float outer=doorWidth+.26f;
            for(int tooth=0;tooth<9;tooth++)
            {
                float a=tooth*Mathf.PI/9,b=(tooth+1)*Mathf.PI/9;
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
            g.Box(V(x,y,face-.018f),V(.22f,height+.16f,.10f),KeepEdge);
            g.Box(V(x,y,face-.076f),V(.079f,height,.03f),Dark);
            g.Box(V(x,y-.06f,face-.093f),V(.155f,.068f,.03f),Dark);
        }

        static void StoneCourses(Geometry g,float width,float height,float face)
        {
            int rows=Mathf.FloorToInt(height/.37f),columns=Mathf.FloorToInt(width/.55f);
            for(int row=0;row<rows;row++)
            {
                float y=.31f+row*.34f;
                for(int col=0;col<columns;col++)
                {
                    if((col*11+row*7)%5>1)continue;
                    float x=(col+.35f+(row%2)*.21f)/columns*width-width*.5f;
                    g.Box(V(x,y,face),V(.39f,.13f,.03f),Color.Lerp(KeepStone,Rock,.25f+((row+col)%4)*.12f));
                }
            }
        }
    }
}
