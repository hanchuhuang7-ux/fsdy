using System.Collections;
using System.Collections.Generic;
using System.Text;
using Frontline.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        IEnumerator CheckVillageArt()
        {
            var view=app.View;
            string initial=Json.Write(app.Config);
            view.SetQuality(2,false);
            var shell=app.GetComponentInChildren<Canvas>(true);
            if(!shell)shell=Object.FindAnyObjectByType<Canvas>();
            Require(shell&&QualitySettings.antiAliasing==4&&QualitySettings.shadowCascades==4,"Village capture needs the Q2 player");
            var estate=WideEstateFixture();
            estate.guideDismissed=true;estate.Upgrade(0);estate.Upgrade(0);
            for(int guard=0;guard<6;guard++)estate.RecruitGuard();
            foreach(var hero in app.Data.Data.heroes)
            {
                estate.HireHero(3,hero);
                for(int tier=0;tier<Manor.MaxHeroArmament;tier++)estate.ForgeHero(hero.id);
                Require(estate.HeroArmament(hero.id)==3,"The exhibition estate could not forge "+hero.id);
            }
            foreach(var id in Manor.SpecialistIds)while(estate.SpecialistRank(id)<Manor.SpecialistMaxLevel)
            {
                int old=estate.SpecialistRank(id);estate.UpgradeSpecialist(id);
                Require(estate.SpecialistRank(id)>old,"A specialist did not qualify: "+id);
            }
            estate.Upgrade(24);estate.Upgrade(24);estate.Upgrade(19);estate.Upgrade(19);
            estate.SetRotation(21,"fallow");
            var gallery=new List<string>();
            var actions=new List<object>();
            var meshList=new List<object>();
            foreach(string key in new[]{"farm","farm_fallow","irrigated_field_rich","bakery","chapel","mill_sails","manor_market",
                "catapult","horse","arm_braced_left","market_cart","plot_ropes","road_path","road_setts"})
            {
                var mesh=Resources.Load<Mesh>("Models/"+key);
                var low=Resources.Load<Mesh>("Models/"+key+"_lod1");
                Require(mesh&&low&&mesh.HasVertexAttribute(VertexAttribute.TexCoord0)&&mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord1)==4,key);
                meshList.Add(new {model=key,main=mesh.vertexCount,proxy=low.vertexCount});
            }
            yield return Press("Mode.Manor");app.SetManorSpeed(0);app.ImportText(estate.Export());app.SelectManorSlot(-1);
            try
            {
                shell.enabled=false;
                var keep=new Vector3(FrontierMap.ManorX,0,0);
                yield return Photo("village-and-lanes",keep+new Vector3(1,1.5f,0),88,45,-34);
                yield return Photo("village-road",keep+new Vector3(14.05f,.66f,3.04f),10.1f,28,134);
                yield return PlotPhoto("chapel",24,3.07f,13.2f);
                yield return PlotPhoto("tavern-gallery",3,1.31f,10.4f);
                yield return Photo("linen-market",keep+new Vector3(7.30f,1.03f,1.76f),5.6f,24,-28);
                yield return PlotPhoto("farm-and-mill",19,1.12f,8.1f);
                yield return PlotPhoto("dye-works",11,.91f,8.8f);
                yield return PlotPhoto("bread-oven",10,.90f,8.9f);
                yield return PlotPhoto("fallow-watercourse",21,.47f,7.6f);
                yield return PlotPhoto("quarry-and-crane",13,1.04f,9.4f);
                yield return Photo("retinue",BattleView.RetinuePost(1)+Vector3.up*.90f,5.6f,16,-56);
                yield return Photo("quay-skiff",BattleView.QuayPoint,10.1f,40,-22);
                view.Focus(keep+new Vector3(4,1,0),42,40,-27);
                yield return WaitForState(()=>view.WalkingResidentVisible,"Could not frame a resident walking between workplaces");
                var local=view.ResidentPoint;
                yield return Photo("resident-on-the-road",local+Vector3.up*.82f,6.4f,21,view.ResidentHeading+138);
                view.Focus(keep,62,44,-30);
                yield return WaitForState(()=>(view.VisibleSpecialistMask&32)!=0,"Bard missing from town");
                yield return Photo("minstrel",view.SpecialistPoint(5)+Vector3.up*.83f,5.3f,19,view.SpecialistFacing(5)+142);
                yield return Photo("border-outpost",BattleView.CampCenter(0)+new Vector3(2,1,1),23.5f,29,-37);
                while(app.ForceCount("militia")>2)app.SelectForce("militia",-1);
                foreach(string hero in Manor.HeroIds)app.SelectForce(hero,1);
                app.PrepareExpedition(0,false);app.RequestStart();app.Paused=true;
                Require(app.InManor&&app.Battle?.World is UnityWorld&&app.Config.manorHall==3,"Missing commissioned expedition");
                foreach(var member in app.Config.red)
                    if(app.Data.Heroes.ContainsKey(member.type))Require(member.armament==3,"Missing forged gear for "+member.type);
                foreach(var member in app.Battle.Units)if(member.entity.type=="hero_commander"){app.Battle.Hit(member,34,null,0);break;}
                var heroic=new[]{
                    new MotionCapture("hero_vanguard","hero-hero_vanguard",MotionMoment.Draw,8),
                    new MotionCapture("hero_ranger","hero-hero_ranger",MotionMoment.Draw),
                    new MotionCapture("hero_commander","hero-hero_commander",MotionMoment.Heal)
                };
                yield return RecordActionMatch(heroic,actions,"310");
                foreach(var shot in heroic)gallery.Add(shot.name);
                app.ReturnManor();app.SetManorMode(false);app.SetMode("sandbox");
                foreach(string map in new[]{"bridge","ruins","highland"})
                {
                    app.SetMap(map);yield return null;
                    yield return Photo("arena-"+map,Vector3.up*.52f,31,39,-26);
                }
                app.ImportText(Json.Write(ActionFormation()));app.RequestStart();app.Paused=true;
                Require(app.Battle?.World is UnityWorld,"Arena art must show live PhysX actors");
                app.Battle.Hit(app.Battle.Units[2],35,null,0);
                view.CheckParallelPoses=true;
                BattleView.CheckActionCurves();
                var shots=new[]{
                    new MotionCapture("cavalry","charger-shoulder",MotionMoment.Gallop),
                    new MotionCapture("shield","vambrace-slash",MotionMoment.Strike),
                    new MotionCapture("spear","vambrace-thrust",MotionMoment.Strike),
                    new MotionCapture("archer","yew-bow",MotionMoment.Draw),
                    new MotionCapture("crossbow","steel-crossbow",MotionMoment.Draw),
                    new MotionCapture("giant","forged-maul",MotionMoment.Draw,9),
                    new MotionCapture("medic","field-surgeon",MotionMoment.Heal),
                    new MotionCapture("bomber","grenadier",MotionMoment.Draw),
                    new MotionCapture("catapult","trebuchet",MotionMoment.Strike)
                };
                yield return RecordActionMatch(shots,actions,"310");
                foreach(var shot in shots)gallery.Add(shot.name);
                gallery.Add("arena-engagement");
                Require(view.CheckedParallelPoses>0,"No verified parallel poses");
            }
            finally {view.CheckParallelPoses=false;shell.enabled=true;}
            app.ReturnToDeploy();app.ImportText(initial);
            Require(Json.Write(app.Config)==initial,"Gallery changed the previous arena formation");
            var report=new {version=Application.version,quality=view.QualityLevel,width=Screen.width,height=Screen.height,
                realPhysX=true,workers=view.ParallelPoseWorkers,checkedParallelPoses=view.CheckedParallelPoses,
                surfaceMeshes=meshList,actions,screenshots=gallery.Count,gallery};
            yield return Save("village-art.json",Encoding.UTF8.GetBytes(Json.Write(report)),"application/json");
            Debug.Log("FRONTLINE_VILLAGE_ART_OK poses="+view.CheckedParallelPoses+" pictures="+gallery.Count);

            IEnumerator Photo(string id,Vector3 center,float distance,float pitch,float yaw)
            {
                view.Focus(center,distance,pitch,yaw);
                yield return ManorShot("310-"+id);gallery.Add(id);
            }
            IEnumerator PlotPhoto(string id,int plot,float height,float distance)
            {
                var center=new Vector3(FrontierMap.ManorX+Manor.SlotX(plot),height,Manor.SlotZ(plot));
                yield return Photo(id,center,distance,27,BattleView.PlotArtYaw(plot)-24);
            }
        }
    }

    public sealed partial class BattleView
    {
        internal static float PlotArtYaw(int plot)=>FacadeYaw(plot);
    }
}
