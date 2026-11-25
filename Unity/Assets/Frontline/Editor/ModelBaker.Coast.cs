using System;
using System.Collections.Generic;
using System.IO;
using Frontline.Core;
using Frontline.UnityGame;
using UnityEditor;
using UnityEngine;

namespace Frontline.Editor
{
    public static partial class ModelBaker
    {
        const float ShoreBottom=-2.75f;
        static readonly Color SeaStone=new Color(.37f,.43f,.42f,0);
        static readonly Color CliffStone=new Color(.55f,.52f,.43f,0);
        static readonly Color Sand=new Color(.65f,.59f,.43f,0);
        static readonly Color Turf=new Color(.58f,.72f,.44f,1);

        static void BakeCoast()
        {
            const string directory="Assets/Frontline/Models/Resources/World";
            if(!Directory.Exists(directory)){Directory.CreateDirectory(directory);AssetDatabase.Refresh();}
            string path=directory+"/FrontierTerrain.asset";
            var asset=AssetDatabase.LoadAssetAtPath<FrontierTerrainAsset>(path);
            if(!asset)
            {
                asset=ScriptableObject.CreateInstance<FrontierTerrainAsset>();
                AssetDatabase.CreateAsset(asset,path);
            }
            var existing=new Dictionary<string,Mesh>(StringComparer.Ordinal);
            foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                if(obj is Mesh mesh)existing.Add(mesh.name,mesh);

            Mesh SaveMesh(string name,Geometry geometry)
            {
                bool render=name.StartsWith("Coast close ",StringComparison.Ordinal);
                var candidate=render?geometry.SurfaceMesh(name):geometry.ProxyMesh(name);
                if(!existing.TryGetValue(name,out var saved))
                {
                    AssetDatabase.AddObjectToAsset(candidate,asset);
                    return candidate;
                }
                existing.Remove(name);
                EditorUtility.CopySerialized(candidate,saved);
                UnityEngine.Object.DestroyImmediate(candidate);
                EditorUtility.SetDirty(saved);
                return saved;
            }

            var manor=FrontierMap.ManorCoast;
            var island=CoastRim(manor);
            var lake=new List<Vector3>(FrontierMap.ShoreStations);
            var outer=new List<Vector3>(FrontierMap.ShoreStations);
            for(int i=0;i<FrontierMap.ShoreStations;i++)
            {
                lake.Add(UnityWorld.Vector(FrontierMap.LakeBank[i]));
                outer.Add(UnityWorld.Vector(FrontierMap.OuterBank[FrontierMap.ShoreStations-1-i]));
            }
            asset.shoreRect=new Vector4(FrontierMap.West-24,-FrontierMap.HalfDepth-24,FrontierMap.East-FrontierMap.West+48,2*FrontierMap.HalfDepth+48);
            ProceduralTextureBaker.BakeShore(new[]{Waterline(island),Waterline(lake),Waterline(outer)},asset.shoreRect);
            var manorFloor=new Geometry();
            for(int i=1;i<manor.Count;i++)
            {
                var a=manor[i-1];var b=manor[i];
                manorFloor.Quad(V(a.x,0,a.south),V(a.x,0,a.north),V(b.x,0,b.north),V(b.x,0,b.south),Turf);
            }
            asset.manorFloor=SaveMesh("Manor walkable ground",manorFloor);
            var mainlandFloor=new Geometry();
            MainlandPatches((a,b,c,d)=>mainlandFloor.Quad(a,b,c,d,Turf));
            asset.frontierFloor=SaveMesh("Frontier walkable ground",mainlandFloor);
            var barrier=new Geometry();
            AddShoreBarrier(barrier,island,FrontierMap.BridgeStart);
            AddShoreBarrier(barrier,lake,FrontierMap.Boundary);
            AddShoreBarrier(barrier,outer,float.PositiveInfinity);
            float bridge=FrontierMap.BridgeHalfWidth,entry=FrontierMap.BridgeStart,exit=FrontierMap.Boundary;
            barrier.Quad(V(entry,-.2f,bridge),V(entry,.8f,bridge),V(exit,.8f,bridge),V(exit,-.2f,bridge),CliffStone);
            barrier.Quad(V(exit,-.2f,-bridge),V(exit,.8f,-bridge),V(entry,.8f,-bridge),V(entry,-.2f,-bridge),CliffStone);
            asset.shoreBarrier=SaveMesh("Inward shore collision",barrier);
            asset.closeChunks=CoastChunks(manor,island,lake,outer,32,true,SaveMesh);
            asset.wideChunks=CoastChunks(manor,island,lake,outer,128,false,SaveMesh);
            asset.distantRelief=SaveMesh("Distant folds and ridges",DistantRelief());
            var water=new Geometry();
            water.Quad(V(-.5f,0,-.5f),V(-.5f,0,.5f),V(.5f,0,.5f),V(.5f,0,-.5f),Color.white);
            asset.lakeSurface=SaveMesh("Lake horizon plane",water);
            foreach(var obsolete in existing.Values)UnityEngine.Object.DestroyImmediate(obsolete,true);
            EditorUtility.SetDirty(asset);
            Debug.Log("FRONTLINE_COAST_BAKED fine="+asset.closeChunks.Length+" coarse="+asset.wideChunks.Length+
                " manor="+FrontierMap.ManorArea+"m2 frontier="+FrontierMap.FrontierArea+"m2");
        }

        static void MainlandPatches(Action<Vector3,Vector3,Vector3,Vector3> add)
        {
            for(int i=0;i<FrontierMap.ShoreStations;i++)
                for(int band=0;band<FrontierMap.TerrainBands;band++)
                {
                    int next=(i+1)%FrontierMap.ShoreStations;
                    add(Survey(i,band),Survey(next,band),Survey(next,band+1),Survey(i,band+1));
                }
        }

        static Vector3 Survey(int station,int band) => UnityWorld.Vector(FrontierMap.TerrainPoint(station,band));

        static List<Vector3> CoastRim(IReadOnlyList<FrontierMap.ShoreSlice> coast)
        {
            var rim=new List<Vector3>(coast.Count*2);
            foreach(var point in coast)rim.Add(V(point.x,0,point.north));
            for(int i=coast.Count-1;i>=0;i--)rim.Add(V(coast[i].x,0,coast[i].south));
            return rim;
        }

        static void AddShoreBarrier(Geometry mesh,List<Vector3> rim,float bridgeX)
        {
            for(int i=0;i<rim.Count;i++)
            {
                var a=rim[i];var b=rim[(i+1)%rim.Count];
                if(a.x==bridgeX&&b.x==bridgeX)
                {
                    SplitBridgeEdge(a,b,BarrierEdge);
                }
                else BarrierEdge(a,b);
            }

            void BarrierEdge(Vector3 a,Vector3 b) =>
                mesh.Quad(a+Vector3.down,a+Vector3.up*5,b+Vector3.up*5,b+Vector3.down,CliffStone);
        }

        static void SplitBridgeEdge(Vector3 start,Vector3 end,Action<Vector3,Vector3> edge)
        {
            float from=Mathf.Min(start.z,end.z),to=Mathf.Max(start.z,end.z);
            float width=FrontierMap.BridgeHalfWidth;
            bool ascending=end.z>start.z;
            if(from< -width)
            {
                var a=V(start.x,0,from);var b=V(start.x,0,Mathf.Min(-width,to));
                edge(ascending?a:b,ascending?b:a);
            }
            if(to>width)
            {
                var a=V(start.x,0,Mathf.Max(width,from));var b=V(start.x,0,to);
                edge(ascending?a:b,ascending?b:a);
            }
        }

        static Mesh[] CoastChunks(IReadOnlyList<FrontierMap.ShoreSlice> manor,List<Vector3> island,List<Vector3> lake,
            List<Vector3> outer,int size,bool detail,Func<string,Geometry,Mesh> save)
        {
            var pages=new Dictionary<(int,int),Geometry>();
            var a=new List<Vector3>(8);
            var b=new List<Vector3>(8);

            void Patch(Vector3 a0,Vector3 b0,Vector3 c0,Vector3 d0,Color tint,bool ground=false,bool shore=false)
            {
                float left=Mathf.Min(Mathf.Min(a0.x,b0.x),Mathf.Min(c0.x,d0.x));
                float right=Mathf.Max(Mathf.Max(a0.x,b0.x),Mathf.Max(c0.x,d0.x));
                float bottom=Mathf.Min(Mathf.Min(a0.z,b0.z),Mathf.Min(c0.z,d0.z));
                float top=Mathf.Max(Mathf.Max(a0.z,b0.z),Mathf.Max(c0.z,d0.z));
                for(int x=Mathf.FloorToInt(left/size);x<=Mathf.FloorToInt(right/size);x++)
                    for(int z=Mathf.FloorToInt(bottom/size);z<=Mathf.FloorToInt(top/size);z++)
                    {
                        a.Clear();a.Add(a0);a.Add(b0);a.Add(c0);
                        if(c0!=d0)a.Add(d0);
                        Clip(a,b,x*size,true,1);
                        Clip(b,a,(x+1)*size,true,-1);
                        Clip(a,b,z*size,false,1);
                        Clip(b,a,(z+1)*size,false,-1);
                        if(a.Count<3)continue;
                        bool face=false;
                        for(int i=2;i<a.Count&&!face;i++)
                            face=Vector3.Cross(a[i-1]-a[0],a[i]-a[0]).sqrMagnitude>=1e-8f;
                        if(!face)continue;
                        if(!pages.TryGetValue((x,z),out var page))pages.Add((x,z),page=new Geometry());
                        page.surface=ground?Surface.Terrain:shore?Surface.Shore:tint.a>.5f?Surface.Leaf:tint.a>.10f?Surface.Earth:Surface.Outcrop;
                        for(int i=2;i<a.Count;i++)
                        {
                            if(ground)page.Gradient(a[0],a[i-1],a[i],Ground(a[0]),Ground(a[i-1]),Ground(a[i]));
                            else page.Triangle(a[0],a[i-1],a[i],tint);
                        }
                    }
            }

            void IslandLand()
            {
                for(int i=1;i<manor.Count;i++)
                {
                    var l=manor[i-1];var r=manor[i];
                    int bands=detail?6:3;
                    for(int band=0;band<bands;band++)
                    {
                        float t=band/(float)bands,u=(band+1)/(float)bands;
                        Patch(V(l.x,0,Mathf.Lerp(l.south,l.north,t)),V(l.x,0,Mathf.Lerp(l.south,l.north,u)),
                            V(r.x,0,Mathf.Lerp(r.south,r.north,u)),V(r.x,0,Mathf.Lerp(r.south,r.north,t)),Turf,true);
                    }
                }
            }

            void Rim(List<Vector3> rim,float bridgeX)
            {
                for(int i=0;i<rim.Count;i++)
                {
                    var prev=rim[(i+rim.Count-1)%rim.Count];
                    var start=rim[i];var end=rim[(i+1)%rim.Count];var next=rim[(i+2)%rim.Count];
                    var outward=Outward(start,end);
                    var aOut=(Outward(prev,start)+outward).normalized;
                    var bOut=(outward+Outward(end,next)).normalized;
                    if(start.x==bridgeX&&end.x==bridgeX)
                        SplitBridgeEdge(start,end,(a,b)=>Cliff(a,b,a==start?aOut:outward,b==end?bOut:outward,i));
                    else Cliff(start,end,aOut,bOut,i);
                }
            }

            void Cliff(Vector3 start,Vector3 end,Vector3 aOut,Vector3 bOut,int index)
            {
                float grit=.65f+.35f*Mathf.Sin(index*2.63f);
                CliffProfile(start,aOut,out var a1,out var a2,out var a3);
                CliffProfile(end,bOut,out var b1,out var b2,out var b3);
                if(detail)
                {
                    Patch(end,start,a1,b1,Color.Lerp(CliffStone,Turf,.23f*grit),shore:true);
                    Patch(b1,a1,a2,b2,Color.Lerp(CliffStone,SeaStone,grit*.75f),shore:true);
                    Patch(b2,a2,a3,b3,Sand,shore:true);
                }
                else Patch(end,start,a3,b3,Color.Lerp(CliffStone,SeaStone,grit*.65f),shore:true);
            }

            IslandLand();
            MainlandPatches((a,b,c,d)=>{
                Patch(a,b,c,c,Turf,true);
                Patch(a,c,d,d,Turf,true);
            });
            Rim(island,FrontierMap.BridgeStart);
            Rim(lake,FrontierMap.Boundary);
            Rim(outer,float.PositiveInfinity);
            LakeRoads(detail,(a,b,c,d,color)=>Patch(a,b,c,d,color));
            if(!detail)FarForests((a,b,c,color)=>Patch(a,b,c,c,color));
            var keys=new List<(int,int)>(pages.Keys);
            keys.Sort();
            var chunks=new Mesh[keys.Count];
            for(int i=0;i<chunks.Length;i++)
            {
                var key=keys[i];
                chunks[i]=save((detail?"Coast close ":"Coast wide ")+key.Item1+"_"+key.Item2,pages[key]);
            }
            return chunks;
        }

        static Vector3 Outward(Vector3 a,Vector3 b) => new Vector3(a.z-b.z,0,b.x-a.x).normalized;

        static List<Vector3> Waterline(List<Vector3> rim)
        {
            var result=new List<Vector3>(rim.Count);
            for(int i=0;i<rim.Count;i++)
            {
                var p=rim[i];
                var outward=(Outward(rim[(i+rim.Count-1)%rim.Count],p)+Outward(p,rim[(i+1)%rim.Count])).normalized;
                CliffProfile(p,outward,out _,out var tide,out var shelf);
                result.Add(Vector3.Lerp(tide,shelf,Mathf.InverseLerp(tide.y,shelf.y,-1.70f)));
            }
            return result;
        }

        static void CliffProfile(Vector3 point,Vector3 outward,out Vector3 ledge,out Vector3 tide,out Vector3 shelf)
        {
            float grit=.65f+.35f*Mathf.Sin(point.x*.281f+point.z*.352f);
            float beach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.14f,.71f,Mathf.Cos(point.x*.109f+point.z*.083f)));
            ledge=point+outward*(.15f+grit*.45f+beach*1.45f)+Vector3.down*Mathf.Lerp(.53f+grit*.42f,.32f,beach);
            tide=point+outward*(.94f+grit*.8f+beach*2.18f)+Vector3.down*1.62f;
            shelf=point+outward*(2.9f+grit*2.2f+beach*3.1f)+Vector3.up*ShoreBottom;
        }

        static Color Ground(Vector3 p)
        {
            float mottled=.5f+.19f*Mathf.Sin(p.x*.091f+p.z*.031f)+.16f*Mathf.Cos(p.z*.139f-p.x*.13f);
            float blend;
            if(FrontierMap.IsIsland(p.x,p.z)&&FrontierMap.ShoreAt(p.x,out var south,out var north))
                blend=Mathf.Min(p.z-south,north-p.z);
            else
            {
                float depth=FrontierMap.MainlandDepth(p.x,p.z,out blend);
                if(depth>.65f)blend=12;
            }
            var grass=Color.Lerp(new Color(.39f,.54f,.35f,1),new Color(.72f,.79f,.56f,1),mottled);
            grass=Color.Lerp(grass,new Color(.53f,.57f,.43f,1),Mathf.Clamp01((p.y-2.0f)/9f)*.72f);
            return Color.Lerp(new Color(.76f,.72f,.53f,.45f),grass,Mathf.Clamp01(blend/12));
        }

        static void Clip(List<Vector3> source,List<Vector3> result,float edge,bool axisX,int sign)
        {
            result.Clear();
            if(source.Count==0)return;
            var previous=source[source.Count-1];
            float delta=((axisX?previous.x:previous.z)-edge)*sign;
            foreach(var point in source)
            {
                float next=((axisX?point.x:point.z)-edge)*sign;
                if((delta<0&&next>0)||(delta>0&&next<0))result.Add(Vector3.LerpUnclamped(previous,point,delta/(delta-next)));
                if(next>=0)result.Add(point);
                previous=point;
                delta=next;
            }
        }
    }
}
