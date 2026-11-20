using Frontline.Core;
using Frontline.UnityGame;
using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static Geometry RetinueGuard(int lod)
        {
            var model=lod==0?Body("shield",lod):DistantBody("shield");
            foreach(float x in new[]{-.15f,.15f})model.Append(Leg(lod),Matrix4x4.Translate(V(x,-.53f,0)));
            model.Append(Arm("militia",true,lod),Matrix4x4.identity);
            model.Append(Arm("militia",true,lod),Matrix4x4.Translate(V(.70f,0,0)));
            model.Bevel(V(-.235f,-.04f,.235f),V(.43f,.66f,.10f),.068f,Gold);
            model.Bevel(V(-.235f,-.04f,.29f),V(.35f,.57f,.035f),.046f,Indigo);
            model.Box(V(-.235f,-.04f,.313f),V(.045f,.34f,.017f),Gold);
            model.Box(V(-.235f,.015f,.314f),V(.23f,.047f,.018f),Gold);
            model.Bevel(V(0,.673f,.195f),V(.38f,.046f,.045f),.013f,Gold);
            model.Bevel(V(0,.24f,.22f),V(.32f,.064f,.035f),.015f,Gold);
            model.Cylinder(V(.36f,-.78f,.15f),V(.36f,1.07f,.15f),.029f,WoodLight,5);
            model.Spear(V(.36f,.99f,.15f),V(.36f,1.32f,.15f),.081f,LightSteel);
            model.Cylinder(V(.36f,.94f,.15f),V(.36f,1.04f,.15f),.042f,Gold,5);
            if(lod==0)
            {
                ShieldInlay(model,-.235f,.322f);
                model.Quad(V(-.15f,.74f,-.025f),V(-.06f,.91f,-.086f),V(.08f,.92f,-.183f),V(.112f,.735f,-.16f),Indigo,true);
                model.Triangle(V(-.15f,.74f,-.025f),V(.08f,.92f,-.183f),V(.135f,.83f,-.18f),Gold);
            }
            model.SetAtGrade();
            return model;
        }
        static void BakeEstateArt(int lod)
        {
            Save("manor_plaza",lod,CobblePlaza(lod));
            Save("manor_well",lod,StoneWell(lod));
            Save("manor_market",lod,MarketStall(lod));
            Save("manor_dock",lod,ShoreDock(lod));
            Save("manor_fence",lod,SplitRailFence(lod));
            Save("manor_gatehouse",lod,ManorGatehouse(lod));
            Save("manor_laundry",lod,YardProp(lod,0));
            Save("manor_firewood",lod,YardProp(lod,1));
            Save("manor_straw",lod,YardProp(lod,2));
            Save("manor_roof_snow",lod,CottageSnow());
            BakeBackyards(lod);
            for(int i=0;i<Manor.SpecialistIds.Length;i++)
                Save("specialist_"+Manor.SpecialistIds[i],lod,Specialist(i,lod));
        }

        static void Bakery(Geometry g,int lod)
        {
            House(g,-.38f,.16f,2.08f,1.70f,1.25f,.57f,Tile,lod);
            Door(g,-.55f,-.69f,.83f,.43f);
            if(lod==0)Window(g,-1.01f,.89f,-.69f,lod);
            g.Frustum(V(.84f,.068f,.19f),V(.93f,0,.97f),V(.78f,1.87f,.16f),V(.73f,0,.83f),Stonework,lod==0?8:5);
            Gable(g,V(.79f,1.82f,.16f),1.04f,1.16f,.47f,Slate,Plaster,lod);
            var hub=ManorPlotArt.BakeryHub;
            g.Cylinder(hub+V(0,0,-.015f),V(hub.x,hub.y,-.14f),.073f,WoodLight,lod==0?8:5);
            Chimney(g,-1.18f,.32f,2.24f,.26f);
            BreadStall(g,V(-.52f,0,-1.02f),lod);
            g.Box(V(.86f,.41f,-.78f),V(.46f,.60f,.38f),Stonework);
            g.Box(V(.86f,.38f,-.979f),V(.29f,.31f,.032f),Dark);
            if(lod==0)
            {
                g.Cylinder(V(.86f,.54f,-.97f),V(.86f,.54f,-1.013f),.18f,Stonework,8);
                g.Box(V(.86f,.414f,-1.002f),V(.241f,.080f,.023f),Amber);
                for(int i=0;i<3;i++)
                    g.Box(V(-1.18f,1.53f+.18f*i,.18f),V(.274f,.035f,.021f),Slate);
            }
        }

        static void BreadStall(Geometry g,Vector3 at,int lod)
        {
            g.Box(at+V(0,.47f,0),V(.89f,.106f,.47f),WoodLight);
            foreach(int side in new[]{-1,1})
                g.Box(at+V(side*.32f,.24f,0),V(.13f,.47f,.31f),Wood);
            if(lod!=0)return;
            for(int i=0;i<4;i++)
            {
                var loaf=at+V(-.31f+i*.20f,.57f,(i%2)*.085f-.044f);
                g.Ellipsoid(loaf,V(.091f,.064f,.136f),i%2==0?Ochre:StrawLight,7,2);
                g.Quad(loaf+V(-.052f,.05f,.006f),loaf+V(-.016f,.064f,.065f),
                    loaf+V(.052f,.054f,.021f),loaf+V(.029f,.068f,-.034f),Linen,true);
            }
            g.Ellipsoid(at+V(.61f,.22f,.02f),V(.17f,.22f,.18f),Linen,8,2);
        }

        static void Weaver(Geometry g,int lod)
        {
            House(g,-.42f,.21f,2.01f,1.76f,1.11f,.55f,Slate,lod);
            Door(g,-.69f,-.67f,.76f,.46f);
            if(lod==0)Window(g,.109f,.69f,-.67f,lod);
            Canopy(g,V(1.01f,0,.52f),1.27f,Tile);
            DyeRack(g,V(.73f,0,-.76f),lod);
            g.Box(V(1.15f,.29f,-.16f),V(.66f,.46f,.51f),WoodLight);
            if(lod==0)
            {
                for(int i=0;i<4;i++)
                    g.Cylinder(V(.93f+i*.116f,.44f,.02f),V(.93f+i*.116f,.83f,-.11f),.015f,Linen,4);
                g.Ellipsoid(V(-1.31f,.15f,-.91f),V(.237f,.172f,.23f),WoodLight,7,2);
                g.Ellipsoid(V(-1.33f,.29f,-.89f),V(.183f,.105f,.19f),Linen,8,2);
            }
        }

        static void DyeRack(Geometry g,Vector3 at,int lod)
        {
            foreach(int side in new[]{-1,1})
                g.Box(at+V(side*.53f,.77f,0),V(.087f,1.54f,.10f),Wood);
            g.Box(at+V(0,1.36f,.011f),V(1.16f,.069f,.065f),WoodLight);
            int bands=lod==0?3:2;
            for(int band=0;band<bands;band++)
            {
                float x=-.46f+band*(lod==0?.32f:.47f), low=.66f+(band%2)*.14f;
                float width=lod==0?.27f:.38f;
                Color dye=band==0?Copper:band==1?new Color(.31f,.61f,.58f,0):new Color(.84f,.57f,.19f,0);
                g.Quad(at+V(x,1.32f,-.034f),at+V(x+width,1.32f,-.034f),
                    at+V(x+width+.02f,low,-.076f),at+V(x-.01f,low-.05f,-.108f),dye,true);
                if(lod==0)
                    g.Quad(at+V(x+.123f,1.30f,-.039f),at+V(x+.157f,1.30f,-.039f),
                        at+V(x+.175f,low,-.105f),at+V(x+.087f,low-.04f,-.119f),dye*.74f,true);
            }
        }

        static void CraftAnnex(Geometry g,string kind,Vector3 at,int lod,bool tower)
        {
            if(kind==Manor.Bakery)
            {
                if(tower)
                {
                    if(lod==0)g.Bevel(at+V(.06f,.66f,0),V(.71f,1.31f,.65f),.067f,Stonework);
                    else g.Box(at+V(.06f,.66f,0),V(.71f,1.31f,.65f),Stonework);
                    g.Box(at+V(.06f,.59f,-.337f),V(.41f,.42f,.065f),Dark);
                    g.Box(at+V(.06f,.45f,-.373f),V(.34f,.065f,.025f),Amber);
                    g.Box(at+V(.06f,1.58f,.13f),V(.30f,.72f,.35f),Stonework);
                    if(lod==0)g.Box(at+V(.06f,1.94f,.13f),V(.39f,.109f,.42f),Slate);
                }
                else
                {
                    Canopy(g,at,1.28f,Tile);
                    BreadStall(g,at-V(0,0,.28f),lod);
                }
            }
            else
            {
                if(tower)
                {
                    if(lod==0)Rack(g,at,1.86f);
                    else g.Box(at+V(0,.91f,0),V(.16f,1.82f,.17f),Wood);
                    g.Box(at+V(0,1.46f,-.08f),V(.66f,.44f,.15f),Indigo);
                    if(lod==0)g.Box(at+V(0,1.46f,-.17f),V(.38f,.067f,.028f),Gold);
                }
                else DyeRack(g,at+V(.19f,0,-.31f),lod);
            }
        }

        static void Granary(Geometry g,int lod)
        {
            int n=lod==0?8:5;
            g.Bevel(V(0,.12f,.25f),V(2.66f,.24f,1.78f),.075f,Stonework);
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                var pier=V(1.02f*x,.29f,.25f+.61f*z);
                g.Cylinder(pier-Vector3.up*.17f,pier+Vector3.up*.13f,.19f,Rock,n);
                g.Box(pier+Vector3.up*.10f,V(.46f,.12f,.43f),Stonework);
            }
            g.Box(V(0,.46f,.25f),V(2.42f,.15f,1.52f),Wood);
            g.Box(V(0,1.02f,.25f),V(2.22f,1.02f,1.40f),WoodLight);
            Gable(g,V(0,1.53f,.25f),2.69f,1.80f,.74f,Tile,Plaster,lod);
            for(int i=0;i<(lod==0?9:4);i++)
                g.Box(V(-.97f+i*(lod==0?.244f:.646f),1.0f,-.472f),V(.044f,1.03f,.04f),Wood);
            foreach(float x in new[]{-.96f,.96f})
                g.Cylinder(V(x,.52f,-.49f),V(x*.50f,1.46f,-.49f),.047f,Wood,5);
            g.Box(V(0,.96f,-.494f),V(.74f,.94f,.105f),Wood);
            g.Box(V(0,.95f,-.56f),V(.56f,.77f,.055f),Dark);
            g.Box(V(.23f,.92f,-.595f),V(.065f,.15f,.04f),Gold);
            for(int i=0;i<3;i++)
                g.Box(V(0,.075f+.145f*i,-1.14f+.17f*i),V(.78f,.15f,.30f),i==0?Rock:WoodLight);
            g.Ellipsoid(V(.97f,.27f,-.80f),V(.23f,.25f,.24f),Linen,n,3);
            g.Ellipsoid(V(1.20f,.19f,-.53f),V(.20f,.19f,.21f),Ochre,n,3);
        }

        static void GranaryAnnex(Geometry g,Vector3 at,int lod)
        {
            Canopy(g,at,1.07f,Tile);
            g.Box(at+V(0,.15f,0),V(.88f,.27f,.80f),WoodLight);
            g.Ellipsoid(at+V(-.24f,.43f,0),V(.21f,.37f,.28f),Linen,lod==0?8:5,3);
            g.Ellipsoid(at+V(.24f,.41f,-.12f),V(.20f,.34f,.24f),Ochre,lod==0?8:5,3);
        }

        static void GranaryLoft(Geometry g,Vector3 at,int lod)
        {
            g.Box(at+V(0,.80f,0),V(.82f,1.57f,.83f),WoodLight);
            g.Box(at+V(0,.13f,0),V(.90f,.26f,.91f),Stonework);
            Gable(g,at+V(0,1.57f,0),1.04f,1.05f,.50f,Tile,Wood,lod);
            g.Box(at+V(0,1.10f,-.44f),V(.42f,.38f,.062f),Dark);
            g.Cylinder(at+V(-.48f,.16f,-.43f),at+V(-.29f,1.48f,-.43f),.041f,Wood,5);
        }

        static void Lancet(Geometry g,float x,float y,float z,float width,float height,int lod)
        {
            float half=width*.50f;
            Vector3 a=V(x-half,y,z),b=V(x+half,y,z),c=V(x+half,y+.61f*height,z),d=V(x-half,y+.61f*height,z);
            var tip=V(x,y+height,z);
            g.Quad(a,b,c,d,Dark,true);g.Triangle(d,c,tip,Dark);
            float pane=width*.36f;
            g.Quad(V(x-pane,y+.07f,z-.014f),V(x,y+.07f,z-.014f),V(x,y+.60f*height,z-.014f),
                V(x-pane,y+.55f*height,z-.014f),new Color(.25f,.53f,.67f),true);
            g.Quad(V(x,y+.07f,z-.014f),V(x+pane,y+.07f,z-.014f),V(x+pane,y+.55f*height,z-.014f),
                V(x,y+.60f*height,z-.014f),new Color(.75f,.31f,.24f),true);
            g.Triangle(V(x-pane,y+.55f*height,z-.015f),V(x+pane,y+.55f*height,z-.015f),V(x,y+.89f*height,z-.015f),Amber);
            if(lod==0)g.Box(V(x,y+.33f*height,z-.024f),V(.043f,height*.53f,.03f),Stonework);
        }

        static void Chapel(Geometry g,int lod)
        {
            g.Bevel(V(-.20f,.11f,.24f),V(2.94f,.22f,2.05f),.095f,Rock);
            g.Box(V(-.39f,.83f,.31f),V(2.22f,1.50f,1.67f),Stonework);
            Gable(g,V(-.39f,1.60f,.31f),2.45f,1.87f,.84f,Slate,Stonework,lod);
            Lancet(g,-1.02f,.62f,-.54f,.48f,.78f,lod);
            Lancet(g,-.27f,.62f,-.54f,.39f,.69f,lod);
            var foot=V(.93f,0,-.32f);
            g.Bevel(foot+Vector3.up*1.28f,V(1.12f,2.45f,1.34f),.065f,Stonework);
            g.Box(foot+V(0,2.12f,0),V(1.18f,.13f,1.37f),Rock);
            Door(g,.93f,-.99f,.91f,.51f);
            Lancet(g,.93f,1.13f,-1.002f,.52f,.79f,lod);
            var spire=new Geometry();
            spire.Cone(Vector3.zero,1.13f,1.48f,Slate,4);
            g.Append(spire,Matrix4x4.TRS(foot+Vector3.up*2.45f,Quaternion.Euler(0,45,0),Vector3.one));
            g.Cylinder(foot+Vector3.up*3.86f,foot+Vector3.up*4.12f,.044f,Copper,lod==0?7:5);
            g.Spear(foot+Vector3.up*4.04f,foot+Vector3.up*4.23f,.083f,Gold);
            if(lod==0)
                for(int i=0;i<5;i++)
                {
                    float y=.36f+i*.31f;
                    g.Box(V(1.4f,y,-.91f),V(.18f,.065f,.115f),Rock);
                    g.Box(V(-1.42f,y,-.50f),V(.18f,.065f,.115f),Rock);
                }
        }

        static void ChapelAnnex(Geometry g,Vector3 at,int lod)
        {
            g.Bevel(at+V(0,.55f,.05f),V(.92f,1.07f,.88f),.07f,Stonework);
            Gable(g,at+V(0,1.08f,.05f),1.12f,.99f,.52f,Slate,Stonework,lod);
            Lancet(g,at.x,.40f,at.z-.40f,.35f,.62f,lod);
        }

        static void ChapelBelfry(Geometry g,Vector3 at,int lod)
        {
            g.Bevel(at+V(0,.81f,0),V(.71f,1.62f,.70f),.045f,Stonework);
            g.Cylinder(at+V(0,1.55f,0),at+V(0,1.91f,0),.17f,Copper,lod==0?9:6);
            foreach(float x in new[]{-.30f,.30f})g.Box(at+V(x,1.92f,0),V(.10f,.67f,.70f),Rock);
            Gable(g,at+V(0,2.22f,0),.91f,.88f,.48f,Slate,Stonework,lod);
        }

        static Geometry CobblePlaza(int lod)
        {
            var g=new Geometry();
            g.Box(V(0,.016f,0),V(13.18f,.029f,6.52f),new Color(.48f,.46f,.40f));
            int cols=lod==0?17:9,rows=lod==0?9:5;
            float w=13.1f/cols,d=6.46f/rows;
            for(int z=0;z<rows;z++)for(int x=0;x<cols;x++)
            {
                float left=-6.55f+x*w+.02f,bottom=-3.23f+z*d+.016f;
                float inset=x%3==1?.06f:.027f;
                float top=.041f+((x*13+z*7)%3)*.003f;
                var color=Color.Lerp(new Color(.48f,.47f,.44f),new Color(.68f,.62f,.49f),((x*71+z*31)%11)*.10f);
                g.Quad(V(left+inset,top,bottom),V(left,top,bottom+d-.038f),
                    V(left+w-.052f,top,bottom+d-.038f),V(left+w-.034f,top,bottom+.028f),color);
            }
            return g;
        }

        static Geometry StoneWell(int lod)
        {
            var g=new Geometry();
            int n=lod==0?12:8;
            Vector3 Rim(float angle,float radius,float y)=>V(radius*Mathf.Cos(angle),y,radius*Mathf.Sin(angle));
            for(int i=0;i<n;i++)
            {
                float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;
                var color=Color.Lerp(Stonework,Rock,.28f+(i%4)*.17f);
                g.Quad(Rim(a,.81f,0),Rim(a,.86f,.60f),Rim(b,.86f,.60f),Rim(b,.81f,0),color);
                g.Quad(Rim(a,.88f,.63f),Rim(a,.53f,.63f),Rim(b,.53f,.63f),Rim(b,.88f,.63f),Stonework);
                g.Quad(Rim(a,.53f,.63f),Rim(a,.53f,.19f),Rim(b,.53f,.19f),Rim(b,.53f,.63f),Rock);
                if(lod==0)g.Quad(Rim(a,.851f,.25f),Rim(a,.853f,.31f),Rim(b,.853f,.31f),Rim(b,.851f,.25f),Rock);
            }
            g.Cylinder(V(0,.22f,0),V(0,.24f,0),.54f,Glass,n);
            foreach(float x in new[]{-.70f,.70f})g.Box(V(x,1.16f,0),V(.13f,1.76f,.15f),Wood);
            g.Cylinder(V(-.76f,1.34f,0),V(.79f,1.34f,0),.058f,WoodLight,n);
            g.Cylinder(V(.79f,1.34f,0),V(.79f,1.54f,.11f),.031f,Steel,5);
            g.Cylinder(V(0,1.35f,.04f),V(0,.62f,.04f),.016f,Linen,5);
            g.Barrel(V(0,.70f,.04f),.115f,.23f,WoodLight,n);
            Gable(g,V(0,1.93f,0),1.76f,1.22f,.43f,Tile,Plaster,lod);
            return g;
        }

        static Geometry MarketStall(int lod)
        {
            var g=new Geometry();
            foreach(float x in new[]{-.86f,.86f})foreach(float z in new[]{-.49f,.49f})
                g.Box(V(x,.78f,z),V(.095f,1.56f,.10f),Wood);
            g.Box(V(0,.73f,-.25f),V(1.85f,.13f,.55f),WoodLight);
            g.Box(V(0,.35f,-.42f),V(1.74f,.63f,.075f),Wood);
            int bands=lod==0?7:5;
            for(int i=0;i<bands;i++)
            {
                float x=-1.03f+i*(2.06f/bands),xx=x+2.06f/bands;
                Color color=i%2==0?new Color(.68f,.26f,.18f,1):Linen;
                g.Quad(V(x,1.66f,.69f),V(xx,1.66f,.69f),V(xx,1.40f,-.70f),V(x,1.40f,-.70f),color,true);
                g.Quad(V(x,1.40f,-.70f),V(xx,1.40f,-.70f),V(xx,1.28f,-.70f),V(x,1.28f,-.70f),color,true);
            }
            int n=lod==0?8:5;
            g.Barrel(V(.58f,.23f,.16f),.22f,.46f,WoodLight,n);
            g.Box(V(-.49f,.91f,-.29f),V(.43f,.23f,.33f),Copper);
            for(int i=0;i<(lod==0?4:2);i++)
                g.Ellipsoid(V(-.64f+i*.11f,1.02f,-.29f),V(.081f,.08f,.09f),i%2==0?Ochre:Leaf,n,2);
            g.Ellipsoid(V(.23f,.94f,-.24f),V(.20f,.16f,.17f),Linen,n,3);
            return g;
        }

        static Geometry ShoreDock(int lod)
        {
            var g=new Geometry();
            int planks=lod==0?11:6;
            for(int i=0;i<planks;i++)
            {
                float step=4.21f/planks;
                g.Box(V(0,.09f,-.44f-step*i),V(2.40f,.16f,step-.025f),Color.Lerp(Wood,WoodLight,.36f+(i%4)*.13f));
            }
            foreach(float x in new[]{-1.01f,1.01f})foreach(float z in new[]{-.45f,-2.34f,-4.64f})
            {
                var post=V(x,0,z);
                g.Cylinder(post-V(0,1.06f,0),post+V(0,.54f,0),.117f,Wood,lod==0?7:5);
                g.Cylinder(post+V(0,.28f,0),post+V(0,.38f,0),.131f,Linen,6);
            }
            for(int i=0;i<4;i++)
                g.Box(V(0,.17f+.13f*i,-.10f+.245f*i),V(1.63f,.16f,.31f),WoodLight);
            return g;
        }

        static Geometry SplitRailFence(int lod)
        {
            var g=new Geometry();
            Vector3 Post(int i)=>V(i*1.19f-1.785f,0,i%2==0?.12f:-.10f);
            for(int i=0;i<4;i++)
            {
                var p=Post(i);
                g.Cylinder(p,p+Vector3.up*(.86f+i%2*.08f),.091f,Wood,lod==0?7:5);
                if(i==3)break;
                for(int row=0;row<2;row++)
                    g.Cylinder(p+Vector3.up*(.34f+row*.33f),Post(i+1)+Vector3.up*(.29f+row*.37f),.056f,WoodLight,5);
            }
            return g;
        }

        static Geometry ManorGatehouse(int lod)
        {
            var g=new Geometry();
            foreach(int side in new[]{-1,1})
            {
                float z=3.48f*side;
                g.Bevel(V(0,.18f,z),V(1.36f,.36f,.66f),.09f,Stonework);
                foreach(float x in new[]{-.46f,.46f})
                    g.Box(V(x,1.71f,z),V(.20f,3.12f,.23f),Wood);
                g.Box(V(0,2.38f,z),V(1.18f,.31f,.38f),WoodLight);
                g.Cylinder(V(-.50f,2.73f,z),V(-.50f,3.28f,z-side*.87f),.075f,WoodLight,5);
                g.Cylinder(V(.50f,2.73f,z),V(.50f,3.28f,z-side*.87f),.075f,WoodLight,5);
                if(lod==0)Lantern(g,V(-.61f,2.12f,z+side*.28f),lod);
            }
            g.Box(V(0,3.33f,0),V(1.30f,.28f,7.31f),Wood);
            var roof=new Geometry();
            Gable(roof,Vector3.zero,7.62f,1.54f,.56f,Slate,Plaster,lod);
            g.Append(roof,Matrix4x4.TRS(V(0,3.48f,0),Quaternion.Euler(0,90,0),Vector3.one));
            Pennant(g,V(-.65f,3.11f,2.67f),1.13f,.63f,Ochre,lod);
            return g;
        }

        static Geometry YardProp(int lod,int kind)
        {
            var g=new Geometry();
            if(kind==0)
            {
                for(int i=-1;i<=1;i+=2)g.Cylinder(V(i*.33f,0,0),V(i*.33f,.83f,0),.032f,Wood,5);
                g.Cylinder(V(-.33f,.78f,0),V(.33f,.78f,0),.016f,Linen,5);
                g.Quad(V(-.25f,.78f,0),V(-.04f,.78f,0),V(-.02f,.45f,.06f),V(-.23f,.50f,.05f),Linen,true);
                g.Quad(V(.03f,.78f,0),V(.26f,.78f,0),V(.31f,.48f,-.044f),V(.04f,.51f,-.04f),Copper,true);
                g.Box(V(0,.17f,.21f),V(.43f,.17f,.26f),WoodLight);
                foreach(float x in new[]{-.24f,.24f})
                    g.Cylinder(V(x,.12f,.22f),V(x+.03f,.12f,.22f),.12f,Wood,7);
            }
            else if(kind==1)
            {
                for(int row=0;row<3;row++)for(int i=0;i<3-row;i++)
                {
                    var a=V(-.21f+i*.18f+row*.085f,.10f+row*.13f,-.24f);
                    g.Cylinder(a,a+V(.041f,0,.49f),.082f,i%2==0?WoodLight:Wood,lod==0?7:5);
                }
            }
            else
            {
                g.Bevel(V(0,.26f,0),V(.72f,.52f,.57f),.11f,Straw);
                foreach(float x in new[]{-.20f,.20f})
                {
                    g.Box(V(x,.48f,0),V(.036f,.052f,.55f),Ochre);
                    g.Box(V(x,.24f,-.29f),V(.036f,.40f,.012f),Ochre);
                }
            }
            return g;
        }

        static Geometry CottageSnow()
        {
            var g=new Geometry();
            void Cap(float x,float z,float halfWidth,float halfDepth,float eave,float rise)
            {
                foreach(int side in new[]{-1,1})
                {
                    float edge=z+side*halfDepth*.76f;
                    float y=eave+rise*.24f+.074f;
                    g.Quad(V(x-halfWidth+.06f,eave+rise+.056f,z),V(x+halfWidth-.07f,eave+rise+.056f,z),
                        V(x+halfWidth-.16f,y,edge),V(x-halfWidth+.11f,y+.019f,edge),Snow,true);
                }
            }
            Cap(-.55f,.05f,.97f,.94f,1,.71f);
            Cap(.91f,.29f,.705f,.73f,.83f,.50f);
            return g;
        }

        static Geometry Specialist(int role,int lod)
        {
            if(lod==1&&role>=4)return DistantCraftSpecialist(role==4);
            var g=new Geometry();
            int n=lod==0?9:6;
            Color coat=role==0?new Color(.17f,.24f,.36f,0):role==1?new Color(.55f,.25f,.16f,0):
                role==2?new Color(.31f,.47f,.32f,0):role==3?new Color(.28f,.37f,.43f,0):
                role==4?new Color(.52f,.36f,.22f,0):new Color(.32f,.24f,.54f,0);
            Tunic(g,lod,coat);
            g.Ellipsoid(V(0,.51f,.025f),V(.167f,.20f,.16f),Skin,n,3);
            g.Frustum(V(0,-.17f,0),V(.469f,0,.366f),V(0,-.10f,0),V(.475f,0,.372f),Wood,n);
            g.Box(V(0,-.115f,.188f),V(.16f,.083f,.048f),new Color(.74f,.68f,.46f,1));
            g.Quad(V(-.245f,.29f,-.18f),V(.245f,.29f,-.18f),V(.33f,-.38f,-.26f),V(-.31f,-.38f,-.26f),coat*.78f,true);
            if(lod!=0)foreach(float x in new[]{-.12f,.12f})
            {
                g.Cylinder(V(x,-.41f,0),V(x,-.74f,0),.083f,Dark,5);
                g.Box(V(x,-.70f,.055f),V(.16f,.15f,.26f),Wood);
            }
            Vector3 left=V(-.30f,.21f,.015f),right=V(.30f,.21f,.015f);
            var leftHand=V(-.19f,-.027f,.31f);
            var rightHand=role==3?V(.39f,-.04f,.13f):V(.19f,-.034f,.31f);
            g.Cylinder(left,leftHand,.080f,coat,n);g.Cylinder(right,rightHand,.080f,coat,n);
            g.Ellipsoid(leftHand,V(.073f,.065f,.076f),Skin,6,2);
            g.Ellipsoid(rightHand,V(.073f,.065f,.076f),Skin,6,2);
            if(role==0)
            {
                g.Cylinder(V(0,.608f,.009f),V(0,.76f,.006f),.168f,Indigo,n);
                g.Cylinder(V(0,.603f,.009f),V(0,.631f,.009f),.196f,Dark,n);
                g.Box(V(0,.017f,.333f),V(.38f,.08f,.25f),Copper);
                g.Box(V(0,.053f,.340f),V(.34f,.022f,.22f),Linen);
            }
            else if(role==1)
            {
                g.Ellipsoid(V(0,.66f,-.040f),V(.22f,.155f,.19f),Copper,n,3);
                g.Cylinder(V(0,.605f,-.004f),V(0,.627f,-.004f),.225f,Ochre,n);
                g.Ellipsoid(V(0,-.016f,.337f),V(.17f,.15f,.12f),Linen,n,3);
            }
            else if(role==2)
            {
                g.Ellipsoid(V(0,.66f,-.055f),V(.201f,.15f,.184f),Moss,n,3);
                g.Bevel(V(0,.083f,-.270f),V(.43f,.39f,.219f),.042f,WoodLight);
                g.Box(V(0,.091f,-.388f),V(.09f,.23f,.026f),Linen);
                g.Box(V(0,.083f,-.389f),V(.25f,.08f,.026f),Linen);
                g.Frustum(V(.12f,-.13f,.318f),V(.14f,0,.15f),V(.11f,.081f,.318f),V(.105f,0,.10f),Copper,n);
            }
            else if(role==3)
            {
                g.Ellipsoid(V(0,.62f,.01f),V(.199f,.174f,.179f),Steel,n,3);
                g.Box(V(0,.569f,.143f),V(.44f,.066f,.164f),LightSteel);
                g.Bevel(V(0,.07f,.148f),V(.42f,.40f,.12f),.038f,Steel);
                var foot=V(.39f,-.61f,.14f);
                g.Cylinder(foot,foot+V(0,1.60f,0),.028f,Wood,n);
                g.Spear(foot+V(0,1.47f,0),foot+V(0,1.87f,0),.068f,Steel);
                g.Quad(foot+V(0,1.43f,.024f),foot+V(.25f,1.49f,.035f),foot+V(.20f,1.72f,.028f),foot+V(0,1.62f,.024f),LightSteel,true);
            }
            else if(role==4) MasonKit(g,n,lod);
            else MinstrelKit(g,n,lod);
            if(lod==0)CapePleats(g,coat,.26f,-.34f);
            return g;
        }

        static Geometry DistantCraftSpecialist(bool mason)
        {
            var g=new Geometry();
            var coat=mason?Copper:new Color(.53f,.19f,.42f,0);
            g.Frustum(V(0,-.38f,0),V(.52f,0,.40f),V(0,.26f,0),V(.48f,0,.36f),coat,5);
            g.Ellipsoid(V(0,.51f,.026f),V(.165f,.20f,.169f),Skin,6,2);
            g.Cone(V(0,.62f,-.024f),.222f,.16f,mason?WoodLight:Indigo,6);
            for(int side=-1;side<=1;side+=2)
            {
                g.Box(V(side*.12f,-.55f,.028f),V(.17f,.38f,.22f),Dark);
                g.Cylinder(V(side*.25f,.22f,.011f),V(side*.16f,-.062f,.26f),.064f,coat,4);
            }
            if(mason)
            {
                g.Quad(V(-.14f,.14f,.176f),V(.14f,.14f,.176f),V(.20f,-.33f,.232f),V(-.195f,-.33f,.232f),Linen,true);
                g.Box(V(.174f,.123f,.334f),V(.09f,.31f,.065f),Wood);
                g.Box(V(.174f,.272f,.334f),V(.24f,.086f,.113f),Steel);
            }
            else
            {
                g.Ellipsoid(V(-.076f,-.051f,.324f),V(.16f,.135f,.07f),WoodLight,6,2);
                g.Quad(V(-.070f,-.035f,.359f),V(.33f,.229f,.389f),V(.302f,.257f,.385f),V(-.099f,.002f,.354f),Linen,true);
                g.Triangle(V(.139f,.655f,-.01f),V(.274f,.867f,-.135f),V(.22f,.742f,-.125f),Linen);
            }
            return g;
        }

        static void MasonKit(Geometry g,int sides,int lod)
        {
            g.Cylinder(V(0,.60f,0),V(0,.66f,0),.222f,WoodLight,sides);
            g.Ellipsoid(V(0,.675f,-.039f),V(.176f,.11f,.172f),Copper,sides,2);
            g.Quad(V(-.16f,.22f,.19f),V(.15f,.22f,.19f),V(.195f,-.37f,.245f),V(-.19f,-.37f,.24f),WoodLight,true);
            g.Cylinder(V(.19f,-.19f,.32f),V(.19f,.17f,.36f),.034f,Wood,sides);
            g.Bevel(V(.19f,.185f,.36f),V(.22f,.105f,.14f),.024f,Steel);
            g.Cylinder(V(-.31f,-.016f,.33f),V(-.081f,-.016f,.34f),.059f,Linen,sides);
            if(lod==0)
            {
                g.Box(V(-.070f,-.09f,.204f),V(.101f,.076f,.033f),Ochre);
                g.Box(V(-.175f,-.184f,.234f),V(.037f,.181f,.030f),LightSteel);
            }
        }

        static void MinstrelKit(Geometry g,int sides,int lod)
        {
            var silk=new Color(.61f,.17f,.37f,0);
            g.Ellipsoid(V(.005f,.675f,-.035f),V(.223f,.109f,.19f),silk,sides,2);
            g.Quad(V(.13f,.667f,-.02f),V(.215f,.83f,-.071f),V(.29f,.947f,-.181f),V(.245f,.763f,-.151f),Linen,true);
            g.Quad(V(-.265f,.265f,.101f),V(-.042f,.332f,.197f),V(.173f,.02f,.21f),V(-.17f,-.094f,.216f),silk,true);
            g.Ellipsoid(V(-.092f,-.062f,.364f),V(.196f,.171f,.079f),WoodLight,sides,3);
            g.Cylinder(V(-.096f,-.063f,.443f),V(-.096f,-.063f,.456f),.054f,Dark,sides);
            g.Cylinder(V(-.087f,-.021f,.403f),V(.375f,.242f,.433f),.040f,WoodLight,5);
            if(lod==0)
                for(int stringIndex=0;stringIndex<3;stringIndex++)
                    g.Cylinder(V(-.221f,-.124f+.014f*stringIndex,.44f),V(.375f,.215f+stringIndex*.015f,.467f),.004f,Linen,3);
        }
    }
}
