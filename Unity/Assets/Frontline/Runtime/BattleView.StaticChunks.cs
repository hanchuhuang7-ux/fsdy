using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        internal int CulledStaticInstances { get; private set; }

        // 3×2 on the peninsula, the bridge, and four quadrants on the frontier.
        const int StaticRegionCount=11;
        static int StaticRegion(Vector3 world)
        {
            if(world.x<=FrontierMap.BridgeStart)
            {
                float u=(world.x-FrontierMap.West)/(2*Manor.PlateauHalfWidth);
                return Mathf.Clamp((int)(u*3),0,2)+(world.z>=0?3:0);
            }
            if(world.x<FrontierMap.Boundary)return 6;
            return 7+(world.x>=27?1:0)+(world.z>=0?2:0);
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
            readonly byte[] regions=new byte[Capacity];
            readonly Batch compact;
            int populated,visible=-1;

            public StaticChunks(MeshSet mesh) { compact=new Batch{mesh=mesh}; }
            public void Clear() { populated=0;visible=-1; }

            public void Add(int index,int region,Bounds instance)
            {
                regions[index]=(byte)region;
                int bit=1<<region;
                if((populated&bit)==0)bounds[region]=instance;
                else bounds[region].Encapsulate(instance);
                populated|=bit;
            }

            public Batch Page(Batch source) => populated==0||visible==populated?source:compact;

            public void Update(Batch source,Plane[] frustum)
            {
                if(populated==0)return;
                int mask=0;
                for(int region=0;region<bounds.Length;region++)
                {
                    int bit=1<<region;
                    if((populated&bit)!=0&&GeometryUtility.TestPlanesAABB(frustum,bounds[region]))mask|=bit;
                }
                if(mask==visible)return;
                visible=mask;
                if(mask==populated)return;

                // Repack only when the visible regions change. Batch order, material and page capacity stay intact.
                compact.Clear();
                if(mask==0)return;
                for(int i=0;i<source.count;i++)
                    if((mask&(1<<regions[i]))!=0)
                        compact.Add(source.matrices[i],source.colors[i],source.winds[i],source.flashes[i]);
            }
        }
    }
}
