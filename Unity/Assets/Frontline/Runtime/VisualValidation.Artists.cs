using System.Collections;
using System.Collections.Generic;
using System.Text;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        IEnumerator CheckArtistRelease()
        {
            string initial=Json.Write(app.Config);
            var view=app.View;view.SetQuality(2,false);view.CheckParallelPoses=true;
            var canvas=app.GetComponentInChildren<Canvas>(true);
            if(!canvas)canvas=Object.FindAnyObjectByType<Canvas>();
            var roster=Resources.Load<ArtistRoster>("CC0/ArtistRoster");
            Require(canvas&&roster&&roster.skeletons.Length==6&&QualitySettings.antiAliasing==4&&QualitySettings.shadowCascades==4,"Incomplete artist player");
            int minBorder=255,edgeSamples=0;
            var captures=new List<string>();var motion=new List<object>();
            var estate=WideEstateFixture();estate.guideDismissed=true;estate.Upgrade(0);estate.Upgrade(0);
            foreach(int plot in new[]{3,5,10,11,19,24})while(estate.slots[plot].level<Manor.BuildingMaxLevel)estate.Upgrade(plot);
            for(int i=0;i<6;i++)estate.RecruitGuard();
            foreach(var hero in app.Data.Data.heroes)
            {
                estate.HireHero(3,hero);for(int t=0;t<3;t++)estate.ForgeHero(hero.id);
                Require(estate.HeroArmament(hero.id)==3,"Missing war gear");
            }
            foreach(var id in Manor.SpecialistIds)while(estate.SpecialistRank(id)<Manor.SpecialistMaxLevel)estate.UpgradeSpecialist(id);
            yield return Press("Mode.Manor");app.SetManorSpeed(0);app.ImportText(estate.Export());app.SelectManorSlot(-1);
            var keep=new Vector3(FrontierMap.ManorX,0,0);
            try
            {
                yield return Photo("manor-hud",keep+new Vector3(0,1.1f,0),74,40,-31);
                canvas.enabled=false;
                yield return Photo("ring-at-maximum-zoom",keep,BattleView.MaxFrontierDistance,57,-14);
                AuditBorder();
                for(int c=0;c<4;c++)for(int a=0;a<4;a++)
                {
                    view.Focus(new Vector3((c&1)==0?FrontierMap.West:FrontierMap.East,0,c<2?-FrontierMap.HalfDepth:FrontierMap.HalfDepth),
                        BattleView.MaxFrontierDistance,a%2==0?27:49,a*90+c*17);
                    yield return new WaitForEndOfFrame();AuditBorder();
                }
                yield return Photo("lake-ring-and-highlands",keep+Vector3.up*2,584,63,-18);
                yield return Photo("cross-lake-post-road",new Vector3((FrontierMap.BridgeStart+FrontierMap.Boundary)/2,0,0),63,25,-52);
                yield return Photo("reed-bank-and-piers",new Vector3(FrontierMap.BridgeStart+1,-.25f,-3.4f),18,19,-42);
                yield return Photo("lake-estate",keep+new Vector3(1,1.8f,0),91,43,-31);
                yield return Photo("dukes-keep",keep+new Vector3(0,4.40f,-.40f),23.7f,20,-17);
                yield return Photo("cobbled-well-and-market",keep+new Vector3(4.9f,1.2f,1.63f),13,22,-33);
                yield return Plot("timber-cottage",1,1.22f,6.4f);
                yield return Plot("inn-and-gallery",3,1.67f,8.5f);
                yield return Plot("parish-church",24,2.23f,10.3f);
                yield return Plot("farm-and-mill",19,.96f,6.8f);
                yield return Plot("forge",5,1.08f,7.3f);
                yield return Plot("dye-yard",11,1.0f,6.7f);
                yield return Plot("bakery",10,1.22f,6.9f);
                foreach(int slot in new[]{2,5})
                {
                    var work=view.PreviewResidentVisit(slot,false);
                    yield return Photo(slot==2?"field-hand-at-work":"smith-at-work",work+Vector3.up*.85f,5.15f,14,view.ClearPortraitAngle(work,BattleView.PlotArtYaw(slot)-14,5.15f));
                }
                var stall=view.PreviewResidentVisit(5,true);
                yield return Photo("resident-on-errand",stall+Vector3.up*.86f,5.4f,19,view.ClearPortraitAngle(stall,139,5.4f));
                view.Focus(keep,54,45,-21);
                for(int frame=0;frame<520;frame++)
                {
                    view.PreviewVillageTime(frame*.43f);
                    yield return new WaitForEndOfFrame();
                    if(view.ResidentsPassing)break;
                }
                Require(view.ResidentsPassing,"No pair of actual pedestrians yielded on the estate lanes");
                var passing=view.PassingPoint;
                yield return Photo("giving-way-on-the-high-street",passing+Vector3.up*.84f,6.2f,20,view.ClearPortraitAngle(passing,-119,6.2f));
                var specimen=view.PortraitTree();
                yield return Photo("scanned-woodland",specimen+Vector3.up*2.07f,9.1f,9,153);
                float x=FrontierMap.IslandWest+2.7f*(9+.42f);FrontierMap.ShoreAt(x,out var shore,out _);
                yield return Photo("scanned-shore",new Vector3(x,-1.08f,shore-2.45f),9.7f,18,-27);
                yield return Photo("household-guard",view.GuardOnDuty(1)+Vector3.up*.88f,4.9f,12,-41);
                view.Focus(keep,45,42,-20);yield return WaitForState(()=>view.WalkingResidentVisible,"Missing estate resident");
                var villager=view.ResidentPoint;
                yield return Photo("estate-resident",villager+Vector3.up*.8f,4.3f,14,view.ClearPortraitAngle(villager,view.ResidentHeading+144,4.3f));
                while(app.ForceCount("militia")>2)app.SelectForce("militia",-1);
                foreach(var hero in Manor.HeroIds)app.SelectForce(hero,1);
                app.PrepareExpedition(0,false);app.RequestStart();app.Paused=true;
                foreach(var u in app.Battle.Units)if(u.entity.type=="hero_commander"){app.Battle.Hit(u,34,null,0);break;}
                var heroes=new[]{new MotionCapture("hero_vanguard","forged-vanguard",MotionMoment.Draw,8),
                    new MotionCapture("hero_ranger","ranger",MotionMoment.Draw),new MotionCapture("hero_commander","commander",MotionMoment.Heal)};
                yield return RecordActionMatch(heroes,motion,"410");foreach(var h in heroes)captures.Add(h.name);
                app.ReturnManor();app.SetManorMode(false);app.SetMode("sandbox");
                var portraits=new Deployment{budget=3000};
                portraits.red.Add(new UnitPlacement("shield",-7,0));portraits.red.Add(new UnitPlacement("archer",-7,5));
                portraits.red.Add(new UnitPlacement("cavalry",-13,-5));portraits.blue.Add(new UnitPlacement("militia",13,-5,"hold"));
                app.ImportText(Json.Write(portraits));
                yield return Photo("knight-portrait",new Vector3(-7,1.12f,.03f),3.55f,10,-111);
                yield return Photo("archer-portrait",new Vector3(-7,1.24f,5),3.45f,10,-76);
                app.RequestStart();app.Paused=true;
                var ride=new[]{new MotionCapture("cavalry","charger-on-the-road",MotionMoment.Gallop)};
                yield return RecordActionMatch(ride,motion,"410");captures.Add(ride[0].name);
                app.ReturnToDeploy();app.ImportText(Json.Write(ActionFormation()));app.RequestStart();app.Paused=true;
                Require(app.Battle.World is UnityWorld,"Missing real PhysX squad");
                var shots=new[]{new MotionCapture("cavalry","galloping-charger",MotionMoment.Gallop),
                    new MotionCapture("archer","archer-loose",MotionMoment.Draw,5),new MotionCapture("shield","knight-and-shield",MotionMoment.Strike),
                    new MotionCapture("spear","spear-infantry",MotionMoment.Strike),new MotionCapture("catapult","siege-engine",MotionMoment.Strike)};
                yield return RecordActionMatch(shots,motion,"410");foreach(var h in shots)captures.Add(h.name);
                yield return Photo("pitched-battle",new Vector3(0,1.1f,-.13f),33,33,-36);
                Require(view.CheckedParallelPoses>0,"No parallel motion verification");
                var report=new {version=Application.version,unity=Application.unityVersion,width=Screen.width,height=Screen.height,
                    quality=view.QualityLevel,workers=view.ParallelPoseWorkers,checkedParallelPoses=view.CheckedParallelPoses,
                    skeletons=roster.skeletons.Length,costumes=roster.costumes.Length,truePhysX=true,edgeSamples,minBorder,actions=motion,screenshots=captures.Count,gallery=captures};
                yield return Save("artist-art.json",Encoding.UTF8.GetBytes(Json.Write(report)),"application/json");
                Debug.Log("FRONTLINE_CC0_GALLERY_OK "+captures.Count+" pictures, parallel="+view.CheckedParallelPoses);
            }
            finally{canvas.enabled=true;view.CheckParallelPoses=false;}
            app.ReturnToDeploy();app.ImportText(initial);
            Require(Json.Write(app.Config)==initial,"Artist preview changed the arena formation");

            void AuditBorder()
            {
                var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGBA32,false);
                try
                {
                    image.ReadPixels(new Rect(0,0,image.width,image.height),0,0,false);
                    var data=image.GetRawTextureData<Color32>();
                    for(int j=0;j<data.Length;j++)
                    {
                        int x=j%image.width,y=j/image.width;
                        if(x>=3&&x<image.width-3&&y>=3&&y<image.height-3)continue;
                        var pixel=data[j];int peak=Mathf.Max(pixel.r,Mathf.Max(pixel.g,pixel.b));
                        if(peak<22)throw new System.InvalidOperationException("Exposed void at "+x+","+y);
                        minBorder=Mathf.Min(minBorder,peak);edgeSamples++;
                    }
                }
                finally{Destroy(image);}
            }
            IEnumerator Photo(string name,Vector3 p,float zoom,float pitch,float yaw)
            {view.Focus(p,zoom,pitch,yaw);yield return ManorShot("410-"+name);captures.Add(name);}
            IEnumerator Plot(string name,int slot,float height,float zoom)
            {return Photo(name,keep+new Vector3(Manor.SlotX(slot),height,Manor.SlotZ(slot)),zoom,23,BattleView.PlotArtYaw(slot)-18);}
        }
    }
}
