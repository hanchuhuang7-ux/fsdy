using System;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    // Visual data only: never creates a GameObject, Collider or changes any map rule.
    // Its independent seed cannot consume the combat random stream.
    public static class MapEcology
    {
        public struct Decoration
        {
            public string mesh; public Matrix4x4 matrix; public Color tint; public float wind; public bool atmosphere,castsShadow;
        }
        public struct Mote
        {
            public Vector3 origin; public float phase,speed,size; public string mesh; public Color tint;
        }
        static readonly Color White=Color.white;
        static void Put(List<Decoration> rows,string mesh,Vector3 p,Vector3 size,float yaw=0,Color? tint=null,float wind=0,bool atmosphere=false,Quaternion? rotation=null,bool castsShadow=true)
        { rows.Add(new Decoration{mesh=mesh,matrix=Matrix4x4.TRS(p,rotation??Quaternion.Euler(0,yaw,0),size),tint=tint??White,wind=wind,atmosphere=atmosphere,castsShadow=castsShadow}); }
        static float Range(System.Random rng,float a,float b){return a+(float)rng.NextDouble()*(b-a);}
        static bool ClearOfCover(MapDef map,float x,float z)
        {
            foreach(var r in map.rocks)if((x-r.x)*(x-r.x)+(z-r.z)*(z-r.z)<(r.r+.5f)*(r.r+.5f))return false;
            foreach(var w in map.walls)if(Mathf.Abs(x-w.x)<w.hx+.5f&&Mathf.Abs(z-w.z)<w.hz+.5f)return false;
            return true;
        }
        public static List<Decoration> Build(Catalog data,string id,int quality)
        {
            var map=data.Maps[id];var rows=new List<Decoration>(1200);var rng=new System.Random(77019+StableHash(id));
            bool snow=id=="highland",marsh=id=="marsh",ruins=id=="ruins",island=id=="island",bridge=id=="bridge";
            var ground=new Color(((map.color>>16)&255)/255f,((map.color>>8)&255)/255f,(map.color&255)/255f);
            var rock=snow?new Color(.56f,.67f,.71f):ruins?new Color(.48f,.43f,.37f):new Color(.52f,.58f,.53f);
            foreach(var p in map.platforms)
            {
                var q=Quaternion.Euler(0,0,p.angle*Mathf.Rad2Deg);
                Put(rows,p.bridge?"platform":"arena_ground",new Vector3(p.x,p.y,p.z),new Vector3(p.hx*2,p.hy*2,p.hz*2),tint:p.bridge?new Color(.57f,.4f,.24f):ground,rotation:q);
                if(!p.bridge&&!p.ramp)
                {
                    Talus(rows,data,id,p,rock);
                    float depth=island?6:bridge?5.2f:2.5f;
                    Put(rows,island?"island_root":"base",new Vector3(p.x,p.y-p.hy-depth*.5f+.03f,p.z),new Vector3(p.hx*1.98f,depth,p.hz*1.98f),tint:rock);
                    Put(rows,"cliff",new Vector3(p.x,p.y-p.hy-.45f,p.z),new Vector3(p.hx*2.01f,.85f,p.hz*2.01f),tint:rock*.85f);
                }
            }
            foreach(var r in map.rocks)Put(rows,"rock",new Vector3(r.x,r.r*.2f,r.z),Vector3.one*r.r*2,CombatMotion.RockYaw(r.x,r.z),rock);
            foreach(var w in map.walls){bool alongZ=w.hz>w.hx;Put(rows,"wall",new Vector3(w.x,w.hy,w.z),alongZ?new Vector3(w.hz*2,w.hy*2,w.hx*2):new Vector3(w.hx*2,w.hy*2,w.hz*2),alongZ?90:0,tint:rock);}
            foreach(var z in map.zones)
            {float h=data.Height(id,z.x,z.z);if(Catalog.Finite(h))Put(rows,"platform",new Vector3(z.x,h+.008f,z.z),new Vector3(z.hx*2,.012f,z.hz*2),tint:z.type=="mud"?new Color(.33f,.28f,.17f):new Color(.53f,.75f,.82f));}
            int density=quality==0?83:quality==1?209:425;
            for(int i=0;i<density;i++)
            {
                float x=Range(rng,-17.6f,17.6f),z=Range(rng,-11.6f,11.6f),h=data.Height(id,x,z);
                if(!Catalog.Finite(h)||!ClearOfCover(map,x,z)||Mathf.Abs(z)<1.6f)continue;
                bool reedBank=marsh&&Mathf.Abs(z)>7.65f&&Mathf.Abs(x)<4.95f;
                if(snow&&Mathf.Abs(z)<4.65f&&Mathf.Abs(x)<8.4f)continue;
                string mesh=reedBank?"reeds":(!snow&&!ruins&&i%9==0?"flowers":"grass");
                float scale=Range(rng,.39f,.94f);
                if(ruins||snow)scale*=.60f;
                if(reedBank)scale*=.73f;
                var tint=snow?new Color(.77f,.84f,.81f):marsh?new Color(.65f,.76f,.48f):ruins?new Color(.64f,.63f,.42f):White;
                Put(rows,mesh,new Vector3(x,h+.006f,z),new Vector3(scale,scale,scale),Range(rng,0,360),tint,mesh=="rock"?0:.035f);
            }
            // All large vegetation and landmarks live outside the playable boundary.
            int trees=quality==0?10:quality==1?20:30;
            if(!island)for(int i=0;i<trees;i++)
            {
                float x=Range(rng,-20.2f,20.2f),z=(i%2==0?-1:1)*Range(rng,12.7f,15.3f),scale=Range(rng,.9f,1.55f);
                if(bridge&&Mathf.Abs(x)<3.3f)continue;
                string mesh=snow?"pine":marsh||ruins?"dead_tree":"tree";
                float bankHeight=-.18f-.41f*Mathf.Max(0,Mathf.Abs(z)-12.5f)-.23f*Mathf.Max(0,Mathf.Abs(x)-18);
                Put(rows,mesh,new Vector3(x,bankHeight,z),Vector3.one*scale,Range(rng,0,360),White,.012f);
                Put(rows,"rock",new Vector3(x,bankHeight-.21f,z),new Vector3(2.2f,.9f,2),Range(rng,0,360),rock,castsShadow:false);
            }
            ArenaTrails(rows,data,id);
            switch(id)
            {
                case "meadow": MeadowTrail(rows); break;
                case "bridge": Gorge(rows,rock); break;
                case "island": IslandFringe(rows,rock); break;
                case "ruins": FallenCourt(rows,data,rock); break;
                case "marsh": MarshBanks(rows,quality); break;
                case "highland": IceShelf(rows,data,rock); break;
            }
            // Distant pillars frame the miniature without extending the simulation map.
            for(int i=0;i<8;i++){float a=(i+.5f)*Mathf.PI*.25f;Put(rows,"rock",new Vector3(Mathf.Cos(a)*31,-8,Mathf.Sin(a)*25),new Vector3(3,Range(rng,5,12),3),Range(rng,0,360),rock*.72f,castsShadow:false);}
            return rows;
        }
        // Marks sit flush with the sampled surface. Overhanging scenery is placed beyond the actors' footprint.
        static void Surface(List<Decoration> rows,Catalog data,string id,float x,float z,float width,float length,Color tint,float lift=.018f,float yaw=0)
        {
            float h=data.Height(id,x,z);
            if(Catalog.Finite(h))Put(rows,"platform",new Vector3(x,h+lift,z),new Vector3(width,.010f,length),yaw,tint);
        }

        static void Talus(List<Decoration> rows,Catalog data,string map,Platform platform,Color rock)
        {
            // Only skirt exposed edges. Shared terraces and the channel under the bridge stay open.
            for(int side=0;side<4;side++)
            {
                var turn=Quaternion.Euler(0,side*90,0);
                float half=side%2==0?platform.hx:platform.hz,reach=side%2==0?platform.hz:platform.hx;
                int spans=Mathf.Max(2,Mathf.CeilToInt(half*.53f));
                for(int i=0;i<spans;i++)
                {
                    float x=(2*(i+.5f)/spans-1)*half;
                    var edge=new Vector3(platform.x,platform.y+platform.hy-.029f,platform.z)+turn*new Vector3(x,0,reach);
                    var outwards=turn*Vector3.forward;
                    var beyond=edge+outwards*.36f;
                    if(Catalog.Finite(data.Height(map,beyond.x,beyond.z)))continue;
                    float spread=4.85f;
                    if(map=="bridge"&&Mathf.Abs(edge.x)<3.4f)spread=Mathf.Min(1.05f,spread);
                    Put(rows,"arena_scree",edge,new Vector3(half*2/spans,2.8f,spread),tint:rock,
                        rotation:turn,castsShadow:false);
                    if((i+side)%3==1)
                        Put(rows,"shrub",edge+outwards*.54f-Vector3.up*.12f,new Vector3(.73f,.49f,.63f),
                            37*i,rock*.96f,.018f,castsShadow:false);
                }
                var corner=new Vector3(platform.x,platform.y+platform.hy-.029f,platform.z)+turn*new Vector3(half,0,reach);
                var probe=corner+turn*new Vector3(.32f,0,.32f);
                if(!Catalog.Finite(data.Height(map,probe.x,probe.z)))
                    Put(rows,"arena_scree_corner",corner,new Vector3(4.85f,2.8f,4.85f),tint:rock,rotation:turn,castsShadow:false);
            }
        }

        static void ArenaTrails(List<Decoration> rows,Catalog data,string id)
        {
            for(int j=0;j<32;j++)
            {
                float x=-17+j*1.06f,next=x+1.06f;
                float Z(float u)=>id=="bridge"?.31f*Mathf.Sin(u*.34f):id=="highland"?1.36f+.45f*Mathf.Sin(u*.35f):
                    id=="marsh"?2.0f+.70f*Mathf.Sin(u*.27f):.83f*Mathf.Sin(u*.26f)+.29f*Mathf.Cos(u*.61f);
                float z=Z(x),endZ=Z(next);
                if(id=="bridge"&&Mathf.Abs((x+next)*.5f)<3.54f)continue;
                float first=data.Height(id,x,z),last=data.Height(id,next,endZ);
                var mid=new Vector3((x+next)*.5f,0,(z+endZ)*.5f);
                float floor=data.Height(id,mid.x,mid.z);
                if(!Catalog.Finite(first)||!Catalog.Finite(last)||!Catalog.Finite(floor)||
                    Mathf.Abs(first-last)>.65f||Mathf.Abs(floor-(first+last)*.5f)>.075f)continue;
                var delta=new Vector3(next-x,last-first,endZ-z);
                // Plane normal and height follow the same walkable surface as the actors on the highland ramps.
                var across=new Vector3(endZ-z,0,x-next).normalized;
                float sideA=data.Height(id,mid.x+across.x*.83f,mid.z+across.z*.83f);
                float sideB=data.Height(id,mid.x-across.x*.83f,mid.z-across.z*.83f);
                if(!Catalog.Finite(sideA)||!Catalog.Finite(sideB)||Mathf.Abs(sideA-sideB)>.8f)continue;
                across.y=(sideA-sideB)/1.66f;
                var normal=Vector3.Cross(delta,across).normalized;
                var turn=Quaternion.LookRotation(delta,normal);
                mid.y=floor+.035f;
                bool paved=id=="ruins";
                var tint=paved?new Color(.48f,.48f,.44f):id=="highland"?new Color(.62f,.66f,.66f):new Color(.55f,.443f,.329f);
                Put(rows,paved?"road_setts":"road_link",mid,new Vector3(paved?2.68f:1.62f,1,delta.magnitude+.26f),
                    tint:tint,rotation:turn,castsShadow:false);
                if(j%5==2)
                {
                    var edge=mid+across*(j%2==0?1.32f:-1.32f);
                    float y=data.Height(id,edge.x,edge.z);
                    if(Catalog.Finite(y)&&ClearOfCover(data.Maps[id],edge.x,edge.z))
                    {
                        edge.y=y+.008f;
                        Put(rows,id=="highland"?"grass":"verge_flowers",edge,Vector3.one*.61f,37*j,
                            id=="highland"?new Color(.71f,.77f,.70f):White,.019f,castsShadow:false);
                    }
                }
            }
        }

        static void MeadowTrail(List<Decoration> rows)
        {
            for(int i=0;i<9;i++)
            {
                float x=-16+i*3.95f;
                Put(rows,"rock",new Vector3(x,-.19f,i%2==0?-12.4f:12.4f),new Vector3(1.2f,.61f,1.04f),i*63);
            }
        }

        static void Gorge(List<Decoration> rows,Color rock)
        {
            Put(rows,"platform",new Vector3(0,-4.15f,0),new Vector3(7.4f,.08f,70),tint:new Color(.21f,.44f,.51f));
            Put(rows,"platform",new Vector3(-.34f,-4.097f,0),new Vector3(2.35f,.012f,57),tint:new Color(.16f,.35f,.43f));
            var oak=new Color(.63f,.43f,.25f);
            for(int i=0;i<12;i++)
            {
                float x=-2.90f+i*.526f;
                Put(rows,"platform",new Vector3(x,.012f,0),new Vector3(.499f,.023f,3.56f),
                    tint:Color.Lerp(oak,new Color(.77f,.56f,.32f),(i%4)*.17f));
            }
            foreach(int side in new[]{-1,1})
            {
                float edge=side*1.93f;
                Put(rows,"platform",new Vector3(0,.345f,edge),new Vector3(5.86f,.101f,.12f),tint:oak);
                for(int i=0;i<4;i++)Put(rows,"platform",new Vector3(-2.85f+i*1.9f,.19f,edge),
                    new Vector3(.19f,.47f,.20f),tint:new Color(.57f,.47f,.32f));
                for(int i=0;i<7;i++)Put(rows,"rock",new Vector3(side*(3.59f+.31f*Mathf.Sin(i*2)), -2.11f, -13+i*4.14f),
                    new Vector3(2.05f,3.4f,3.2f),38*i,rock,castsShadow:false);
                Put(rows,"column",new Vector3(side*1.62f,-4.03f,-1.03f),new Vector3(1.11f,1.87f,1.09f),tint:rock,castsShadow:false);
                Put(rows,"column",new Vector3(side*1.62f,-4.03f,1.03f),new Vector3(1.11f,1.87f,1.09f),tint:rock,castsShadow:false);
                Put(rows,"platform",new Vector3(side*1.62f,-.43f,0),new Vector3(.52f,.43f,3.66f),tint:oak);
            }
        }

        static void IslandFringe(List<Decoration> rows,Color rock)
        {
            for(int i=0;i<11;i++)
            {
                float a=i*Mathf.PI*2/11;
                var p=new Vector3(Mathf.Cos(a)*14.1f,-3.65f-i%3*.50f,Mathf.Sin(a)*9.15f);
                Put(rows,"island_root",p,new Vector3(3.6f+i%3*.58f,3.68f+i%4,3.05f),31*i,rock);
            }
            for(int i=0;i<6;i++)
            {
                float x=-12+i*4.8f,z=i%2==0?10.47f:-10.47f;
                Put(rows,"rock",new Vector3(x,-.27f,z),new Vector3(2.51f,.88f,1.91f),16*i,rock);
                Put(rows,"column",new Vector3(x,.02f,z),Vector3.one*(i%2==0?1.02f:.81f),31*i,rock);
                if(i%2==0)Put(rows,"brazier",new Vector3(x+.61f,.022f,-z),Vector3.one*.88f);
            }
        }

        static void FallenCourt(List<Decoration> rows,Catalog data,Color stone)
        {
            for(int i=0;i<8;i++)
            {
                float x=-15+i*4.4f,z=i%2==0?-12.66f:12.66f;
                Put(rows,"column",new Vector3(x,-.11f,z),new Vector3(.90f,1.12f,.9f),
                    tint:stone,rotation:Quaternion.Euler(0,i*69,i%3==0?80:0));
                if(i%2==0)Put(rows,"brazier",new Vector3(x+.66f,.028f,z),Vector3.one*.84f);
            }
            Put(rows,"arch",new Vector3(-10,0,13.3f),Vector3.one*1.55f,15);
            Put(rows,"arch",new Vector3(10,0,-13.5f),Vector3.one*1.4f,-15);
        }

        static void MarshBanks(List<Decoration> rows,int quality)
        {
            var dry=new Color(.65f,.60f,.37f);
            var wet=new Color(.30f,.41f,.37f);
            foreach(int side in new[]{-1,1})
            {
                for(int i=0;i<10;i++)
                {
                    float x=-13.8f+i*3.04f,z=side*(9.52f+.24f*Mathf.Sin(i*2.03f));
                    Put(rows,"dot",new Vector3(x,.018f,z),new Vector3(3.63f,.9f,1.36f),26*i,dry);
                }
                for(int i=0;i<7;i++)
                {
                    float x=-3.58f+i*1.17f,z=side*(6.78f+Mathf.Sin(i*1.68f)*.44f);
                    Put(rows,"dot",new Vector3(x,.024f,z),new Vector3(.89f,.58f,.52f),34*i,wet);
                }
            }
            for(int i=0;i<(quality==2?6:4);i++)
            {
                int bank=i/2;
                float x=(i%2==0?-1:1)*(5.80f+bank*4.65f),z=bank*2.41f-3.1f+(i%2)*.78f;
                Put(rows,"mist",new Vector3(x,.11f,z),new Vector3(4.48f,.7f,2.8f),tint:new Color(.73f,.79f,.69f,.13f),wind:1,atmosphere:true);
            }
        }

        static void IceShelf(List<Decoration> rows,Catalog data,Color rock)
        {
            var snow=new Color(.87f,.94f,.96f);
            var edge=new Color(.40f,.64f,.73f);
            for(int side=-1;side<=1;side+=2)
            {
                Surface(rows,data,"highland",0,side*4.34f,5.61f,.17f,snow,.029f);
                for(int i=0;i<8;i++)
                {
                    float x=side*(3.18f+i*.62f),z=side*4.14f;
                    Surface(rows,data,"highland",x,z,.25f+i%3*.03f,.15f,snow,.044f);
                    Put(rows,"icicle",new Vector3(-16.8f+i*4.75f,-1.05f,side*12.02f),
                        new Vector3(.81f,.77f+i%3*.27f,.77f),rotation:Quaternion.Euler(0,0,180));
                    Put(rows,"rock",new Vector3(-17+i*4.72f,-.30f,side*12.23f),
                        new Vector3(1.53f,1.28f,1.52f),37*i,rock);
                }
                for(int i=0;i<3;i++)
                {
                    float z=side*(1.04f+i*.79f);
                    Surface(rows,data,"highland",side*(.35f+i*.77f),z,1.08f,.055f,edge,.032f,31+i*21);
                }
            }
        }

        public static Mote[] Particles(string id,int quality)
        {
            int n=quality==0?12:quality==1?40:88;var seeds=new Mote[n];var rng=new System.Random(701+StableHash(id));
            bool snow=id=="highland",sparks=id=="ruins"||id=="island",water=id=="bridge";
            for(int i=0;i<n;i++)seeds[i]=new Mote{origin=new Vector3(Range(rng,-18,18),Range(rng,0,7),Range(rng,-12,12)),phase=Range(rng,0,100),speed=Range(rng,.25f,.75f),size=snow?.95f:sparks?.08f:water?.75f:.6f,mesh=snow?"snow":sparks?"flame":"snow",tint=snow?new Color(.94f,.97f,1,.8f):sparks?new Color(1,.51f,.15f,.8f):new Color(.72f,.86f,.73f,.25f)};
            return seeds;
        }
        static int StableHash(string s){int h=0;for(int i=0;i<s.Length;i++)h=(h*31+s[i])&0xffff;return h;}
    }
}

