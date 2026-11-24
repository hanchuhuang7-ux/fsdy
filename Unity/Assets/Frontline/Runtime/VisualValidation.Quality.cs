using System;
using System.Collections;
using System.Linq;
using Frontline.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        bool meritVerified,portraitMeritVerified,extraShortcutsVerified,idleBadgeVerified,helpKeysVerified;
        bool impactReviewVerified,portraitReportVerified,expeditionLeadersVerified,bridgeWaterVerified;
        int orderAccentMask,impactAccentMask,reportJumps,peakAccentInstances;
        (int attempts,int accepted,float peak,float minGain) mix60,mix120;

        IEnumerator CheckMerit(bool captures,bool compact)
        {
            var keys=app.GetComponent<BattleInput>();
            if(!compact)
            {
                Require(keys.Shortcut(KeyCode.L)&&Displays("领地收支账本")&&keys.Shortcut(KeyCode.Escape),"L did not open and dismiss the ledger");
                Require(keys.Shortcut(KeyCode.C)&&Displays("游戏图鉴")&&keys.Shortcut(KeyCode.Escape),"C did not open and dismiss the Codex");
                Require(keys.Shortcut(KeyCode.N)&&Displays("贵族头衔与赐土")&&keys.Shortcut(KeyCode.Escape),"N did not open and dismiss the nobility panel");
                yield return Press("庄园工坊");
                var field=app.GetComponentsInChildren<InputField>().Single();
                field.Select();field.ActivateInputField();
                yield return WaitForState(()=>field.isFocused,"Workshop input did not focus");
                Require(!keys.Shortcut(KeyCode.C)&&!keys.Shortcut(KeyCode.L)&&!keys.Shortcut(KeyCode.N)&&
                    !keys.Shortcut(KeyCode.Escape)&&app.Modal,"Focused input did not shield the key bindings");
                field.DeactivateInputField();
                EventSystem.current.SetSelectedGameObject(null);
                yield return Press("关闭");
                extraShortcutsVerified=true;
            }
            Require(keys.Shortcut(KeyCode.M),"M did not open the manor ambitions");
            yield return Press("Manor.Merit.Open");
            var merit=app.Estate.MeritOverview();
            int[] counts={merit.ranks,merit.sectors,merit.milestones,merit.specialists,merit.armaments,merit.expeditions};
            int[] limits={Manor.NobleRanks.Count,Manor.Sectors.Count,Manor.Milestones.Count,
                Manor.SpecialistIds.Length,Manor.HeroIds.Length,Manor.Route.Length};
            Require(ManorReadout("Manor.Merit.Title").text==merit.Title&&ManorReadout("Manor.Merit.Percent").text==merit.Percent+"%",
                "Merit header disagrees with the estate");
            for(int i=0;i<counts.Length;i++)
                Require(ManorReadout("Manor.Merit.Row."+i).text==counts[i]+" / "+limits[i],"Wrong merit category "+i);
            CheckEconomyLayout("领主勋绩总览");
            CheckButtonHit("Manor.Merit.Back");
            if(captures)yield return ManorShot(compact?"180-portrait-merit":"180-merit");
            yield return Press("Manor.Merit.Back");
            yield return Press("关闭");
            if(compact)portraitMeritVerified=true;else meritVerified=true;
        }

        IEnumerator CheckManorQuality(bool captures)
        {
            yield return CheckMerit(captures,false);
            string saved=app.Estate.Export();
            int index=Array.FindIndex(app.Estate.slots,1,plot=>plot.kind=="smithy"&&plot.workers>0);
            if(index<0)
            {
                index=Array.FindIndex(app.Estate.slots,1,plot=>Manor.StaffLimit(plot.kind)>0&&plot.workers>0);
            }
            Require(index>0,"No staffed plot for the idle badge");
            app.SelectManorSlot(index,focus:true);
            yield return WaitForState(()=>!app.View.CameraMoving,"Plot camera did not settle");
            while(app.Estate.slots[index].workers>0)yield return Press("Manor.Worker.Minus");
            var idle=ManorReadout("Manor.Plot.Idle");
            Require(idle.text=="闲置"&&idle.gameObject.activeInHierarchy,"The zero-staff badge was missing");
            if(captures)yield return ManorShot("180-idle");
            yield return Press("Manor.Worker.Plus");
            Require(!idle.gameObject.activeInHierarchy&&app.Estate.slots[index].workers==1,"Staffing did not clear the idle badge in place");
            idleBadgeVerified=true;
            app.ImportText(saved);

            yield return Press("领主手册");
            string help=ManorReadout("Manor.Help.Keys").text;
            Require(help.Contains("L 收支账本")&&help.Contains("C 游戏图鉴")&&help.Contains("N 贵族头衔与赐土"),"Manor help missed keys");
            if(captures)yield return ManorShot("180-help");
            yield return Press("关闭");
            helpKeysVerified=true;

            app.View.Focus(new Vector3((FrontierMap.BridgeStart+FrontierMap.Boundary)*.5f,0,0),23,39,-12);
            yield return new WaitForEndOfFrame();
            Require(app.View.LakeShoreReady&&!app.View.AccentCasters,"Bridge water was missing or cast shadows");
            if(captures)yield return ManorShot("180-bridge-ripples");
            bridgeWaterVerified=true;
        }

        void CheckLeaderRows(BattleReport report)
        {
            var (red,blue)=BattleHighlights.Leaders(report);
            void Check(string name,UnitReport row)
            {
                var text=ManorReadout("Report.Leaders."+name);
                Require(row==null?text.text=="—":text.text.Contains("#"+row.id),"Report leader differs from the recorded unit: "+name);
            }
            Check("Red.Damage",red.Damage);Check("Red.Kills",red.Kills);Check("Red.Healing",red.Healing);
            Check("Blue.Damage",blue.Damage);Check("Blue.Kills",blue.Kills);Check("Blue.Healing",blue.Healing);
        }

        IEnumerator CheckExpeditionLeaders()
        {
            string saved=app.Estate.Export();
            yield return Press("Manor.Expedition");
            yield return Press("Manor.Force.Recommend");
            yield return Press("Manor.March");
            yield return Press("Manor.Start");
            yield return WaitForState(()=>app.Battle!=null&&app.Phase=="battle"&&app.Battle.Tick>=20,"Expedition did not enter combat");
            app.TogglePause();
            yield return Press("战报");
            CheckLeaderRows(app.Battle.Report());
            yield return Press("关闭");
            app.ReturnManor();
            app.ImportText(saved);
            expeditionLeadersVerified=true;
        }

        IEnumerator CheckBattleQuality(bool captures)
        {
            string original=Json.Write(app.Config);
            var keys=app.GetComponent<BattleInput>();
            Require(!keys.Shortcut(KeyCode.L)&&!keys.Shortcut(KeyCode.N)&&keys.Shortcut(KeyCode.C)&&
                Displays("游戏图鉴")&&keys.Shortcut(KeyCode.Escape),"Arena shortcuts leaked manor navigation or could not open the Codex");
            var c=new Deployment {seed=3701};
            c.commander.enabled=true;
            void Place(string team,string id,float x,float z,string stance="advance",bool prop=false)
            {
                float sign=team=="red"?-1:1;
                for(int col=0;col<5;col++)for(int row=0;row<13;row++)
                {
                    float near=x+sign*col*1.25f;
                    float lane=z+(row+1)/2*(row%2==0?-1:1)*1.2f;
                    if(!app.Data.CanPlace(c,team,id,near,lane,prop))continue;
                    if(prop)c.props.Add(new PropPlacement{team=team,type=id,x=near,z=lane});
                    else c.Army(team).Add(new UnitPlacement(id,near,lane,stance));
                    return;
                }
                throw new InvalidOperationException("No fixture position for "+id);
            }
            foreach(string team in new[]{"red","blue"})
            {
                int side=team=="red"?-1:1;
                Place(team,"giant",side*3.9f,0);
                Place(team,"shield",side*2.7f,-1.9f);
                Place(team,"spear",side*2.7f,1.8f);
                Place(team,"cavalry",side*6.8f,-2.7f);
                Place(team,"medic",side*6.1f,2.7f);
                Place(team,"bomber",side*7.0f,-5);
                Place(team,"catapult",side*10.2f,.6f);
                Place(team,"archer",side*13.6f,8.8f,"hold");
                Place(team,"shield",side*15.7f,8.1f,"hold");
            }
            Place("red","barrel",-3.5f,4.6f,prop:true);
            app.ImportText(Json.Write(c));
            yield return Press("开 战");
            yield return WaitForState(()=>app.Phase=="battle"&&app.Battle.Tick>=4,"Review fixture could not start");
            app.Team="red";keys.Shortcut(KeyCode.Alpha1);app.CommandAt(new Vector3(-4,0,0));
            yield return WaitForState(()=>app.Battle.Tick>=55,"Review fixture stalled at first order");
            app.Team="blue";keys.Shortcut(KeyCode.Alpha2);app.CommandAt(new Vector3(3,0,.5f));
            app.Speed=4;
            yield return WaitForState(()=>app.Battle.Tick>=503&&app.Phase=="battle","Review fixture ended before the cooldown");
            app.Team="red";keys.Shortcut(KeyCode.Alpha3);app.CommandAt(new Vector3(-2,0,1));
            Require(app.Battle.Commander.log.Count==3,"One of the three live orders was rejected");
            app.Speed=20;
            yield return WaitForState(()=>app.Phase=="result","Review fixture did not finish");
            app.Speed=1;
            var replay=app.ReplayFile;
            Require(replay?.kind=="trajectory"&&replay.orderLog.Count==3&&replay.highlights.Count>=3&&
                replay.config.manorHall==0&&replay.config.red.All(unit=>unit.armament==0),"Review fixture did not record a fair battle");
            app.Watch();
            for(int i=0;i<replay.orderLog.Count;i++)
            {
                var order=replay.orderLog[i];
                app.Seek((order.tick+15)/60f);
                app.Refresh();
                app.View.Focus(new Vector3(order.x*.74f,.48f,order.z),25,51,-9);
                yield return new WaitForEndOfFrame();
                int bit=1<<i;
                Require((app.View.VisibleOrderAccents&bit)!=0&&!app.View.AccentCasters&&app.View.AccentInstances>1,
                    "Order accent did not come from the recorded command");
                orderAccentMask|=bit;
                peakAccentInstances=Mathf.Max(peakAccentInstances,app.View.AccentInstances);
                if(captures)yield return ManorShot("180-order-"+order.type);
            }
            string[] impacts={"explosion","slam"};
            for(int i=0;i<impacts.Length;i++)
            {
                string type=impacts[i];
                var frame=replay.frames.FirstOrDefault(item=>item.effects.Any(effect=>effect.type==type&&
                    effect.life<effect.maxLife*.81f&&effect.life>effect.maxLife*.50f));
                Require(frame!=null,"No recorded "+type+" in the real battle");
                var hit=frame.effects.First(effect=>effect.type==type);
                app.Seek(frame.time);
                app.Refresh();
                app.View.Focus(new Vector3(hit.x,.62f,hit.z),13.8f,35,-24);
                yield return new WaitForEndOfFrame();
                Require((app.View.VisibleImpactAccents&(1<<i))!=0&&!app.View.AccentCasters,"Impact accents lost their replay source");
                impactAccentMask|=1<<i;
                peakAccentInstances=Mathf.Max(peakAccentInstances,app.View.AccentInstances);
                if(captures)yield return ManorShot("180-impact-"+type);
            }
            yield return CheckReplayReport(captures,false);
            Screen.SetResolution(390,844,FullScreenMode.Windowed);
            yield return WaitForState(()=>Screen.width==390&&Screen.height==844,"Review portrait resolution did not apply");
            yield return CheckReplayReport(captures,true);
            Screen.SetResolution(1280,800,FullScreenMode.Windowed);
            yield return WaitForState(()=>Screen.width==1280&&Screen.height==800,"Review did not restore desktop resolution");
            yield return CheckChargeAccent(captures);
            impactReviewVerified=true;
            app.ReturnToDeploy();
            app.ImportText(original);
        }

        IEnumerator CheckChargeAccent(bool captures)
        {
            app.ReturnToDeploy();
            var fixture=new Deployment();
            fixture.red.Add(new UnitPlacement("cavalry",-13,0));
            fixture.blue.Add(new UnitPlacement("shield",5,0,"hold"));
            app.ImportText(Json.Write(fixture));
            yield return Press("开 战");
            // The same collision-only fixture as the PhysX motion check.
            app.Battle.Units[0].cooldown=999;
            app.Speed=3;
            yield return WaitForState(()=>app.CurrentFrame.effects.Any(fx=>fx.type=="charge"&&fx.life<fx.maxLife*.76f),
                "Real cavalry collision did not produce a charge pulse");
            app.TogglePause();app.Speed=1;
            var contact=app.CurrentFrame.effects.First(fx=>fx.type=="charge");
            app.View.Focus(new Vector3(contact.x,.8f,contact.z),11.8f,33,13);
            yield return new WaitForEndOfFrame();
            Require((app.View.VisibleImpactAccents&4)!=0&&!app.View.AccentCasters,"Cavalry contact did not render its pulse");
            impactAccentMask|=4;
            peakAccentInstances=Mathf.Max(peakAccentInstances,app.View.AccentInstances);
            if(captures)yield return ManorShot("180-impact-charge");
        }

        IEnumerator CheckReplayReport(bool captures,bool compact)
        {
            yield return Press("战报");
            Require(Displays("全场亮点")&&Displays("高光时刻 · 点击跳转"),"Review did not expose leaders and jump list");
            CheckLeaderRows(app.ReplayFile.result.report);
            CheckEconomyLayout("战果 · 贡献与关键战况");
            var moment=app.Highlights.First(entry=>entry.type=="order");
            var button=VisibleButton("Report.Moment."+moment.id);
            Require(button.GetComponentInChildren<Text>().text.Contains("鼓舞"),"Order moment did not show its name");
            CheckButtonHit(button.name);
            if(captures)yield return ManorShot(compact?"180-portrait-report":"180-report");
            float target=app.ReplayFile.frames[Replay.AfterTick(app.ReplayFile.frames,moment.tick)].time;
            yield return Press(button.name);
            Require(app.Phase=="replay"&&!app.Modal&&app.Paused&&Mathf.Abs(app.ReplayTime-target)<.002f,"Named highlight did not seek");
            reportJumps++;
            if(compact)portraitReportVerified=true;
        }
    }
}
