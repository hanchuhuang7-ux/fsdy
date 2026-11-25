using Frontline.Core;
using UnityEngine;
using CropAppearance = Frontline.UnityGame.ManorPlotArt.CropAppearance;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        static readonly Plane ManorGround = new Plane(Vector3.up, Vector3.zero);
        static readonly Color[] ManorSoils = {
            new Color(.49f,.65f,.36f), new Color(.57f,.65f,.34f),
            new Color(.69f,.53f,.32f), new Color(.76f,.83f,.83f)
        };
        static readonly Color[] ManorHorizons = {
            new Color(.74f,.83f,.77f), new Color(.86f,.87f,.71f),
            new Color(.85f,.73f,.57f), new Color(.74f,.82f,.90f)
        };
        static readonly Color[] ManorSkies = {
            new Color(.26f,.46f,.56f), new Color(.32f,.49f,.58f),
            new Color(.44f,.40f,.46f), new Color(.30f,.44f,.64f)
        };
        static readonly Color Track = new Color(.76f, .65f, .44f);
        static readonly Color WinterTrack = new Color(.72f,.71f,.64f);
        static readonly Color WinterPlot = new Color(.76f,.77f,.75f);
        static readonly Color WinterPine = new Color(.91f,.96f,1f);
        static readonly Quaternion SmokeBillboard = Quaternion.AngleAxis(90,Vector3.right);
        static readonly Color Timber = new Color(.46f, .30f, .19f);
        static readonly Color Survey = new Color(.96f, .86f, .63f);
        static readonly Color[] Clothes = {
            new Color(.35f,.48f,.61f),new Color(.64f,.32f,.27f),
            new Color(.39f,.51f,.35f),new Color(.81f,.71f,.54f),
            new Color(.56f,.39f,.44f),new Color(.46f,.56f,.51f),
            new Color(.58f,.43f,.31f),new Color(.40f,.43f,.55f)
        };

        Manor manor;
        int manorSelection = -1, manorPace = 1;
        internal int HoveredPlot {get;set;}=-1;
        bool manorDirty,winterHeating;
        float manorTime, villageTime, manorScreenShift;
        readonly string[] builtKinds = new string[Manor.PlotCount], builtBackyards=new string[Manor.PlotCount];
        readonly int[] builtLevels = new int[Manor.PlotCount], homes = new int[Manor.PlotCount];
        readonly CropAppearance[] builtCrops = new CropAppearance[Manor.PlotCount];
        internal CropAppearance DisplayedCrop(int index) => builtCrops[index];
        internal Vector3 ResidentPoint {get;private set;}
        internal float ResidentHeading {get;private set;}
        internal bool WalkingResidentVisible {get;private set;}
        internal bool ResidentsPassing {get;private set;}
        internal Vector3 PassingPoint {get;private set;}
        Vector3 nearestPedestrian;
        bool yieldingToPedestrian;
        internal void PreviewVillageTime(float clock){villageTime=clock;ambientDirty=true;}
        int builtReclaimedPlots,builtNobility;
        int homeCount,tavernSlot,builtSeason=-1;
        MeshSet countryLane,shortLane,pavedLane,grassTrace,surveyRopes,earthBlock,surveyBlock, sails;
        MeshSet crate,basket;
        readonly Vector3[] millHubs=new Vector3[Manor.PlotCount];
        readonly int[] millPlots=new int[Manor.PlotCount],forgePlots=new int[Manor.PlotCount];
        readonly Vector3[] occupiedWalkSpace=new Vector3[OrdinaryResidentLimit+Manor.SpecialistIds.Length+Manor.HeroIds.Length+6];
        readonly Vector3[] heroBays=new Vector3[Manor.HeroIds.Length];
        int forgeCount,occupiedWalkCount;
        MeshSet forgeFlame;
        readonly Quaternion[] millFacing=new Quaternion[Manor.PlotCount];
        readonly int[] destinations=new int[Manor.PlotCount+1];
        readonly ResidentVisit[] residents=new ResidentVisit[OrdinaryResidentLimit];
        int millCount,destinationCount,idleWorkers,residentCount;
        bool residentRosterDirty;
        const int OrdinaryResidentLimit=28, IdleFirst=Manor.PlotCount*5;
        const int HeroFirst=IdleFirst+OrdinaryResidentLimit;
        static readonly int SpecialistFirst=HeroFirst+Manor.HeroIds.Length;
        public int VisibleFarmers {get;private set;}
        public int VisibleVillagers {get;private set;}
        internal int VisibleHeroMask {get;private set;}
        internal Vector3 ManorHeroPoint(int index) => ManorOrigin+heroBays[index];
        internal float ManorHeroYaw(int index) => localHeading[HeroFirst+index];
        WalkRoute[] walks;
        readonly System.Collections.Generic.List<Batch> ambient=new System.Collections.Generic.List<Batch>();
        readonly float[] localHeading=new float[SpecialistFirst+Manor.SpecialistIds.Length];
        readonly bool[] headingSet=new bool[SpecialistFirst+Manor.SpecialistIds.Length];
        MeshSet solidBlock,surveyPeg,contactShade,raidPennant,hoverRing;
        bool fillingAmbient,ambientDirty=true,ambientPopulated;
        float fadeReach=-1,fadeStartSquared;
        static readonly int GroundFadeId=Shader.PropertyToID("_GroundFade");
        static readonly Color HoverTint=new Color(.49f,.92f,.94f,.78f);
        float nextAmbient;
        int ambientCameraVersion;
        static readonly Color GroundShade=new Color(.09f,.105f,.075f,.24f);

        struct CameraBookmark
        {
            public int width, height, mode;
            public Vector3 focus;
            public float yaw, pitch, distance, shift;
        }
        CameraBookmark arenaCamera, villageCamera;

        CameraBookmark Bookmark()
        {
            return new CameraBookmark {width = Screen.width, height = Screen.height, mode = CameraMode,
                focus = target, yaw = yaw, pitch = pitch, distance = distance, shift = manorScreenShift};
        }

        bool Restore(CameraBookmark saved)
        {
            if (saved.width != Screen.width || saved.height != Screen.height || saved.distance <= 0) return false;
            CameraMode = saved.mode;
            target = saved.focus;
            yaw = saved.yaw;
            pitch = saved.pitch;
            distance = saved.distance;
            manorScreenShift = saved.shift;
            return true;
        }

        public void SetManor(Manor state, int selected, int pace = 1,int guardsAway = 0)
        {
            bool changingMode = (manor == null) != (state == null);
            bool sameEstate=state!=null&&ReferenceEquals(state,manor);
            if (changingMode)
            {
                cameraBlending=false;
                zoomTarget=0;
                ClearAmbient();
                System.Array.Clear(headingSet,0,headingSet.Length);
                System.Array.Clear(watchProgress,0,watchProgress.Length);
                watchTick=-1;
                if (manor == null) arenaCamera = Bookmark();
                else villageCamera = Bookmark();
                cachedMap = null;
                cachedQuality = -1;
                Marker = null;
                HoveredPlot=-1;
            }
            manor = state;
            manorGuardsAway = guardsAway;
            SetSeasonLight(manor?.Season??-1,manor?.day??0,sameEstate);
            manorSelection = selected;
            manorPace = Mathf.Clamp(pace, 0, 2);
            if (changingMode)
            {
                ViewCamera.fieldOfView=manor!=null?FrontierFov:Viewport.Compact?65:42;
                ViewCamera.farClipPlane=manor!=null?2600:160;
                if (!Restore(manor == null ? arenaCamera : villageCamera))
                {
                    if(manor==null)ResetCamera();else FrameFrontier(1);
                }
                UpdateShadowDistance();
            }
            if (manor == null) return;
            ambientDirty=residentRosterDirty=true;
            idleWorkers=manor.FreeWorkers;
            for(int i=0;i<specialistLevels.Length;i++)specialistLevels[i]=manor.SpecialistLevel(Manor.SpecialistIds[i]);
            winterHeating=manor.Season==3 && manor.Forecast().winterTimber>0;
            manorDirty |= changingMode || builtSeason != manor.Season || builtReclaimedPlots != manor.reclaimedPlots ||
                builtNobility!=manor.nobility;
            for (int i = 0; i < manor.slots.Length && !manorDirty; i++)
                manorDirty = manor.slots[i].kind != builtKinds[i] || manor.slots[i].level != builtLevels[i] ||
                    manor.slots[i].backyard != builtBackyards[i] || FieldAppearance(i) != builtCrops[i];
        }

        void UpdateShadowDistance()
        {
            float reach=QualityLevel==0?22:QualityLevel==1?42:82;
#if !UNITY_WEBGL
            if(manor==null&&QualityLevel==2)
            {
                // Cover the far edge and restore the pre-zoom reach for very wide views.
                reach=Mathf.Clamp(distance+24,34,72)+Mathf.SmoothStep(0,10,Mathf.InverseLerp(48,60,distance));
            }
#endif
            if(manor!=null)
            {
                bool fineDesktop=QualityLevel==2&&!Viewport.Compact;
                float nearCap=QualityLevel==0?42:QualityLevel==1?70:fineDesktop?144:108;
                float farCap=QualityLevel==0?32:QualityLevel==1?54:fineDesktop?100:86;
                float wide=Mathf.InverseLerp(fineDesktop?116:80,fineDesktop?162:132,distance);
                float cap=Mathf.SmoothStep(nearCap,farCap,wide);
                reach=Mathf.Clamp(distance+14,24,cap);
            }
            if(!Mathf.Approximately(QualitySettings.shadowDistance,reach))QualitySettings.shadowDistance=reach;
        }

        void ClearAmbient()
        {
            if(!ambientPopulated)return;
            foreach(var page in ambient)page.Clear();
            ambientPopulated=false;
        }
        internal int CachedAmbientInstances
        {
            get
            {
                int count=0;
                foreach(var batch in ambient)for(var page=batch;page!=null;page=page.next)count+=page.count;
                return count;
            }
        }

        void FrameManor() => FrameFrontier(FrontierFocus);

        void FitPoint(Vector3 point, Quaternion toView, float tanX, float tanY)
        {
            var p = toView * (point - target);
            distance = Mathf.Max(distance, Mathf.Max(Mathf.Abs(p.x) / tanX, Mathf.Abs(p.y) / tanY) - p.z);
        }

        static Vector3 Plot(int index) => new Vector3(Manor.SlotX(index), 0, Manor.SlotZ(index));
        const float LaneZ=4.95f,CrossX=5.48f,WestLaneX=-20.65f*Manor.TownScaleX,OuterLaneZ=12.35f*Manor.TownScaleZ;
        const float BridgeLocalX=Manor.PlateauHalfWidth;
        const int WellSite=Manor.PlotCount+1,MarketSite=WellSite+1,RouteCount=MarketSite+1;
        const int Gate=Manor.PlotCount, WestOuterStart=Manor.FirstNoblePlot-2;
        const float OuterLaneEast=24.65f*Manor.TownScaleX;

        static bool WestPlot(int plot) => plot>=Manor.OriginalPlotCount&&plot<Manor.FirstOuterPlot||
            plot>=WestOuterStart&&plot<Manor.FirstNoblePlot||plot>=Manor.LegacyNoblePlotCount&&plot<Manor.LegacyNoblePlotCount+2;
        static bool OuterRow(int plot) => plot>=Manor.FirstOuterPlot&&plot<WestOuterStart||
            plot>=Manor.FirstNoblePlot+2&&plot<Manor.LegacyNoblePlotCount||plot>=Manor.LegacyNoblePlotCount+2&&plot<Gate;
        const float OuterConnectorX=-5.36f*Manor.TownScaleX;

        static Vector3 LaneMouth(int plot)
        {
            if(plot==MarketSite||plot==WellSite)return new Vector3(plot==WellSite?-CrossX:CrossX,0,-LaneZ);
            if (plot == Gate) return new Vector3(BridgeLocalX-6.65f,0,-LaneZ);
            var p=Plot(plot);
            if(WestPlot(plot))return new Vector3(WestLaneX,0,p.z);
            p.z=Mathf.Sign(p.z)* (OuterRow(plot)?OuterLaneZ:LaneZ);
            if(plot==0||Mathf.Abs(Manor.SlotZ(plot))<2)p.z=-LaneZ;
            return p;
        }

        static Vector3 Door(int plot)
        {
            if(plot==MarketSite)return new Vector3(CrossX,0,MarketSouth.z);
            if(plot==WellSite)return new Vector3(-CrossX,0,WellOrigin.z);
            if (plot == Gate) return new Vector3(BridgeLocalX-.38f,0,0);
            var p=Plot(plot);
            var mouth=LaneMouth(plot);
            if(WestPlot(plot))return p+Vector3.right*(Mathf.Sign(mouth.x-p.x)*2.10f);
            p.z += Mathf.Sign(mouth.z-p.z)*(plot==0?4.22f:2.10f);
            return p;
        }

        static float FacadeYaw(int index) => WestPlot(index)?(index>=WestOuterStart?-90:90):Door(index).z>Plot(index).z?180:0;

        void Still(MeshSet mesh, Vector3 p, Quaternion rotation, Vector3 size, Color tint, float wind = 0)
        {
            var page=fillingOuter?(mesh.outer??Buffer(mesh,fillingVegetation?outerVegetation:outerStill,3)):
                (mesh.still??Buffer(mesh,still,1));
            page.Add(Pose(p, rotation, size),tint,wind,
                chunk:manor!=null?StaticRegion(p):-1);
        }
        void ManorStill(MeshSet mesh,Vector3 p,Quaternion rotation,Vector3 size,Color tint,float wind=0) =>
            Still(mesh,p+ManorOrigin,rotation,size,tint,wind);

        void Tile(Vector3 p, Vector3 size, Color tint, float angle = 0)
        {
            ManorStill(size.x>1?earthBlock:surveyBlock, p, Quaternion.Euler(0, angle, 0), size, tint);
        }

        void Lane(Vector3 from,Vector3 to,float width)
        {
            var span=to-from;
            float length=span.magnitude;
            if(length<.032f)return;
            var turn=Quaternion.LookRotation(span);
            int pieces=Mathf.Max(1,Mathf.CeilToInt(length/8.6f));
            float step=length/pieces;
            for(int i=0;i<pieces;i++)
            {
                var mid=Vector3.Lerp(from,to,(i+.5f)/pieces);
                bool paved=Mathf.Abs(mid.x)<9.5f&&Mathf.Abs(mid.z)<6.1f;
                var facing=((i+Mathf.RoundToInt(from.z))&1)==0?turn:turn*Quaternion.Euler(0,180,0);
                var dye=builtSeason==3?WinterTrack:paved?new Color(.48f,.49f,.46f):new Color(.557f,.452f,.329f);
                ManorStill(paved?pavedLane:step<3.4f?shortLane:countryLane,mid+Vector3.up*.023f,facing,
                    new Vector3(width*(paved?2.07f:1.91f),1,step+Mathf.Min(.29f,step*.13f)),dye);
            }
        }

        void MarkUnbuilt(Vector3 p,Quaternion facing)
        {
            ManorStill(grassTrace,p+Vector3.up*.017f,facing,new Vector3(3.28f,1,2.41f),
                builtSeason==3?WinterPlot:new Color(.45f,.44f,.30f));
            ManorStill(surveyRopes,p,facing,Vector3.one,White);
        }

        void MeadowBorder()
        {
            var tree = Load(manor.Season==3?"pine":"tree");
            var rock = Load("rock").WithoutShadows();
            var grass = Load("grass");
            var flowers = Load("flowers");
            int sprigs=QualityLevel==0?62:QualityLevel==1?119:182;
            for(int i=0;i<sprigs;i++)
            {
                float along=Mathf.Repeat(.62f+i*.618034f,1),inset=Mathf.Repeat(.3f+i*.414214f,1);
                float x=(along*2-1)*(Manor.PlateauHalfWidth-3.0f);
                FrontierMap.ShoreAt(ManorOrigin.x+x,out float south,out float north);
                bool sunny=(i&1)!=0;
                float z=sunny?north-1.1f-2.8f*inset:south+1.1f+2.8f*inset;
                if(!sunny&&Mathf.Abs(x-DockOrigin.x)<3.8f)continue;
                if(x>BridgeLocalX-8&&Mathf.Abs(z)<7.5f)continue;
                var point=new Vector3(x,0,z);
                var rotation=Quaternion.Euler(0,i*137.5f,0);
                bool blocked=false;
                for(int j=0;j<Manor.PlotCount&&!blocked;j++)blocked=PlotObstacle(j).Contains(point,.9f);
                if(blocked)continue;
                float size=.55f+.70f*inset;
                if(i%9==0)
                {
                    ManorStill(tree,point,rotation,Vector3.one*size,builtSeason==3?WinterPine:White,.011f);
                    StillShade(ManorOrigin+point,2.4f*size,1.9f*size);
                }
                else if(i%7==0)ManorStill(rock,point-Vector3.up*.25f,rotation,new Vector3(size,.51f,.83f),White);
                else if(builtSeason!=3||i%4==0)
                    ManorStill(i%5==0?flowers:grass,point,rotation,Vector3.one*size,White,.026f);
            }
        }

        CropAppearance FieldAppearance(int i)
        {
            var plot=manor.slots[i];
            if(!Manor.IsCrop(plot.kind))return CropAppearance.Regular;
            if(manor.IsRestingField(i))return CropAppearance.Fallow;
            return plot.fertility>=Manor.RichFertility?CropAppearance.Rich:CropAppearance.Regular;
        }

        void DrawPlotGeometry(int i)
        {
            var p = Plot(i);
            var plot = manor.slots[i];
            builtKinds[i] = plot.kind;
            builtBackyards[i]=plot.backyard;
            builtLevels[i] = plot.level;
            var crop=builtCrops[i]=FieldAppearance(i);
            float yaw = FacadeYaw(i);
            var facing = Quaternion.Euler(0, yaw, 0);
            if (!manor.IsPlotCleared(i))
            {
                if(manor.IsPlotGranted(i))DrawUnclearedPlot(i,p,facing);
                else DrawNobleBoundary(i,p,facing);
                AddPlotObstacle(i);
                return;
            }
            if (string.IsNullOrEmpty(plot.kind))
            {
                MarkUnbuilt(p,facing);
                return;
            }

            var scale=ManorPlotArt.BuildingScale(plot.kind);
            var foundation=i==0?new Vector3(9.75f,.04f,8.18f):new Vector3(3.85f*scale.x,.04f,2.97f*scale.z);
            ManorStill(i==0?pavedLane:grassTrace,p+Vector3.up*.018f,facing,new Vector3(foundation.x,1,foundation.z),
                builtSeason==3?WinterPlot:i==0?StoneCap:new Color(.67f,.64f,.46f));
            var basePoint=p+Vector3.up*.042f;
            string upgrade=plot.level==3?"_tower":plot.level==2?"_wing":"";
            if(i==0)
            {
                ManorStill(Load("manor_hall"+upgrade),basePoint,facing,Vector3.one,White);
                if(plot.level==3)ManorStill(props["banner"].WithoutShadows(),basePoint+new Vector3(-.5f,8.05f,.30f),
                    Quaternion.Euler(0,-24,0),new Vector3(1.25f,.62f,1.25f),new Color(.78f,.22f,.16f),.023f);
            }
            else
            {
                ManorStill(Load(ManorPlotArt.CropModel(plot.kind,crop)+upgrade),basePoint,facing,scale,White);
            }
            StillShade(ManorOrigin+p,WestPlot(i)?foundation.z:foundation.x,WestPlot(i)?foundation.x:foundation.z);
            AddPlotObstacle(i);
            DecoratePlot(i,plot.kind,basePoint,facing);
            if(plot.kind=="farm"||plot.kind==Manor.Bakery)
            {
                var hub=plot.kind==Manor.Bakery?ManorPlotArt.BakeryHub:ManorPlotArt.MillHub;
                millHubs[millCount]=basePoint+ManorOrigin+facing*Vector3.Scale(hub,scale);
                millPlots[millCount]=i;
                millFacing[millCount++]=facing;
            }
            if(plot.kind=="smithy")forgePlots[forgeCount++]=i;
            if(!IsFarm(plot.kind))destinations[destinationCount++]=i;
            // Rank remains available in the inspector; the route marker is an authored wooden sign.
            var sign=p+facing*(i==0?new Vector3(2.39f,0,-4.21f):new Vector3(1.61f,0,-1.69f));
            ManorStill(Load("plot_marker").WithoutShadows(),sign,facing,Vector3.one*(.78f+plot.level*.075f),White);
            if (plot.kind == "cottage" || plot.kind == "pasture") homes[homeCount++] = i;
            if (tavernSlot == 0 && plot.kind == "tavern") tavernSlot = i;
        }

        void RebuildManor()
        {
            foreach (var b in still) b.Clear();
            foreach (var b in grid) b.Clear();
            surveyBlock = Load("platform");
            earthBlock = Load("arena_ground");
            countryLane=Load("road_path");shortLane=Load("road_link");pavedLane=Load("road_setts");
            grassTrace=Load("road_grass");surveyRopes=Load("plot_ropes").WithoutShadows();
            if(solidBlock==null)
            {
                solidBlock=surveyBlock.Variant(true,true);
                surveyPeg=surveyBlock.Variant(false,false,true);
                contactShade=dust.Variant(false,false);
                raidPennant=props["banner"].Variant(true,true);
                hoverRing=Load("ring",true);
            }
            ambientDirty=true;
            sails=Load("mill_sails");forgeFlame=Load("flame",true);
            crate=Load("carry_crate");basket=Load("carry_basket");
            builtSeason = manor.Season;
            builtReclaimedPlots=manor.reclaimedPlots;
            builtNobility=manor.nobility;
            material.SetFloat("_LakeEnvironment",1);
            roadMaterial.SetVector("_Court",new Vector4(ManorOrigin.x,0,1,0));
            PrepareOuterWorld();
            MeadowBorder();
            PlantOrchards();
            Lane(new Vector3(WestLaneX,0,-LaneZ),LaneMouth(Gate),.78f);
            Lane(new Vector3(WestLaneX,0,LaneZ),new Vector3(LaneMouth(Gate).x,0,LaneZ),.68f);
            Lane(LaneMouth(Gate),Door(Gate),1.08f);
            Lane(new Vector3(LaneMouth(Gate).x,0,LaneZ),Door(Gate),.86f);
            Lane(new Vector3(WestLaneX,0,-OuterLaneZ),new Vector3(WestLaneX,0,OuterLaneZ),.78f);
            for(int side=-1;side<=1;side+=2)
            {
                float z=side*OuterLaneZ, x=OuterConnectorX;
                Lane(new Vector3(WestLaneX,0,z),new Vector3(OuterLaneEast,0,z),.69f);
                Lane(new Vector3(x,0,z),new Vector3(x,0,side*LaneZ),.65f);
            }
            var landingJunction=new Vector3(DockOrigin.x,0,-OuterLaneZ);
            Lane(landingJunction,DockApproach,.85f);
            for (int side = -1; side <= 1; side += 2)
                Lane(new Vector3(side*CrossX,0,-LaneZ),new Vector3(side*CrossX,0,LaneZ),.57f);
            homeCount = tavernSlot = millCount = forgeCount = 0;
            estateObstacleCount=0;
            BuildEstateDetails();
            BuildCivicDetails();
            destinationCount=1;destinations[0]=Gate;
            for (int i = 0; i < manor.slots.Length; i++)
            {
                Lane(Door(i), LaneMouth(i), i == 0 ? .93f : .50f);
                DrawPlotGeometry(i);
                if(i!=0&&manor.slots[i].kind!=null)
                {
                    var turn=Quaternion.Euler(0,FacadeYaw(i),0);
                    Lane(Door(i),Plot(i)+turn*new Vector3(-.09f,0,-1.08f),.45f);
                }
            }
            if (walks == null) BuildRoutes();
            PlantVillageVerges();
            residentRosterDirty=true;
            variedTerrain=false;
            AlertCampMask=-1;
            for(int i=0;i<2;i++)
                ManorStill(raidPennant,new Vector3(BridgeLocalX-3.45f,0,-4.1f-i*1.45f),Quaternion.Euler(0,-21,0),
                    Vector3.one*1.12f,new Color(.98f,.12f,.08f),.048f);
            sky.SetColor("_Horizon",ManorHorizons[builtSeason]);
            sky.SetColor("_Zenith",ManorSkies[builtSeason]);
            sky.SetColor("_Nadir",ManorHorizons[builtSeason]);
            ViewCamera.backgroundColor=ManorHorizons[builtSeason];
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = ManorHorizons[builtSeason];
            RenderSettings.fogDensity = .00137f;
            material.SetColor("_SkyLight",Color.Lerp(ManorHorizons[builtSeason],new Color(.52f,.54f,.55f),.45f));
            material.SetColor("_GroundLight",Color.Lerp(ManorSoils[builtSeason],new Color(.16f,.18f,.16f),.70f));
            cachedQuality = QualityLevel;
            manorDirty = false;
            StaticRebuilds++;
        }

        sealed class WalkRoute
        {
            readonly struct Leg
            {
                public readonly Vector3 origin, direction;
                public readonly float length;
                public Leg(Vector3 origin,Vector3 direction,float length)
                {
                    this.origin=origin;this.direction=direction;this.length=length;
                }
            }

            readonly Leg[] legs=new Leg[9];
            int count;
            public float total;
            public void Segment(Vector3 from, Vector3 to)
            {
                Vector3 span = to - from;
                float len = span.magnitude;
                if (len < .001f) return;
                legs[count++]=new Leg(from,span/len,len);
                total += len;
            }
            public void Sample(float travel, out Vector3 p, out Vector3 dir)
            {
                int i = 0;
                while (i < count - 1 && travel > legs[i].length) travel -= legs[i++].length;
                var leg=legs[i];
                dir=leg.direction;
                p=leg.origin+dir*Mathf.Min(leg.length,travel);
            }

            public bool NearVerge(Vector3 point,float clearance)
            {
                for(int i=0;i<count;i++)
                {
                    var leg=legs[i];
                    float t=Mathf.Clamp(Vector3.Dot(point-leg.origin,leg.direction),0,leg.length);
                    if((point-leg.origin-leg.direction*t).sqrMagnitude<clearance*clearance)return true;
                }
                return false;
            }

            public float MarketStop()
            {
                float distance=0,best=2.3f*2.3f,stop=-1;
                for(int i=0;i<count;i++)
                {
                    var leg=legs[i];
                    float offset=Mathf.Clamp(Vector3.Dot(MarketSouth-leg.origin,leg.direction),0,leg.length);
                    float along=distance+offset;
                    float square=(leg.origin+leg.direction*offset-MarketSouth).sqrMagnitude;
                    if(square<best&&along>1&&along<total-1)
                    {
                        best=square;
                        stop=along;
                    }
                    distance+=leg.length;
                }
                return stop;
            }
        }

        void BuildRoutes()
        {
            // Each route follows the actual lanes and stops at doorsteps.
            walks = new WalkRoute[RouteCount*RouteCount];
            for (int home = 0; home < RouteCount; home++)
                for (int dest = 0; dest < RouteCount; dest++)
                {
                    if (home == dest) continue;
                    walks[home * RouteCount + dest] = WalkBetween(home,dest);
                }
            BuildSpecialistRoutes();
            for(int i=0;i<watchRoutes.Length;i++)watchRoutes[i]=WatchRoute(i);
        }

        static Vector3 MainLaneJoin(int plot,Vector3 mouth)
        {
            if(WestPlot(plot))return new Vector3(mouth.x,0,Mathf.Sign(mouth.z)*LaneZ);
            if(OuterRow(plot))return new Vector3(OuterConnectorX,0,Mathf.Sign(mouth.z)*LaneZ);
            return mouth;
        }

        static WalkRoute WalkBetween(int home,int dest)
        {
            var path=new WalkRoute();
            Vector3 a=LaneMouth(home), b=LaneMouth(dest);
            path.Segment(Door(home),a);
            if(WestPlot(home)&&WestPlot(dest)||OuterRow(home)&&OuterRow(dest)&&a.z==b.z)path.Segment(a,b);
            else
            {
                Vector3 start=MainLaneJoin(home,a),end=MainLaneJoin(dest,b);
                Vector3 firstTurn=new Vector3(start.x,0,a.z),lastTurn=new Vector3(end.x,0,b.z);
                path.Segment(a,firstTurn);
                path.Segment(firstTurn,start);
                if(start.z!=end.z)
                {
                    float cross=start.x+end.x<0?-CrossX:CrossX;
                    var near=new Vector3(cross,0,start.z);
                    var far=new Vector3(cross,0,end.z);
                    path.Segment(start,near);path.Segment(near,far);
                    start=far;
                }
                path.Segment(start,end);path.Segment(end,lastTurn);path.Segment(lastTurn,b);
            }
            path.Segment(b,Door(dest));
            return path;
        }

        static bool IsFarm(string kind) => kind=="farm"||kind=="irrigated_field"||kind=="pasture";

        void UpdateGroundFade()
        {
            float reach=QualitySettings.shadowDistance;
            if(Mathf.Approximately(reach,fadeReach))return;
            fadeReach=reach;
            float start=reach-Mathf.Clamp(.17f*reach,4,23);
            float end=reach+Mathf.Clamp(.045f*reach,1.5f,6);
            fadeStartSquared=start*start;
            groundingMaterial.SetVector(GroundFadeId,new Vector4(start,1/(end-start),0,0));
        }
        void StillShade(Vector3 point,float width,float depth)
        {
            Still(contactShade,new Vector3(point.x+.18f,.052f,point.z+.12f),Quaternion.identity,
                new Vector3(width,1,depth),GroundShade,1);
        }
        void ContactAt(Vector3 point,float width)
        {
            point.y=.057f;
            bool distant=(point-cameraPosition).sqrMagnitude>fadeStartSquared;
            if(!distant)
            {
                Add(dust,Pose(point,Quaternion.identity,new Vector3(width*.76f,1,width*.53f)),new Color(.13f,.105f,.067f,.215f),1);
                return;
            }
            Add(contactShade,Pose(point,Quaternion.identity,new Vector3(width,1,width*.76f)),GroundShade,1);
        }
        float LocalHeading(int index,float desired)
        {
            float dt=fillingAmbient?.08f:Mathf.Min(.12f,Time.unscaledDeltaTime);
            localHeading[index]=headingSet[index]?Mathf.MoveTowardsAngle(localHeading[index],desired,dt*195):desired;
            headingSet[index]=true;
            return localHeading[index];
        }

        readonly struct ResidentVisit
        {
            public readonly WalkRoute outbound,errand,homeward;
            public readonly int id,work,social,lane;
            public readonly bool idle;
            public ResidentVisit(WalkRoute first,WalkRoute second,WalkRoute third,int id,int work,int social,int lane,bool idle)
            {outbound=first;errand=second;homeward=third;this.id=id;this.work=work;this.social=social;this.lane=lane;this.idle=idle;}

            public void Sample(float clock,out WalkRoute route,out int site,out float along,out int stage)
            {
                float speed=.85f+(id%4)*.09f;
                float a=outbound.total/speed,b=errand.total/speed,c=homeward.total/speed;
                float workTime=idle?5.5f:8.4f,socialTime=4.2f+id%3;
                float t=Mathf.Repeat(clock,Lap);
                if(t<a+workTime){route=outbound;site=work;along=Mathf.Min(outbound.total,t*speed);stage=t<a?0:1;return;}
                t-=a+workTime;
                if(t<b+socialTime){route=errand;site=social;along=Mathf.Min(errand.total,t*speed);stage=t<b?2:3;return;}
                t-=b+socialTime;route=homeward;site=-1;along=Mathf.Min(homeward.total,t*speed);stage=t<c?4:5;
            }
            public float Offset=>outbound.total/(.85f+id%4*.09f)+(lane*.21f+id*.137f)*Lap;
            public float Lap=>(outbound.total+errand.total+homeward.total)/(.85f+(id%4)*.09f)+(idle?5.5f:8.4f)+4.2f+id%3+2.4f;
        }

        internal static object CheckVillageSchedule()
        {
            var a=WalkBetween(1,2);var b=WalkBetween(2,MarketSite);var c=WalkBetween(MarketSite,1);
            var visit=new ResidentVisit(a,b,c,5,2,MarketSite,0,false);
            int mask=0;float largest=0;var previous=Door(1);
            // Warm JIT outside the measured loop, including the route sampler.
            visit.Sample(0,out var path,out _,out var at,out _);path.Sample(at,out _,out _);
            long before=System.GC.GetAllocatedBytesForCurrentThread();
            for(float t=0;t<visit.Lap+.2f;t+=.0625f)
            {
                visit.Sample(t,out path,out _,out at,out int stage);path.Sample(at,out var position,out _);
                largest=Mathf.Max(largest,Vector3.Distance(position,previous));previous=position;mask|=1<<stage;
            }
            long bytes=System.GC.GetAllocatedBytesForCurrentThread()-before;
            if(mask!=63||largest>.15f||bytes!=0)throw new System.InvalidOperationException("Resident path, shift or allocation regression: "+mask+"/"+largest+"/"+bytes);
            return new {stages=mask,loopSeconds=visit.Lap,maximumStepMetres=largest,allocatedBytes=bytes};
        }

        void AddResident(int id,int home,int dest,int lane,bool idle)
        {
            if(home==dest)home=dest==0?Gate:0;
            int social=(id&1)==0?WellSite:MarketSite;
            if(id%4>=2)
            {
                string venue=id%4==2?"chapel":"tavern";
                for(int i=1;i<Manor.PlotCount;i++)
                    if(i!=home&&i!=dest&&manor.slots[i].kind==venue){social=i;break;}
            }
            residents[residentCount++]=new ResidentVisit(walks[home*RouteCount+dest],walks[dest*RouteCount+social],
                walks[social*RouteCount+home],id,dest,social,lane,idle);
        }

        static ArtistMotion WorkGesture(string trade,int site)
        {
            if(site==WellSite||site==MarketSite||trade==Manor.Bakery||trade=="granary")return ArtistMotion.Lift;
            if(trade=="smithy"||trade=="timber_camp")return ArtistMotion.Heavy;
            if(IsFarm(trade)||trade==Manor.Weaver)return ArtistMotion.Tend;
            return trade=="tavern"||trade=="chapel"?ArtistMotion.Converse:ArtistMotion.Work;
        }

        void DrawResident(in ResidentVisit visit)
        {
            int id=visit.id;
            // Stagger shifts by household; the village clock integrates weather, so a shower never teleports walkers.
            visit.Sample(villageTime+visit.Offset,
                out var route,out int site,out float along,out int stage);
            route.Sample(along,out var pos,out var dir);
            bool walking=(stage&1)==0,working=stage==1&&!visit.idle;
            string trade=visit.work<Gate?manor.slots[visit.work].kind:null;
            bool farmer=!visit.idle&&IsFarm(trade);
            string venue=site>=0&&site<Gate?manor.slots[site].kind:null;
            float laneEase=Mathf.Clamp01(Mathf.Min(along,route.total-along)/.82f);
            pos+=new Vector3(dir.z,0,-dir.x)*((.22f+id%3*.046f)*laneEase);
            if(!walking&&site>=0)
            {
                var look=site==WellSite?WellOrigin:site==MarketSite?MarketSouth:site==Gate?GatehouseOrigin:Plot(site);
                dir=(look-pos).normalized;
                pos+=new Vector3(dir.z,0,-dir.x)*(.24f*((visit.lane%3)-1));
            }
            if(!InView(ManorOrigin+pos,3)||!TryPlaceWalker(pos,dir,out pos))return;
            if(walking&&yieldingToPedestrian)
            {
                PassingPoint=ManorOrigin+(pos+nearestPedestrian)*.5f;
                ResidentsPassing=true;
            }
            float speed=walking?(.85f+id%4*.09f)*(WeatherMakesLampsVisible?1.16f:1)*Ease(Mathf.Min(along,route.total-along)/.42f):0;
            float heading=LocalHeading(id,Mathf.Atan2(dir.x,dir.z)*Mathf.Rad2Deg);
            if(VisibleFarmers+VisibleVillagers==0||!WalkingResidentVisible&&speed>.45f)
            {ResidentPoint=ManorOrigin+pos;ResidentHeading=heading;WalkingResidentVisible=speed>.45f;}
            if(farmer)VisibleFarmers++;else VisibleVillagers++;
            var point=ManorOrigin+pos;ContactAt(point,.92f);
            string role=trade=="smithy"?"smith":farmer?"farmer":trade=="timber_camp"?"woodcutter":
                id%4==0||trade=="granary"||trade==Manor.Bakery?"carrier":"villager";
            ArtistMotion duty=stage==1||stage==3?WorkGesture(venue,site):
                walking&&stage!=0&&role=="carrier"?ArtistMotion.Haul:ArtistMotion.Idle;
            if(working){WorkingResidentPoint=point;WorkingTrade=trade;}
            QueuePose(new PoseInput{artist=true,working=!walking&&stage!=5,town=TownRole.Resident,survey=true,id=id,
                duty=duty,scale=.78f,position=point+Vector3.up*(.78f*.79f),angle=heading*Mathf.Deg2Rad,gait=along/.91f,
                speed=speed,maxSpeed=2.6f,direction=1,hp=1,color=Clothes[id%Clothes.Length],
                nod=WeatherMakesLampsVisible?9:working?6:0},new PoseDraw{costume=collection[role]});
        }
        internal Vector3 PreviewResidentVisit(int work,bool errand)
        {
            for(int i=0;i<residentCount;i++)
            {
                var visit=residents[i];if(visit.work!=work||visit.idle)continue;
                float speed=.85f+visit.id%4*.09f;
                float stop=visit.outbound.total/speed+2.1f;
                if(errand)stop+=8.4f+visit.errand.total/speed;
                villageTime=Mathf.Repeat(stop-visit.Offset,visit.Lap);ambientDirty=true;
                return ManorOrigin+Door(errand?visit.social:work);
            }
            throw new System.InvalidOperationException("No assigned resident for plot "+work);
        }
        internal Vector3 WorkingResidentPoint {get;private set;}
        internal string WorkingTrade {get;private set;}
        bool FreeWalkSpace(Vector3 p)
        {
            if(!FrontierMap.HasGround(ManorOrigin.x+p.x,p.z))return false;
            for(int i=0;i<occupiedWalkCount;i++)
                if((p-occupiedWalkSpace[i]).sqrMagnitude<.45f*.45f)return false;
            for(int i=0;i<estateObstacleCount;i++)if(estateObstacles[i].Contains(p))return false;
            int ready=manor.RetinueReady;
            const float separation=ManorPlotArt.RetinuePostRadius+.23f;
            for(int i=manorGuardsAway;i<ready;i++)
                if((p-RetinuePosts[i]).sqrMagnitude<separation*separation)return false;
            return true;
        }

        bool TryPlaceWalker(Vector3 wanted,Vector3 heading,out Vector3 chosen)
        {
            var tangent=new Vector3(heading.z,0,-heading.x).normalized;
            float shift=0,nearest=.94f*.94f;yieldingToPedestrian=false;
            for(int i=0;i<occupiedWalkCount;i++)
            {
                var away=wanted-occupiedWalkSpace[i];float sq=away.sqrMagnitude;
                if(sq>=.94f*.94f)continue;
                if(sq<nearest){nearest=sq;nearestPedestrian=occupiedWalkSpace[i];}
                float side=Vector3.Dot(away,tangent);
                shift+=(side<-.015f?-1:1)*(.94f-Mathf.Sqrt(sq))*.53f;
            }
            var original=wanted;
            var adjusted=wanted+tangent*Mathf.Clamp(shift,-.42f,.42f);
            if(FreeWalkSpace(adjusted))wanted=adjusted;
            chosen=wanted;
            if(!FreeWalkSpace(wanted))
            {
                var side=new Vector3(heading.z,0,-heading.x).normalized;
                var right=wanted+side*.29f;
                var left=wanted-side*.29f;
                var back=wanted-heading.normalized*.49f;
                if(FreeWalkSpace(right))chosen=right;
                else if(FreeWalkSpace(left))chosen=left;
                else if(FreeWalkSpace(back))chosen=back;
                else return false;
            }
            yieldingToPedestrian=nearest<.85f*.85f&&(chosen-original).sqrMagnitude>.04f*.04f;
            occupiedWalkSpace[occupiedWalkCount++]=chosen;
            return true;
        }

        void ReserveHeroBays()
        {
            var plot=Plot(tavernSlot);
            var facade=Quaternion.Euler(0,FacadeYaw(tavernSlot),0);
            float offset=ManorPlotArt.HalfWidth(tavernSlot==0)+.50f;
            for(int i=0;i<Manor.HeroIds.Length;i++)
            {
                heroBayClear[i]=false;
                if(manor.HeroLevel(Manor.HeroIds[i])==0||HeroOnPatrol(Manor.HeroIds[i]))continue;
                float side=i==0?-1:1;
                float wave=manor.HeroRest(Manor.HeroIds[i])>0?0:Mathf.Sin(manorTime*.32f+i*1.7f)*.063f;
                var bay=plot+facade*new Vector3(offset*side+wave,0,i==2?.44f:-1.17f);
                heroBayClear[i]=InView(ManorOrigin+bay,2.4f)&&TryPlaceWalker(bay,facade*Vector3.forward,out heroBays[i]);
            }
        }

        void RefreshResidentRoster()
        {
            residentRosterDirty=false;
            residentCount=0;
            int restingWorkers=0;
            for(int i=0;i<builtCrops.Length;i++)
                if(builtCrops[i]==CropAppearance.Fallow)restingWorkers+=manor.slots[i].workers;
            int idle=Mathf.Min(idleWorkers+restingWorkers,OrdinaryResidentLimit);
            int workingLimit=OrdinaryResidentLimit-Mathf.Min(idle,4);
            for(int worker=0;worker<3&&residentCount<workingLimit;worker++)
                for(int slot=0;slot<manor.slots.Length&&residentCount<workingLimit;slot++)
                {
                    if(manor.slots[slot].workers<=worker||builtCrops[slot]==CropAppearance.Fallow)continue;
                    int id=slot*5+worker;
                    int home=homeCount>0?homes[id%homeCount]:0;
                    AddResident(id,home,slot,worker,false);
                }
            int idleCount=Mathf.Min(idle,OrdinaryResidentLimit-residentCount);
            for(int i=0;i<idleCount;i++)
                AddResident(IdleFirst+i,homeCount>0?homes[i%homeCount]:0,destinations[(i+1)%destinationCount],i,true);
        }

        void DrawVillagePeople()
        {
            VisibleFarmers=VisibleVillagers=occupiedWalkCount=VisibleHeroMask=0;
            WalkingResidentVisible=ResidentsPassing=false;WorkingTrade=null;
            DrawRetinue();
            ReserveHeroBays();
            DrawSpecialists();
            for(int i=0;i<residentCount;i++)DrawResident(residents[i]);
            for(int heroIndex=0;heroIndex<Manor.HeroIds.Length;heroIndex++)
            {
                string id=Manor.HeroIds[heroIndex];
                if (manor.HeroLevel(id)==0 || HeroOnPatrol(id) || !heroBayClear[heroIndex]) continue;
                VisibleHeroMask|=1<<heroIndex;
                var model=Model(id);
                bool resting=manor.HeroRest(id)>0;
                float pace=manorTime*.32f+heroIndex*1.7f;
                const float townScale=.72f;
                var p=heroBays[heroIndex]+Vector3.up*(model.definition.scale*townScale*.79f);
                float desired=(heroIndex==0?-83:81)+(resting?4:13)*Mathf.Sin(pace);
                float facing=LocalHeading(HeroFirst+heroIndex,desired+FacadeYaw(tavernSlot))*Mathf.Deg2Rad;
                Soldier(model,"red",p+ManorOrigin,facing,200+heroIndex,armament:manor.HeroArmament(id),townScale:townScale);
            }
        }

        void DrawWorkshopActivity()
        {
            for(int i=0;i<millCount;i++)
            {
                if(!InView(millHubs[i],2.5f))continue;
                int workers=manor.slots[millPlots[i]].workers;
                float angle=21+i*37+(workers>0?manorTime*(13+workers*6):0);
                float size=manor.slots[millPlots[i]].kind==Manor.Bakery?.80f:1f;
                Add(sails,Pose(millHubs[i],millFacing[i]*Quaternion.AngleAxis(angle,Vector3.forward),Vector3.one*size),White);
            }
            for(int i=0;i<forgeCount;i++)
            {
                int site=forgePlots[i];
                int workers=manor.slots[site].workers;
                if(workers==0)continue;
                var facing=Quaternion.Euler(0,FacadeYaw(site),0);
                var mouth=ManorOrigin+Plot(site)+facing*ManorPlotArt.ForgeMouth;
                if(!InView(mouth,1.6f))continue;
                float breathe=.89f+Mathf.Sin(manorTime*6.8f+site)*.11f;
                float glow=WeatherMakesLampsVisible?1.22f:1f;
                Add(forgeFlame,Pose(mouth,facing,new Vector3(.31f*glow,.33f*breathe*glow,.18f)),new Color(1,.66f,.25f,.89f));
                if(!lowActors)
                {
                    float rise=Mathf.Repeat(manorTime*.72f+site*.37f,1);
                    Add(forgeFlame,Pose(mouth+facing*new Vector3(.12f*rise,.28f+rise*.45f,-.09f),facing,
                        Vector3.one*(.061f-.037f*rise)),new Color(1,.76f,.36f,1-rise));
                }
            }
        }

        void DrawWinterSmoke()
        {
            if(builtSeason!=3||QualityLevel!=2||lowActors||distance>44)return;
            var billboard=ViewCamera.transform.rotation*SmokeBillboard;
            int visible=0;
            for(int site=1;site<manor.slots.Length&&visible<4;site++)
            {
                var plot=manor.slots[site];
                Vector3 vent;
                if(plot.kind=="cottage"&&winterHeating)vent=ManorPlotArt.CottageChimney;
                else if(plot.kind=="smithy"&&plot.workers>0)vent=ManorPlotArt.ForgeChimney;
                else if(plot.kind==Manor.Bakery&&plot.workers>0)vent=ManorPlotArt.BakeryChimney;
                else continue;
                Vector3 chimney=ManorOrigin+Plot(site)+Vector3.up*.042f+
                    Quaternion.Euler(0,FacadeYaw(site),0)*Vector3.Scale(vent,ManorPlotArt.BuildingScale(plot.kind));
                if(!InView(chimney,1.2f))continue;
                float rise=Mathf.Repeat(manorTime*.43f+site*.37f,1);
                Vector3 center=chimney+new Vector3(rise*.37f,.12f+rise*.88f,-rise*.17f);
                float size=.28f+rise*.44f;
                Add(dust,Pose(center,billboard,new Vector3(size,1,size*.82f)),new Color(.82f,.85f,.87f,.34f*(1-rise)),1);
                visible++;
            }
        }

        void DrawManor()
        {
            if(!lowActors)ClearAmbient();
            if (manorDirty || cachedQuality != QualityLevel) RebuildManor();
            SyncCampMarkers();
            DrawOuterWorld();
            foreach (var b in still) Draw(b,true);
            float elapsed=Mathf.Min(Time.unscaledDeltaTime,.2f)*Mathf.Max(.65f,manorPace);
            manorTime+=elapsed;
            villageTime+=elapsed*(WeatherMakesLampsVisible?1.16f:1);
            if(!lowActors||ambientDirty||ambientCameraVersion!=visibilityVersion||Time.unscaledTime>=nextAmbient)
            {
                fillingAmbient=lowActors;
                if(fillingAmbient){ClearAmbient();ambientPopulated=true;}
                DrawWorkshopActivity();
                DrawWetWeatherLights();
                DrawWinterSmoke();
                if(residentRosterDirty)RefreshResidentRoster();
                bool jobs=!lowActors&&BeginSoldierPoses(residentCount+Manor.SpecialistIds.Length+Manor.HeroIds.Length+32);
                DrawVillagePeople();
                if(Current==null||Config.map!=FrontierMap.Id)DrawPreviewGuards();
                if(jobs)FinishSoldierPoses();
                fillingAmbient=false;ambientDirty=!lowActors;
                nextAmbient=Time.unscaledTime+.08f;ambientCameraVersion=visibilityVersion;
            }
            if(lowActors)foreach(var page in ambient)Draw(page,false);
            if(Config.map!=FrontierMap.Id && HoveredPlot!=manorSelection && (uint)HoveredPlot<manor.slots.Length)
            {
                float radius=HoveredPlot==0?5.25f:2.35f;
                var at=ManorOrigin+Plot(HoveredPlot)+Vector3.up*.069f;
                if(InView(at,radius))
                {
                    Add(hoverRing,Pose(at,Quaternion.identity,new Vector3(radius,1,radius*.86f)),HoverTint);
                    Add(dust,Pose(at-Vector3.up*.008f,Quaternion.identity,new Vector3(2*radius,1,1.72f*radius)),new Color(.43f,.85f,.86f,.16f),1);
                }
            }
            if ((uint)manorSelection < manor.slots.Length)
            {
                Vector3 p = Plot(manorSelection) + ManorOrigin + Vector3.up*.073f;
                float radius = manorSelection == 0 ? 5.30f : 2.39f;
                if(InView(p,radius))Add(ring,Pose(p,Quaternion.identity,new Vector3(radius,1,radius*.86f)),Gold);
            }
            DrawFrontierPatrol();
            PrepareWeatherPage(manorTime);
            Draw(weatherPage,false);
            foreach (var b in moving) Draw(b,false);
        }
    }
}
