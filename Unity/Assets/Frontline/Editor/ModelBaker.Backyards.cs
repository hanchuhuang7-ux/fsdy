using Frontline.Core;
using Frontline.UnityGame;
using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        static void BakeBackyards(int lod)
        {
            foreach(var item in Manor.Backyards)
            {
                var g=new Geometry();
                switch(item.id)
                {
                    case "garden": BackyardGarden(g,lod);break;
                    case "poultry": BackyardCoop(g,lod);break;
                    case "workshop": BackyardLoom(g,lod);break;
                    case "militia_yard": BackyardWeapons(g,lod);break;
                }
                Save("backyard_"+item.id,lod,g);
            }
        }

        static void BackyardGarden(Geometry g,int lod)
        {
            g.Box(V(0,.027f,0),V(.76f,.054f,.48f),Earth);
            for(int side=-1;side<=1;side+=2)
            {
                g.Box(V(side*.37f,.059f,0),V(.045f,.075f,.52f),Wood);
                g.Box(V(0,.060f,side*.25f),V(.76f,.077f,.045f),WoodLight);
            }
            for(int row=-1;row<=1;row++)
            {
                g.Box(V(row*.212f,.073f,0),V(.13f,.071f,.42f),row==0?Dark:Earth);
                int count=lod==0?3:2;
                for(int i=0;i<count;i++)
                {
                    var at=V(row*.212f,.112f,.30f*(i/(float)(count-1)-.5f));
                    g.Rock(at,.082f,row==0?Green:Leaf,5,2);
                    if(lod==0&&row==0)g.Cone(at,.028f,.105f,Ochre,4);
                }
            }
        }

        static void BackyardCoop(Geometry g,int lod)
        {
            foreach(int side in new[]{-1,1})
                g.Box(V(-.15f+side*.17f,.083f,.031f),V(.047f,.166f,.34f),Wood);
            g.Box(V(-.15f,.23f,.034f),V(.42f,.27f,.34f),WoodLight);
            Gable(g,V(-.15f,.36f,.034f),.49f,.42f,.16f,Tile,Wood,lod:1);
            g.Box(V(-.11f,.244f,-.143f),V(.138f,.17f,.016f),Dark);
            g.Box(V(-.11f,.099f,-.185f),V(.18f,.046f,.14f),Wood);
            for(int i=0;i<(lod==0?2:1);i++)
            {
                var hen=V(.22f,.145f,.129f-i*.24f);
                g.Ellipsoid(hen,V(.122f,.138f,.131f),i==0?Linen:Ochre,6,2);
                g.Ellipsoid(hen+V(.02f,.079f,-.055f),V(.075f,.072f,.072f),Linen,6,2);
                g.Box(hen+V(.024f,.129f,-.059f),V(.026f,.038f,.044f),Tile);
            }
        }

        static void BackyardLoom(Geometry g,int lod)
        {
            foreach(int side in new[]{-1,1})
            {
                g.Box(V(side*.298f,.275f,.075f),V(.068f,.55f,.083f),Wood);
                g.Box(V(side*.298f,.038f,0),V(.10f,.075f,.43f),WoodLight);
            }
            g.Box(V(0,.50f,.075f),V(.68f,.07f,.088f),WoodLight);
            g.Box(V(0,.20f,.075f),V(.63f,.059f,.063f),Wood);
            g.Quad(V(-.234f,.46f,.024f),V(.21f,.46f,.024f),V(.21f,.218f,.023f),V(-.234f,.189f,.023f),Linen,true);
            int threads=lod==0?5:2;
            for(int i=0;i<threads;i++)
                g.Box(V(-.181f+i*.36f/(threads-1),.326f,.018f),V(.023f,.248f,.009f),i%2==0?Tile:Indigo);
            g.Box(V(-.047f,.088f,-.152f),V(.385f,.105f,.142f),Ochre);
        }

        static void BackyardWeapons(Geometry g,int lod)
        {
            g.Box(V(0,.046f,.019f),V(.73f,.091f,.442f),WoodLight);
            foreach(int side in new[]{-1,1})g.Box(V(side*.29f,.241f,.043f),V(.059f,.46f,.081f),Wood);
            g.Box(V(0,.422f,.043f),V(.65f,.069f,.093f),Wood);
            for(int i=0;i<(lod==0?3:2);i++)
            {
                float x=.31f-i*.127f;
                g.Cylinder(V(x,.058f,.11f),V(x-.044f,.632f,.114f),.018f,WoodLight,5);
                g.Spear(V(x-.044f,.629f,.114f),V(x-.056f,.747f,.114f),.043f,Steel);
            }
            g.Cylinder(V(-.18f,.25f,-.026f),V(-.18f,.25f,-.09f),.164f,WoodLight,lod==0?8:6);
            g.Cylinder(V(-.18f,.25f,-.09f),V(-.18f,.25f,-.129f),.075f,Steel,6);
        }
    }
}
