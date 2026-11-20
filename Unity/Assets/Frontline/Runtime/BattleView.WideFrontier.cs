using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        readonly List<Batch> outerStill=new List<Batch>();
        readonly Matrix4x4[] terrainMatrix={Matrix4x4.identity};
        readonly Vector4[] terrainTint=new Vector4[1];
        readonly float[] terrainEffect=new float[1];
        FrontierTerrainAsset worldTerrain;
        MaterialPropertyBlock terrainProperties;
        Bounds[] closeBounds,wideBounds;
        Mesh[] visibleTerrain;
        int outerSeason=-1,outerQuality=-1;
        int terrainVisibilityVersion=-1;
        bool wideTerrain;
        bool fillingOuter;
        internal int TerrainChunksDrawn {get;private set;}
        internal int TerrainChunkCount {get;private set;}
        internal int FrontierStaticRebuilds {get;private set;}

        static readonly Vector3[][] FrontierRoads={
            new[]{new Vector3(FrontierMap.Boundary,0,0),new Vector3(4,0,0),new Vector3(15,0,2),CampCenter(0),
                new Vector3(44,0,3),new Vector3(65,0,7)},
            new[]{new Vector3(65,0,7),new Vector3(73,0,22),new Vector3(86,0,40),CampCenter(1)},
            new[]{new Vector3(65,0,7),new Vector3(97,0,-1),new Vector3(121,0,-15),new Vector3(151,0,-21),
                new Vector3(174,0,-43),new Vector3(202,0,-47),CampCenter(2)}
        };

        void PrepareOuterWorld()
        {
            if(outerSeason==builtSeason&&outerQuality==QualityLevel)return;
            if(!worldTerrain)
            {
                worldTerrain=Resources.Load<FrontierTerrainAsset>(FrontierTerrainAsset.Resource);
                if(!worldTerrain)throw new System.InvalidOperationException("Run ModelBaker to bake the frontier coast.");
                terrainProperties=new MaterialPropertyBlock();
                terrainProperties.SetFloatArray(HitId,terrainEffect);
                terrainProperties.SetFloatArray(WindId,terrainEffect);
                closeBounds=TerrainBounds(worldTerrain.closeChunks);
                wideBounds=TerrainBounds(worldTerrain.wideChunks);
                visibleTerrain=new Mesh[Mathf.Max(closeBounds.Length,wideBounds.Length)];
            }
            terrainTint[0]=Color.Lerp(White,ManorSoils[builtSeason],builtSeason==3?.72f:.48f);
            terrainProperties.SetVectorArray(TeamId,terrainTint);
            foreach(var page in outerStill)page.Clear();
            fillingOuter=true;
            try
            {
                BuildFrontierLand();
                PlantFrontier();
            }
            finally {fillingOuter=false;}
            outerSeason=builtSeason;
            outerQuality=QualityLevel;
            FrontierStaticRebuilds++;
        }

        static Bounds[] TerrainBounds(Mesh[] meshes)
        {
            var bounds=new Bounds[meshes.Length];
            for(int i=0;i<meshes.Length;i++)bounds[i]=meshes[i].bounds;
            return bounds;
        }

        void DrawOuterWorld()
        {
            bool wide=distance>155;
            if(terrainVisibilityVersion!=visibilityVersion||wideTerrain!=wide)
            {
                var chunks=wide?worldTerrain.wideChunks:worldTerrain.closeChunks;
                var bounds=wide?wideBounds:closeBounds;
                TerrainChunksDrawn=0;
                TerrainChunkCount=chunks.Length;
                for(int i=0;i<chunks.Length;i++)
                    if(GeometryUtility.TestPlanesAABB(frustum,bounds[i]))visibleTerrain[TerrainChunksDrawn++]=chunks[i];
                terrainVisibilityVersion=visibilityVersion;
                wideTerrain=wide;
            }
            for(int i=0;i<TerrainChunksDrawn;i++)
            {
                var chunk=visibleTerrain[i];
                Graphics.DrawMeshInstanced(chunk,0,material,terrainMatrix,1,terrainProperties,
                    ShadowCastingMode.Off,true,0,ViewCamera,LightProbeUsage.Off);
                RenderSubmissions++;
                RenderInstances++;
                if(MeasureCasterVertices)MainVertices+=chunk.vertexCount;
            }
            foreach(var page in outerStill)Draw(page,true);
        }

        static float Scatter(uint number)
        {
            number ^= number >> 16;
            number *= 0x7feb352du;
            number ^= number >> 15;
            number *= 0x846ca68bu;
            return ((number ^ (number >> 16)) & 0xffffff) / 16777216f;
        }

        void BuildFrontierRoads()
        {
            foreach(var road in FrontierRoads)
            {
                var origin=RoadSurface(road[0]);
                for(int i=1;i<road.Length;i++)
                {
                    var start=road[i-1];var end=road[i];
                    var before=road[Mathf.Max(0,i-2)];var after=road[Mathf.Min(road.Length-1,i+1)];
                    var enter=(end-before)*.5f;
                    var leave=(after-start)*.5f;
                    int pieces=Mathf.CeilToInt(Vector3.Distance(start,end)/4.5f);
                    for(int step=1;step<=pieces;step++)
                    {
                        float t=step/(float)pieces;
                        float t2=t*t,t3=t2*t;
                        var next=RoadSurface((2*t3-3*t2+1)*start+(t3-2*t2+t)*enter+
                            (-2*t3+3*t2)*end+(t3-t2)*leave);
                        FrontierTrail(origin,next,2.12f);
                        origin=next;
                    }
                }
            }
        }

        static Vector3 RoadSurface(Vector3 point)
        {
            float height=FrontierMap.SurfaceHeight(point.x,point.z);
            point.y=Catalog.Finite(height)?height:0;
            return point;
        }

        static bool BesideRoad(Vector3 point)
        {
            foreach(var road in FrontierRoads)
                for(int i=1;i<road.Length;i++)
                {
                    var start=road[i-1];
                    var leg=road[i]-start;
                    float along=Mathf.Clamp01(Vector3.Dot(point-start,leg)/leg.sqrMagnitude);
                    if((point-start-leg*along).sqrMagnitude<49)return true;
                }
            for(int i=0;i<Manor.Sectors.Count;i++)
                if((point-CampCenter(i)).sqrMagnitude<15.5f*15.5f)return true;
            return false;
        }

        void PlantFrontier()
        {
            var oak=Load("tree").WithoutShadows();
            var pine=Load("pine").WithoutShadows();
            var grass=Load("grass");
            var shrub=Load("shrub").WithoutShadows();
            int perCell=QualityLevel==0?6:QualityLevel==1?11:15;
            for(int x=0;x<9;x++)
                for(int z=-4;z<4;z++)
                    for(int i=0;i<perCell;i++)
                    {
                        uint seed=(uint)((x*8+z+4)*33+i+11)*0x9e3779b9u;
                        float px=(x+Scatter(seed))*ChunkSize,pz=(z+Scatter(seed+29))*ChunkSize;
                        if(!FrontierMap.ShoreAt(px,out float south,out float north)||pz<south+2||pz>north-2)continue;
                        var point=new Vector3(px,0,pz);
                        if(BesideRoad(point))continue;
                        float variation=Scatter(seed+71);
                        float grove=Mathf.Sin(px*.083f+pz*.038f)+Mathf.Cos(pz*.083f-px*.023f);
                        bool woods=grove+.71f*variation>.3f;
                        var plant=woods?(builtSeason==3||grove>1.15f||variation>.67f?pine:oak):
                            variation>.64f?shrub:grass;
                        float size=woods?1.08f+variation*1.12f:.70f+variation*.81f;
                        point.y=FrontierMap.SurfaceHeight(px,pz);
                        Still(plant,point,Quaternion.Euler(0,Scatter(seed+94)*360,0),Vector3.one*size,
                            builtSeason==3?WinterPine:Color.Lerp(new Color(.73f,.80f,.60f),White,variation),woods?.011f:.02f);
                    }
            ScatterShore(FrontierMap.ManorCoast,2.7f);
            ScatterShore(FrontierMap.FrontierCoast,8.8f);
        }

        void ScatterShore(IReadOnlyList<FrontierMap.ShoreSlice> coast,float spacing)
        {
            var rock=Load("rock").WithoutShadows();
            var wave=Load("water_ripple");
            float first=coast[0].x,last=coast[coast.Count-1].x;
            int count=Mathf.FloorToInt((last-first)/spacing);
            for(int i=0;i<count;i++)
            {
                float x=first+spacing*(i+.42f);
                if(x>FrontierMap.BridgeStart-4&&x<FrontierMap.Boundary+5)continue;
                if(coast==FrontierMap.ManorCoast&&Mathf.Abs(x-ManorOrigin.x-DockOrigin.x)<4.1f)continue;
                FrontierMap.ShoreAt(x,out float south,out float north);
                for(int side=-1;side<=1;side+=2)
                {
                    uint seed=(uint)(i*33+(side+1)*99+count+1);
                    float variation=Scatter(seed+74);
                    float z=side<0?south:north;
                    float offshore=1.1f+9.3f*variation;
                    bool stack=i%9==4&&spacing>5;
                    var center=new Vector3(x,stack?-1.89f:-1.46f,z+side*offshore);
                    float width=stack?6+variation*4:1.25f+2.65f*variation;
                    float height=stack?3.5f+1.45f*variation:1.1f+variation*.55f;
                    Still(rock,center,Quaternion.Euler(0,i*137.5f+side*57,0),new Vector3(width,height,width*(stack?.61f:.85f)),
                        Color.Lerp(new Color(.56f,.63f,.60f),new Color(.77f,.73f,.56f),variation));
                    if(QualityLevel>0&&i%2==0)
                        Still(wave,new Vector3(x+.7f,-1.63f,z+side*(offshore+4.2f)),Quaternion.Euler(0,side*i*29,0),
                            new Vector3(3.3f+variation,1,1.48f),new Color(.52f,.71f,.70f),.07f);
                }
            }
        }

        static bool PickFrontierGround(Ray ray,out Vector3 point)
        {
            point=Vector3.zero;
            if(ray.direction.y>=0||!ManorGround.Raycast(ray,out float bottom))return false;
            point=ray.GetPoint(bottom);
            bool flat=FrontierMap.HasGround(point.x,point.z);
            float ceiling=Mathf.Max(0,(FrontierMap.TerrainCeiling-ray.origin.y)/ray.direction.y);
            var entry=ray.GetPoint(ceiling);
            float west=Mathf.Min(entry.x,point.x),east=Mathf.Max(entry.x,point.x);
            if(east<FrontierMap.Boundary||west>FrontierMap.East)return flat;
            var coast=FrontierMap.FrontierCoast;
            float south=Mathf.Min(entry.z,point.z),north=Mathf.Max(entry.z,point.z);
            float nearest=bottom+.0001f;
            bool hit=false;
            for(int station=1;station<coast.Count;station++)
            {
                if(coast[station].x<west||coast[station-1].x>east)continue;
                for(int band=0;band<FrontierMap.TerrainBands;band++)
                {
                    var a=UnityWorld.Vector(FrontierMap.TerrainPoint(station-1,band));
                    var b=UnityWorld.Vector(FrontierMap.TerrainPoint(station-1,band+1));
                    var c=UnityWorld.Vector(FrontierMap.TerrainPoint(station,band+1));
                    var d=UnityWorld.Vector(FrontierMap.TerrainPoint(station,band));
                    if(Mathf.Min(a.z,d.z)>north||Mathf.Max(b.z,c.z)<south||
                        a.y==0&&b.y==0&&c.y==0&&d.y==0)continue;
                    hit|=HitTerrain(ray,a,b,c,ref nearest);
                    hit|=HitTerrain(ray,a,c,d,ref nearest);
                }
            }
            if(hit)point=ray.GetPoint(nearest);
            return hit||flat;
        }

        static bool HitTerrain(Ray ray,Vector3 a,Vector3 b,Vector3 c,ref float nearest)
        {
            var first=b-a;var second=c-a;
            var p=Vector3.Cross(ray.direction,second);
            float determinant=Vector3.Dot(first,p);
            if(determinant<.000001f)return false;
            var delta=ray.origin-a;
            float u=Vector3.Dot(delta,p)/determinant;
            if(u<0||u>1)return false;
            var q=Vector3.Cross(delta,first);
            float v=Vector3.Dot(ray.direction,q)/determinant;
            if(v<0||u+v>1)return false;
            float distance=Vector3.Dot(second,q)/determinant;
            if(distance<0||distance>=nearest)return false;
            nearest=distance;
            return true;
        }
    }
}
