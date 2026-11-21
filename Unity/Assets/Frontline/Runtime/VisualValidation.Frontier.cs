using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        internal const string FrontierSaveKey="frontline.qa.frontier.v2";

        IEnumerator WaitForState(Func<bool> ready,string failure)
        {
            float before=Time.realtimeSinceStartup;
            while(!ready() && Time.realtimeSinceStartup-before<15)yield return null;
            Require(ready(),failure);
        }
        IEnumerator CheckFrontierPreset(int focus)
        {
            yield return Press("Frontier.View."+focus);
            yield return WaitForState(()=>!app.View.CameraMoving,"Camera did not finish changing presets");
            yield return new WaitForEndOfFrame();
            var subject=focus==1?new V3(FrontierMap.ManorX,0,0):focus==2?FrontierMap.CampCenter(0):new V3(FrontierMap.Boundary,0,0);
            var screen=(Vector2)app.View.ViewCamera.WorldToScreenPoint(UnityWorld.Vector(subject));
            Require(app.View.FrontierFocus==focus && screen.x>0&&screen.x<Screen.width&&screen.y>0&&screen.y<Screen.height &&
                UiHits(screen).Count==0 && app.View.Ground(screen,out var ground)&&
                Vector2.Distance(new Vector2(ground.x,ground.z),new Vector2(subject.x,subject.z))<.2f,
                "Preset obscures its point of interest or breaks picking");
            if(!Viewport.Compact&&app.View.QualityLevel==2&&focus==1)
                Require(!app.View.LowActorLod&&!app.View.LowSceneryLod,"A focused fine desktop view lost its detailed models");
        }

        Button VisibleButton(string name) => app.GetComponentsInChildren<Button>().Single(b=>b.name==name&&b.gameObject.activeInHierarchy);
        void CheckButtonHit(string name)
        {
            var button=VisibleButton(name);
            var point=Center((RectTransform)button.transform);
            var hits=UiHits(point);
            Require(point.x>=0&&point.x<Screen.width&&point.y>=0&&point.y<Screen.height&&hits.Count>0&&
                hits[0].gameObject.GetComponentInParent<Button>()==button,"Control is outside its usable area: "+name+
                " at "+point+" (top hit "+(hits.Count>0?hits[0].gameObject.name:"none")+")");
        }

        IEnumerator CheckDesktopControls()
        {
            string original=Json.Write(app.Config),tool=app.Tool;
            app.Tool="select";app.Refresh();
            yield return new WaitForEndOfFrame();
            var input=app.GetComponent<BattleInput>();
            var camera=app.View.ViewCamera;
            var u=app.Config.red[0];
            float floor=app.Data.Height(app.Config.map,u.x,u.z);
            Vector2 foot=camera.WorldToScreenPoint(new Vector3(u.x,floor,u.z));
            Vector2 center=camera.WorldToScreenPoint(new Vector3(u.x,floor+.8f,u.z));
            Require(UiHits(foot).Count==0&&foot.x>285,"Desktop sidebar covers the first formation slot");
            input.BeginMouse(0,foot,false,false);input.EndMouse(foot,false);
            Require(app.Selected.Contains(0),"Left click selection failed");
            Vector2 a=center-new Vector2(13,13),b=center+new Vector2(13,13);
            input.BeginMouse(0,a,false,false);input.MoveMouse(b);input.EndMouse(b,false);
            Require(app.Selected.Contains(0),"Marquee missed its unit");

            app.Tool="place";
            var rotation=camera.transform.rotation;
            var defaultPosition=camera.transform.position;
            float defaultDistance=app.View.CameraDistance;
            input.BeginMouse(1,center,false,false);input.MoveMouse(center+new Vector2(48,16));input.EndMouse(center+new Vector2(48,16),false);
            yield return new WaitForEndOfFrame();
            Require(Quaternion.Angle(rotation,camera.transform.rotation)>1,"Right drag did not orbit");
            for(int i=0;i<2;i++)
            {
                var position=camera.transform.position;
                input.BeginMouse(i==0?0:2,center,i==0,false);input.MoveMouse(center+Vector2.right*35);input.EndMouse(center+Vector2.right*35,false);
                yield return new WaitForEndOfFrame();
                Require(Vector3.Distance(position,camera.transform.position)>.1f,"Desktop pan failed");
            }
            input.BeginMouse(1,foot,false,false);input.EndMouse(foot,false);
            input.BeginMouse(2,foot,false,false);input.EndMouse(foot,false);
            Require(Json.Write(app.Config)==original,"A camera gesture edited the formation");
            app.View.Zoom(3);
            Require(Mathf.Abs(defaultDistance-app.View.CameraDistance)>1&&input.Shortcut(KeyCode.R),
                "Arena R did not handle a changed zoom");
            yield return new WaitForEndOfFrame();
            Require(Quaternion.Angle(rotation,camera.transform.rotation)<.01f &&
                Vector3.Distance(defaultPosition,camera.transform.position)<.01f &&
                Mathf.Abs(defaultDistance-app.View.CameraDistance)<.01f,"Arena R did not restore the default camera");
            app.Selected.Clear();app.Tool=tool;app.Refresh();

            Screen.SetResolution(1280,720,FullScreenMode.Windowed);
            yield return WaitForState(()=>Screen.width==1280&&Screen.height==720,"16:9 viewport did not apply");
            yield return new WaitForEndOfFrame();
            CheckButtonHit("Settings.Quality");CheckButtonHit("Mode.Manor");
            foreach(var unit in app.Config.red)
            {
                Vector2 point=camera.WorldToScreenPoint(new Vector3(unit.x,app.Data.Height(app.Config.map,unit.x,unit.z),unit.z));
                Require(point.x>285&&point.x<1280&&point.y>0&&point.y<720&&UiHits(point).Count==0,"16:9 deployment slot is obstructed");
            }
            Screen.SetResolution(1280,800,FullScreenMode.Windowed);
            yield return WaitForState(()=>Screen.width==1280&&Screen.height==800,"16:10 viewport did not restore");
        }

        IEnumerator CheckQualityMenu()
        {
            Require(app.View.QualityLevel==2&&app.View.PreferredQuality==2,"Fresh native desktop did not launch on Fine");
            yield return Press("Settings.Quality");
            for(int level=0;level<=2;level++)
            {
                yield return Press("Quality."+level);
                int msaa=level==2?4:level==1?2:0;
                var res=level==2?ShadowResolution.VeryHigh:level==1?ShadowResolution.High:ShadowResolution.Medium;
                Require(app.View.QualityLevel==level&&app.View.PreferredQuality==level&&QualitySettings.antiAliasing==msaa&&QualitySettings.shadowResolution==res&&
                    (level==0?QualitySettings.shadowCascades<=1:QualitySettings.shadowCascades==msaa),"Graphics preset did not take effect");
            }
            var input=app.GetComponent<BattleInput>();
            var dismiss=VisibleButton("关闭");
            dismiss.interactable=false;
            Require(!input.Shortcut(KeyCode.Escape)&&app.Modal,"Escape bypassed a disabled dismissal");
            dismiss.interactable=true;
            Require(input.Shortcut(KeyCode.Escape)&&!app.Modal,"Escape did not close the quality menu");
        }

        Vector2 PlotPoint(int slot) => app.View.ViewCamera.WorldToScreenPoint(new Vector3(FrontierMap.ManorX+Manor.SlotX(slot),0,Manor.SlotZ(slot)));

        IEnumerator CheckManorShortcutsAndHover()
        {
            var input=app.GetComponent<BattleInput>();
            yield return Press("庄园工坊");
            var edit=app.GetComponentsInChildren<InputField>().Single(f=>f.gameObject.activeInHierarchy);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(edit.gameObject);
            edit.ActivateInputField();yield return null;
            Require(!input.Shortcut(KeyCode.Escape)&&app.Modal&&!input.Shortcut(KeyCode.Space)&&
                !input.Shortcut(KeyCode.Alpha3)&&!input.Shortcut(KeyCode.G)&&!input.Shortcut(KeyCode.P)&&!input.Shortcut(KeyCode.M)&&
                app.ManorSpeed==0,"Editing leaked a shortcut");
            edit.DeactivateInputField();UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            Require(!input.Shortcut(KeyCode.R)&&!input.Shortcut(KeyCode.Space)&&app.Modal,"Modal allowed a world shortcut");
            Require(input.Shortcut(KeyCode.Escape)&&!app.Modal,"Workshop did not close with Escape");
            Require(input.Shortcut(KeyCode.Space)&&app.ManorSpeed==1&&input.Shortcut(KeyCode.Space)&&app.ManorSpeed==0,"Estate pause or resume failed");
            var accent=VisibleButton("Frontier.View.0").image.sprite;
            foreach(int index in new[]{2,0,1})
            {
                Require(input.Shortcut((KeyCode)((int)KeyCode.Alpha1+index))&&app.View.FrontierFocus==index&&
                    VisibleButton("Frontier.View."+index).image.sprite==accent,"Camera shortcut or tab highlight failed");
            }
            yield return WaitForState(()=>!app.View.CameraMoving,"Half-island focus incomplete");
            yield return new WaitForEndOfFrame();
            int selected=app.ManorSlot;
            foreach(int slot in new[]{3,6})
            {
                Vector2 point=PlotPoint(slot);
                Require(UiHits(point).Count==0,"Plot under HUD");
                input.HoverAt(point,false);
                Require(app.View.HoveredPlot==slot&&app.ManorSlot==selected,"Hover did not identify an occupied or empty plot");
                input.HoverAt(point,true);Require(app.View.HoveredPlot==-1,"UI did not suppress hover");
                input.BeginMouse(1,point,false,false);input.HoverAt(point,false);
                Require(app.View.HoveredPlot==-1,"Camera gesture kept the hover hint");
                input.EndMouse(point,false);
            }
            Require(input.Shortcut(KeyCode.R)&&app.View.FrontierFocus==0&&VisibleButton("Frontier.View.0").image.sprite==accent,"R did not reset the preset");
        }

        IEnumerator CheckCameraState()
        {
            var selected=VisibleButton("Frontier.View.0").image.sprite;
            app.View.FrameFrontier(1);
            Require(VisibleButton("Frontier.View.1").image.sprite==selected,"Programmatic focus did not update tabs");
            yield return new WaitForEndOfFrame();
            app.View.FocusManorPlot(0);
            yield return WaitForState(()=>!app.View.CameraMoving,"Plot focus did not settle");
            yield return new WaitForEndOfFrame();
            float near=QualitySettings.shadowDistance;
            var nearFade=app.View.GroundFade;
            Require(nearFade.x<near&&nearFade.y>0&&nearFade.x+1/nearFade.y>near,
                "Grounding fade no longer overlaps the near shadow limit");
            app.View.FrameFrontier(2,true);
            Require(Mathf.Abs(near-QualitySettings.shadowDistance)<.01f,"Shadow distance jumped before the first transition frame");
            float intermediate=near;
            while(app.View.CameraMoving)
            {
                yield return null;
                if(app.View.CameraMoving)intermediate=QualitySettings.shadowDistance;
            }
            Require(intermediate>near && QualitySettings.shadowDistance>=intermediate-.01f,"Shadows did not track camera interpolation");
            yield return new WaitForEndOfFrame();
            var distantFade=app.View.GroundFade;
            Require(distantFade.x>nearFade.x&&distantFade.x<QualitySettings.shadowDistance&&
                distantFade.y>0&&distantFade.x+1/distantFade.y>QualitySettings.shadowDistance,
                "Grounding fade did not follow the shadow range");
            app.View.Focus(new Vector3(FrontierMap.ManorX,0,0),190,54,-10);
            yield return new WaitForEndOfFrame();
            Require(app.View.CachedAmbientInstances>0,"Distant population did not populate the cache");
            app.View.FrameFrontier(1);yield return new WaitForEndOfFrame();
            Require(app.View.CachedAmbientInstances==0,"Near view retained old far matrices");
            app.View.ResetCamera();
            Require(VisibleButton("Frontier.View.0").image.sprite==selected,"Reset did not restore the overview highlight");
        }

        IEnumerator CheckPortrait(bool captures)
        {
            Screen.SetResolution(390,844,FullScreenMode.Windowed);
            yield return WaitForState(()=>Screen.width==390&&Screen.height==844,"Portrait viewport not applied");
            yield return null;
            bool touchCamera=app.TouchCamera;
            yield return Press(touchCamera?"拖动视角  ⇄  切换点选":"点选布阵 / 军令  ⇄  切换视角");
            Require(app.TouchCamera!=touchCamera,"Compact input toggle failed");
            yield return Press("Mode.Manor");
            string saved=app.Estate.Export();
            var guide=Manor.Import(saved);
            guide.guideDismissed=false;
            guide.guideStep=1;
            app.ImportText(guide.Export());
            yield return CheckFrontierPreset(1);
            CheckButtonHit("Settings.Quality");CheckButtonHit("Frontier.View.1");CheckButtonHit("Frontier.View.2");CheckButtonHit("Manor.Patrol");
            CheckFloatingStatus();
            if(captures)yield return ManorShot("160-portrait-manor");
            yield return Press("Manor.Governance");
            CheckButtonHit("Manor.Decree.mobilize");CheckButtonHit("Manor.Work.balanced");
            CheckGovernanceViewport();
            if(captures)yield return ManorShot("160-portrait-governance");
            yield return Press("Manor.Milestones");
            CheckButtonHit("Manor.Milestones.Back");
            yield return Press("Manor.Milestones.Back");
            yield return Press("Manor.Specialists");
            CheckButtonHit("Manor.Specialist.Upgrade.steward");
            var specialists=HudElement<RectTransform>("领地专才").GetComponentInChildren<ScrollRect>();
            specialists.verticalScrollbar.value=0;
            yield return new WaitForEndOfFrame();
            CheckButtonHit("Manor.Specialist.Upgrade."+Manor.Minstrel);
            if(captures)yield return ManorShot("160-portrait-specialists");
            Require(app.GetComponent<BattleInput>().Shortcut(KeyCode.Escape)&&!app.Modal,"Portrait governance could not close");
            yield return Press("Manor.Patrol");
            CheckButtonHit("Manor.Force.Recommend");CheckButtonHit("Manor.Patrol.Start");
            Require(app.GetComponent<BattleInput>().Shortcut(KeyCode.Escape)&&!app.Modal,"Portrait muster could not close");
            yield return CheckPortraitCodex(captures);
            yield return CheckEconomy(captures,true);
            yield return CheckMerit(captures,true);
            app.ImportText(saved);
        }

        Text ManorReadout(string name) => app.GetComponentsInChildren<Text>().Single(label=>label.name==name);

        IEnumerator CheckGovernance(bool captures)
        {
            var estate=app.Estate;
            var input=app.GetComponent<BattleInput>();
            var forecast=ManorReadout("Manor.Ledger.0.Net");var hud=app.GetComponent<NativeHud>();
            hud.SetLedgerHover(true);
            Require(ManorReadout("Manor.Forecast.Factors").text.Contains("耗柴 2"),"Winter heating was missing from the ledger");
            hud.SetLedgerHover(false);
            int staticBuilds=app.View.StaticRebuilds;
            string standard=forecast.text;
            yield return Press("Manor.Governance");
            Require(app.Modal&&Displays("领地政务与集市")&&Displays("民心 "+estate.approval)&&
                ManorReadout("Manor.Nobility.Ledger").text==estate.NobleTitle&&Displays("冬柴足额 0"),
                "Governance did not bind approval and winter forecast");
            var marketControl=VisibleButton("Manor.Trade.Buy.food");
            yield return Press("Manor.Tax.high");
            Require(estate.tax=="high"&&Displays("税率 -3")&&forecast.text!=standard,"Tax change did not refresh the daily ledger");
            yield return Press("Manor.Tax.low");
            int baseArms=estate.Forecast().arms;
            yield return Press("Manor.Decree.mobilize");
            Require(estate.tax=="low"&&estate.decree=="mobilize"&&estate.Forecast().arms>baseArms&&Displays("政令 -1"),
                "Mobilization or the approval breakdown did not update");
            yield return Press("Manor.Decree.trade");
            var quote=estate.QuoteTrade("food");
            int quota=estate.MarketSeasonLimit;
            string BuyLeft(int used)=>Manor.FormatHundredths(quote.buyPrice)+" 金  ·  余 "+(quota-used);
            string SellLeft(int used)=>Manor.FormatHundredths(quote.sellPrice)+" 金  ·  余 "+(quota-used);
            int margin=quote.buyPrice-quote.sellPrice;
            Require(quota==35&&estate.decree=="trade"&&quote.buyPrice==265&&quote.sellPrice==220&&Displays(BuyLeft(0)),
                "Market did not apply the trade decree");
            yield return Press("Manor.Work.balanced");
            Require(estate.slots[3].workers==1&&estate.slots[5].workers>0&&estate.slots[8].workers>0&&estate.slots[0].workers>0&&
                estate.FreeWorkers==0&&VisibleButton("Manor.Trade.Buy.food")==marketControl,"Balanced staffing did not update the estate in place");
            int purse=estate.coin*100+estate.remainder.coin,food=estate.food;
            yield return Press("Manor.Trade.Buy.food");
            Require(estate.food==food+1&&estate.coin*100+estate.remainder.coin==purse-quote.buyPrice&&
                ManorReadout("Manor.Ledger.1").text==(food+1).ToString()&&ManorReadout("Manor.Ledger.0").text==Manor.FormatHundredths(purse-quote.buyPrice)&&
                Displays("粮食  "+(food+1))&&Displays(BuyLeft(1)),"Buying did not update stock, purse and quota in place");
            yield return Press("Manor.Trade.Sell.food");
            Require(estate.food==food&&estate.coin*100+estate.remainder.coin==purse-margin&&
                ManorReadout("Manor.Ledger.1").text==food.ToString()&&ManorReadout("Manor.Ledger.0").text==Manor.FormatHundredths(purse-margin)&&
                Displays(SellLeft(1))&&VisibleButton("Manor.Trade.Buy.food")==marketControl&&
                app.View.StaticRebuilds==staticBuilds,"Selling did not refresh in place or preserve scene batches");
            yield return Press("Manor.Trade.Buy5.food");
            Require(estate.food==food+5&&estate.coin*100+estate.remainder.coin==purse-margin-5*quote.buyPrice&&
                ManorReadout("Manor.Ledger.1").text==(food+5).ToString()&&Displays(BuyLeft(6)),"Bulk purchase did not settle atomically");
            yield return Press("Manor.Trade.Sell5.food");
            Require(estate.food==food&&estate.coin*100+estate.remainder.coin==purse-6*margin&&
                Displays(SellLeft(6))&&estate.market.food.sold==6&&app.View.StaticRebuilds==staticBuilds,
                "Bulk sale did not update stock and quota");
            Require(estate.Export()==PlayerPrefs.GetString(FrontierSaveKey),"Governance changes were not saved immediately");
            yield return Press("Manor.Tax.high");
            Require(input.Shortcut(KeyCode.Escape)&&!app.Modal&&!marketControl.gameObject.activeInHierarchy,"Escape did not close governance");
            yield return PickPlot(0);
            yield return Press("Manor.Hall.Governance");
            Require(app.Modal&&Displays("领地政务与集市")&&input.Shortcut(KeyCode.Escape)&&!app.Modal,
                "The manor hall shortcut did not open the same dismissible dialog");
            Debug.Log("FRONTLINE_GOVERNANCE_SMOKE_OK approval="+estate.approval+" balanced=true tradeCost="+
                Manor.FormatHundredths(6*margin)+" quota="+(quota-6)+"/"+(quota-6));
        }

        IEnumerator CheckSeasonalDispatch(bool captures)
        {
            for(int i=0;i<5;i++)yield return Press("Manor.Day");
            var estate=app.Estate;
            Require(!app.Modal&&estate.eventDay==42&&estate.ActiveEvent?.title=="寒潮来袭","Dispatch did not arrive silently on day six");
            CheckButtonHit("Manor.Event");
            yield return Press("Manor.Governance");
            Require(ManorReadout("Manor.Todos.Priority").text=="待办 · 领地急报","Pending dispatch did not outrank other actions");
            yield return Press("Manor.Governance.Event");
            Require(app.Modal&&Displays("领地急报 · 寒潮来袭")&&Displays("建材 -4 · 民心 +6"),"Winter dispatch options did not describe the costs");
            if(captures)yield return ManorShot("142-dispatch");
            int approval=estate.approval, timber=estate.timber;
            yield return Press("Manor.Event.Choose.0");
            Require(estate.eventDay==0&&estate.approval==approval+6&&estate.timber==timber-4&&
                app.Modal&&Displays("领地政务与集市")&&estate.Export()==PlayerPrefs.GetString(FrontierSaveKey),
                "Winter dispatch choice did not settle and save");
            yield return Press("Manor.Tax.normal");
            Require(app.GetComponent<BattleInput>().Shortcut(KeyCode.Escape)&&!app.Modal,"Escape did not close returning governance");
            Debug.Log("FRONTLINE_DISPATCH_SMOKE_OK day=42 approval="+approval+"->"+estate.approval+" timber=-4");
        }

        IEnumerator CheckExpandedEstate(bool captures)
        {
            yield return CheckFrontierPreset(1);
            for(int index=0;index<Manor.PlotCount;index++)
            {
                Vector2 screen=PlotPoint(index);
                Require(screen.x>0&&screen.x<1280&&screen.y>0&&screen.y<800&&UiHits(screen).Count==0&&
                    app.View.Ground(screen,out var position)&&Manor.PickSlot(position.x-FrontierMap.ManorX,position.z)==index,
                    "Expanded estate hides plot "+index+" at "+screen);
            }
            yield return BuildPlot(14,"granary");
            yield return BuildPlot(15,"cottage");
            yield return BuildPlot(16,"chapel");
            for(int i=2;i<=3;i++)
            {
                yield return Press("Manor.Upgrade");yield return Press("Manor.ConfirmUpgrade");
            }
            yield return BuildPlot(17,"timber_camp");
            yield return PickPlot(14);
            for(int i=2;i<=3;i++)
            {
                yield return Press("Manor.Upgrade");yield return Press("Manor.ConfirmUpgrade");
            }

            yield return Press("Manor.Governance");yield return Press("Manor.Work.balanced");yield return Press("关闭");
            yield return PickPlot(5);yield return Press("Manor.Worker.Minus");
            yield return PickPlot(14);yield return Press("Manor.Worker.Plus");
            var estate=app.Estate;
            var f=estate.Forecast();
            app.GetComponent<NativeHud>().SetLedgerHover(true);
            Require(estate.slots[14].level==3&&estate.slots[16].level==3&&f.synergy.grainPercent==12&&
                f.synergy.cropPercent==8&&f.synergy.forgePercent==10&&f.synergy.settledApproval==1&&
                ManorReadout("Manor.Forecast.Synergy").text.Contains("谷仓产粮 +12%"),"Expanded estate links did not reach the ledger");
            app.GetComponent<NativeHud>().SetLedgerHover(false);

            yield return Press("Manor.Governance");
            if(captures)yield return ManorShot("145-governance");
            int tax=estate.Forecast().coin;
            yield return Press("Manor.Specialists");
            foreach(string id in Manor.SpecialistIds)
            {
                yield return Press("Manor.Specialist.Upgrade."+id);
                Require(estate.SpecialistLevel(id)==2&&ManorReadout("Manor.Specialist.Level."+id).text.EndsWith("Lv.2"),"Specialist status did not update: "+id);
            }
            Require(estate.Forecast().coin>tax&&estate.MarketSeasonLimit==40&&estate.Logistics.recoveryRate==195&&
                estate.ExpeditionLogistics(true).raidPercent==4&&PlayerPrefs.GetString(FrontierSaveKey)==estate.Export(),
                "Specialist bonuses did not refresh or save");
            if(captures)yield return ManorShot("142-specialists");
            yield return Press("Manor.Specialists.Back");yield return Press("关闭");
            int seen=0;
            int allSpecialists=(1<<Manor.SpecialistIds.Length)-1;
            yield return WaitForState(()=>{seen|=app.View.VisibleSpecialistMask;return seen==allSpecialists;},"One or more specialists never appeared on the estate paths");
            if(captures)
            {
                yield return ManorShot("142-estate-overview");
                app.View.Focus(new Vector3(FrontierMap.ManorX,.75f,-1.16f),15.5f,38,-12);
                yield return ManorShot("142-market-square");
                app.View.Focus(new Vector3(FrontierMap.ManorX-17.14f,.84f,2.22f),23,40,27);
                yield return ManorShot("142-western-landmarks");
                app.View.Focus(app.View.SpecialistPoint(0)+Vector3.up*.75f,7.6f,26,-12);
                yield return ManorShot("142-steward");
            }
            yield return CheckFrontierPreset(1);
            yield return Press("Manor.Raid");
            Require(ManorReadout("Manor.March.Logistics").text.Contains("巡庄 +4%"),"Raid did not display the watchman's budget");
            yield return Press("关闭");
            Debug.Log("FRONTLINE_ESTATE_SMOKE_OK plots="+Manor.PlotCount+" specialists="+seen+" crop=8 grain=12 forge=10");
        }

        int outerCloseupCulled;

        IEnumerator CheckOuterEstate(bool captures)
        {
            int north=Manor.FirstOuterPlot, south=north+2;
            var estate=app.Estate;
            yield return Press("Manor.Plots");
            Require(Displays("建地 18\n待开垦"),"Plot list did not show the frontier woodland state");
            yield return Press("Manor.Slot."+north);
            Require(app.ManorSlot==north&&app.View.FrontierFocus==1&&app.View.CameraMoving,
                "Selecting a plot from the list did not animate the camera");
            yield return WaitForState(()=>!app.View.CameraMoving,"Plot focus did not finish");
            app.View.MeasureCasterVertices=true;
            yield return new WaitForEndOfFrame();
            Vector2 near=PlotPoint(north);
            outerCloseupCulled=app.View.CulledStaticInstances;
            app.View.MeasureCasterVertices=false;
            Require(Vector2.Distance(near,new Vector2(640,400))<100&&UiHits(near).Count==0&&outerCloseupCulled>0&&
                !app.View.LowSceneryLod,"Outer plot focus did not centre the high-detail plot or cull far geometry");
            Require(VisibleButton("Manor.Reclaim").IsInteractable()&&!estate.IsPlotCleared(north)&&
                (estate.ReadyMilestones&1)==0,"Locked land is not presented as reclaimable");
            if(captures)yield return ManorShot("142-outer-clearing");

            int coin=estate.coin, timber=estate.timber;
            yield return Press("Manor.Reclaim");
            Require(estate.IsPlotCleared(north)&&estate.coin==coin-Manor.ReclaimPrice&&estate.timber==timber+Manor.ReclaimTimber&&
                PlayerPrefs.GetString(FrontierSaveKey)==estate.Export(),"Reclamation did not update the ledger and save");
            yield return BuildPlot(north,"farm");
            yield return Press("Manor.Plots");yield return Press("Manor.Slot."+south);
            yield return WaitForState(()=>!app.View.CameraMoving,"South plot focus did not finish");
            yield return Press("Manor.Reclaim");
            Require(estate.IsPlotCleared(south)&&(estate.ReadyMilestones&1)!=0,"Two reclaimed plots did not qualify");

            yield return Press("Manor.Governance");yield return Press("Manor.Milestones");
            Require(VisibleButton("Manor.Milestone.0").IsInteractable(),"Milestone list did not show the earned reward");
            Canvas.ForceUpdateCanvases();
            foreach(var milestone in Manor.Milestones)
            {
                var label=app.GetComponentsInChildren<Text>().Single(t=>t.text==milestone.title);
                Require(label.preferredHeight<=label.rectTransform.rect.height,"Clipped milestone title: "+milestone.title);
            }
            coin=estate.coin;timber=estate.timber;
            yield return Press("Manor.Milestone.0");
            Require(estate.MilestoneClaimed(0)&&estate.coin==coin+Manor.Milestones[0].coin&&estate.timber==timber+Manor.Milestones[0].timber&&
                !VisibleButton("Manor.Milestone.0").IsInteractable()&&PlayerPrefs.GetString(FrontierSaveKey)==estate.Export(),
                "Milestone reward was not applied, disabled and saved");
            if(captures)yield return ManorShot("142-milestones");
            Require(app.GetComponent<BattleInput>().Shortcut(KeyCode.Escape)&&!app.Modal,"Escape did not close the milestones");
            if(captures)
            {
                app.View.Focus(new Vector3(FrontierMap.ManorX-6,.28f,-17.0f),26,48,-34);
                yield return ManorShot("142-south-quay");
                yield return CheckFrontierPreset(1);
                yield return ManorShot("142-expanded-peninsula");
                yield return CheckFrontierPreset(0);
                yield return ManorShot("142-estate-and-frontier");
            }
            Debug.Log("FRONTLINE_MILESTONE_SMOKE_OK plots="+Manor.PlotCount+" reclaimed="+estate.reclaimedPlots+
                " claims="+estate.milestoneClaims+" closeupCulled="+outerCloseupCulled);
        }

        IEnumerator CheckRecommendedParty(bool frontier,bool captures)
        {
            if(!frontier)yield return Press("Manor.Stage.3");
            yield return Press("Manor.Force.Recommend");
            int limit=frontier?app.Estate.FrontierLimit:app.Estate.FieldLimit;
            Require(app.PartyUnits(frontier)>0&&app.PartyUnits(frontier)<=limit&&app.PartyCost(frontier)<=app.Estate.Budget&&
                Manor.HeroIds.All(id=>app.ForceCount(id,frontier)==1),"Recommended party exceeded supply or omitted an available hero");
            Require(Displays(frontier?"远程偏多 · 宜配骑兵或盾卫":"敌军有骑兵 · 宜配长枪兵拦截冲锋")&&
                ManorReadout("Manor.March.Logistics").text.Contains("士气 +8%")&&ManorReadout("Manor.March.Logistics").text.Contains("伤员救回 65%"),
                "Muster did not explain the counter or logistics");
            CheckButtonHit(frontier?"Manor.Patrol.Start":"Manor.March");
            if(captures)yield return ManorShot(frontier?"145-patrol-roster":"142-expedition-roster");
            Debug.Log("FRONTLINE_RECOMMEND_SMOKE_OK "+(frontier?"patrol":"expedition")+" units="+app.PartyUnits(frontier)+
                " budget="+app.PartyCost(frontier)+"/"+app.Estate.Budget);
        }

        IEnumerator CaptureArenaMaps(string expectedArena)
        {
            string smokeOutput=output;
            output=Option("--qa-maps",Path.Combine(Path.GetDirectoryName(Path.GetFullPath(smokeOutput)),"maps"));
            try
            {
                app.SetMap("bridge");app.View.Focus(new Vector3(1,-.62f,0),37,45,-24);
                yield return ManorShot("142-map-bridge");
                app.SetMap("marsh");app.View.Focus(new Vector3(.85f,0,0),40,56,13);
                yield return ManorShot("142-map-marsh");
                app.SetMap("highland");app.View.Focus(new Vector3(.6f,.78f,0),37,47,-19);
                yield return ManorShot("142-map-highland");
            }
            finally
            {
                output=smokeOutput;app.SetMap("meadow");app.View.ResetCamera();
                Require(Json.Write(app.Config)==expectedArena,"Map stills changed the arena formation");
            }
        }

        IEnumerator RunFrontierSmoke()
        {
            output=Option("--qa-out",Path.Combine(Application.persistentDataPath,"FrontierSmoke"));
            PlayerPrefs.DeleteKey(FrontierSaveKey);
            FrontlineApp.ResetManorQaSlots();
            app.Muted=true;
            yield return null;
            if(Screen.width!=1280||Screen.height!=800)Screen.SetResolution(1280,800,FullScreenMode.Windowed);
            yield return WaitForState(()=>Screen.width==1280&&Screen.height==800,"Desktop viewport not applied");
            if(Application.platform==RuntimePlatform.WindowsPlayer)
            {
                Require(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Direct3D11&&
                    SystemInfo.renderingThreadingMode==UnityEngine.Rendering.RenderingThreadingMode.LegacyJobified,
                    "The Windows smoke test did not start with D3D11 Legacy Graphics Jobs");
                Require(app.View.SupportedDesktopShaders,"The D3D11 instanced, shadow or atmosphere passes are missing");
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--glass-hud-only")>=0)
            {
                yield return CheckGlassHud();
#if !UNITY_WEBGL && !UNITY_EDITOR
                Application.Quit(0);
#endif
                yield break;
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--wide-world-only")>=0)
            {
                yield return CheckWideWorld();
#if !UNITY_WEBGL && !UNITY_EDITOR
                Application.Quit(0);
#endif
                yield break;
            }
            sharedHudAtlas=VisibleButton("Mode.Manor").image.sprite.texture;
            yield return CheckQualityMenu();
            yield return CheckPresentationSettings();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--presentation-only")>=0)
            {
#if !UNITY_WEBGL && !UNITY_EDITOR
                Application.Quit(0);
#endif
                yield break;
            }
            yield return CheckDesktopControls();
            string arena=Json.Write(app.Config);
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--no-smoke-images")<0)
                yield return CaptureArenaMaps(arena);

            yield return Press("Mode.Manor");
            yield return Press("Manor.Speed.0");
            bool artOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"--art-framing-only")>=0;
            bool captures=Array.IndexOf(Environment.GetCommandLineArgs(),"--no-smoke-images")<0;
            CheckFloatingStatus();
            yield return Press("Settings.Quality");
            CheckButtonHit("Quality.2");
            CheckModalSkin("显示与声音");
            if(captures)yield return ManorShot("145-settings");
            yield return Press("关闭");
            if(!artOnly)yield return CheckGuideAndSlots();
            var starting=new Manor {coin=3000,timber=1200,food=120,influence=90,cleared=3,day=36,guideDismissed=true};
            starting.Upgrade(0);starting.Upgrade(0);
            starting.stock["militia"]=16;
            starting.Build(8,"irrigated_field");starting.SetRotation(8,"intensive");starting.Build(9,"pasture");
            starting.Build(4,"drill_yard");starting.Upgrade(4);starting.Upgrade(4);
            starting.Build(1,"cottage");starting.Upgrade(1);starting.Upgrade(1);
            starting.workers=7;
            starting.Build(5,"smithy");starting.Upgrade(5);starting.Upgrade(5);starting.Assign(5,1);
            starting.Build(3,"tavern");
            starting.Assign(3,1);starting.Upgrade(3);starting.Upgrade(3);
            foreach(var hero in app.Data.Data.heroes)starting.HireHero(3,hero);
            app.ImportText(starting.Export());
            yield return CheckFrontierPreset(1);
            yield return BuildPlot(2,"farm");
            for(int i=0;i<3;i++)yield return Press("Manor.Worker.Plus");
            yield return Press("Manor.Rotation.intensive");
            app.View.ResetCamera();
            int displayLimit=app.Estate.FrontierLimit;
            Require(displayLimit==14 && app.View.FrontierFocus==0 && app.View.ViewingFrontier,"Opening frontier overview missing");
            yield return new WaitForEndOfFrame();
            foreach(float x in new[]{FrontierMap.ManorX,FrontierMap.BridgeStart,47f})
            {
                var screen=app.View.ViewCamera.WorldToViewportPoint(new Vector3(x,0,0));
                Require(screen.x>0 && screen.x<1 && screen.y>0 && screen.y<1,"Overview clipped a region");
            }
            Require(app.View.RenderInstances>100,"Frontier scene was not rendered");
            yield return WaitForState(()=>app.View.VisibleFarmers>=2&&app.View.VisibleFarmers+app.View.VisibleVillagers>=6,
                "Overview is missing households on its resident routes");
            yield return CheckManorShortcutsAndHover();
            yield return CheckCameraState();
            yield return CheckFrontierPreset(2);
            yield return CheckFrontierPreset(0);
            yield return CheckFrontierPreset(1);
            var farm=new Vector3(FrontierMap.ManorX+Manor.SlotX(2),.92f,Manor.SlotZ(2)-.6f);
            if(artOnly){app.View.Focus(farm,8.8f,31,-28);yield return ManorShot("farm-level-one");yield return CheckFrontierPreset(1);}
            yield return PickPlot(2);
            yield return Press("Manor.Upgrade");
            Require(Displays("5 粮 / 户·日 · 2 驻军 · 1 行军\n→ 5.5 粮 / 户·日 · 3 驻军 · 2 行军"),"Capacity preview differs from upgrade");
            yield return Press("Manor.ConfirmUpgrade");
            yield return Press("Manor.Upgrade");yield return Press("Manor.ConfirmUpgrade");
            yield return CheckAutumnWinterLight(captures);
            Require(app.Estate.Season==3&&app.Estate.approval==66&&app.Estate.Forecast().winterTimber==200,
                "Autumn settlement did not transition the winter ledger");
            app.View.Focus(new Vector3(FrontierMap.ManorX+3.0f,1.02f,4.9f),15.5f,35,-14);
            if(captures)
            {
                yield return null;
                var point=PlotPoint(3);
                app.GetComponent<BattleInput>().HoverAt(point,UiHits(point).Count>0);
                Require(app.View.HoveredPlot==3&&app.ManorSlot==2,"Estate screenshot lost hover or selection");
                int builds=app.View.StaticRebuilds;
                app.GetComponent<NativeHud>().UpdateManorHover();
                Require(Displays("建地 3  英雄酒馆  Lv.3  ·  已满级  ·  派工 1 / 1"),"Manor hover status omitted level or assignment");
                Require(app.View.StaticRebuilds==builds,"Hover triggered a static batch rebuild");
                yield return ManorShot("145-manor");
                app.View.Focus(new Vector3(FrontierMap.ManorX+Manor.SlotX(5),.66f,Manor.SlotZ(5)),9.5f,35,-29);
                yield return ManorShot("142-desktop-forge");
            }
            displayLimit=app.Estate.FrontierLimit;
            Require(displayLimit==16,"Farm improvement did not grant march capacity");
            if(!artOnly)
            {
                yield return CheckFrontierPreset(1);
                yield return CheckGovernance(captures);
                yield return CheckSeasonalDispatch(captures);
                yield return CheckExpandedEstate(captures);
                yield return CheckOuterEstate(captures);
                yield return CheckManorDevelopment(captures);
                yield return CheckFertilityAndSupport(captures);
            }
            yield return CheckFrontierPreset(0);
            if(artOnly)
            {
                Debug.Log("FRONTLINE_ART_FRAMING_OK "+Screen.width+"x"+Screen.height);
#if !UNITY_WEBGL && !UNITY_EDITOR
                Application.Quit(0);
#endif
                yield break;
            }
            yield return Press("Manor.Expedition");
            yield return CheckRecommendedParty(false,captures);
            yield return Press("关闭");
            yield return Press("Manor.Patrol");
            yield return CheckRecommendedParty(true,captures);
            yield return CheckRosterScroll();
            // Keep the established contact fixture after verifying the player can edit the recommendation.
            foreach(string id in Manor.HeroIds.Concat(new[]{"archer"}))
                while(app.ForceCount(id,true)>0)yield return Press("Manor.Force.Minus."+id);
            int wanted=displayLimit-app.PartyUnits(true);
            for(int i=0;i<wanted;i++)yield return Press("Manor.Force.Plus.militia");
            Require(app.PartyUnits(true)==displayLimit && Displays("110 军费 · 已达巡边人数上限"),"Patrol limit is not visible at muster");
            var plus=app.GetComponentsInChildren<Button>().Single(b=>b.name=="Manor.Force.Plus.archer"&&b.gameObject.activeInHierarchy);
            Require(!plus.IsInteractable(),"Muster exceeded the displayed cap");
            yield return Press("Manor.Force.Minus.militia");
            yield return Press("Manor.Force.Plus.archer");
            yield return Press("Manor.Patrol.Start");
            var battle=app.Battle;
            Require(app.InFrontier && battle.World is UnityWorld && battle.Config.red.Count==displayLimit &&
                battle.Config.blue.Count==15 && battle.Units.Take(displayLimit).All(u=>FrontierMap.AtManor(u.entity.body.Position)),
                "Party or guards did not spawn in the live PhysX world");
            var allies=battle.Units.Take(displayLimit).ToArray();
            var mouth=battle.Units.Skip(displayLimit).Take(5).ToArray();
            var farther=battle.Units.Skip(displayLimit+5).ToArray();
            var controls=app.GetComponent<BattleInput>();
            Require(controls.Shortcut(KeyCode.Alpha2)&&app.OrderType=="focus"&&app.Tool=="order","Patrol number key did not select its order");
            yield return Press("Frontier.Advance");
            Require(controls.Shortcut(KeyCode.R)&&app.View.FrontierFocus==0,"Patrol reset failed");
            int terrainBuilds=app.View.StaticRebuilds;

            yield return Press("Frontier.Speed");
            yield return Press("Frontier.Speed");
            app.View.CheckParallelPoses=app.View.ParallelPoseWorkers>1;
            yield return ClickBattlefield(-13,0);
            yield return new WaitForEndOfFrame();
            if(app.View.CheckParallelPoses)
                Require(app.View.CheckedParallelPoses>=displayLimit+FrontierMap.Guards.Count,"Live pose parity test did not run");
            app.View.CheckParallelPoses=false;
            yield return WaitForState(()=>allies.All(u=>u.entity.body.Position.x>-19),"Party stalled before the bridge");
            Require(allies.All(u=>FrontierMap.Safe(u.entity.body.Position) && u.entity.hp==u.entity.maxHp) &&
                mouth.All(u=>u.target==null) && battle.Shots.Count==0,"Enemy acquired or damaged a safe party");

            var westCamp=FrontierMap.CampCenter(0);
            yield return ClickBattlefield(westCamp.x,westCamp.z);
            int forwardStart=battle.Tick;
            yield return WaitForState(()=>mouth.Any(u=>u.target!=null) && allies.Any(u=>u.entity.stats.damageDealt>0 || u.entity.stats.damageTaken>0),
                "Leaving the causeway did not trigger combat");
            Require(allies.Any(u=>!FrontierMap.Safe(u.entity.body.Position)) && farther.All(u=>u.target==null),"Patrols aggroed outside their own sector");
            app.View.Focus(BattleView.CampCenter(0)+new Vector3(-10,.65f,0),25,40,-16);
            yield return new WaitForEndOfFrame();
            if(Application.platform==RuntimePlatform.WindowsPlayer)
            {
                Require(QualitySettings.antiAliasing==4&&QualitySettings.shadowResolution==ShadowResolution.VeryHigh&&
                    QualitySettings.shadowCascades==4&&app.View.ViewCamera.allowMSAA&&app.View.ViewCamera.nearClipPlane==.5f,
                    "Fine graphics settings regressed");
                Require(!app.View.LowActorLod&&!app.View.LowSceneryLod&&app.View.ShadowOnlySubmissions>0,
                    "Fine main view lost high meshes or did not use separate shadow casters");
            }
            if(captures)yield return ManorShot("142-desktop-contact");
            yield return WaitForState(()=>allies.All(u=>!u.entity.alive||!FrontierMap.Safe(u.entity.body.Position)),
                "The last file stalled inside the causeway");
            int forwardTicks=battle.Tick-forwardStart;

            yield return WaitForState(()=>mouth.All(u=>!u.entity.alive)&&app.View.AlertCampMask==6,"Cleared camp retained its warning ring");
            Require(farther.All(u=>u.entity.alive)&&app.View.StaticRebuilds==terrainBuilds,"Clearing one camp affected another camp or rebuilt terrain");
            yield return CheckFrontierPreset(2);
            if(captures)yield return ManorShot("142-desktop-clearance");
            yield return Press("Frontier.Order.inspire");
            yield return ClickBattlefield(westCamp.x+2,westCamp.z+9);
            Require(battle.Commander.log.Any(o=>o.type=="inspire"&&o.x>40),"Farther terrain rejected the army order");
            Require(app.Tool=="march" && app.Message=="军令已释放，已切回推进模式","Order did not restore march");
            yield return ClickBattlefield(westCamp.x-4,westCamp.z-7);
            Require(battle.Commander.log.Count==1 && battle.MarchDestination.Value.x>40,"Next click did not move the party");
            yield return Press("Frontier.Recall");
            int recallStart=battle.Tick;
            yield return WaitForState(()=>allies.Any(u=>u.entity.alive) && allies.All(u=>!u.entity.alive||FrontierMap.AtManor(u.entity.body.Position)),
                "Survivors could not withdraw across the bridge");
            foreach(var enemy in battle.Units.Skip(displayLimit))
            {
                Require(enemy.target==null,"Guard "+enemy.entity.id+" still targets a recalled soldier; alive="+enemy.entity.alive);
                if(enemy.entity.alive)
                    Require(!FrontierMap.Safe(enemy.entity.body.Position)&&!FrontierMap.GuardCrossedGate(enemy.entity.body.Position),
                        "Guard crossed gate at "+enemy.entity.body.Position.x);
            }
            int ticks=battle.Tick;
            int beforeReturn=app.Estate.approval;
            int defeated=battle.Units.Skip(displayLimit).Count(u=>!u.entity.alive);
            int approvalGain=Math.Min(6,defeated/5*2);
            Require(app.Estate.clearedSectors==0,"A live camp defeat already modified the estate");
            yield return Press("Frontier.Settle");
            Require(app.Phase=="manor" && app.Modal && Displays("巡边结算清单") &&
                app.Estate.Export()==PlayerPrefs.GetString(FrontierSaveKey) && app.Estate.cleared==3,"Withdrawal did not settle and save");
            Require(app.Estate.version==Manor.SaveVersion&&app.Estate.tax=="normal"&&app.Estate.decree=="trade"&&
                app.Estate.eventDay==0&&app.Estate.market.food.bought==6&&app.Estate.market.food.sold==6,"Settlement lost governance or market state");
            Require(approvalGain>0&&app.Estate.approval==beforeReturn+approvalGain&&
                ManorReadout("Manor.Settlement.Approval").text=="民心 "+beforeReturn+" → "+app.Estate.approval+"（+"+approvalGain+"）",
                "Patrol report did not display the approval gain");
            Require(app.Estate.clearedSectors==1&&app.Estate.claimedSectors==0&&
                ManorReadout("Manor.Settlement.Claims").text=="新开放宣称 · 西桥前哨","The settled first camp did not unlock its territory");
            if(captures)yield return ManorShot("145-settlement");
            yield return Press("Manor.Settlement.Done");
            Require(app.View.AlertCampMask==7,"Manor preview did not restore all patrol markers");
            yield return CheckClaimsAndWeather(captures);
            yield return Press("Manor.Expedition");
            Require(Displays("1  风起草甸"),"Six-mission expedition is no longer accessible");
            yield return Press("关闭");
            yield return CheckNobilityAndCodex(captures);
            yield return CheckExpeditionLeaders();
            yield return CheckManorQuality(captures);
            yield return Press("Mode.Duel");
            Require(Json.Write(app.Config)==arena&&app.View.Weather==ManorWeather.Clear&&app.View.WeatherParticles==0,
                "Manor state or weather leaked into the arena");
            yield return CheckArenaCodex(captures);
            yield return CheckHudArena(captures);
            yield return CheckArenaLandscape(captures);
            yield return CheckBattleQuality(captures);

            Require(Screen.width==1280&&Screen.height==800,"Main smoke switched away from the desktop viewport");
            yield return CheckPortrait(captures);
            int portraitWidth=Screen.width,portraitHeight=Screen.height;
            Screen.SetResolution(1280,800,FullScreenMode.Windowed);
            yield return WaitForState(()=>Screen.width==1280&&Screen.height==800,"Desktop viewport did not restore");
            var proof=new {version=Application.version,unity=Application.unityVersion,width=Screen.width,height=Screen.height,
                portraitWidth,portraitHeight,quality=app.View.QualityLevel,desktopMouse=true,presetsSynchronized=true,ambientCleared=true,
                api=SystemInfo.graphicsDeviceType.ToString(),graphicsJobMode=SystemInfo.renderingThreadingMode.ToString(),
                arenaReset=true,groundFadeVerified=true,checkedParallelPoses=app.View.CheckedParallelPoses,
                nearClip=app.View.ViewCamera.nearClipPlane,msaa=QualitySettings.antiAliasing,
                shadowResolution=QualitySettings.shadowResolution.ToString(),shadowCascades=QualitySettings.shadowCascades,
                escapeAndEditing=true,manorHotkeys=true,hoverVerified=true,clearedCampMask=6,previewCampMask=7,
                capacity=displayLimit,enemyCount=FrontierMap.Guards.Count,ticks,forwardTicks,recallTicks=ticks-recallStart,
                safeGate=true,localAggro=true,returnSaved=true,governanceVerified=true,winterVerified=true,
                dispatchDay=42,staffingVerified=true,bulkTradeVerified=true,patrolRecommendation=true,expeditionRecommendation=true,
                plotCount=app.Estate.slots.Length,granary=app.Estate.slots[14].level,chapel=app.Estate.slots[16].level,
                specialistRanks=app.Estate.specialists,estateLinksVerified=true,
                widthMetres=Manor.PlateauHalfWidth*2,depthMetres=Manor.PlateauHalfDepth*2,bridgeStart=FrontierMap.BridgeStart,
                saveVersion=app.Estate.version,reclaimedPlots=app.Estate.reclaimedPlots,milestoneClaims=app.Estate.milestoneClaims,
                outerPlotConstruction=app.Estate.slots[Manor.FirstOuterPlot].kind,outerCloseupCulled,
                proceduralClips=verifiedAudioClips,displaySettingsVerified,localSlotsVerified,guideVerified,
                tierSelectionVerified,seasonalLightingVerified,qaSilent=app.Muted,
                backyardVerified,developmentVerified,marketNeedsVerified,chronicleVerified,
                fertilityVerified,retinueVerified,influenceVerified,mercenaryVerified,governanceBottomMargin,
                claimsVerified,todosVerified,hoverDetailsVerified,weatherVerified,weatherAllocationBytes,weatherBudgets,
                modalSkinVerified,rosterScrollVerified,portraitStatusVerified,portraitGovernanceBottomMargin,
                outpostPickingVerified,verifiedGarrisons,flurryAllocationBytes,
                nobilityVerified,codexTabsVerified,arenaCodexVerified,portraitCodexVerified,dukePlotsBuilt,rankSounds,nobleDetails,
                nobleRank=app.Estate.nobility,grantedPlots=app.Estate.GrantedPlots,
                craftBuildingsVerified,codexLineBreaksVerified,specialistCloseupMask,heroCloseupMask,retinueCloseup,arenaCloseup,
                forgedTiers,forgeClips,ledgerVerified,plotFiltersVerified,caravanVerified,portraitEconomyVerified,armedPatrolVerified,landscapeVerified,
                meritVerified,portraitMeritVerified,extraShortcutsVerified,idleBadgeVerified,helpKeysVerified,bridgeWaterVerified,
                impactReviewVerified,portraitReportVerified,expeditionLeadersVerified,orderAccentMask,impactAccentMask,reportJumps,
                peakAccentInstances,accentCasters=app.View.AccentCasters,
                crowdLow=new {mix60.attempts,mix60.accepted,mix60.peak,mix60.minGain},
                crowdHigh=new {mix120.attempts,mix120.accepted,mix120.peak,mix120.minGain},
                clearedSectors=app.Estate.clearedSectors,claimedSectors=app.Estate.claimedSectors,
                barnyard=app.Estate.slots[1].backyard,developmentMask=app.Estate.developmentMask,
                chronicleEntries=app.Estate.chronicle.Count,
                beforeReturn,defeated,approvalGain,approval=app.Estate.approval,tax=app.Estate.tax,decree=app.Estate.decree,
                market=app.Estate.market,actions=manorActions};
            yield return Save("frontier-smoke.json",Encoding.UTF8.GetBytes(Json.Write(proof)),"application/json");
            Debug.Log("FRONTLINE_FRONTIER_SMOKE_OK "+Screen.width+"x"+Screen.height+" party="+displayLimit+" guards="+FrontierMap.Guards.Count+" ticks="+ticks);
#if !UNITY_WEBGL && !UNITY_EDITOR
            Application.Quit(0);
#endif
        }
    }
}
