using Frontline.Core;
using Frontline.UnityGame;
using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static readonly Color Indigo=new Color(.16f,.25f,.38f,0), Linen=new Color(.91f,.85f,.67f,0),
            Copper=new Color(.67f,.35f,.18f,0), Amber=new Color(1,.70f,.23f,0), Moss=new Color(.25f,.39f,.22f,0);

        static void BakeManorArt(int lod)
        {
            Save("mill_sails",lod,MillSails(lod));
            Save("farmer_clothes",lod,ResidentCoat(true,lod));
            Save("villager_clothes",lod,ResidentCoat(false,lod));
            Save("villager_wrap",lod,HeadWrap(lod));
            var sleeve=new Geometry();
            sleeve.Frustum(Vector3.zero,V(.20f,0,.22f),V(0,-.22f,.04f),V(.15f,0,.16f),Linen,lod==0?8:5);
            sleeve.Cylinder(V(0,-.20f,.04f),V(0,-.40f,.11f),.062f,Skin,lod==0?7:5);
            Save("resident_sleeve",lod,sleeve);
            foreach(var name in new[]{"hoe","sickle","bucket","basket","crate","jug"})
                Save("carry_"+name,lod,HandProp(name,lod));
            BakeEstateArt(lod);
            SaveUpgrades("manor_hall",lod);
            foreach(var kind in Manor.Buildings)SaveUpgrades(kind,lod);
        }

        static void Tunic(Geometry g,int lod,Color dye)
        {
            int sides=lod==0?10:6;
            g.Frustum(V(0,-.33f,0),V(.54f,0,.40f),V(0,-.14f,0),V(.42f,0,.32f),dye,sides);
            g.Frustum(V(0,-.14f,0),V(.42f,0,.32f),V(0,.23f,0),V(.57f,0,.40f),dye,sides);
            g.Frustum(V(0,.23f,0),V(.57f,0,.40f),V(0,.34f,0),V(.26f,0,.23f),dye,sides);
        }

        static void SoldierTrim(Geometry g,string kind,int lod)
        {
            bool heavy=kind=="shield"||kind=="giant"||kind=="cavalry";
            if(heavy)
            {
                g.Frustum(V(0,-.2f,.008f),V(.46f,0,.37f),V(0,.24f,0),V(.60f,0,.43f),Steel,lod==0?10:6);
                g.Spear(V(0,.69f,.19f),V(0,.48f,.228f),.052f,LightSteel);
            }
            else
            {
                g.Cylinder(V(-.21f,.27f,.13f),V(.17f,-.20f,.14f),.039f,Wood,5);
                g.Frustum(V(0,.22f,-.015f),V(.55f,0,.43f),V(0,.37f,-.01f),V(.27f,0,.23f),kind=="archer"?Moss:Linen,7);
            }
            if(lod!=0)return;
            g.Bevel(V(.21f,-.21f,.08f),V(.14f,.18f,.13f),.035f,Copper);
            foreach(float side in new[]{-1f,1f})
                g.Cylinder(V(side*.18f,.29f,.12f),V(side*.18f,.29f,.155f),.027f,Gold,6);
        }

        static void Blade(Geometry g,float x)
        {
            var tip=V(x,.10f,.89f); var root=V(x,.10f,.08f);
            var l=V(x-.074f,.10f,.28f); var r=V(x+.074f,.10f,.28f);
            foreach(float side in new[]{-1f,1f})
            {
                var ridge=V(x,.10f+.043f*side,.27f);
                g.Quad(root,l,tip,ridge,LightSteel,true);g.Quad(root,ridge,tip,r,Steel,true);
            }
        }

        static void WeaponTrim(Geometry g,string type,int lod)
        {
            if(type=="crossbow")g.Bevel(V(.36f,.19f,.34f),V(.18f,.19f,.37f),.036f,Wood);
            if(type=="medic")
            {
                g.Spear(V(.36f,.63f,.28f),V(.36f,.87f,.28f),.098f,new Color(.25f,.76f,.58f,0));
                g.Cylinder(V(.36f,.51f,.28f),V(.36f,.56f,.28f),.078f,Gold,6);
            }
            if(lod!=0)return;
            if(type=="crossbow")
                for(int i=0;i<3;i++)g.Cylinder(V(.31f+i*.047f,.29f,.20f),V(.31f+i*.047f,.29f,.48f),.013f,LightSteel,4);
            if(type=="giant")g.Bevel(V(.36f,.64f,.565f),V(.30f,.16f,.032f),.013f,LightSteel);
            if(type=="spear"||type=="cavalry")
                g.Quad(V(.36f,.04f,1.40f),V(.36f,.04f,1.21f),V(.36f,-.23f,1.02f),V(.36f,-.17f,1.31f),Cloth,true);
        }

        static void WeaponGrip(Geometry g,string type,int lod)
        {
            float reach=type=="archer"?.51f:type=="giant"?.36f:type=="crossbow"?.31f:type=="medic"?.28f:type=="bomber"?.31f:0;
            if(reach<=0)return;
            var palm=V(.36f,-.09f,.115f);
            var grip=V(.36f,type=="archer"?.10f:.025f,reach);
            g.Cylinder(palm,grip,.043f,type=="giant"?Wood:Skin,lod==0?6:4);
            if(lod==0)g.Bevel(grip,V(.118f,.104f,.116f),.031f,WoodLight);
        }

        static void BlastBarrel(Geometry g,int lod)
        {
            const int sides=8;
            g.Frustum(V(0,0,0),V(.718f,0,.718f),V(0,.24f,0),V(.876f,0,.876f),WoodLight,sides);
            g.Frustum(V(0,.24f,0),V(.876f,0,.876f),V(0,.55f,0),V(.914f,0,.914f),Copper,sides);
            g.Frustum(V(0,.55f,0),V(.914f,0,.914f),V(0,.86f,0),V(.876f,0,.876f),WoodLight,sides);
            g.Frustum(V(0,.86f,0),V(.876f,0,.876f),V(0,1.10f,0),V(.718f,0,.718f),Wood,sides);
            g.Cylinder(V(0,1.092f,0),V(0,1.112f,0),.345f,WoodLight,sides);
            if(lod==0)
            {
                for(int stave=0;stave<8;stave++)
                {
                    float a=stave*Mathf.PI*.25f,x=Mathf.Sin(a),z=Mathf.Cos(a);
                    var foot=V(x*.396f,.14f,z*.396f);
                    var shoulder=V(x*.423f,.923f,z*.423f);
                    var bow=V(x*.459f,.556f,z*.459f);
                    var span=V(z*.012f,0,-x*.012f);
                    g.Quad(foot-span,foot+span,bow+span,bow-span,Wood,true);
                    g.Quad(bow-span,bow+span,shoulder+span,shoulder-span,Wood,true);
                }
                g.Frustum(V(0,.303f,0),V(.898f,0,.898f),V(0,.363f,0),V(.919f,0,.919f),Steel,sides);
                g.Frustum(V(0,.762f,0),V(.909f,0,.909f),V(0,.819f,0),V(.901f,0,.901f),Dark,sides);
                g.Cylinder(V(.10f,1.106f,.056f),V(.108f,1.128f,.078f),.016f,Dark,5);
                g.Cylinder(V(.108f,1.126f,.078f),V(.166f,1.142f,.11f),.009f,Linen,4);
                g.Box(V(.165f,1.143f,.11f),V(.025f,.011f,.022f),Amber);
            }
        }

        static void Stockade(Geometry g,int lod)
        {
            var envelope=ContactShape.BarricadeSize;
            g.Bevel(V(0,.20f,0),V(envelope.x,.4f,envelope.z),.064f,Wood);
            g.Box(V(0,.52f,0),V(.55f,.28f,envelope.z),WoodLight);
            for(int i=-1;i<=1;i++)
            {
                float z=i*1.06f;
                foreach(int side in new[]{-1,1})
                {
                    g.Cylinder(V(side*.41f,.16f,z),V(-side*.28f,1.13f,z),.092f,WoodLight,6);
                    g.Spear(V(-side*.27f,1.10f,z),V(-side*.44f,1.40f,z),.079f,Steel);
                    if(lod==0)
                    {
                        float y=1.02f, x=-side*.21f;
                        g.Cylinder(V(x+side*.038f,y-.045f,z),V(x-side*.029f,y+.051f,z),.101f,Steel,5);
                        g.Triangle(V(-side*.30f,1.15f,z+.062f),V(-side*.44f,1.40f,z),
                            V(-side*.335f,1.168f,z+.035f),LightSteel);
                    }
                }
                if(lod==0)g.Cylinder(V(-.36f,.55f,z),V(.36f,.55f,z),.108f,Dark,6);
            }
        }

        static void BattleStandard(Geometry g,int lod)
        {
            var stand=ContactShape.BannerStand;
            g.Bevel(V(0,stand.y*.5f,0),stand,.042f,Stonework);
            g.Cylinder(Vector3.zero,V(0,2.64f,0),.058f,WoodLight,lod==0?8:6);
            g.Spear(V(0,2.61f,0),V(0,ContactShape.BannerHeight,0),.075f,Gold);
            g.Cylinder(V(0,2.44f,0),V(.82f,2.44f,0),.025f,Wood,5);
            int parts=lod==0?6:3;
            for(int i=0;i<parts;i++)
            {
                float a=i/(float)parts,b=(i+1)/(float)parts;
                float za=Mathf.Sin(a*5)*.044f,zb=Mathf.Sin(b*5)*.044f;
                float ya=1.84f+.21f*a,yb=1.84f+.21f*b;
                g.Quad(V(.82f*a,2.43f,za),V(.82f*b,2.43f,zb),V(.82f*b,yb,zb),V(.82f*a,ya,za),Cloth,true);
                g.Quad(V(.82f*a,ya+.057f,za-.005f),V(.82f*b,yb+.057f,zb-.005f),
                    V(.82f*b,yb+.027f,zb-.005f),V(.82f*a,ya+.027f,za-.005f),Gold,true);
            }
            if(lod==0)
            {
                const float z=.052f;
                g.Quad(V(.343f,2.32f,z),V(.436f,2.32f,z),V(.436f,2.02f,z),V(.343f,2.02f,z),Linen,true);
                g.Quad(V(.248f,2.20f,z),V(.530f,2.20f,z),V(.530f,2.117f,z),V(.248f,2.117f,z),Linen,true);
                g.Cylinder(V(.797f,2.42f,-.018f),V(.862f,2.42f,-.018f),.040f,Gold,6);
                for(int i=0;i<3;i++)g.Triangle(V(.16f+i*.14f,1.90f+i*.037f,0),
                    V(.197f+i*.14f,1.91f+i*.037f,0),V(.177f+i*.14f,1.83f+i*.037f,.007f),Gold);
            }
        }

        static void WallMasonry(Geometry g,int lod)
        {
            var mortar=new Color(.40f,.43f,.40f,0);
            g.Box(V(0,-.043f,0),V(1,.914f,.993f),mortar);
            g.Bevel(V(0,.415f,0),V(1,.17f,1),.017f,Stonework);
            foreach(int side in new[]{-1,1})
            {
                int courses=lod==0?4:2, count=lod==0?4:2;
                for(int row=0;row<courses;row++)for(int i=0;i<count;i++)
                {
                    float width=1f/count,y=-.48f+(row+.5f)*.81f/courses;
                    var tint=(row+i)%3==0?new Color(.60f,.61f,.56f,0):Rock;
                    g.Box(V(-.5f+(i+.5f)*width,y,side*.497f),V(width-.016f,.81f/courses-.022f,.006f),tint);
                }
            }
        }

        static void CanopyLayers(Geometry g,string name,int lod)
        {
            if(lod==0&&name!="dead_tree")
            {
                FacetedCanopy(g,name=="pine");
                return;
            }
            int sides=lod==0?8:6;
            g.Cylinder(Vector3.zero,V(.06f,2.68f,.01f),.13f,Wood,sides);
            if(name=="pine")
            {
                int layers=lod==0?4:3;
                for(int i=0;i<layers;i++)
                {
                    float t=i/(float)(layers-1),y=.89f+t*1.8f,r=1-t*.56f;
                    g.Cone(V(0,y,0),r,1.2f,Color.Lerp(Green,Leaf,t),sides);
                    g.Cone(V(.013f,y+.33f,-.018f),r*.76f,1.05f,Color.Lerp(Glass,Snow,.69f+.26f*t),sides);
                }
                return;
            }
            for(int j=0;j<(lod==0?4:3);j++)
            {
                float a=j*2.18f;
                var branch=V(Mathf.Sin(a)*.76f,1.72f+j*.22f,Mathf.Cos(a)*.77f);
                g.Cylinder(V(0,1.17f+j*.26f,0),branch,.056f,WoodLight,5);
                if(name=="tree")g.Rock(branch+Vector3.up*.36f,.72f,Color.Lerp(Moss,Leaf,j*.28f),sides,3);
                else g.Cylinder(branch,branch+V(Mathf.Cos(a)*.36f,.41f,Mathf.Sin(a)*.35f),.031f,WoodLight,4);
            }
            if(name=="tree")g.Rock(V(0,2.72f,0),1.04f,new Color(.36f,.53f,.29f,0),sides,3);
        }

        static void Saddle(Geometry g,int lod)
        {
            foreach(float side in new[]{-1f,1f})
            {
                g.Quad(V(side*.30f,-.25f,.24f),V(side*.31f,-.25f,-.40f),V(side*.38f,-.68f,-.32f),V(side*.38f,-.56f,.23f),Indigo,true);
                g.Cylinder(V(side*.31f,-.27f,.06f),V(side*.39f,-.70f,.06f),.028f,Copper,5);
                if(lod==0)g.Bevel(V(side*.39f,-.71f,.06f),V(.08f,.05f,.19f),.014f,Gold);
            }
        }

        static void HeroRegalia(Geometry g,string type,int lod)
        {
            if(type=="hero_commander")
            {
                Pennant(g,V(-.18f,-.18f,-.23f),1.68f,.63f,Indigo,lod);
                g.Bevel(V(0,.68f,.04f),V(.39f,.09f,.36f),.034f,Gold);
                g.Quad(V(-.23f,.27f,-.19f),V(.22f,.27f,-.19f),V(.25f,-.34f,-.28f),V(-.24f,-.42f,-.29f),Indigo,true);
                g.Quad(V(-.13f,1.42f,-.29f),V(.27f,1.39f,-.20f),V(.21f,1.29f,-.21f),V(-.13f,1.32f,-.29f),Gold,true);
            }
            else if(type=="hero_vanguard")
            {
                foreach(float side in new[]{-1f,1f}){
                    g.Frustum(V(side*.30f,.22f,0),V(.34f,0,.49f),V(side*.31f,.43f,0),V(.20f,0,.30f),Dark,lod==0?8:5);
                    g.Bevel(V(side*.35f,.327f,.21f),V(.20f,.15f,.087f),.028f,Copper);
                    g.Spear(V(side*.17f,.73f,-.02f),V(side*.38f,1.03f,-.18f),.095f,Gold);
                }
            }
            else
            {
                g.Quad(V(-.23f,.30f,-.20f),V(.24f,.31f,-.20f),V(.40f,-.57f,-.42f),V(-.05f,-.41f,-.43f),Moss,true);
                g.Quad(V(-.23f,.30f,-.20f),V(-.05f,-.41f,-.43f),V(-.32f,-.61f,-.45f),V(-.39f,-.35f,-.36f),Green,true);
                g.Spear(V(-.14f,.74f,-.04f),V(-.26f,1.09f,-.27f),.078f,Linen);
                g.Bevel(V(0,.602f,.182f),V(.33f,.054f,.077f),.017f,Moss);
                g.Cylinder(V(.14f,.292f,.152f),V(.14f,.294f,.215f),.038f,Gold,5);
            }
        }

        static Geometry ResidentCoat(bool farm,int lod)
        {
            if(lod==1)return DistantResident(farm);
            var g=new Geometry();Tunic(g,lod,farm?Linen:Cloth);
            int n=lod==0?10:6;
            if(farm)
            {
                g.Quad(V(-.14f,.21f,.19f),V(.14f,.21f,.19f),V(.22f,-.42f,.225f),V(-.22f,-.42f,.225f),Copper,true);
                if(lod==0)g.Box(V(.06f,-.17f,.227f),V(.13f,.11f,.024f),WoodLight);
            }
            else
            {
                g.Frustum(V(0,-.44f,-.015f),V(.59f,0,.43f),V(0,-.08f,0),V(.39f,0,.32f),Indigo,n);
                foreach(float s in new[]{-1f,1f})
                    g.Quad(V(s*.24f,.23f,.12f),V(s*.08f,.32f,.15f),V(s*.05f,-.14f,.181f),V(s*.20f,-.19f,.16f),Moss,true);
            }
            g.Frustum(V(0,-.14f,0),V(.47f,0,.40f),V(0,-.07f,0),V(.47f,0,.39f),Wood,n);
            CapePleats(g,farm?Linen:Cloth,.24f,farm?-.29f:-.39f);
            g.Quad(V(-.13f,.325f,.094f),V(.11f,.33f,.098f),V(.181f,.244f,.17f),V(-.181f,.237f,.16f),
                farm?Copper:Moss,true);
            g.Quad(V(-.11f,-.286f,.228f),V(.052f,-.269f,.232f),V(.051f,-.39f,.254f),V(-.13f,-.41f,.247f),farm?Linen:Indigo,true);
            return g;
        }

        // Distant residents are a single instance. Distant soldiers retain two animated legs and their weapon arm.
        static Geometry DistantResident(bool farm)
        {
            var g=new Geometry();
            g.Frustum(V(0,-.39f,0),V(.56f,0,.41f),V(0,.25f,0),V(.47f,0,.37f),farm?Linen:Cloth,6);
            g.Quad(V(-.16f,.21f,.17f),V(.16f,.21f,.17f),V(.22f,-.37f,.22f),V(-.22f,-.37f,.22f),farm?Copper:Indigo,true);
            g.Ellipsoid(V(0,.48f,0),V(.19f,.22f,.18f),Skin,6,2);
            if(farm)g.Cone(V(0,.60f,0),.34f,.23f,Straw,8);
            else g.Ellipsoid(V(0,.65f,-.025f),V(.225f,.15f,.20f),Indigo,6,2);
            foreach(int s in new[]{-1,1})
            {
                g.Box(V(s*.14f,-.55f,.015f),V(.18f,.43f,.23f),Dark);
                g.Cylinder(V(s*.24f,.22f,0),V(s*.31f,-.13f,.06f),.087f,Linen,5);
            }
            return g;
        }

        static string Equipment(string type)
        {
            switch(type){case "hero_vanguard":return "giant";case "hero_ranger":return "crossbow";case "hero_commander":return "medic";default:return type;}
        }
        static Geometry DistantBody(string type)
        {
            var g=new Geometry();string kit=Equipment(type);
            bool iron=kit=="shield"||kit=="giant"||kit=="spear"||kit=="cavalry";
            g.Frustum(V(0,-.31f,0),V(.51f,0,.36f),V(0,.21f,0),V(.56f,0,.40f),Cloth,6);
            g.Frustum(V(0,.21f,0),V(.56f,0,.40f),V(0,.34f,0),V(.25f,0,.24f),iron?Steel:Cloth,6);
            g.Ellipsoid(V(0,.52f,.015f),V(.19f,.22f,.18f),Skin,6,2);
            if(kit=="militia")g.Cone(V(0,.66f,0),.26f,.18f,Straw,7);
            else g.Ellipsoid(V(0,.66f,-.025f),V(.239f,.18f,.224f),iron?Steel:kit=="medic"?Linen:Moss,6,2);
            g.Cylinder(V(-.275f,.23f,0),V(-.35f,-.12f,.065f),.10f,iron?Steel:Cloth,5);
            if(kit=="shield")g.Append(Shield(1),Matrix4x4.identity);
            if(type=="hero_vanguard")
                foreach(int s in new[]{-1,1})g.Spear(V(s*.18f,.75f,0),V(s*.38f,1.06f,-.18f),.105f,Gold);
            if(type=="hero_ranger")
                g.Quad(V(-.23f,.31f,-.18f),V(.23f,.3f,-.18f),V(.37f,-.51f,-.42f),V(-.31f,-.61f,-.44f),Moss,true);
            if(type=="hero_commander")Pennant(g,V(-.2f,-.18f,-.25f),1.76f,.75f,Indigo,1);
            return g;
        }
        static Geometry DistantArm(string type)
        {
            var g=new Geometry();const float x=.36f;
            g.Cylinder(V(x,.23f,0),V(x,-.12f,.065f),.096f,Cloth,5);
            g.Box(V(x,-.13f,.09f),V(.14f,.14f,.16f),Skin);
            string kit=type=="hero_commander"?"spear":Equipment(type);
            WeaponGrip(g,kit,1);
            switch(kit)
            {
                case "spear":case "cavalry":
                    g.Cylinder(V(x,.05f,-.33f),V(x,.05f,1.52f),.031f,WoodLight,4);
                    g.Spear(V(x,.05f,1.47f),V(x,.05f,1.90f),.11f,LightSteel);break;
                case "archer":
                    g.Cylinder(V(x,-.37f,.42f),V(x,.10f,.61f),.038f,WoodLight,4);
                    g.Cylinder(V(x,.10f,.61f),V(x,.57f,.42f),.038f,WoodLight,4);break;
                case "crossbow":
                    g.Box(V(x,.04f,.39f),V(.15f,.17f,.80f),WoodLight);
                    g.Cylinder(V(x-.45f,.12f,.53f),V(x,.13f,.72f),.045f,Steel,4);
                    g.Cylinder(V(x,.13f,.72f),V(x+.45f,.12f,.53f),.045f,Steel,4);break;
                case "bomber":g.Ellipsoid(V(x,.03f,.37f),V(.19f,.19f,.19f),Dark,6,2);break;
                case "giant":
                    g.Cylinder(V(x,-.41f,.32f),V(x,.70f,.32f),.06f,WoodLight,5);
                    g.Box(V(x,.64f,.32f),V(.70f,.37f,.40f),type=="hero_vanguard"?Gold:Rock);break;
                case "medic":
                    g.Cylinder(V(x,-.43f,.27f),V(x,.67f,.27f),.046f,WoodLight,5);
                    g.Spear(V(x,.60f,.27f),V(x,.91f,.27f),.13f,Green);break;
                default:Blade(g,x);g.Box(V(x,.10f,.055f),V(.27f,.07f,.065f),Gold);break;
            }
            return g;
        }
        static Geometry HeadWrap(int lod)
        {
            var g=new Geometry();int n=lod==0?10:6;
            g.Ellipsoid(V(0,.015f,-.02f),V(.219f,.16f,.203f),Indigo,n,3);
            g.Frustum(V(0,-.042f,0),V(.44f,0,.403f),V(0,.011f,0),V(.45f,0,.413f),Linen,n);
            g.Ellipsoid(V(0,-.095f,-.16f),V(.135f,.131f,.11f),Wood,6,2);
            return g;
        }

        static Geometry HandProp(string kind,int lod)
        {
            var g=new Geometry();int n=lod==0?9:5;
            if(kind=="hoe"||kind=="sickle")
            {
                float length=kind=="hoe"?.69f:.38f;
                g.Cylinder(V(0,-.28f,0),V(0,length,0),.034f,WoodLight,n);
                if(kind=="hoe")
                    g.Quad(V(-.055f,length,.015f),V(.055f,length,.015f),V(.11f,length-.15f,.27f),V(-.11f,length-.15f,.27f),Steel,true);
                else {
                    Vector3 a=V(0,length,0),b=V(0,length+.075f,.01f);
                    for(int i=1;i<=4;i++){
                        float angle=i*Mathf.PI*.20f;
                        var c=V(0,length+.19f*(1-Mathf.Cos(angle)),.25f*Mathf.Sin(angle));
                        var d=c+V(0,.073f*(1-i/4f),.03f*(1-i/4f));
                        g.Quad(a,b,d,c,LightSteel,true);a=c;b=d;
                    }
                }
            }
            else if(kind=="crate")
            {
                g.Box(V(0,-.12f,0),V(.40f,.31f,.30f),WoodLight);
                foreach(float x in new[]{-.14f,.14f})g.Box(V(x,-.12f,0),V(.045f,.33f,.32f),Wood);
            }
            else if(kind=="jug")
            {
                g.Ellipsoid(V(0,-.17f,0),V(.16f,.20f,.15f),Copper,n,3);
                g.Cylinder(V(0,0,0),V(0,.09f,0),.074f,Plaster,n);
            }
            else
            {
                bool basket=kind=="basket";float w=basket?.40f:.29f;
                g.Frustum(V(0,-.35f,0),V(w*.75f,0,w*.71f),V(0,-.10f,0),V(w,0,w*.93f),basket?Straw:WoodLight,n);
                g.Cylinder(V(0,-.108f,0),V(0,-.096f,0),w*.44f,basket?Ochre:Glass,n);
                foreach(float s in new[]{-1f,1f})g.Cylinder(V(s*w*.46f,-.11f,0),V(s*.08f,.06f,0),.019f,Wood,5);
                g.Cylinder(V(-.08f,.06f,0),V(.08f,.06f,0),.023f,WoodLight,5);
            }
            return g;
        }

        static Geometry MillSails(int lod)
        {
            var g=new Geometry();
            for(int i=0;i<4;i++){
                var turn=Quaternion.AngleAxis(i*90,Vector3.forward);
                g.Cylinder(Vector3.zero,turn*V(0,1.04f,0),.037f,Wood,5);
                g.Quad(turn*V(.015f,.26f,-.02f),turn*V(-.19f,.33f,-.02f),turn*V(-.26f,1.03f,-.055f),turn*V(.015f,1.03f,-.04f),Linen,true);
                g.Cylinder(turn*V(-.20f,.34f,-.026f),turn*V(-.26f,1.035f,-.054f),.014f,WoodLight,4);
                if(lod==0)for(int j=0;j<3;j++)g.Cylinder(turn*V(0,.49f+j*.19f,-.047f),turn*V(-.21f,.49f+j*.19f,-.049f),.012f,WoodLight,4);
            }
            g.Cylinder(V(0,0,-.08f),V(0,0,.06f),.12f,Gold,lod==0?8:5);
            return g;
        }

        static void Lantern(Geometry g,Vector3 at,int lod)
        {
            int n=lod==0?8:5;
            g.Ellipsoid(at,V(.18f,.24f,.18f),Amber,n,3);
            g.Cone(at+V(0,.17f,0),.20f,.14f,Slate,n);
            g.Cylinder(at-V(0,.23f,0),at-V(0,.19f,0),.11f,Copper,n);
        }
        static void YardDetail(Geometry g,string kind,int lod)
        {
            var p=V(-1.35f,0,-1.03f);
            switch(kind){
                case "tavern": case "manor_hall": Lantern(g,V(.48f,1.16f,-1.40f),lod);break;
                case "farm": case "pasture": case "stables":
                    g.Bevel(p+V(0,.24f,0),V(.67f,.48f,.48f),.10f,Straw);
                    foreach(float x in new[]{-.19f,.19f})g.Box(p+V(x,.24f,0),V(.046f,.48f,.49f),WoodLight);break;
                case "irrigated_field":g.Barrel(V(1.45f,.23f,1.07f),.23f,.46f,WoodLight,lod==0?9:6);break;
                case "timber_camp":case "smithy":
                    g.Box(p+V(0,.14f,0),V(.64f,.26f,.43f),WoodLight);
                    for(int i=0;i<3;i++)g.Bevel(p+V(i*.17f-.17f,.33f,0),V(.15f,.14f,.38f),.04f,kind=="smithy"?Steel:Wood);break;
                case "barracks":case "drill_yard":
                    g.Box(V(-1.59f,.20f,-.68f),V(.28f,.40f,.60f),Wood);
                    for(int i=0;i<3;i++){var a=V(-1.59f,.3f,-.90f+i*.19f);g.Cylinder(a,a+V(.10f,.95f,0),.032f,WoodLight,5);g.Spear(a+V(.10f,.95f,0),a+V(.13f,1.19f,0),.065f,Steel);}break;
                case "cottage":
                    g.Box(V(-.08f,.37f,-.96f),V(.60f,.15f,.22f),WoodLight);
                    for(int i=0;i<3;i++)g.Ellipsoid(V(-.28f+i*.19f,.49f,-.98f),V(.11f,.09f,.095f),i==1?Ochre:Leaf,6,2);break;
            }
        }

        static void SaveUpgrades(string kind,int lod)
        {
            var g=new Geometry();var left=V(-1.52f,0,.98f);var right=V(1.37f,0,.90f);
            int n=lod==0?8:5;
            switch(kind)
            {
                case "granary": GranaryAnnex(g,left,lod);break;
                case "chapel": ChapelAnnex(g,left,lod);break;
                case Manor.Bakery:case Manor.Weaver:CraftAnnex(g,kind,left,lod,false);break;
                case "tavern":
                    Canopy(g,left,1.25f,Tile);foreach(float z in new[]{-.28f,.27f})
                        g.Barrel(left+V(0,.29f,z),.24f,.57f,WoodLight,n);break;
                case "smithy":
                    Canopy(g,left,1.24f,Slate);g.Box(left+V(0,.25f,0),V(.47f,.50f,.45f),Stonework);
                    g.Box(left+V(0,.55f,0),V(.74f,.14f,.39f),Dark);
                    g.Spear(left+V(.31f,.55f,0),left+V(.61f,.59f,0),.13f,Steel);break;
                case "drill_yard":case "barracks":
                    Rack(g,left,1.26f);for(int i=0;i<3;i++){var a=left+V(i*.26f-.26f,.07f,.06f);
                        g.Cylinder(a,a+V(.08f,1.37f,.13f),.037f,WoodLight,4);
                        g.Spear(a+V(.08f,1.37f,.13f),a+V(.10f,1.67f,.13f),.082f,Steel);}break;
                case "irrigated_field":
                    g.Box(left+V(0,.34f,0),V(.89f,.68f,1.04f),Stonework);
                    g.Box(left+V(0,.70f,0),V(.74f,.05f,.91f),Glass);break;
                case "cottage":
                    g.Box(left+V(0,.48f,0),V(.83f,.96f,.95f),Plaster);
                    Gable(g,left+Vector3.up*.98f,1.13f,1.18f,.55f,Straw,Plaster,1);break;
                case "timber_camp":
                    for(int i=0;i<3;i++)g.Box(left+V(0,.16f+i*.24f,0),V(1.06f,.22f,1.04f-i*.14f),WoodLight);break;
                default:
                    Canopy(g,left,1.12f,kind=="manor_hall"?Slate:Straw);
                    g.Box(left+V(0,.23f,0),V(.67f,.46f,.63f),kind=="manor_hall"?Stonework:StrawLight);break;
            }
            var wing=new Geometry();
            wing.Append(g,Matrix4x4.identity);
            wing.FitFootprint(ManorPlotArt.HalfWidth(kind=="manor_hall"),ManorPlotArt.HalfDepth(kind=="manor_hall"));
            Save(kind+"_wing",lod,wing);
            // A third-tier annex includes its second-tier wing in the same mesh and submission.
            switch(kind)
            {
                case "granary": GranaryLoft(g,right,lod);break;
                case "chapel": ChapelBelfry(g,right,lod);break;
                case Manor.Bakery:case Manor.Weaver:CraftAnnex(g,kind,right,lod,true);break;
                case "manor_hall":
                    Rack(g,right,3.22f);g.Cone(right+Vector3.up*3.22f,.78f,.71f,Gold,4);
                    Lantern(g,right+Vector3.up*2.87f,lod);break;
                case "cottage":
                    g.Box(V(.90f,1.02f,.20f),V(1.15f,1.71f,1.02f),Plaster);
                    Gable(g,V(.90f,1.91f,.20f),1.43f,1.36f,.74f,Tile,Plaster,lod);
                    Gable(g,V(.90f,1.76f,-.42f),.59f,.51f,.33f,Slate,Plaster,1);
                    Window(g,.90f,1.57f,-.35f,lod);break;
                case "tavern":
                    Rack(g,right,2.12f);g.Box(right+V(0,1.62f,-.075f),V(.69f,.66f,.14f),Wood);
                    g.Barrel(right+V(0,1.63f,-.17f),.22f,.43f,Ochre,n);
                    Pennant(g,right+V(-.45f,1.95f,0),.91f,.81f,Tile,lod);break;
                case "smithy":
                    Chimney(g,right.x,right.z,2.93f,.64f);g.Box(right+V(0,.42f,-.34f),V(.46f,.38f,.065f),Dark);
                    g.Box(right+V(0,.35f,-.385f),V(.35f,.13f,.042f),Amber);break;
                case "drill_yard":
                    Pennant(g,right,2.76f,.94f,Indigo,lod);
                    Pennant(g,right+V(-.63f,0,-.34f),2.12f,.66f,Ochre,lod);break;
                case "barracks":
                    Rack(g,right,1.78f);
                    g.Cylinder(right+V(0,1.16f,-.19f),right+V(0,1.16f,.01f),.53f,Straw,n);
                    g.Cylinder(right+V(0,1.16f,-.23f),right+V(0,1.16f,-.19f),.28f,Tile,n);break;
                case "timber_camp":
                    Rack(g,right,2.17f);g.Cylinder(right+V(.25f,.82f,0),right+V(.25f,2.11f,0),.036f,Straw,4);
                    g.Box(right+V(.25f,.61f,0),V(.69f,.44f,.58f),WoodLight);break;
                case "irrigated_field":
                    Rack(g,right,1.42f);g.Frustum(right+Vector3.up*1.30f,V(.90f,0,.88f),right+Vector3.up*2.03f,V(1.08f,0,1.08f),WoodLight,n);
                    g.Cylinder(right+Vector3.up*2.035f,right+Vector3.up*2.05f,.46f,Glass,n);break;
                case "pasture":case "stables":
                    Canopy(g,right,kind=="pasture"?1.44f:1.78f,kind=="pasture"?Green:Slate);
                    for(int i=0;i<3;i++)g.Box(right+V(0,.17f+i*.31f,0),V(.85f-i*.1f,.30f,.72f),StrawLight);break;
                default:
                    g.Frustum(right,V(.91f,0,.91f),right+Vector3.up*1.83f,V(.84f,0,.84f),Plaster,n);
                    g.Cone(right+Vector3.up*1.83f,.68f,.73f,StrawLight,n);break;
            }
            g.FitFootprint(ManorPlotArt.HalfWidth(kind=="manor_hall"),ManorPlotArt.HalfDepth(kind=="manor_hall"));
            Save(kind+"_tower",lod,g);
        }
        static void Rack(Geometry g,Vector3 foot,float height)
        {
            foreach(float x in new[]{-.43f,.43f})g.Box(foot+V(x,height*.5f,0),V(.13f,height,.15f),Wood);
            g.Box(foot+V(0,height,0),V(1.11f,.15f,.19f),WoodLight);
        }
        static void Canopy(Geometry g,Vector3 foot,float height,Color roof)
        {
            foreach(float z in new[]{-.38f,.38f})
                foreach(float x in new[]{-.38f,.38f})g.Box(foot+V(x,height*.5f,z),V(.09f,height,.09f),Wood);
            Gable(g,foot+Vector3.up*height,1.08f,1.10f,.36f,roof,Plaster,1);
        }
    }
}
