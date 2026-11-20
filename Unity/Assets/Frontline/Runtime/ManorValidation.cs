using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Frontline.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        // Interactive file-dialog QA shares the shipping UI while keeping acceptance saves out of the player's manor.
        internal static bool IsolatedManor => ManorSmokeRequested || ManorRequested || Array.IndexOf(Environment.GetCommandLineArgs(),"--manor-file-qa") >= 0;
        readonly List<object> manorActions = new List<object>();
        string Prefix => "v10-" + Application.platform + "-" + Screen.width + "-";
        IEnumerator ManorShot(string name)
        {
            yield return new WaitForEndOfFrame();
            var image = new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0);
            image.Apply();
            var colors = new HashSet<Color32>();
            for (int x=3; x<image.width; x+=31) for (int y=7; y<image.height; y+=29) colors.Add(image.GetPixel(x,y));
            if (colors.Count < 12) { Destroy(image); throw new InvalidOperationException("Blank player framebuffer: "+name); }
            var bytes = image.EncodeToPNG(); Destroy(image);
            yield return Save(Prefix+name+".png",bytes,"image/png");
        }
        List<RaycastResult> UiHits(Vector2 screen)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},hits);
            return hits;
        }
        static Vector2 Center(RectTransform rect) =>
            RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));

        // Drive the shipping layout: hit-testing must reach the rendered button at its actual screen position.
        IEnumerator Press(string name)
        {
            // A fixture may replace the HUD at end of frame. Raycast only after the new canvas has rendered.
            yield return new WaitForEndOfFrame();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var button = app.GetComponentsInChildren<Button>().LastOrDefault(b => b.name == name && b.gameObject.activeInHierarchy);
            Require(button != null,"Missing button "+name+" in "+app.Phase+" / "+app.Message);
            var rect = (RectTransform)button.transform;
            var scroll = button.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;
                var local = viewport.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
                if (local.y < viewport.rect.yMin + rect.rect.height/2 || local.y > viewport.rect.yMax - rect.rect.height/2)
                {
                    var offset = scroll.content.anchoredPosition;
                    offset.y = Mathf.Clamp(offset.y + viewport.rect.center.y-local.y,0,Mathf.Max(0,scroll.content.rect.height-viewport.rect.height));
                    scroll.content.anchoredPosition = offset; scroll.StopMovement(); Canvas.ForceUpdateCanvases();
                    yield return null;
                }
            }
            var point = Center(rect);
            var hits = UiHits(point);
            var hit = hits.Count == 0 ? null : hits[0].gameObject.GetComponentInParent<Button>();
            Require(point.x >= 0 && point.y >= 0 && point.x < Screen.width && point.y < Screen.height &&
                    button.IsInteractable() && hit == button, "Button occluded or disabled: "+name+" at "+point+" hit "+(hit?hit.name:"none"));
            var pointer = new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            manorActions.Add(new {button=name,x=(int)point.x,y=(int)point.y,phase=app.Phase,width=Screen.width,height=Screen.height});
            yield return null;
        }
        IEnumerator PickPlot(int slot)
        {
            while(app.View.CameraMoving)yield return null;
            yield return new WaitForEndOfFrame();
            var center = new Vector3(FrontierMap.ManorX+Manor.SlotX(slot),0,Manor.SlotZ(slot));
            var point = (Vector2)app.View.ViewCamera.WorldToScreenPoint(center);
            Require(point.x>=0 && point.x<Screen.width && point.y>=0 && point.y<Screen.height && UiHits(point).Count==0,
                    "Plot occluded: "+slot+" at "+point);
            app.GetComponent<BattleInput>().Click(point);
            Require(app.ManorSlot==slot,"Plane pick selected the wrong plot");
            manorActions.Add(new {plot=slot,x=(int)point.x,y=(int)point.y,width=Screen.width,height=Screen.height});
            yield return null;
        }
        IEnumerator BuildPlot(int plot,string kind)
        {
            yield return PickPlot(plot);
            yield return Press("Manor.Construction"); yield return Press("Manor.Build."+kind);
            Require(app.Estate.slots[plot].kind==kind,"Construction failed: "+kind+" / "+app.Message);
        }
        IEnumerator ClickBattlefield(float x,float z)
        {
            yield return new WaitForEndOfFrame();
            var point=(Vector2)app.View.ViewCamera.WorldToScreenPoint(new Vector3(x,app.Data.Height(app.Config.map,x,z),z));
            Require(point.x>=0 && point.x<Screen.width && point.y>=0 && point.y<Screen.height && UiHits(point).Count==0,
                "Battlefield point occluded: " + point);
            app.GetComponent<BattleInput>().Click(point);
            yield return null;
        }
        IEnumerator RunManorSmoke()
        {
            output=Option("--qa-out",Path.Combine(Application.persistentDataPath,"ManorSmoke"));
            PlayerPrefs.DeleteKey("frontline.qa.manor.v1");
            app.Muted=true;
            yield return null;
            string duelConfig=Json.Write(app.Config);
            yield return Press("Mode.Manor");
            yield return Press("Manor.Speed.0");
            yield return Press("Frontier.View.1");
            Require(app.Estate.slots.Length==Manor.PlotCount && app.Estate.FreeWorkers==2,"Founding estate failed");

            yield return BuildPlot(10,"irrigated_field");
            yield return Press("Manor.Worker.Plus");
            yield return BuildPlot(13,"pasture");
            yield return Press("Manor.Season");
            Require(app.Estate.workers==4 && app.Estate.raidReady,"Pasture housing or raider warning failed");
            yield return BuildPlot(3,"timber_camp");
            yield return Press("Manor.Worker.Plus");
            yield return Press("Manor.Season");
            yield return Press("Manor.Season");
            Require(app.Estate.Season==3,"Winter did not begin");

            yield return BuildPlot(11,"tavern");
            yield return Press("Manor.Worker.Plus");
            for(int level=2;level<=3;level++)
            {
                yield return Press("Manor.Upgrade");
                yield return Press("Manor.ConfirmUpgrade");
                Require(app.Estate.slots[11].level==level,"Tavern upgrade failed");
            }
            yield return Press("Manor.Heroes");
            Require(!app.GetComponentsInChildren<Text>().Any(label=>label.transform.parent.name=="英雄酒馆 · 招募与培养" &&
                label.text=="建筑已升级"),"New dialog inherited the last building notice");
            foreach(var id in Manor.HeroIds)yield return Press("Manor.Hero."+id);
            yield return Press("Manor.Hero.hero_vanguard");
            Require(app.Estate.HeroLevel("hero_vanguard")==2 && app.Estate.heroes.Count==3,"Hero acquisitions failed");
            yield return Press("关闭");

            yield return BuildPlot(12,"drill_yard");
            yield return Press("Manor.Training");
            yield return Press("Manor.Train.militia");
            Require(app.Estate.UnitLevel("militia")==2,"Permanent training did not apply");
            yield return Press("关闭");
            yield return BuildPlot(6,"stables");
            yield return Press("Manor.Recruits");
            yield return Press("Manor.Recruit.spikes");
            yield return Press("关闭");
            yield return ManorShot("expansion");

            yield return Press("Manor.Expedition");
            yield return Press("Manor.Stage.3");
            Require(Displays("敌情 · 6 人 · 军费 650"),"Marsh preview differs from actual enemy budget");
            yield return Press("Manor.Stage.0");
            for(int i=0;i<2;i++)
            {
                yield return Press("Manor.Force.Minus.archer");
                yield return Press("Manor.Force.Minus.militia");
            }
            foreach(var id in Manor.HeroIds)yield return Press("Manor.Force.Plus."+id);
            yield return Press("Manor.Force.Plus.spikes");
            Require(app.ForceCost==590 && Displays("40 军费 · 超出宅邸军费"),"Muster cost or disabled reason");
            yield return Press("Manor.March");
            Require(app.Config.red.Count==5 && app.Config.red.Where(u=>u.type=="militia").All(u=>u.level==2),"Draft lost trained soldiers");
            yield return Press("Mode.Duel");
            Require(app.InManor && app.Modal && app.Phase=="deploy","Deployment left without confirmation");
            yield return Press("Manor.Stay");

            var item=app.Config.props.Single();
            float originalX=item.x,originalZ=item.z;
            yield return ClickBattlefield(originalX,originalZ);
            Require(app.SelectedProp==0,"Carried gear was not selectable");
            yield return Press("Manor.Move");
            yield return ClickBattlefield(-11,3);
            Require(Mathf.Abs(app.Config.props[0].x+11)<.01f,"Carried gear did not move");
            yield return Press("Manor.Undo");
            Require(app.Config.props[0].x==originalX && app.Config.props[0].z==originalZ,"Undo changed carried gear");

            int before=app.Estate.ArmySize,coins=app.Estate.coin;
            yield return Press("Manor.Start");
            Require(app.Phase=="battle" && app.Battle.World is UnityWorld && Displays("2 集火 · Lv.2") && Displays("3 盾阵 · Lv.3"),
                "Live PhysX or visible command locks missing");
            yield return Press("Mode.Duel");
            int pausedAt=app.Battle.Tick;
            yield return null;
            Require(app.Modal && app.Battle.Tick==pausedAt,"Confirmation did not hold the battle");
            yield return Press("Manor.Stay");
            yield return Press("1×");
            yield return Press("2×");
            float until=Time.realtimeSinceStartup+40;
            while(app.Phase=="battle" && Time.realtimeSinceStartup<until)yield return null;
            Require(app.Phase=="result" && app.PendingSettlement,"Expedition did not produce a settlement");
            var replay=app.ReplayFile;
            Require(replay.engineVersion==Replay.RefinedExpeditionEngine && replay.result.report.units.Count(u=>app.Data.Heroes.ContainsKey(u.type))==3,
                "Hero replay did not preserve the expedition roster");
            yield return Press("Manor.Watch");
            app.Seek(replay.result.time*.5f);
            Require(app.Phase=="replay" && app.Paused && app.CurrentFrame.tick>0,"Trajectory seek failed");
            yield return Press("Manor.Retreat");
            Require(app.Phase=="manor" && app.Modal && Displays("远征结算清单") && Displays("消耗道具 · 地刺 ×1") &&
                app.Estate.Stock("spikes")==0 && app.Estate.coin>coins && app.Estate.ArmySize<=before,"Settlement ledger or dialogue failed");
            yield return Press("Manor.Settlement.Done");
            string saved=app.Estate.Export();
            Require(saved==PlayerPrefs.GetString("frontline.qa.manor.v1"),"Settlement save mismatch");
            app.ImportText(Json.Write(replay));
            Require(app.Phase=="replay" && !app.PendingSettlement,"Imported trajectory created a second settlement");
            yield return Press("Manor.Retreat");
            yield return Press("Mode.Duel");
            Require(!app.InManor && app.Phase=="deploy" && Json.Write(app.Config)==duelConfig && app.Estate.Export()==saved,"Arena state changed");

            var proof=new {version=Application.version,unity=Application.unityVersion,width=Screen.width,height=Screen.height,
                winner=replay.result.winner,tick=replay.frames.Last().tick,engine=replay.engineVersion,actions=manorActions};
            yield return Save("manor-smoke.json",Encoding.UTF8.GetBytes(Json.Write(proof)),"application/json");
            Debug.Log("FRONTLINE_MANOR_SMOKE_OK "+Application.platform+" "+Screen.width+"x"+Screen.height+" "+manorActions.Count+" interactions");
#if !UNITY_WEBGL && !UNITY_EDITOR
            Application.Quit(0);
#endif
        }
        IEnumerator RunManor()
        {
            output = Option("--qa-out",Path.Combine(Application.persistentDataPath,"ManorQA"));
            PlayerPrefs.DeleteKey("frontline.qa.manor.v1"); app.Muted=true;
            yield return null; yield return null;
            string original = Json.Write(app.Config), originalMode=app.Mode;
            yield return ManorShot("duel");
            yield return Press("Mode.Manor"); yield return Press("Manor.Speed.0");
            yield return Press("Frontier.View.1");
            Require(app.Phase=="manor" && app.Estate.slots[0].kind=="manor_hall","No founding hall");
            int firstDay=app.Estate.day;
            yield return BuildPlot(1,"farm"); yield return Press("Manor.Worker.Plus");
            yield return Press("Manor.Worker.Minus"); yield return Press("Manor.Worker.Plus");
            int food=app.Estate.food; yield return Press("Manor.Day");
            Require(app.Estate.day==firstDay+1 && app.Estate.food>food,"Harvest or staffing failed");
            yield return BuildPlot(2,"cottage"); yield return BuildPlot(3,"timber_camp");
            yield return Press("Manor.Worker.Plus"); yield return Press("Manor.Season");
            Require(app.Estate.workers==5 && app.Estate.day==firstDay+13,"Season growth");
            yield return BuildPlot(4,"smithy"); yield return Press("Manor.Worker.Plus");
            yield return BuildPlot(5,"barracks"); yield return Press("Manor.Recruits");
            int roster=app.Estate.Stock("militia"); yield return Press("Manor.Recruit.militia"); yield return Press("关闭");
            Require(app.Estate.Stock("militia")==roster+1,"Recruitment failed");
            yield return BuildPlot(6,"stables"); yield return Press("Manor.Recruits");
            yield return Press("Manor.Recruit.spikes"); yield return Press("关闭");
            yield return PickPlot(1); yield return Press("Manor.Upgrade"); yield return Press("Manor.ConfirmUpgrade");
            Require(app.Estate.slots[1].level==2,"Farm upgrade failed");
            int day=app.Estate.day; yield return Press("Manor.Speed.2");
            float until=Time.realtimeSinceStartup+8;
            while(app.Estate.day==day && Time.realtimeSinceStartup<until)yield return null;
            Require(app.Estate.day>day,"Automatic day did not advance at 2x");
            yield return ManorShot("estate"); yield return Press("Manor.Speed.0");
            yield return Press("Manor.Expedition");
            yield return Press("Manor.Force.Minus.militia"); yield return Press("Manor.Force.Plus.militia");
            yield return Press("Manor.Force.Plus.spikes"); yield return ManorShot("muster");
            int sent=app.ForceUnits, before=app.Estate.ArmySize, stockBefore=app.Estate.Stock("spikes"), coins=app.Estate.coin;
            yield return Press("Manor.March");
            Require(app.Phase=="deploy" && app.Config.map=="meadow" && app.Config.red.Count==sent,"Draft did not reach deployment");
            yield return ManorShot("deploy"); yield return Press("Manor.Start");
            Require(app.Battle!=null && app.Battle.World is UnityWorld && app.Phase=="battle","Live PhysX missing");
            yield return Press("1×"); yield return Press("2×");
            var target=app.View.ViewCamera.WorldToScreenPoint(new Vector3(-4,0,0));
            app.GetComponent<BattleInput>().Click(target);
            Require(app.Battle.Commander.log.Any(o=>o.team=="red" && o.type=="inspire"),"Player order did not reach battle");
            yield return new WaitForSecondsRealtime(.5f); yield return ManorShot("battle");
            until=Time.realtimeSinceStartup+120;
            while(app.Phase=="battle" && Time.realtimeSinceStartup<until) yield return null;
            Require(app.Phase=="result" && app.Battle.Result!=null,"No expedition result");
            var result=app.Battle.Result; var losses=result.report.units.Count(u=>u.team=="red"&&!u.alive);
            int expected=before-losses+(losses+2)/3, ticks=app.Battle.Tick;
            var replay=app.ReplayFile;
            yield return ManorShot("result"); yield return Press("Manor.Return");
            Require(app.Phase=="manor" && app.Battle==null && app.Estate.ArmySize==expected &&
                    app.Estate.Stock("spikes")==stockBefore-1 && app.Estate.coin>coins &&
                    app.Estate.cleared==(result.winner=="red"?1:0),"Return ledger mismatch");
            string saved=app.Estate.Export();
            Require(saved==PlayerPrefs.GetString("frontline.qa.manor.v1"),"PlayerPrefs differs from visible state");
            yield return ManorShot("return");
            yield return Press("Manor.Settlement.Done");
            yield return Press("Mode.Duel");
            Require(!app.InManor && app.Phase=="deploy" && app.Mode==originalMode && Json.Write(app.Config)==original,"Duel state leaked");
            yield return Press("Mode.Manor"); Require(app.Estate.Export()==saved,"Switch lost the manor");
            yield return Press("Manor.Raid"); yield return Press("Manor.March");
            Require(app.Phase=="deploy","Raider draft missing");
            yield return Press("Manor.Start");
            Require(app.Battle!=null && app.Phase=="battle","Raider world missing");
            yield return Press("Mode.Duel");
            yield return Press("Manor.Leave");
            Require(app.Battle==null && !app.Busy && Json.Write(app.Config)==original,"Abandoned sortie leaked");
            string style=app.Style;
            app.Style="oracle"; app.PlanArmy("blue",true);
            Require(app.Busy,"Oracle job did not start");
            yield return Press("Mode.Manor");
            Require(!app.Busy && app.Progress==null && app.Battle==null && app.Phase=="manor" && app.Estate.Export()==saved,"Switch did not cancel oracle cleanly");
            yield return Press("Mode.Duel"); app.Style=style;
            var notices=new List<string>(); app.Notice+=notices.Add;
            app.PlanArmy("blue",true);
            yield return null; yield return null;
            Require(app.Phase=="battle"&&!app.Busy&&!notices.Contains("已停止计算"),"Completion raced cancellation");
            app.Notice-=notices.Add; app.ReturnToDeploy();
            yield return ManorShot("duel-return");
            yield return Save(Prefix+"estate.json",Encoding.UTF8.GetBytes(saved),"application/json");
            yield return Save(Prefix+"expedition.json",Encoding.UTF8.GetBytes(Json.Write(replay)),"application/json");
            var proof = new {version=Application.version,unity=Application.unityVersion,platform=Application.platform.ToString(),
                width=Screen.width,height=Screen.height,gpu=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),
                world="UnityWorld / PhysX",tick=ticks,winner=result.winner,losses,armyBefore=before,armyAfter=expected,
                coinsBefore=coins,coinsAfter=app.Estate.coin,sourceModeRestored=true,prefsMatched=true,actions=manorActions};
            yield return Save(Prefix+"flow.json",Encoding.UTF8.GetBytes(Json.Write(proof)),"application/json");
            Debug.Log("FRONTLINE_MANOR_QA_OK "+Application.platform+" "+Screen.width);
#if !UNITY_WEBGL && !UNITY_EDITOR
            Application.Quit(0);
#endif
        }
    }
}

