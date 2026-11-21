using System.Collections;
using System;
using System.Text;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        IEnumerator CheckWideWorld()
        {
            var view=app.View;
            string arena=Json.Write(app.Config);
            yield return Press("Mode.Manor");
            yield return Press("Manor.Speed.0");
            var estate=WideEstateFixture();
            estate.guideDismissed=true;
            foreach(var id in Manor.HeroIds)
            {
                estate.HireHero(3,app.Data.Heroes[id]);
                for(int tier=0;tier<Manor.MaxHeroArmament;tier++)estate.ForgeHero(id);
            }
            foreach(var id in Manor.SpecialistIds)
                while(estate.SpecialistRank(id)<Manor.SpecialistMaxLevel)estate.UpgradeSpecialist(id);

            var keep=new Vector3(FrontierMap.ManorX,0,0);
            for(int level=1;level<=3;level++)
            {
                if(level>1)estate.Upgrade(0);
                if(level==3)for(int guard=0;guard<6;guard++)estate.RecruitGuard();
                app.ImportText(estate.Export());
                view.Focus(keep+new Vector3(level==3?-3.25f:-1.7f,level==3?5.8f:3.45f,0),
                    level==3?31.5f:25f,32,-21);
                yield return ManorShot("210-castle-level-"+level);
            }
            Require(app.Estate.GrantedPlots==36&&app.Estate.version==Manor.SaveVersion,"Wide estate lost permissions or changed save schema");
            yield return Press("Frontier.View.1");
            yield return WaitForState(()=>!view.CameraMoving,"Manor preset did not settle");
            yield return new WaitForEndOfFrame();
            CheckButtonHit("Manor.Governance");
            CheckWorldOutline(FrontierMap.ManorCoast);
            int firstBuild=view.FrontierStaticRebuilds;
            yield return ManorShot("210-ducal-island");

            yield return Press("Frontier.View.0");
            yield return WaitForState(()=>!view.CameraMoving,"Wide map preset did not finish its transition");
            yield return new WaitForEndOfFrame();
            CheckWorldOutline(FrontierMap.ManorCoast);
            CheckRingOutline();
            float desktopDistance=view.CameraDistance;
            Require(desktopDistance>250&&view.LowSceneryLod&&view.TerrainChunksDrawn>0,"Wide terrain LOD is missing");
            yield return ManorShot("210-lake-overview");
            int edgePixels=0,minEdgeChannel=255;
            yield return CheckZoomHorizon("210-zoom-limit",true);
            view.FrameFrontier(0);

            view.Zoom(4);
            yield return WaitForState(()=>!view.CameraMoving,"Proportional wheel zoom did not settle");
            Require(Mathf.Abs(view.CameraDistance/desktopDistance-Mathf.Exp(-4*.118f))<.025f,"Wheel zoom was not proportional");
            var hill=HighestHill();
            view.Focus(hill,32,46,-20);
            yield return new WaitForEndOfFrame();
            var hillScreen=(Vector2)view.ViewCamera.WorldToScreenPoint(hill);
            Require(view.Ground(hillScreen,out var picked)&&Vector3.Distance(hill,picked)<.045f&&hill.y>6,
                "Click picking does not follow the hillside");
            view.MeasureCasterVertices=true;
            yield return new WaitForEndOfFrame();
            int visible=view.TerrainChunksDrawn,total=view.TerrainChunkCount,culled=view.CulledStaticInstances;
            Require(visible>0&&visible<total&&culled>0&&view.FrontierStaticRebuilds==firstBuild,
                "A near view did not cull outer chunks or caused a rebuild");
            view.MeasureCasterVertices=false;
            yield return ManorShot("210-frontier-ridge");

            float center=(FrontierMap.BridgeStart+FrontierMap.Boundary)*.5f;
            view.Focus(new Vector3(center-3,.1f,0),73,35,56);
            yield return ManorShot("210-east-lake-bridge");

            var flag=BattleView.CampFlagAt(0);
            view.Focus(flag+new Vector3(.65f,1.7f,.35f),14,31,-28);
            yield return new WaitForEndOfFrame();
            Require(view.ClaimedFlagMask==7&&view.OutpostSentries==3&&view.OutpostSupplies==3&&view.AlertCampMask==0,
                "Claimed frontier lost its flag, crates or sentry");
            ClickSector(flag+Vector3.up*1.5f,0,"已筑哨",true);
            yield return ManorShot("210-claimed-outpost");
            app.SelectManorSlot(-1);

            view.Focus(new Vector3(FrontierMap.ManorX+3.2f,2.0f,4.3f),20.3f,26,143);
            yield return ManorShot("210-half-timber-town");
            yield return CheckResidentCloseups(true,"210");
            yield return CaptureArmamentArt(true,"210-armed");
            var farm=new Vector3(FrontierMap.ManorX+Manor.SlotX(2),1.0f,Manor.SlotZ(2));
            view.Focus(farm,24,41,-26);
            yield return WaitForState(()=>view.VisibleFarmers+view.VisibleVillagers>0,"No resident within the town lanes");
            view.Focus(view.ResidentPoint+Vector3.up*.77f,6.8f,24,view.ResidentHeading+168);
            yield return ManorShot("210-villager");

            yield return Press("Manor.Patrol");
            yield return Press("Manor.Patrol.Start");
            var battle=app.Battle;
            Require(app.InFrontier&&battle!=null&&battle.Config.blue.Count==15&&battle.World is UnityWorld,"Wide patrol did not spawn");
            yield return new WaitForEndOfFrame();
            Require(view.AlertCampMask==7&&view.ClaimedFlagMask==0,"Claim ownership suppressed live guards");
            var camp=FrontierMap.CampCenter(0);
            app.Speed=4;
            view.Focus(BattleView.CampCenter(0)+new Vector3(-7,1,-.8f),28,41,-19);
            yield return ClickBattlefield(camp.x,camp.z);
            yield return WaitForState(()=>battle.Units[0].entity.stats.damageTaken>0||
                battle.Units.Exists(u=>u.entity.stats.damageDealt>0),"No actual battle at the relocated outpost");
            Debug.Log("FRONTLINE_WIDE_PATROL_CONTACT tick="+battle.Tick);
            app.Paused=true;
            yield return ManorShot("210-frontier-contact");
            int contactTick=battle.Tick;
            yield return Press("Frontier.Settle");
            yield return Press("Manor.Settlement.Done");

            Screen.SetResolution(390,844,FullScreenMode.Windowed);
            yield return WaitForState(()=>Screen.width==390&&Screen.height==844,"Portrait viewport did not apply");
            yield return Press("Frontier.View.1");
            yield return WaitForState(()=>!view.CameraMoving,"Portrait manor preset did not settle");
            yield return new WaitForEndOfFrame();
            CheckButtonHit("Settings.Quality");CheckButtonHit("Frontier.View.1");
            CheckButtonHit("Manor.Patrol");CheckButtonHit("Manor.Governance");CheckFloatingStatus();
            CheckWorldOutline(FrontierMap.ManorCoast);
            CheckHorizonPixels();
            yield return ManorShot("210-portrait-island");
            yield return Press("Frontier.View.0");
            yield return WaitForState(()=>!view.CameraMoving,"Portrait overview did not settle");
            yield return new WaitForEndOfFrame();
            CheckWorldOutline(FrontierMap.ManorCoast);CheckRingOutline();
            CheckButtonHit("Frontier.View.2");CheckButtonHit("Manor.Patrol");
            yield return ManorShot("210-portrait-lake");
            yield return CheckZoomHorizon("210-portrait-zoom-limit",false);
            Screen.SetResolution(1280,800,FullScreenMode.Windowed);
            yield return WaitForState(()=>Screen.width==1280&&Screen.height==800,"Desktop viewport did not restore");
            yield return Press("Mode.Duel");
            Require(Json.Write(app.Config)==arena&&!view.ViewingFrontier,"Expanded frontier altered the arena");
            yield return CheckArenaLandscape(true,"210");
            var proof=new {version=Application.version,unity=Application.unityVersion,manorArea=FrontierMap.ManorArea,
                frontierArea=FrontierMap.FrontierArea,lakeArea=FrontierMap.LakeArea,areaRatio=FrontierMap.FrontierArea/FrontierMap.ManorArea,
                bridgeSpan=FrontierMap.Boundary-FrontierMap.BridgeStart,maxCameraDistance=BattleView.MaxFrontierDistance,
                horizonEdgePixels=edgePixels,minBrightestEdgeChannel=minEdgeChannel,blackEdgePixels=0,
                desktop="1280x800",portrait="390x844",hillHeight=hill.y,visibleTerrainChunks=visible,
                terrainChunks=total,culledDecorations=culled,contactTick,duelUnchanged=true};
            yield return Save("wide-frontier-smoke.json",Encoding.UTF8.GetBytes(Json.Write(proof)),"application/json");
            Debug.Log("FRONTLINE_WIDE_WORLD_SMOKE_OK "+Json.Write(proof));

            IEnumerator CheckZoomHorizon(string shot,bool fullMatrix)
            {
                view.Zoom(-100);
                yield return WaitForState(()=>!view.CameraMoving,"Max map zoom did not settle");
                yield return new WaitForEndOfFrame();
                Require(view.CameraDistance==BattleView.MaxFrontierDistance,"Did not reach max zoom");
                CheckHorizonPixels();
                yield return ManorShot(shot);
                int headings=fullMatrix?8:4;
                for(int corner=0;corner<4;corner++)
                    for(int angle=0;angle<headings;angle++)
                    {
                        var end=new Vector3(corner%2==0?FrontierMap.West-3:FrontierMap.East+3,0,
                            (corner<2?-1:1)*(FrontierMap.HalfDepth+5));
                        view.Focus(end,BattleView.MaxFrontierDistance,angle%2==0?25:41,angle*360f/headings+13*corner);
                        yield return new WaitForEndOfFrame();
                        CheckHorizonPixels();
                    }
                view.FrameFrontier(0);
            }

            void CheckHorizonPixels()
            {
                var camera=view.ViewCamera;
                Require(camera.farClipPlane>=Mathf.Max(2600,view.CameraDistance*5.5f)-.1f,"Horizon far plane was shortened");
                var page=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);
                try
                {
                    page.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0,false);
                    var pixels=page.GetRawTextureData<Color32>();
                    int w=page.width,h=page.height;
                    for(int y=0;y<h;y++)
                    {
                        if(y<4||y>=h-4)
                            for(int x=0;x<w;x++)Probe(pixels[y*w+x],x,y);
                        else
                            for(int x=0;x<4;x++)
                            {
                                Probe(pixels[y*w+x],x,y);
                                Probe(pixels[y*w+w-1-x],w-1-x,y);
                            }
                    }
                }
                finally {Destroy(page);}
            }

            void Probe(Color32 pixel,int x,int y)
            {
                int peak=Mathf.Max(pixel.r,Mathf.Max(pixel.g,pixel.b));
                if(peak<22)throw new InvalidOperationException("Black horizon pixel at "+x+","+y+" camera="+view.ViewCamera.transform.position);
                edgePixels++;
                minEdgeChannel=Mathf.Min(minEdgeChannel,peak);
            }
        }

        static Vector3 HighestHill()
        {
            V3 peak=new V3();
            for(int i=0;i<FrontierMap.ShoreStations;i++)
                for(int b=2;b<FrontierMap.TerrainBands;b++)
                {
                    var site=(FrontierMap.TerrainPoint(i,b)+FrontierMap.TerrainPoint((i+1)%FrontierMap.ShoreStations,b)+
                        FrontierMap.TerrainPoint((i+1)%FrontierMap.ShoreStations,b+1))*(1/3f);
                    if(site.y>peak.y)peak=site;
                }
            return UnityWorld.Vector(peak);
        }

        static Manor WideEstateFixture()
        {
            var estate=new Manor {coin=10000,timber=10000,arms=1000,food=1000,influence=999,
                cleared=6,clearedSectors=Manor.SectorMask,prosperityPeak=3,day=2};
            for(int i=0;i<Manor.Sectors.Count;i++)estate.ClaimSector(i);
            for(int rank=1;rank<Manor.NobleRanks.Count;rank++)estate.PromoteNobility(rank);
            for(int i=18;i<24;i++)estate.ReclaimPlot(i);
            string[] city={"cottage","farm","tavern","cottage","smithy","barracks","cottage","chapel","pasture",
                Manor.Bakery,Manor.Weaver,"granary","timber_camp","cottage","stables","drill_yard","cottage",null,
                "farm","timber_camp","irrigated_field",null,"pasture","chapel","cottage",null,"farm",null,"weaver",null,
                "cottage",null,"timber_camp",null,"barracks"};
            for(int i=0;i<city.Length;i++)
            {
                if(city[i]==null)continue;
                estate.Build(i+1,city[i]);
                if(i<17){estate.Upgrade(i+1);estate.Upgrade(i+1);}
            }
            estate.stock["militia"]=16;
            estate.workers=14;
            for(int i=1;i<=9;i++)
                if(i!=1&&i!=4&&i!=7)estate.Assign(i,1);
            return estate;
        }

        void CheckRingOutline()
        {
            foreach(var point in FrontierMap.OuterBank)CheckWorldPoint(UnityWorld.Vector(point));
            foreach(var point in FrontierMap.LakeBank)CheckWorldPoint(UnityWorld.Vector(point));
        }

        void CheckWorldOutline(System.Collections.Generic.IReadOnlyList<FrontierMap.ShoreSlice> shore)
        {
            foreach(var bank in shore)
            {
                CheckWorldPoint(new Vector3(bank.x,0,bank.south));
                CheckWorldPoint(new Vector3(bank.x,0,bank.north));
            }
        }

        void CheckWorldPoint(Vector3 bank)
        {
            var point=app.View.ViewCamera.WorldToViewportPoint(bank);
            Require(point.x>.01f&&point.x<.99f&&point.y>.012f&&point.y<.99f&&point.z>0,
                "Camera cropped the coastline at "+bank+" ("+point+")");
        }
    }
}
