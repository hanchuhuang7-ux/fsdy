using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        internal int CulledStaticInstances {get;private set;}
        const int ChunkSize=32,MinChunkX=-13,MaxChunkX=8,MinChunkZ=-8,MaxChunkZ=8;
        const int RegionsWide=MaxChunkX-MinChunkX+1,RegionsDeep=MaxChunkZ-MinChunkZ+1;
        const int RegionCount=RegionsWide*RegionsDeep,GroupsWide=(RegionsWide+3)/4;
        const int GroupCount=GroupsWide*((RegionsDeep+3)/4);
        static int StaticRegion(Vector3 world)
        {
            int x=Mathf.Clamp(Mathf.FloorToInt(world.x/ChunkSize)-MinChunkX,0,RegionsWide-1);
            int z=Mathf.Clamp(Mathf.FloorToInt(world.z/ChunkSize)-MinChunkZ,0,RegionsDeep-1);
            return z*RegionsWide+x;
        }

        static int ArenaRegion(Vector3 position)
        {
            int x=Mathf.Clamp(Mathf.FloorToInt(position.x/8)+RegionsWide/2,0,RegionsWide-1);
            int z=Mathf.Clamp(Mathf.FloorToInt(position.z/8)+RegionsDeep/2,0,RegionsDeep-1);
            return z*RegionsWide+x;
        }

        Batch VisibleStaticPage(Batch source,out Batch distant)
        {
            var chunks=source.chunks;distant=null;
            if(source.visibilityVersion!=visibilityVersion)
            {
                source.visible=GeometryUtility.TestPlanesAABB(frustum,source.bounds);
                source.visibilityVersion=visibilityVersion;
                if(source.visible&&chunks!=null)chunks.Update(source,frustum,cameraPosition,QualityLevel,manor!=null);
            }
            Batch near=null;
            if(source.visible)
            {
                near=chunks==null?source:chunks.Near(source);
                if(chunks!=null)distant=chunks.Far(source);
            }
            if(MeasureCasterVertices&&chunks!=null)CulledStaticInstances+=source.count-(near?.count??0)-(distant?.count??0);
            return near;
        }

        // Static instance lists are sealed into 32 m cells, then grouped under 128 m parents.
        // A stationary camera reuses the exact matrix pages and material properties.
        sealed class StaticChunks
        {
            readonly Bounds[] cells=new Bounds[RegionCount],parents=new Bounds[GroupCount];
            readonly int[] first=new int[RegionCount],last=new int[RegionCount],next=new int[Capacity];
            readonly int[] children=new int[GroupCount],sibling=new int[RegionCount];
            readonly ushort[] active=new ushort[RegionCount];readonly byte[] groups=new byte[GroupCount],lod=new byte[RegionCount];
            readonly bool[] used=new bool[RegionCount],usedGroup=new bool[GroupCount];
            readonly Batch near,far;
            int cellCount,groupCount;
            bool prepared,allNear,allFar;

            public StaticChunks(MeshSet mesh){near=new Batch{mesh=mesh};far=new Batch{mesh=mesh};}
            public void Clear()
            {
                for(int i=0;i<cellCount;i++)used[active[i]]=false;
                for(int i=0;i<groupCount;i++)usedGroup[groups[i]]=false;
                cellCount=groupCount=0;prepared=false;
            }
            public void Add(int index,int cell,Bounds bounds)
            {
                int group=(cell/RegionsWide/4)*GroupsWide+(cell%RegionsWide/4);
                if(!usedGroup[group])
                {usedGroup[group]=true;groups[groupCount++]=(byte)group;children[group]=-1;parents[group]=bounds;}
                else parents[group].Encapsulate(bounds);
                next[index]=-1;
                if(!used[cell])
                {
                    used[cell]=true;active[cellCount++]=(ushort)cell;cells[cell]=bounds;first[cell]=index;
                    sibling[cell]=children[group];children[group]=cell;
                }
                else {cells[cell].Encapsulate(bounds);next[last[cell]]=index;}
                last[cell]=index;
            }
            public Batch Near(Batch source)=>allNear?source:!allFar&&near.count>0?near:null;
            public Batch Far(Batch source)=>allFar?source:!allNear&&far.count>0?far:null;
            public void Update(Batch source,Plane[] planes,Vector3 eye,int quality,bool frontier)
            {
                if(cellCount==0){allNear=true;allFar=false;return;}
                bool changed=!prepared;prepared=true;allNear=allFar=true;
                float reach=frontier?(quality==2?112:quality==1?86:64):
                    source.mesh.cutout?(quality==2?39:32):quality==2?59:44;
                bool small=source.mesh.nearDetail;
                for(int g=0;g<groupCount;g++)
                {
                    int parent=groups[g];bool visible=GeometryUtility.TestPlanesAABB(planes,parents[parent]);
                    bool wholeFar=parents[parent].SqrDistance(eye)>reach*reach;
                    for(int cell=children[parent];cell>=0;cell=sibling[cell])
                    {
                        byte tier=0;
                        if(visible&&GeometryUtility.TestPlanesAABB(planes,cells[cell]))
                        {
                            float d=cells[cell].SqrDistance(eye);
                            if(!small||d<145*145)
                                tier=(byte)(wholeFar||d>reach*reach?2:1);
                        }
                        changed|=lod[cell]!=tier;lod[cell]=tier;
                        allNear&=tier==1;allFar&=tier==2;
                    }
                }
                if(!changed||allNear||allFar)return;
                near.Clear();far.Clear();
                for(int r=0;r<cellCount;r++)
                {
                    int cell=active[r];byte tier=lod[cell];if(tier==0)continue;
                    var batch=tier==1?near:far;
                    for(int i=first[cell];i>=0;i=next[i])batch.Add(source.matrices[i],source.colors[i],source.winds[i],source.flashes[i]);
                }
            }
        }
    }
}
