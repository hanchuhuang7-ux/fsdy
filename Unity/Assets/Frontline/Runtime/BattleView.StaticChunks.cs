using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        internal int CulledStaticInstances { get; private set; }

        const int ChunkSize=32,RegionsWide=16,RegionsDeep=10,StaticRegionCount=RegionsWide*RegionsDeep;
        static int StaticRegion(Vector3 world)
        {
            int x=Mathf.Clamp(Mathf.FloorToInt(world.x/ChunkSize)+5,0,RegionsWide-1);
            int z=Mathf.Clamp(Mathf.FloorToInt(world.z/ChunkSize)+5,0,RegionsDeep-1);
            return z*RegionsWide+x;
        }

        Batch VisibleStaticPage(Batch source)
        {
            var chunks=source.chunks;
            if(source.visibilityVersion!=visibilityVersion)
            {
                source.visible=GeometryUtility.TestPlanesAABB(frustum,source.bounds);
                source.visibilityVersion=visibilityVersion;
                if(source.visible&&chunks!=null)chunks.Update(source,frustum);
            }
            var visible=source.visible?(chunks?.Page(source)??source):null;
            if(MeasureCasterVertices&&chunks!=null)CulledStaticInstances+=source.count-(visible?.count??0);
            return visible!=null&&visible.count>0?visible:null;
        }

        sealed class StaticChunks
        {
            readonly Bounds[] bounds=new Bounds[StaticRegionCount];
            readonly int[] first=new int[StaticRegionCount],last=new int[StaticRegionCount],next=new int[Capacity];
            readonly byte[] active=new byte[StaticRegionCount];
            readonly bool[] occupied=new bool[StaticRegionCount],visible=new bool[StaticRegionCount];
            readonly Batch compact;
            int populated;
            bool allVisible,prepared;

            public StaticChunks(MeshSet mesh) { compact=new Batch{mesh=mesh}; }
            public void Clear()
            {
                for(int i=0;i<populated;i++)occupied[active[i]]=false;
                populated=0;
                prepared=false;
            }

            public void Add(int index,int region,Bounds instance)
            {
                next[index]=-1;
                if(!occupied[region])
                {
                    occupied[region]=true;
                    active[populated++]=(byte)region;
                    bounds[region]=instance;
                    first[region]=index;
                }
                else
                {
                    bounds[region].Encapsulate(instance);
                    next[last[region]]=index;
                }
                last[region]=index;
            }

            public Batch Page(Batch source) => populated==0||allVisible?source:compact;

            public void Update(Batch source,Plane[] frustum)
            {
                if(populated==0)return;
                bool changed=!prepared;
                prepared=true;
                allVisible=true;
                for(int i=0;i<populated;i++)
                {
                    int region=active[i];
                    bool inView=GeometryUtility.TestPlanesAABB(frustum,bounds[region]);
                    changed|=inView!=visible[region];
                    visible[region]=inView;
                    allVisible&=inView;
                }
                if(!changed||allVisible)return;
                // The links are built once per static page. Hidden instance indices are never visited.
                compact.Clear();
                for(int region=0;region<populated;region++)
                {
                    int key=active[region];
                    if(!visible[key])continue;
                    for(int i=first[key];i>=0;i=next[i])
                        compact.Add(source.matrices[i],source.colors[i],source.winds[i],source.flashes[i]);
                }
            }
        }
    }
}
