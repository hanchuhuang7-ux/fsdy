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
            public string mesh; public Matrix4x4 matrix; public Color tint; public float wind; public bool atmosphere;
        }
        public struct Mote
        {
            public Vector3 origin; public float phase,speed,size; public string mesh; public Color tint;
        }
        static readonly Color White=Color.white;
        static void Put(List<Decoration> rows,string mesh,Vector3 p,Vector3 size,float yaw=0,Color? tint=null,float wind=0,bool atmosphere=false,Quaternion? rotation=null)
        { rows.Add(new Decoration{mesh=mesh,matrix=Matrix4x4.TRS(p,rotation??Quaternion.Euler(0,yaw,0),size),tint=tint??White,wind=wind,atmosphere=atmosphere}); }
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
                Put(rows,"platform",new Vector3(p.x,p.y,p.z),new Vector3(p.hx*2,p.hy*2,p.hz*2),tint:p.bridge?new Color(.57f,.4f,.24f):ground,rotation:q);
                if(!p.bridge&&!p.ramp)
                {
                    float depth=island?6:bridge?5.2f:2.5f;
                    Put(rows,island?"island_root":"base",new Vector3(p.x,p.y-p.hy-depth*.5f+.03f,p.z),new Vector3(p.hx*1.98f,depth,p.hz*1.98f),tint:rock);
                    Put(rows,"cliff",new Vector3(p.x,p.y-p.hy-.45f,p.z),new Vector3(p.hx*2.01f,.85f,p.hz*2.01f),tint:rock*.85f);
                }
            }
            foreach(var r in map.rocks)Put(rows,"rock",new Vector3(r.x,r.r*.2f,r.z),Vector3.one*r.r*2,Range(rng,0,360),rock);
            foreach(var w in map.walls){bool alongZ=w.hz>w.hx;Put(rows,"wall",new Vector3(w.x,w.hy,w.z),alongZ?new Vector3(w.hz*2,w.hy*2,w.hx*2):new Vector3(w.hx*2,w.hy*2,w.hz*2),alongZ?90:0,tint:rock);}
            foreach(var z in map.zones)
            {float h=data.Height(id,z.x,z.z);if(Catalog.Finite(h))Put(rows,"platform",new Vector3(z.x,h+.008f,z.z),new Vector3(z.hx*2,.012f,z.hz*2),tint:z.type=="mud"?new Color(.31f,.34f,.24f):new Color(.61f,.8f,.85f));}
            int density=quality==0?90:quality==1?280:560;
            for(int i=0;i<density;i++)
            {
                float x=Range(rng,-17.6f,17.6f),z=Range(rng,-11.6f,11.6f),h=data.Height(id,x,z);
                if(!Catalog.Finite(h)||!ClearOfCover(map,x,z)||(!marsh&&Mathf.Abs(z)<1.6f))continue;
                string mesh=snow?"grass":marsh?"reeds":ruins?(i%4==0?"rock":"grass"):i%8==0?"flowers":i%13==0?"shrub":"grass";
                float scale=Range(rng,.45f,1.05f);if(ruins)scale*=.65f;if(snow)scale*=.5f;
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
                Put(rows,mesh,new Vector3(x,-.15f,z),Vector3.one*scale,Range(rng,0,360),White,.012f);
                Put(rows,"rock",new Vector3(x,-.45f,z),new Vector3(2.2f,.9f,2),Range(rng,0,360),rock);
            }
            if(id=="meadow")
            {
                for(int i=0;i<26;i++)
                {float x=-17+i*1.35f,z=Mathf.Sin(i*.22f)*.55f,h=data.Height(id,x,z);Put(rows,"dot",new Vector3(x,h+.014f,z),new Vector3(1.7f,.015f,1.05f),tint:new Color(.64f,.56f,.37f));}
                for(int i=0;i<12;i++)Put(rows,"shrub",new Vector3(-16+i*3,-.02f,(i%2==0?-1:1)*11.5f),Vector3.one*1.1f,Range(rng,0,360),wind:.025f);
            }
            else if(bridge)
            {
                Put(rows,"platform",new Vector3(0,-4.15f,0),new Vector3(6.1f,.08f,70),tint:new Color(.19f,.42f,.47f));
                for(int i=0;i<12;i++){float z=-16+i*2.8f;Put(rows,"water_ripple",new Vector3(Range(rng,-1.5f,1.5f),-4.06f,z),new Vector3(1.3f,.4f,2.4f),tint:new Color(.65f,.85f,.85f,.32f),atmosphere:true);}
                for(int i=0;i<4;i++){float x=i%2==0?-2.8f:2.8f,z=i<2?-1.4f:1.4f;Put(rows,"column",new Vector3(x,-4,z),new Vector3(.35f,1.2f,.35f),tint:new Color(.5f,.34f,.22f));}
                for(int i=0;i<10;i++)Put(rows,"rock",new Vector3((i%2==0?-1:1)*Range(rng,3.4f,5),-3.8f,Range(rng,-17,17)),new Vector3(2,3,2),Range(rng,0,360),rock);
            }
            else if(island)
            {
                for(int i=0;i<10;i++){float a=i*Mathf.PI*.2f;Put(rows,"rock",new Vector3(Mathf.Cos(a)*17,-3-Range(rng,0,4),Mathf.Sin(a)*12),Vector3.one*Range(rng,.8f,2.8f),Range(rng,0,360),rock);}
                for(int i=0;i<6;i++){float x=-13+i*5.2f;Put(rows,"column",new Vector3(x,0,10.6f),Vector3.one*Range(rng,.7f,1.1f));if(i%2==0)Put(rows,"brazier",new Vector3(x,.04f,-10.5f),Vector3.one);}
            }
            else if(ruins)
            {
                for(int i=0;i<8;i++){float x=-15+i*4.4f;Put(rows,"column",new Vector3(x,.12f,(i%2==0?-1:1)*11.6f),new Vector3(1,1.2f,1),rotation:Quaternion.Euler(0,Range(rng,0,360),i%3==0?78:0));}
                Put(rows,"arch",new Vector3(-10,0,12.8f),Vector3.one*1.8f,15);Put(rows,"arch",new Vector3(10,0,-12.6f),Vector3.one*1.5f,-15);
                for(int i=0;i<5;i++)Put(rows,"brazier",new Vector3(-14+i*7,0,(i%2==0?-1:1)*10.4f),Vector3.one*.9f);
                for(int i=0;i<20;i++){float x=Range(rng,-16,16),z=Range(rng,-10,10);Put(rows,"dot",new Vector3(x,.018f,z),new Vector3(Range(rng,.5f,1.8f),.01f,Range(rng,.7f,1.4f)),tint:new Color(.28f,.27f,.23f));}
            }
            else if(marsh)
            {
                for(int i=0;i<14;i++){float x=Range(rng,-3.8f,3.8f),z=Range(rng,-7.2f,7.2f);Put(rows,"water_ripple",new Vector3(x,.032f,z),Vector3.one*Range(rng,.7f,1.4f),tint:new Color(.53f,.69f,.62f,.28f),atmosphere:true);}
                for(int i=0;i<12;i++){float x=Range(rng,-15,15),z=Range(rng,-9,9);Put(rows,"mist",new Vector3(x,.14f+Range(rng,0,.2f),z),new Vector3(6.8f,.7f,4.6f),tint:new Color(.7f,.79f,.71f,.16f),wind:1,atmosphere:true);}
            }
            else if(snow)
            {
                for(int i=0;i<16;i++){float x=-17+i*2.25f;Put(rows,"icicle",new Vector3(x,-.4f,(i%2==0?-1:1)*12.05f),new Vector3(.7f,Range(rng,.7f,1.5f),.7f));}
                for(int i=0;i<20;i++)Put(rows,"rock",new Vector3(Range(rng,-18,18),-.5f,(i%2==0?-1:1)*12),new Vector3(1.8f,2.2f,1.6f),Range(rng,0,360),new Color(.75f,.85f,.87f));
            }
            // Distant pillars frame the miniature without extending the simulation map.
            for(int i=0;i<8;i++){float a=(i+.5f)*Mathf.PI*.25f;Put(rows,"rock",new Vector3(Mathf.Cos(a)*31,-8,Mathf.Sin(a)*25),new Vector3(3,Range(rng,5,12),3),Range(rng,0,360),rock*.72f);}
            return rows;
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

