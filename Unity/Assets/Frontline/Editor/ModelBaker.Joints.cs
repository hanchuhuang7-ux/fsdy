using System;
using Frontline.Core;
using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static Geometry SoldierPortrait(string unit,string hero)
        {
            var g=new Geometry();
            bool armored=unit=="shield"||unit=="spear"||unit=="giant"||unit=="cavalry";
            Portrait(g,(hero!=null?11:0)+Array.IndexOf(Types,unit),armored);
            if(armored&&unit!="spear")Bascinet(g,WroughtIron,hero!=null?OldGold:Steel);
            else if(unit=="archer"||unit=="medic")
                WoolHood(g,hero=="hero_commander"?Indigo:unit=="archer"?Moss:UndyedLinen,true);
            else KettleHat(g,unit=="militia"?Walnut:WroughtIron);
            HeroFeathers(g,hero);
            if(hero=="hero_vanguard")g.Spear(V(-.073f,.748f,-.042f),V(-.257f,.972f,-.122f),.063f,OldGold);
            if(hero=="hero_ranger")g.Triangle(V(.112f,.652f,-.066f),V(.356f,.924f,-.209f),V(.176f,.687f,-.223f),UndyedLinen);
            return g;
        }

        static Geometry SmallProxy(float length,float width,Color dye)
        {
            var g=new Geometry();
            g.Frustum(Vector3.zero,V(width,0,width),V(0,-length,0),V(width*.67f,0,width*.73f),dye,4);
            return g;
        }

        static void BakeJoints(int lod)
        {
            foreach(var type in Types)
                if(type!="catapult")Save("head_"+type,lod,lod==0?SoldierPortrait(type,null):SmallProxy(.22f,.21f,Skin));
            foreach(var hero in Manor.HeroIds)
                Save("head_"+hero,lod,lod==0?SoldierPortrait(hero=="hero_ranger"?"archer":Equipment(hero),hero):SmallProxy(.23f,.22f,Steel));
            var watch=lod==0?SoldierPortrait("shield",null):SmallProxy(.20f,.23f,Steel);
            if(lod==0)
            {
                watch.Frustum(V(0,.65f,-.018f),V(.296f,0,.293f),V(0,.677f,-.020f),V(.25f,0,.257f),OldGold,10);
                watch.Quad(V(-.12f,.732f,0),V(-.08f,.90f,-.067f),V(.08f,.94f,-.18f),V(.112f,.735f,-.16f),Indigo,true);
            }
            Save("head_watch",lod,watch);
            foreach(bool plate in new[]{false,true})
            {
                string suffix=plate?"_plate":"";
                Save("thigh"+suffix,lod,lod==0?ManLimb(true,true,plate):SmallProxy(.29f,.16f,Dark));
                Save("shin"+suffix,lod,lod==0?ManLimb(true,false,plate):SmallProxy(.263f,.14f,Walnut));
                Save("upper_arm"+suffix,lod,lod==0?ManLimb(false,true,plate):SmallProxy(.27f,.15f,Cloth));
                Save("forearm"+suffix,lod,lod==0?ManLimb(false,false,plate):SmallProxy(.25f,.13f,Walnut));
            }
            Save("horse_upper",lod,lod==0?EquineSegment(true):SmallProxy(.49f,.22f,Wood));
            Save("horse_cannon",lod,lod==0?EquineSegment(false):SmallProxy(.48f,.12f,Wood));
            Save("arena_scree",lod,ArenaScree(lod));
            Save("steel_arc",lod,SteelArc());
        }

        // Segment vertices run down local Y from the proximal joint. Cuffs overlap both sides
        // of the pivot and remain concentric through the full anatomical range.
        static Geometry ManLimb(bool leg,bool upper,bool plate)
        {
            float length=leg?(upper?.29f:.263f):(upper?.27f:.25f);
            float radius=leg?(upper?.100f:.070f):(upper?.087f:.068f);
            var g=new Geometry{surface=upper?Surface.Cloth:plate?Surface.Metal:Surface.Hide};
            var dye=upper?(leg?Pigment(Walnut,.85f):DyedWool):plate?WroughtIron:Walnut;
            g.Loft(new[]{V(0,.026f,0),V(0,-.039f,0),V(0,-length*.44f,-.014f),V(0,-length*.83f,0),V(0,-length,0)},
                new[]{Section(radius*.83f,radius*.88f),Section(radius,radius*1.08f),Section(radius*.95f,radius),
                    Section(radius*.68f,radius*.75f),Section(radius*.61f,radius*.64f)},10,2,dye);
            if(plate||!upper)
            {
                g.surface=plate?Surface.Metal:Surface.Cloth;
                var cap=V(0,-.008f,leg?.017f:0);
                g.Ellipsoid(cap,V(radius*1.20f,radius*1.07f,radius*1.19f),plate?Steel:leg?Pigment(Walnut,.91f):DyedWool,10,4);
                if(plate)
                {
                    g.Frustum(V(0,-.070f,0),V(radius*2.05f,0,radius*2.18f),V(0,-.045f,0),
                        V(radius*2.20f,0,radius*2.32f),LightSteel,10);
                    if(leg&&!upper)
                        g.Loft(new[]{cap+V(-.103f,.025f,.004f),cap+V(0,.017f,.048f),cap+V(.104f,.032f,0)},
                            new[]{Section(.014f,.010f),Section(.057f,.039f),Section(.012f,.011f)},8,2,LightSteel);
                }
            }
            if(upper)return g;
            g.surface=Surface.Hide;
            if(leg)
            {
                var sole=V(0,-length-.014f,.021f);
                // Rounded heel, arch, ball and tapering toe, with a separate welt.
                g.Loft(new[]{sole+V(0,.011f,-.081f),sole+V(0,.011f,-.048f),sole+V(0,.015f,.011f),
                        sole+V(.003f,0,.080f),sole+V(.003f,-.004f,.14f),sole+V(.002f,-.008f,.170f)},
                    new[]{Section(.044f,.032f),Section(.066f,.051f),Section(.068f,.064f),Section(.073f,.045f),
                        Section(.053f,.033f),Section(.017f,.017f)},11,2,plate?Pigment(Walnut,.79f):Wood);
                if(plate)
                {
                    g.surface=Surface.Metal;
                    for(int j=0;j<3;j++)
                        g.Ellipsoid(sole+V(0,.030f-j*.008f,.067f+j*.033f),
                            V(.069f-j*.006f,.023f-j*.003f,.039f),j==0?Steel:WroughtIron,9,3);
                }
            }
            else
            {
                g.Ellipsoid(V(0,-length,0),V(.052f,.065f,.054f),plate?Walnut:Skin,9,4);
                g.Ellipsoid(V(-.031f,-length+.013f,.029f),V(.030f,.044f,.028f),plate?Walnut:Skin,7,3);
            }
            return g;
        }

        static Geometry EquineSegment(bool upper)
        {
            var g=new Geometry{surface=Surface.Hide};
            if(upper)
            {
                var bay=new Color(.423f,.239f,.148f,0);
                g.Loft(new[]{V(0,.096f,0),V(0,.035f,.003f),V(0,-.116f,-.013f),V(0,-.31f,-.019f),V(0,-.49f,0)},
                    new[]{Section(.076f,.101f),Section(.134f,.152f),Section(.128f,.146f),Section(.087f,.096f),Section(.061f,.072f)},11,2,bay);
            }
            else
            {
                g.Ellipsoid(Vector3.zero,V(.076f,.080f,.091f),Wood,9,4);
                g.Loft(new[]{V(0,.021f,0),V(0,-.062f,.009f),V(0,-.24f,-.009f),V(0,-.337f,-.019f),V(0,-.431f,.030f)},
                    new[]{Section(.064f,.072f),Section(.060f,.066f),Section(.045f,.049f),Section(.064f,.071f),Section(.051f,.054f)},9,2,Wood);
                g.Loft(new[]{V(0,-.405f,.025f),V(0,-.443f,.041f),V(0,-.478f,.054f),V(0,-.485f,.058f)},
                    new[]{Section(.053f,.060f),Section(.071f,.084f),Section(.078f,.096f),Section(.070f,.090f)},10,2,Dark);
            }
            return g;
        }

        static Geometry SteelArc()
        {
            var g=new Geometry();
            for(int i=0;i<12;i++)
            {
                Vector3 Edge(int j,bool rim)
                {
                    float t=j/12f,a=Mathf.Lerp(-1.13f,1.13f,t);
                    float r=rim?1:1-.087f*Mathf.Sin(t*Mathf.PI);
                    return V(Mathf.Sin(a)*r,0,Mathf.Cos(a)*r);
                }
                g.Quad(Edge(i,false),Edge(i,true),Edge(i+1,true),Edge(i+1,false),Cloth);
            }
            return g;
        }

        static Geometry ArenaScree(int lod)
        {
            var g=new Geometry{surface=Surface.Outcrop};
            int n=lod==0?12:5;
            Vector3 Edge(int i,int row)
            {
                float u=i/(float)n,noise=Mathf.Sin(u*27+.2f)*Mathf.Sin(u*Mathf.PI);
                if(row==0)return V(u-.5f,0,-.025f);
                if(row==1)return V(u-.5f,-.17f+.042f*noise,.43f+.095f*noise);
                return V(u-.5f,-1.0f+.12f*noise,.88f+.18f*noise);
            }
            for(int i=0;i<n;i++)
            {
                g.surface=Surface.Terrain;
                g.Quad(Edge(i,0),Edge(i,1),Edge(i+1,1),Edge(i+1,0),i%3==0?Moss:Green,true);
                g.surface=Surface.Outcrop;
                g.Quad(Edge(i,1),Edge(i,2),Edge(i+1,2),Edge(i+1,1),i%4==1?Rock:CliffStone,true);
            }
            return g;
        }
    }
}
