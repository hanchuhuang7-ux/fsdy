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
                var candidate=geometry.Build(name,false);
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
            var frontier=FrontierMap.FrontierCoast;
            asset.manorFloor=SaveMesh("Manor walkable ground",FloorMesh(manor,false));
            asset.frontierFloor=SaveMesh("Frontier walkable ground",FloorMesh(frontier,true));
            var barrier=new Geometry();
            AddShoreBarrier(barrier,manor,FrontierMap.BridgeStart);
            AddShoreBarrier(barrier,frontier,FrontierMap.Boundary);
            asset.shoreBarrier=SaveMesh("Inward shore collision",barrier);
            asset.closeChunks=CoastChunks(manor,frontier,32,true,SaveMesh);
            asset.wideChunks=CoastChunks(manor,frontier,96,false,SaveMesh);
            foreach(var obsolete in existing.Values)UnityEngine.Object.DestroyImmediate(obsolete,true);
            EditorUtility.SetDirty(asset);
            Debug.Log("FRONTLINE_COAST_BAKED fine="+asset.closeChunks.Length+" coarse="+asset.wideChunks.Length+
                " manor="+FrontierMap.ManorArea+"m2 frontier="+FrontierMap.FrontierArea+"m2");
        }

        static Geometry FloorMesh(IReadOnlyList<FrontierMap.ShoreSlice> coast,bool mainland)
        {
            var floor=new Geometry();
            for(int i=1;i<coast.Count;i++)
            {
                var a=coast[i-1];var b=coast[i];
                if(!mainland)floor.Quad(V(a.x,0,a.south),V(a.x,0,a.north),V(b.x,0,b.north),V(b.x,0,b.south),Turf);
                else
                    for(int band=0;band<FrontierMap.TerrainBands;band++)
                        floor.Quad(Survey(i-1,band),Survey(i-1,band+1),Survey(i,band+1),Survey(i,band),Turf);
            }
            return floor;
        }

        static Vector3 Survey(int station,int band) => UnityWorld.Vector(FrontierMap.TerrainPoint(station,band));

        static List<Vector3> CoastRim(IReadOnlyList<FrontierMap.ShoreSlice> coast)
        {
            var rim=new List<Vector3>(coast.Count*2);
            foreach(var point in coast)rim.Add(V(point.x,0,point.north));
            for(int i=coast.Count-1;i>=0;i--)rim.Add(V(coast[i].x,0,coast[i].south));
            return rim;
        }

        static void AddShoreBarrier(Geometry mesh,IReadOnlyList<FrontierMap.ShoreSlice> coast,float bridgeX)
        {
            var rim=CoastRim(coast);
            for(int i=0;i<rim.Count;i++)
            {
                var a=rim[i];var b=rim[(i+1)%rim.Count];
                if(a.x==bridgeX&&b.x==bridgeX)
                {
                    float sign=a.z>b.z?1:-1;
                    BarrierEdge(a,V(bridgeX,0,sign*FrontierMap.BridgeHalfWidth));
                    BarrierEdge(V(bridgeX,0,-sign*FrontierMap.BridgeHalfWidth),b);
                }
                else BarrierEdge(a,b);
            }

            void BarrierEdge(Vector3 a,Vector3 b) =>
                mesh.Quad(a+Vector3.down,a+Vector3.up*5,b+Vector3.up*5,b+Vector3.down,CliffStone);
        }

        static Mesh[] CoastChunks(IReadOnlyList<FrontierMap.ShoreSlice> manor,IReadOnlyList<FrontierMap.ShoreSlice> frontier,
            int size,bool detail,Func<string,Geometry,Mesh> save)
        {
            var pages=new Dictionary<(int,int),Geometry>();
            var a=new List<Vector3>(8);
            var b=new List<Vector3>(8);

            void Patch(Vector3 a0,Vector3 b0,Vector3 c0,Vector3 d0,Color tint,bool ground=false)
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
                        for(int i=2;i<a.Count;i++)
                        {
                            if(ground)page.Gradient(a[0],a[i-1],a[i],Ground(a[0]),Ground(a[i-1]),Ground(a[i]));
                            else page.Triangle(a[0],a[i-1],a[i],tint);
                        }
                    }
            }

            void Land(IReadOnlyList<FrontierMap.ShoreSlice> coast,float bridgeX,bool mainland)
            {
                for(int i=1;i<coast.Count;i++)
                {
                    var l=coast[i-1];var r=coast[i];
                    // Cross-bands soften the colour changes around the inlets at either LOD.
                    int bands=mainland?FrontierMap.TerrainBands:detail?6:3;
                    for(int band=0;band<bands;band++)
                    {
                        float t=band/(float)bands,u=(band+1)/(float)bands;
                        if(mainland)
                        {
                            var start=Survey(i-1,band);var north=Survey(i-1,band+1);
                            var diagonal=Survey(i,band+1);var end=Survey(i,band);
                            // Clip the two original triangles separately so the facets inland
                            // coincide with the farm and patrol physics floor at both distances.
                            Patch(start,north,diagonal,diagonal,Turf,true);
                            Patch(start,diagonal,end,end,Turf,true);
                        }
                        else Patch(V(l.x,0,Mathf.Lerp(l.south,l.north,t)),V(l.x,0,Mathf.Lerp(l.south,l.north,u)),
                            V(r.x,0,Mathf.Lerp(r.south,r.north,u)),V(r.x,0,Mathf.Lerp(r.south,r.north,t)),Turf,true);
                    }
                }
                var rim=CoastRim(coast);
                for(int i=0;i<rim.Count;i++)
                {
                    var prev=rim[(i+rim.Count-1)%rim.Count];
                    var start=rim[i];var end=rim[(i+1)%rim.Count];var next=rim[(i+2)%rim.Count];
                    var outward=Outward(start,end);
                    var aOut=(Outward(prev,start)+outward).normalized;
                    var bOut=(outward+Outward(end,next)).normalized;
                    if(start.x==bridgeX&&end.x==bridgeX)
                    {
                        float sign=start.z>end.z?1:-1;
                        Cliff(start,V(bridgeX,0,sign*FrontierMap.BridgeHalfWidth),aOut,outward,i);
                        Cliff(V(bridgeX,0,-sign*FrontierMap.BridgeHalfWidth),end,outward,bOut,i);
                    }
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
                    Patch(end,start,a1,b1,Color.Lerp(CliffStone,Turf,.23f*grit));
                    Patch(b1,a1,a2,b2,Color.Lerp(CliffStone,SeaStone,grit*.75f));
                    Patch(b2,a2,a3,b3,Sand);
                }
                else Patch(end,start,a3,b3,Color.Lerp(CliffStone,SeaStone,grit*.65f));
            }

            Land(manor,FrontierMap.BridgeStart,false);
            Land(frontier,FrontierMap.Boundary,true);
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

        static void CliffProfile(Vector3 point,Vector3 outward,out Vector3 ledge,out Vector3 tide,out Vector3 shelf)
        {
            float grit=.65f+.35f*Mathf.Sin(point.x*.281f+point.z*.352f);
            ledge=point+outward*(.15f+grit*.45f)+Vector3.down*(.53f+grit*.42f);
            tide=point+outward*(.94f+grit*.8f)+Vector3.down*1.62f;
            shelf=point+outward*(2.9f+grit*2.2f)+Vector3.up*ShoreBottom;
        }

        static Color Ground(Vector3 p)
        {
            float mottled=.5f+.19f*Mathf.Sin(p.x*.091f+p.z*.031f)+.16f*Mathf.Cos(p.z*.139f-p.x*.13f);
            float blend=FrontierMap.ShoreAt(p.x,out var south,out var north)?Mathf.Min(p.z-south,north-p.z):0;
            var grass=Color.Lerp(new Color(.46f,.61f,.38f,1),new Color(.79f,.84f,.59f,1),mottled);
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
