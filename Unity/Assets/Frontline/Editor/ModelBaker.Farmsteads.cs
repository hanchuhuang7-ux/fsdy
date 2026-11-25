using UnityEngine;
using CropAppearance = Frontline.UnityGame.ManorPlotArt.CropAppearance;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static void Sack(Geometry g,Vector3 at,float r,Color burlap)
        {
            g.surface=Surface.Cloth;
            g.Loft(new[]{at+V(0,.01f,0),at+V(0,r*.33f,0),at+V(r*.09f,r*1.25f,0),
                    at+V(r*.16f,r*1.77f,.012f),at+V(r*.21f,r*1.92f,.021f)},
                new[]{Section(r*.67f,r*.58f),Section(r*.98f,r*.80f),Section(r*.87f,r*.72f),
                    Section(r*.18f,r*.19f),Section(r*.35f,r*.24f)},8,2,burlap);
            g.Torus(at+V(r*.16f,r*1.73f,.01f),r*.20f,r*.044f,Walnut,7);
            g.surface=Surface.Auto;
        }

        static void StackHay(Geometry g,Vector3 at)
        {
            g.surface=Surface.Thatch;
            g.Loft(new[]{at+V(0,.017f,0),at+V(0,.17f,0),at+V(.016f,.32f,0),at+V(.029f,.49f,0),at+V(.047f,.56f,0)},
                new[]{Section(.289f,.241f),Section(.342f,.251f),Section(.30f,.23f),Section(.175f,.143f),Section(.034f,.047f)},11,2,Straw);
            for(int i=0;i<11;i++)
            {
                float a=i*(Mathf.PI*2/11);var edge=V(Mathf.Sin(a)*.30f,0,Mathf.Cos(a)*.24f);
                g.Quad(at+edge+V(-.017f,.20f,0),at+edge+V(.016f,.19f,0),
                    at+edge*1.12f+V(.014f,.061f,0),at+edge*1.16f+V(-.009f,.013f,0),StrawLight,true);
            }
            g.surface=Surface.Oak;g.Cylinder(at+V(.035f,.22f,0),at+V(.044f,.64f,0),.021f,Walnut,6);
            g.surface=Surface.Auto;
        }

        static void FieldStore(Geometry g,Vector3 at)
        {
            const float width=.78f,depth=.74f;
            g.surface=Surface.Stone;
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                var pier=at+V(x*.29f,0,z*.26f);
                g.Frustum(pier,V(.21f,0,.21f),pier+Vector3.up*.32f,V(.15f,0,.15f),Stonework,7);
                g.Box(pier+Vector3.up*.31f,V(.27f,.073f,.24f),Rock);
            }
            g.surface=Surface.Auto;
            var store=new Geometry();
            TimberHouse(store,0,0,width,depth,.87f,.38f,FiredClay);
            g.Append(store,Matrix4x4.Translate(at+Vector3.up*.34f));
            g.Box(at+V(0,.78f,-.396f),V(.295f,.53f,.041f),Walnut);
            for(int i=0;i<4;i++)g.Box(at+V(0,.83f+i*.068f,-.422f),V(.252f,.032f,.028f),WoodLight);
        }

        static void WorkLadder(Geometry g,Vector3 foot,float height,float width)
        {
            var rise=V(0,height,height*.28f);
            foreach(int side in new[]{-1,1})
                g.Cylinder(foot+Vector3.right*(side*width*.5f),foot+rise+Vector3.right*(side*width*.5f),.031f,Walnut,6);
            int rungs=Mathf.Max(3,Mathf.RoundToInt(height/.22f));
            for(int i=1;i<rungs;i++)
            {
                var p=foot+rise*(i/(float)rungs);
                g.Cylinder(p-Vector3.right*(width*.55f),p+Vector3.right*(width*.55f),.022f,WoodLight,5);
            }
        }

        static void Wattle(Geometry g,Vector3 a,Vector3 b,float height)
        {
            g.surface=Surface.Oak;
            for(int post=0;post<4;post++)
            {
                var p=Vector3.Lerp(a,b,post/3f);
                g.Cylinder(p,p+V(.014f,height+.016f*(post%2),.013f),.031f,Walnut,5);
            }
            var normal=Vector3.Cross((b-a).normalized,Vector3.up)*.026f;
            for(int row=0;row<3;row++)
            {
                var p=a+Vector3.up*height*(.22f+.27f*row);
                var end=b+(p-a);var bias=row%2==0?normal:-normal;
                var r=Section(.019f,.021f);
                g.Loft(new[]{p+bias,Vector3.Lerp(p,end,1f/3)-bias,Vector3.Lerp(p,end,2f/3)+bias,end-bias},
                    new[]{r,r,r,r},4,1,WoodLight);
            }
            g.surface=Surface.Auto;
        }

        static void Wheat(Geometry g,Vector3 at,float height,float seed,bool ripe)
        {
            var tip=at+V(.047f*Mathf.Cos(seed),height,.060f*Mathf.Sin(seed));
            var light=ripe?RichGrain:new Color(.68f,.68f,.32f,0);
            var straw=ripe?RichStalk:new Color(.41f,.53f,.24f,0);
            g.surface=Surface.Leaf;
            g.Cylinder(at,tip,.0085f,straw,3);
            for(int side=-1;side<=1;side+=2)
            {
                var low=Vector3.Lerp(at,tip,.30f+(side+1)*.10f);
                var end=low+V(side*.086f,.115f,.047f);
                g.Quad(low,end+V(-.017f,.035f,0),end+V(side*.052f,.054f,0),low+V(.010f,0,.005f),straw,true);
                for(int grain=0;grain<3;grain++)
                {
                    var p=tip+V(0,-.02f+grain*.025f,0);
                    var q=p+V(side*(.030f-grain*.003f),.037f,.011f);
                    g.Quad(p,q+V(-.010f,0,-.008f),q+V(side*.011f,.025f,0),q+V(.011f,0,.009f),light,true);
                }
            }
            g.surface=Surface.Auto;
        }

        static void PloughedBed(Geometry g,float x,float z,float width,float length,int rows,CropAppearance crop)
        {
            bool rest=crop==CropAppearance.Fallow,rich=crop==CropAppearance.Rich;
            float furrow=width/rows;
            Vector3 Point(int row,int flank,int along)
            {
                float t=along/10f;
                float waviness=.024f*Mathf.Sin(5.8f*t+row*.63f)+.012f*Mathf.Sin(13.7f*t-row);
                float center=x-width*.5f+(row+.5f)*furrow;
                // Both upper-tier annexes are drawn over the same base field. Leave the
                // rear threshing floor clear on both sides for their ladders and store piers.
                float headland=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.57f,1.03f,Mathf.Abs(center)))*.92f;
                return V(x-width*.5f+(row+.5f+.50f*flank)*furrow+waviness,
                    .064f+(flank==0?.065f+.022f*Mathf.Cos(row*.63f+t*8.1f):.008f),z-length*.5f+t*(length-headland));
            }
            g.surface=Surface.Earth;
            for(int row=0;row<rows;row++)
            {
                for(int j=0;j<10;j++)for(int k=-1;k<1;k++)
                {
                    var dye=Pigment(rest?FallowRidge:Earth,.86f+.14f*((row+3*j)%5)/4);
                    g.Quad(Point(row,k,j),Point(row,k,j+1),Point(row,k+1,j+1),Point(row,k+1,j),dye);
                }
            }
            if(!rest)for(int row=0;row<rows;row++)for(int j=1;j<10;j++)
            {
                var root=Point(row,0,j);
                float phase=row*3.11f+j*2.37f;
                Wheat(g,root,.27f+.085f*Mathf.Sin(phase)+(rich?.11f:0),phase,rich||j%3==0);
                if(rich||j%2==0)Wheat(g,root+V(furrow*.18f,0,-.082f),rich?.38f:.25f,phase+2,rich);
            }
            g.surface=Surface.Auto;
        }

        static void FieldWork(Geometry g,bool irrigated,CropAppearance crop)
        {
            if(irrigated)
            {
                PloughedBed(g,-.98f,0,1.33f,2.26f,4,crop);
                PloughedBed(g,.98f,0,1.33f,2.26f,4,crop);
                var water=new Color(.37f,.56f,.50f,0);
                g.surface=Surface.Glass;g.Box(V(0,.071f,0),V(.45f,.027f,2.38f),water);
                g.surface=Surface.Oak;
                foreach(float x in new[]{-.253f,.253f})g.Box(V(x,.139f,0),V(.069f,.24f,2.41f),Walnut);
                for(int j=0;j<4;j++)g.Box(V(0,.27f,-1.05f+j*.098f),V(.70f,.058f,.081f),WoodLight);
                g.Box(V(0,.30f,1.02f),V(.57f,.55f,.097f),Wood);
                g.Cylinder(V(-.31f,.63f,1.02f),V(.31f,.63f,1.02f),.045f,Steel,7);
                Wattle(g,V(-1.56f,0,-1.19f),V(-.47f,0,-1.19f),.29f);
            }
            else
            {
                PloughedBed(g,.82f,0,1.74f,2.31f,5,crop);
                var mill=V(-.9f,0,.24f);
                g.surface=Surface.Stone;
                g.Frustum(mill,V(1.25f,0,1.2f),mill+Vector3.up*.56f,V(1.08f,0,1.02f),Stonework,12);
                g.surface=Surface.Auto;
                WorkLadder(g,V(-1.36f,0,-.48f),1.01f,.25f);
                Sack(g,V(-1.46f,0,-.82f),.17f,Linen);
                Wattle(g,V(.05f,0,1.23f),V(.68f,0,1.23f),.32f);
                g.Frustum(mill+Vector3.up*.56f,V(1.10f,0,1.06f),mill+Vector3.up*.63f,
                    V(1.09f,0,1.05f),Walnut,12);
            }
            g.surface=Surface.Auto;
        }

        static void DyeTub(Geometry g,Vector3 at,float r,Color dye)
        {
            g.surface=Surface.Oak;
            int sides=12;
            g.Frustum(at,V(r*1.63f,0,r*1.63f),at+Vector3.up*r*1.24f,V(2*r,0,2*r),Walnut,sides);
            g.surface=Surface.Metal;
            for(int i=0;i<2;i++)g.Torus(at+Vector3.up*r*(.31f+i*.68f),r*(.87f+.09f*i),.018f,WroughtIron,sides);
            g.surface=Surface.Glass;
            g.Cylinder(at+Vector3.up*r*1.24f,at+Vector3.up*r*1.27f,r*.89f,dye,sides);
            g.surface=Surface.Auto;
            g.Cylinder(at+V(-r*.63f,r*1.27f,.018f),at+V(r*.48f,r*1.92f,0),.019f,WoodLight,5);
        }

        static void StoneOven(Geometry g,Vector3 at,float s)
        {
            var oven=new Geometry{surface=Surface.Stone};
            oven.Box(V(0,.24f,0),V(.88f,.48f,.70f),Stonework);
            oven.Loft(new[]{V(0,.46f,0),V(0,.60f,0),V(0,.79f,.03f),V(.035f,.89f,.10f)},
                new[]{Section(.48f,.395f),Section(.45f,.37f),Section(.32f,.26f),Section(.115f,.11f)},12,2,FiredClay);
            oven.Box(V(0,.37f,-.381f),V(.45f,.29f,.08f),Dark);
            for(int k=0;k<9;k++)
            {
                float a=k*Mathf.PI/9,b=(k+1)*Mathf.PI/9;
                Vector3 Edge(float t,float r)=>V(Mathf.Cos(t)*r,.49f+Mathf.Sin(t)*r,-.415f);
                oven.Quad(Edge(a,.225f),Edge(b,.225f),Edge(b,.315f),Edge(a,.315f),k%3==0?KeepEdge:FiredClay,true);
            }
            oven.Box(V(0,.257f,-.469f),V(.58f,.063f,.25f),Stonework);
            oven.surface=Surface.Ember;oven.Box(V(0,.261f,-.421f),V(.38f,.037f,.03f),Amber);
            g.Append(oven,Matrix4x4.TRS(at,Quaternion.identity,Vector3.one*s));
        }
    }
}
