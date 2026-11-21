using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static readonly Color Limewash=new Color(.92f,.88f,.777f,0),
            FiredClay=new Color(.57f,.289f,.207f,0);

        static void TimberHouse(Geometry g,float x,float z,float width,float depth,float eave,float rise,Color roof)
        {
            g.Bevel(V(x,.103f,z),V(width+.16f,.206f,depth+.16f),.055f,Stonework);
            g.Box(V(x,.042f,z),V(width+.18f,.081f,depth+.18f),Pigment(Stonework,.75f));
            g.Box(V(x,eave*.5f,z),V(width,eave,depth),Limewash);
            rise*=1.22f;
            var ridge=V(x,eave,z);
            Gable(g,ridge,width+.28f,depth+.31f,rise,roof,Limewash,0);
            for(int face=0;face<4;face++)
            {
                bool end=face%2!=0;
                float span=end?depth:width,reach=end?width:depth;
                var turn=Quaternion.Euler(0,90*face,0);
                var foot=V(x,0,z)+turn*V(0,0,-reach*.5f);
                HalfTimberWall(g,foot,turn,span,eave,end?rise:0,face==0);
                MasonryFace(g,foot+turn*V(0,0,-.085f),turn,span,.208f,.104f);
            }
            if(width>1.95f&&eave>1.07f)
                Dormer(g,ridge+V(width*.19f,rise*.42f,-(depth+.31f)*.29f),Mathf.Min(.63f,width*.27f),rise,roof);
        }

        static void HalfTimberWall(Geometry g,Vector3 at,Quaternion turn,float width,float height,float gable,bool front)
        {
            Vector3 Point(float x,float y,float z) => at+turn*V(x,y,z);
            void Beam(float x,float y,float u,float v,float breadth,float offset=0)
            {
                var a=Point(x,y,-.025f-offset);var b=Point(u,v,-.025f-offset);
                var across=turn*V(v-y,x-u,0).normalized*breadth*.5f;
                g.Quad(a-across,a+across,b+across,b-across,Walnut,true);
                var grain=turn*V(0,0,-.002f);
                g.Quad(a+across+grain,a+across*.76f+grain,b+across*.76f+grain,b+across+grain,
                    Pigment(Walnut,1.36f),true);
            }

            float left=-width*.5f+.036f,right=-left;
            float sill=.218f,middle=Mathf.Max(sill+.26f,height*.59f),rail=height-.021f;
            int bays=Mathf.Clamp(Mathf.RoundToInt(width/.66f),2,7);
            float bay=(right-left)/bays;
            for(int i=0;i<bays;i++)
            {
                float lo=left+i*bay,hi=lo+bay;
                var a=Point(lo,sill,-.005f);var b=Point(hi,sill,-.005f);
                var c=Point(hi,rail,-.005f);var d=Point(lo,rail,-.005f);
                var shade=Pigment(Limewash,.78f+.035f*(i%3));var highlight=Pigment(Limewash,.96f+.025f*(i%2));
                g.Gradient(a,d,c,shade,highlight,highlight);
                g.Gradient(a,c,b,shade,highlight,shade);
                Beam(lo,sill,lo,rail,.078f);
                float inset=bay*.085f;
                if(!front||i==0||i==bays-1)
                {
                    Beam(lo+inset,sill+.03f,hi-inset,middle-.049f,.047f);
                    Beam(hi-inset,middle+.034f,lo+bay*.43f,rail-.041f,.048f);
                }
                else if(height>1.47f)
                    Beam(lo+inset,middle+.035f,hi-inset,rail-.042f,.047f);
            }
            Beam(right,sill,right,rail,.078f);
            Beam(left,sill,right,sill,.071f);
            Beam(left,rail,right,rail,.102f);
            if(height>1.01f)Beam(left,middle,right,middle,.073f);
            if(gable<=0)return;
            Beam(left-.13f,rail,0,height+gable-.045f,.081f,.141f);
            Beam(0,height+gable-.045f,right+.13f,rail,.081f,.141f);
            Beam(0,rail,0,height+gable-.035f,.075f,.141f);
            Beam(left*.74f,rail+.014f,0,height+gable*.485f,.058f,.141f);
            Beam(right*.74f,rail+.014f,0,height+gable*.485f,.058f,.141f);
        }

        static void Dormer(Geometry g,Vector3 basePoint,float width,float rise,Color roof)
        {
            var rooflet=new Geometry();
            float door=.29f+rise*.23f;
            rooflet.Box(V(0,door*.24f,.092f),V(width,door*.76f,.38f),Limewash);
            rooflet.Box(V(0,door*.30f,-.101f),V(width*.50f,door*.50f,.022f),Dark);
            rooflet.Box(V(0,door*.30f,-.121f),V(width*.075f,door*.48f,.016f),Walnut);
            rooflet.Box(V(0,.003f,-.096f),V(width*1.03f,.063f,.083f),Walnut);
            var canopy=new Geometry();
            Gable(canopy,V(0,door*.60f,0),.60f,width+.16f,width*.46f,roof,Walnut,1);
            rooflet.Append(canopy,Matrix4x4.TRS(V(0,0,.102f),Quaternion.Euler(0,90,0),Vector3.one));
            g.Append(rooflet,Matrix4x4.Translate(basePoint));
        }

        static void MasonryFace(Geometry g,Vector3 at,Quaternion turn,float width,float height,float course)
        {
            int rows=Mathf.Max(1,Mathf.FloorToInt(height/course));
            int columns=Mathf.Max(2,Mathf.RoundToInt(width/(course*2)));
            float rowHeight=height/rows,colWidth=width/columns;
            for(int row=0;row<rows;row++)
                for(int i=0;i<=columns;i++)
                {
                    int seed=i*53+row*61;
                    float left=Mathf.Max(-width*.5f,(i-.5f*(row%2))*colWidth-width*.5f+.014f);
                    float right=Mathf.Min(width*.5f,left+colWidth-.021f);
                    if(right-left<.048f)continue;
                    float y=(row+.08f)*rowHeight;
                    float top=y+rowHeight*.83f;
                    var color=Color.Lerp(Stonework,KeepStone,(seed%9)*.113f);
                    g.Quad(at+turn*V(left,y,-.007f),at+turn*V(left,top,-.011f),at+turn*V(right,top,-.011f),
                        at+turn*V(right,y,-.007f),Pigment(color,.83f+(seed%11)*.025f),true);
                }
        }
    }
}
