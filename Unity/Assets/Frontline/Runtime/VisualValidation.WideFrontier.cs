using System.Collections;
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

            var keep=new Vector3(FrontierMap.ManorX,0,0);
            for(int level=1;level<=3;level++)
            {
                if(level>1)estate.Upgrade(0);
                if(level==3)for(int guard=0;guard<6;guard++)estate.RecruitGuard();
                app.ImportText(estate.Export());
                view.Focus(keep+new Vector3(level==3?-3.25f:-1.7f,level==3?5.8f:3.45f,0),
                    level==3?31.5f:25f,32,-21);
                yield return ManorShot("190-castle-level-"+level);
            }
            Require(app.Estate.GrantedPlots==36&&app.Estate.version==Manor.SaveVersion,"Wide estate lost permissions or changed save schema");
            yield return Press("Frontier.View.1");
            yield return WaitForState(()=>!view.CameraMoving,"Manor preset did not settle");
            yield return new WaitForEndOfFrame();
            CheckButtonHit("Manor.Governance");
            CheckWorldOutline(FrontierMap.ManorCoast);
            int firstBuild=view.FrontierStaticRebuilds;
            yield return ManorShot("190-ducal-town");

            yield return Press("Frontier.View.0");
            yield return WaitForState(()=>!view.CameraMoving,"Wide map preset did not finish its transition");
            yield return new WaitForEndOfFrame();
            CheckWorldOutline(FrontierMap.ManorCoast);
            CheckWorldOutline(FrontierMap.FrontierCoast);
            float desktopDistance=view.CameraDistance;
            Require(desktopDistance>250&&view.LowSceneryLod&&view.TerrainChunksDrawn>0,"Wide terrain LOD is missing");
            yield return ManorShot("190-world-coast");

            view.Zoom(4);
            yield return WaitForState(()=>!view.CameraMoving,"Proportional wheel zoom did not settle");
            Require(Mathf.Abs(view.CameraDistance/desktopDistance-Mathf.Exp(-4*.118f))<.025f,"Wheel zoom was not proportional");
            var hill=new Vector3(224,FrontierMap.SurfaceHeight(224,27),27);
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
            yield return ManorShot("190-frontier-hills");

            var flag=BattleView.CampFlagAt(0);
            view.Focus(flag+new Vector3(.65f,1.7f,.35f),14,31,-28);
            yield return new WaitForEndOfFrame();
            Require(view.ClaimedFlagMask==7&&view.OutpostSentries==3&&view.OutpostSupplies==3&&view.AlertCampMask==0,
                "Claimed frontier lost its flag, crates or sentry");
            ClickSector(flag+Vector3.up*1.5f,0,"已筑哨",true);
            yield return ManorShot("190-claimed-outpost");

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
            yield return ManorShot("190-frontier-contact");
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
            yield return ManorShot("190-portrait-manor");
            yield return Press("Frontier.View.0");
            yield return WaitForState(()=>!view.CameraMoving,"Portrait overview did not settle");
            yield return new WaitForEndOfFrame();
            CheckWorldOutline(FrontierMap.ManorCoast);CheckWorldOutline(FrontierMap.FrontierCoast);
            CheckButtonHit("Frontier.View.2");CheckButtonHit("Manor.Patrol");
            yield return ManorShot("190-portrait-world");
            yield return Press("Mode.Duel");
            Require(Json.Write(app.Config)==arena&&!view.ViewingFrontier,"Expanded frontier altered the arena");
            var proof=new {version=Application.version,unity=Application.unityVersion,manorArea=FrontierMap.ManorArea,
                frontierArea=FrontierMap.FrontierArea,areaRatio=FrontierMap.FrontierArea/FrontierMap.ManorArea,
                desktop="1280x800",portrait="390x844",hillHeight=hill.y,visibleTerrainChunks=visible,
                terrainChunks=total,culledDecorations=culled,contactTick,duelUnchanged=true};
            yield return Save("wide-frontier-smoke.json",Encoding.UTF8.GetBytes(Json.Write(proof)),"application/json");
            Debug.Log("FRONTLINE_WIDE_WORLD_SMOKE_OK "+Json.Write(proof));
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

        void CheckWorldOutline(System.Collections.Generic.IReadOnlyList<FrontierMap.ShoreSlice> shore)
        {
            foreach(var bank in shore)
            {
                Check(bank.x,bank.south);
                Check(bank.x,bank.north);
            }

            void Check(float x,float z)
            {
                var point=app.View.ViewCamera.WorldToViewportPoint(new Vector3(x,0,z));
                Require(point.x>.01f&&point.x<.99f&&point.y>.012f&&point.y<.99f&&point.z>0,
                    "Camera cropped the coastline at "+x+", "+z+" ("+point+")");
            }
        }
    }
}
