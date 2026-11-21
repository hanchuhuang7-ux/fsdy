using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        readonly List<Batch> outerStill=new List<Batch>();
        readonly List<Batch> outerVegetation=new List<Batch>();
        readonly Matrix4x4[] terrainMatrix={Matrix4x4.identity};
        readonly Vector4[] terrainTint=new Vector4[1];
        readonly float[] terrainEffect=new float[1];
        FrontierTerrainAsset worldTerrain;
        MaterialPropertyBlock terrainProperties;
        Material lakeMaterial;
        static readonly int LakeColorId=Shader.PropertyToID("_Water"),LakeMistId=Shader.PropertyToID("_Mist");
        Bounds[] closeBounds,wideBounds;
        Mesh[] visibleTerrain;
        int outerSeason=-1,outerQuality=-1;
        int terrainVisibilityVersion=-1;
        bool wideTerrain;
        bool fillingOuter,fillingVegetation;
        internal int TerrainChunksDrawn {get;private set;}
        internal int TerrainChunkCount {get;private set;}
        internal int FrontierStaticRebuilds {get;private set;}

        static readonly Vector3[] FrontierRoadSites={new Vector3(FrontierMap.Boundary,0,0),CampCenter(0),CampCenter(1),CampCenter(2)};
        static readonly Vector3[] FrontierRoadJunctions=RoadJunctions();

        static Vector3[] RoadJunctions()
        {
            var points=new Vector3[FrontierRoadSites.Length];
            for(int i=0;i<points.Length;i++)points[i]=UnityWorld.Vector(FrontierMap.RoadJunction(UnityWorld.Value(FrontierRoadSites[i])));
            return points;
        }

        void PrepareOuterWorld()
        {
            if(outerSeason==builtSeason&&outerQuality==QualityLevel)return;
            if(!worldTerrain)
            {
                worldTerrain=Resources.Load<FrontierTerrainAsset>(FrontierTerrainAsset.Resource);
                if(!worldTerrain)throw new System.InvalidOperationException("Run ModelBaker to bake the frontier coast.");
                lakeMaterial=new Material(Resources.Load<Shader>("FrontlineLake"));
                terrainProperties=new MaterialPropertyBlock();
                terrainProperties.SetFloatArray(HitId,terrainEffect);
                terrainProperties.SetFloatArray(WindId,terrainEffect);
                closeBounds=TerrainBounds(worldTerrain.closeChunks);
                wideBounds=TerrainBounds(worldTerrain.wideChunks);
                visibleTerrain=new Mesh[Mathf.Max(closeBounds.Length,wideBounds.Length)];
            }
            terrainTint[0]=Color.Lerp(White,ManorSoils[builtSeason],builtSeason==3?.72f:.48f);
            terrainProperties.SetVectorArray(TeamId,terrainTint);
            lakeMaterial.SetColor(LakeColorId,builtSeason==3?new Color(.28f,.43f,.49f):new Color(.236f,.454f,.414f));
            lakeMaterial.SetColor(LakeMistId,ManorHorizons[builtSeason]);
            foreach(var page in outerStill)page.Clear();
            foreach(var page in outerVegetation)page.Clear();
            fillingOuter=true;
            try
            {
                BuildFrontierLand();
                fillingVegetation=true;
                PlantFrontier();
            }
            finally {fillingOuter=fillingVegetation=false;}
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
            DrawLakeHorizon();
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
                DrawTerrain(visibleTerrain[i],!wide);
            DrawTerrain(worldTerrain.distantRelief,false);
            foreach(var page in outerStill)Draw(page,true);
            if(!wide)foreach(var page in outerVegetation)Draw(page,true);
        }

        void DrawLakeHorizon()
        {
            // Even at a grazing angle, every point inside the far plane lies over this
            // camera-centred surface. At the clip distance it resolves to the sky's horizon.
            float span=Mathf.Max(4800,ViewCamera.farClipPlane*3.6f);
            var at=new Vector3(cameraPosition.x,-1.70f,cameraPosition.z);
            Graphics.DrawMesh(worldTerrain.lakeSurface,Matrix4x4.TRS(at,Quaternion.identity,new Vector3(span,1,span)),
                lakeMaterial,0,ViewCamera,0,null,ShadowCastingMode.Off,false,null,LightProbeUsage.Off);
            RenderSubmissions++;
            RenderInstances++;
            if(MeasureCasterVertices)MainVertices+=worldTerrain.lakeSurface.vertexCount;
        }

        void DrawTerrain(Mesh chunk,bool receiveShadows)
        {
            Graphics.DrawMeshInstanced(chunk,0,material,terrainMatrix,1,terrainProperties,
                ShadowCastingMode.Off,receiveShadows,0,ViewCamera,LightProbeUsage.Off);
            RenderSubmissions++;
            RenderInstances++;
            if(MeasureCasterVertices)MainVertices+=chunk.vertexCount;
        }

        static float Scatter(uint number)
        {
            number ^= number >> 16;
            number *= 0x7feb352du;
            number ^= number >> 15;
            number *= 0x846ca68bu;
            return ((number ^ (number >> 16)) & 0xffffff) / 16777216f;
        }

        static Vector3 RoadSurface(Vector3 point)
        {
            float height=FrontierMap.SurfaceHeight(point.x,point.z);
            point.y=Catalog.Finite(height)?height:0;
            return point;
        }

        static bool BesideRoad(Vector3 point,float depth)
        {
            if(FrontierMap.BesideRingRoad(point.x,point.z,depth,.039f))return true;
            for(int i=0;i<FrontierRoadSites.Length;i++)
            {
                var start=FrontierRoadSites[i];
                var leg=FrontierRoadJunctions[i]-start;
                float along=Mathf.Clamp01(Vector3.Dot(point-start,leg)/leg.sqrMagnitude);
                if((point-start-leg*along).sqrMagnitude<31||i>0&&(point-start).sqrMagnitude<15.5f*15.5f)return true;
            }
            return false;
        }

        void PlantFrontier()
        {
            var oak=Load("tree").WithoutShadows();
            var pine=Load("pine").WithoutShadows();
            var grass=Load("grass");
            var shrub=Load("shrub").WithoutShadows();
            int perCell=QualityLevel==0?4:QualityLevel==1?7:10;
            for(int x=-10;x<6;x++)
                for(int z=-6;z<6;z++)
                    for(int i=0;i<perCell;i++)
                    {
                        uint seed=(uint)(((x+10)*12+z+6)*33+i+11)*0x9e3779b9u;
                        float px=(x+Scatter(seed))*ChunkSize,pz=(z+Scatter(seed+29))*ChunkSize;
                        float depth=FrontierMap.MainlandDepth(px,pz,out float bankDistance);
                        if(bankDistance<2||!FrontierMap.OnMainland(px,pz))continue;
                        var point=new Vector3(px,0,pz);
                        if(BesideRoad(point,depth))continue;
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
            ScatterIslandShore();
            ScatterRingShore(FrontierMap.LakeBank,-1);
            ScatterRingShore(FrontierMap.OuterBank,1);
        }

        void ScatterIslandShore()
        {
            const float spacing=2.7f;
            int count=Mathf.FloorToInt((FrontierMap.BridgeStart-FrontierMap.IslandWest)/spacing);
            for(int i=0;i<count;i++)
            {
                float x=FrontierMap.IslandWest+spacing*(i+.42f);
                if(x>FrontierMap.BridgeStart-4||Mathf.Abs(x-ManorOrigin.x-DockOrigin.x)<4.1f)continue;
                FrontierMap.ShoreAt(x,out float south,out float north);
                for(int side=-1;side<=1;side+=2)
                    ShoreProp(new Vector3(x,0,side<0?south:north),Vector3.forward*side,i+(side+1)*99,false);
            }
        }

        void ScatterRingShore(IReadOnlyList<V3> bank,int side)
        {
            for(int i=0;i<bank.Count;i++)
            {
                var start=UnityWorld.Vector(bank[i]);
                var end=UnityWorld.Vector(bank[(i+1)%bank.Count]);
                var point=Vector3.Lerp(start,end,.38f);
                if(side<0&&Mathf.Abs(point.z)<8.6f&&Mathf.Abs(point.x-FrontierMap.Boundary)<8)continue;
                var tangent=(end-start).normalized;
                ShoreProp(point,new Vector3(tangent.z,0,-tangent.x)*side,i+(side+1)*101,i%15==4);
            }
        }

        void ShoreProp(Vector3 shore,Vector3 outward,int index,bool stack)
        {
            var rock=Load("rock").WithoutShadows();
            float variation=Scatter((uint)(index*33+74));
            float offshore=1.35f+5.25f*variation;
            var center=shore+outward*offshore+Vector3.up*(stack?-1.89f:-1.46f);
            float width=stack?5.2f+variation*3.2f:1.13f+2.35f*variation;
            float height=stack?3.15f+1.45f*variation:1.08f+variation*.55f;
            Still(rock,center,Quaternion.Euler(0,index*137.5f,0),new Vector3(width,height,width*(stack?.64f:.85f)),
                Color.Lerp(new Color(.56f,.63f,.60f),new Color(.77f,.73f,.56f),variation));
            if(QualityLevel>0&&index%3==0)
                Still(Load("water_ripple"),shore+outward*(offshore+3.4f)+Vector3.down*1.60f,Quaternion.Euler(0,index*29,0),
                    new Vector3(3.3f+variation,1,1.48f),new Color(.52f,.71f,.70f),.07f);
        }

        static bool PickFrontierGround(Ray ray,out Vector3 point)
        {
            point=Vector3.zero;
            if(ray.direction.y>=0||!ManorGround.Raycast(ray,out float bottom))return false;
            point=ray.GetPoint(bottom);
            bool flat=FrontierMap.Safe(point.x,point.z);
            float ceiling=Mathf.Max(0,(FrontierMap.TerrainCeiling-ray.origin.y)/ray.direction.y);
            var entry=ray.GetPoint(ceiling);
            float west=Mathf.Min(entry.x,point.x),east=Mathf.Max(entry.x,point.x);
            if(east<FrontierMap.West||west>FrontierMap.East)return flat;
            float south=Mathf.Min(entry.z,point.z),north=Mathf.Max(entry.z,point.z);
            float nearest=bottom+.0001f;
            bool hit=false;
            for(int station=0;station<FrontierMap.ShoreStations;station++)
            {
                int next=(station+1)%FrontierMap.ShoreStations;
                var inner=FrontierMap.LakeBank[station];var innerNext=FrontierMap.LakeBank[next];
                var outer=FrontierMap.OuterBank[station];var outerNext=FrontierMap.OuterBank[next];
                if(Mathf.Max(Mathf.Max(inner.x,innerNext.x),Mathf.Max(outer.x,outerNext.x))<west||
                    Mathf.Min(Mathf.Min(inner.x,innerNext.x),Mathf.Min(outer.x,outerNext.x))>east||
                    Mathf.Max(Mathf.Max(inner.z,innerNext.z),Mathf.Max(outer.z,outerNext.z))<south||
                    Mathf.Min(Mathf.Min(inner.z,innerNext.z),Mathf.Min(outer.z,outerNext.z))>north)continue;
                for(int band=0;band<FrontierMap.TerrainBands;band++)
                {
                    var a=UnityWorld.Vector(FrontierMap.TerrainPoint(station,band));
                    var b=UnityWorld.Vector(FrontierMap.TerrainPoint(next,band));
                    var c=UnityWorld.Vector(FrontierMap.TerrainPoint(next,band+1));
                    var d=UnityWorld.Vector(FrontierMap.TerrainPoint(station,band+1));
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
