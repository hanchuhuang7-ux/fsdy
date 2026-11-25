using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static void LeadedWindow(Geometry g,Vector3 at,float width,float height,Quaternion facing)
        {
            var pane=new Geometry();
            Vector3 Point(float u,float v)
            {
                float slope=1-Mathf.Clamp01((v-.55f)/.45f);
                return V(u*width*.5f*(v<=.55f?1:Mathf.Sin(slope*Mathf.PI*.5f)),v*height,-.018f);
            }
            var dyes=new[]{new Color(.33f,.51f,.53f,0),new Color(.54f,.27f,.19f,0),new Color(.66f,.52f,.28f,0)};
            for(int column=0;column<3;column++)for(int row=0;row<4;row++)
            {
                float l=column/1.5f-1,r=(column+1)/1.5f-1,b=row*.25f,t=(row+1)*.25f;
                var a=Point(l,b);var c=Point(r,t);var d=Point(l,t);
                pane.surface=Surface.Glass;pane.Quad(a,Point(r,b),c,d,dyes[(column+2*row)%3],true);
                pane.surface=Surface.Metal;pane.Cylinder(a,d,.010f,Dark,4);pane.Cylinder(d,c,.008f,Dark,4);
            }
            pane.surface=Surface.Stone;
            for(int side=-1;side<=1;side+=2)
            {
                var last=Point(side,0);
                for(int i=1;i<=6;i++)
                {
                    var next=Point(side,i/6f);
                    pane.Cylinder(last,next,.041f,KeepEdge,6);
                    last=next;
                }
            }
            g.Append(pane,Matrix4x4.TRS(at,facing,Vector3.one));
        }

        static void TimberGallery(Geometry g)
        {
            var front=V(-.12f,1.10f,-.99f);
            g.Box(front,V(1.67f,.09f,.62f),Walnut);
            foreach(float x in new[]{-.78f,0,.78f})
            {
                var foot=front+V(x,-1.08f,-.25f);
                g.Cylinder(foot,foot+Vector3.up*1.56f,.047f,Walnut,7);
                g.Cylinder(foot+Vector3.up*.79f,foot+V(.14f,1.04f,.22f),.038f,WoodLight,5);
            }
            g.Box(front+V(0,.42f,-.247f),V(1.70f,.065f,.059f),WoodLight);
            for(int i=0;i<9;i++)g.Box(front+V(-.70f+i*.174f,.21f,-.248f),V(.033f,.40f,.043f),Walnut);
            SagLine(g,V(.53f,1.74f,-.72f),V(.98f,1.64f,-1.12f),.14f,.022f,WroughtIron);
            g.Cylinder(V(1.0f,1.57f,-1.12f),V(1.0f,1.19f,-1.12f),.019f,WroughtIron,6);
            g.Box(V(1.0f,1.17f,-1.12f),V(.42f,.29f,.067f),Walnut);
            g.Torus(V(1.0f,1.17f,-1.168f),.080f,.017f,OldGold,10);
            g.Box(V(-1.26f,.28f,-1.02f),V(.60f,.065f,.38f),WoodLight);
            foreach(float x in new[]{-1.46f,-1.06f})g.Box(V(x,.14f,-1.02f),V(.065f,.27f,.29f),Walnut);
        }

        static void WatchTurret(Geometry g,Vector3 at,float size)
        {
            var watch=new Geometry();
            watch.Frustum(Vector3.zero,V(.83f,0,.82f),V(0,.51f,0),V(.73f,0,.70f),Stonework,6);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                watch.Box(V(x*.235f,1.05f,z*.235f),V(.086f,1.18f,.091f),Walnut);
            watch.Box(V(0,1.24f,0),V(.69f,.094f,.69f),WoodLight);
            for(int face=0;face<4;face++)
            {
                var turn=Quaternion.Euler(0,90*face,0);
                for(int k=-1;k<=1;k+=2)
                    watch.Box(turn*V(k*.193f,1.39f,-.307f),(face%2==0?V(.23f,.29f,.065f):V(.065f,.29f,.23f)),Walnut);
            }
            Gable(watch,V(0,1.67f,0),.88f,.86f,.40f,Slate,Plaster,0);
            watch.Cylinder(V(-.27f,.53f,-.24f),V(.24f,1.19f,-.24f),.038f,WoodLight,5);
            g.Append(watch,Matrix4x4.TRS(at,Quaternion.identity,Vector3.one*size));
        }

        static void TradeFront(Geometry g,string kind)
        {
            switch(kind)
            {
                case "chapel":
                    for(int i=0;i<3;i++)
                    {
                        float x=-1.42f+i*.81f;
                        g.Frustum(V(x,.03f,-.66f),V(.21f,0,.54f),V(x,1.26f,-.54f),V(.16f,0,.21f),Stonework,4);
                        g.Box(V(x,1.26f,-.57f),V(.24f,.075f,.26f),KeepEdge);
                    }
                    LeadedWindow(g,V(-1.02f,.59f,-.569f),.41f,.83f,Quaternion.identity);
                    LeadedWindow(g,V(-.26f,.59f,-.569f),.34f,.75f,Quaternion.identity);
                    LeadedWindow(g,V(.926f,1.13f,-1.033f),.45f,.80f,Quaternion.identity);
                    g.Box(V(.93f,4.00f,-.32f),V(.34f,.055f,.054f),OldGold);
                    for(int i=0;i<3;i++)g.Box(V(.93f,.046f+i*.074f,-1.34f+i*.107f),V(.88f-i*.06f,.089f+i*.14f,.21f),Stonework);
                    break;
                case "tavern":TimberGallery(g);break;
                case "bakery":
                    StoneOven(g,V(.96f,0,-.71f),1.05f);Sack(g,V(.30f,0,-1.09f),.15f,Linen);break;
                case "weaver":
                    DyeTub(g,V(1.38f,0,-.68f),.262f,Indigo);DyeTub(g,V(1.28f,0,.56f),.201f,Madder);break;
                case "timber_camp":
                    for(int i=0;i<3;i++)
                        g.Bevel(V(.76f,.084f+i*.108f,.008f+i*.099f),V(1.29f-i*.11f,.16f,.55f),.042f,Pigment(Stonework,.91f+i*.041f));
                    g.Torus(V(.66f,1.77f,.54f),.14f,.034f,WroughtIron,12);
                    for(int i=0;i<2;i++)
                    {
                        var center=V(-.85f+i*.75f,.31f,-1.05f);
                        g.Cylinder(center+V(-.19f,-.28f,0),center+V(.15f,.21f,0),.044f,Walnut,5);
                        g.Cylinder(center+V(.19f,-.28f,0),center+V(-.15f,.21f,0),.044f,Walnut,5);
                    }
                    break;
                case "granary":
                    for(int side=-1;side<=1;side+=2)
                    {
                        float x=side*.76f;
                        g.Box(V(x,1.05f,-.531f),V(.35f,.35f,.050f),Dark);
                        for(int i=0;i<4;i++)g.Bevel(V(x,.939f+i*.073f,-.572f),V(.36f,.035f,.072f),.007f,Walnut,Quaternion.Euler(18,0,0));
                    }
                    g.Bevel(V(-.62f,.245f,-.84f),V(.40f,.069f,1.02f),.014f,WoodLight,Quaternion.Euler(-23,0,0));
                    Sack(g,V(-1.27f,0,-.63f),.19f,Linen);break;
                case "smithy":
                    g.Ellipsoid(V(1.33f,.40f,.26f),V(.29f,.18f,.37f),Walnut,8,4);
                    g.Cylinder(V(1.29f,.42f,.12f),V(1.02f,.40f,-.28f),.062f,Steel,7);
                    WorkLadder(g,V(-1.32f,0,-.57f),1.13f,.23f);break;
                case "barracks":WatchTurret(g,V(-1.24f,0,.73f),.96f);break;
                case "drill_yard":
                    for(int i=0;i<3;i++)
                    {
                        var basePoint=V(.26f+i*.26f,0,-.62f);
                        g.Cylinder(basePoint,basePoint+V(.043f,1.02f,0),.024f,WoodLight,6);
                        g.Spear(basePoint+V(.043f,.98f,0),basePoint+V(.05f,1.21f,0),.058f,Steel);
                    }
                    break;
                case "pasture": Wattle(g,V(-1.45f,0,.98f),V(-.22f,0,1.16f),.46f);break;
                case "stables": DyeTub(g,V(1.19f,0,-.84f),.254f,Glass);Wattle(g,V(-1.46f,0,1.16f),V(.23f,0,1.16f),.44f);break;
                case "cottage": Wattle(g,V(.27f,0,-1.30f),V(1.47f,0,-1.29f),.32f);break;
            }
        }

        static void AnnexFinish(Geometry g,string kind,Vector3 p,bool upper)
        {
            switch(kind)
            {
                case "chapel":
                    LeadedWindow(g,p+V(0,upper?1.26f:.39f,upper?-.36f:-.416f),upper?.30f:.27f,upper?.53f:.57f,Quaternion.identity);
                    if(upper)g.Loft(new[]{p+V(0,1.55f,0),p+V(0,1.61f,0),p+V(0,1.80f,0)},
                        new[]{Section(.219f,.219f),Section(.162f,.162f),Section(.081f,.081f)},12,2,Copper);break;
                case "tavern":case "weaver":case "smithy":
                    if(upper)SagLine(g,p+V(-.34f,1.53f,0),p+V(.33f,1.53f,-.09f),.10f,.022f,WroughtIron);
                    else DyeTub(g,p+V(.25f,0,-.31f),.173f,kind=="weaver"?Indigo:WoodLight);break;
                case "barracks":case "drill_yard":WatchTurret(g,p,upper?.92f:.67f);break;
                case "timber_camp":
                    if(upper)WorkLadder(g,p+V(-.29f,0,-.37f),1.64f,.26f);
                    else g.Bevel(p+V(0,.86f,0),V(.84f,.16f,.62f),.061f,Rock);break;
                case "irrigated_field":case "pasture":case "stables":
                    Wattle(g,p+V(-.35f,0,-.32f),p+V(.33f,0,-.36f),upper?.54f:.40f);break;
                case "farm":case "granary":case "bakery":
                    if(upper)WorkLadder(g,p+V(.16f,0,-.34f),1.46f,.24f);
                    else {Sack(g,p+V(-.19f,0,-.30f),.168f,Linen);Sack(g,p+V(.12f,0,-.26f),.152f,Ochre);}break;
                case "cottage":
                    UpperWindow(g,p+V(0,0,-.47f),Quaternion.identity,upper?1.46f:.61f,.035f,.16f);break;
            }
        }
    }
}
