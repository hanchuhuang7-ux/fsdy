using System;
using System.Collections;
using System.Linq;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        int verifiedAudioClips;
        bool displaySettingsVerified,localSlotsVerified,guideVerified,tierSelectionVerified,seasonalLightingVerified;

        IEnumerator CheckPresentationSettings()
        {
            var settings=app.Presentation;
            Require(settings.FrameRateMode==0&&settings.Shake&&!settings.AudioMuted&&settings.AmbienceEnabled&&Application.targetFrameRate==60&&
                QualitySettings.vSyncCount==0&&app.Muted,"Desktop defaults or QA mute differ");
            verifiedAudioClips=BattleAudio.VerifyClips();
            mix60=BattleAudio.VerifyCrowdMix(1);
            mix120=BattleAudio.VerifyCrowdMix(2);
            Require(BattleAudio.OrderCue("inspire")=="order_inspire"&&BattleAudio.OrderCue("focus")=="order_focus"&&
                BattleAudio.OrderCue("fortify")=="order_fortify"&&mix120.attempts==2*mix60.attempts,
                "Accent routing or crowd fixture density changed");
            Debug.Log("FRONTLINE_CROWD_MIX_OK 60="+mix60+" 120="+mix120);
            yield return Press("Settings.Quality");
            yield return Press("Settings.Fps.1");
            Require(Application.targetFrameRate==120&&QualitySettings.vSyncCount==0,"120 FPS not applied");
            yield return Press("Settings.Fps.2");
            Require(Application.targetFrameRate==-1&&QualitySettings.vSyncCount==1,"VSync not applied");
            yield return Press("Settings.Fps.3");
            Require(Application.targetFrameRate==-1&&QualitySettings.vSyncCount==0,"Uncapped mode not applied");
            yield return Press("Settings.Window.1");
            yield return WaitForState(()=>Screen.width==1600&&Screen.height==900,"1600×900 window did not apply");
            yield return Press("Settings.Window.0");
            yield return WaitForState(()=>Screen.width==1280&&Screen.height==800,"Window did not restore");
            yield return Press("Settings.Shake");
            yield return Press("Settings.Ambience");
            Require(!settings.AmbienceEnabled&&settings.AmbienceGain==0&&settings.EffectsGain>0,
                "Ambience switch also muted action sounds");
            yield return Press("Settings.Mute");
            var levels=new[]{72,63,29};
            for(int bus=0;bus<levels.Length;bus++)
            {
                var slider=app.GetComponentsInChildren<Slider>().Single(s=>s.name=="Settings.Volume."+bus);
                slider.value=levels[bus];
            }
            settings.Flush();
            var restored=new PlayerPresentation(true);
            Require(restored.FrameRateMode==3&&restored.WindowMode==0&&!restored.Shake&&restored.AudioMuted&&!restored.AmbienceEnabled&&
                Enumerable.Range(0,3).All(bus=>restored.Volume(bus)==levels[bus]),"Display and audio preferences did not persist");
            Require(app.GetComponent<BattleAudio>().PlayingVoices==0,"QA created an audible source");
            yield return Press("Settings.Fps.0");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--no-smoke-images")<0)yield return ManorShot("141-settings");
            Require(app.GetComponent<BattleInput>().Shortcut(KeyCode.Escape)&&!app.Modal,"Escape did not close display and audio settings");
            displaySettingsVerified=true;
            Debug.Log("FRONTLINE_PRESENTATION_OK 60/120/vsync/unlimited Window=1280x800 volume=72/63/29 clips="+verifiedAudioClips+" silent=true ambienceToggle=true");
        }

        IEnumerator CheckGuideAndSlots()
        {
            var input=app.GetComponent<BattleInput>();
            yield return WaitForState(()=>app.Estate.GuideActive&&Displays("① 建一块麦田并派工"),"Founding guide missing");
            yield return CheckFrontierPreset(1);
            yield return BuildPlot(2,"farm");yield return Press("Manor.Worker.Plus");
            yield return WaitForState(()=>app.Estate.guideStep==1&&Displays("② 打开领地政务"),"Staffing did not advance the guide");
            Require(input.Shortcut(KeyCode.G)&&app.Modal&&Displays("领地政务与集市")&&app.Estate.guideStep==2&&
                !input.Shortcut(KeyCode.P)&&!input.Shortcut(KeyCode.M)&&input.Shortcut(KeyCode.Escape),"G or modal shortcut guard failed");
            Require(input.Shortcut(KeyCode.P)&&app.Modal&&Displays("建地清单")&&input.Shortcut(KeyCode.Escape),"P did not open the plot list");
            Require(input.Shortcut(KeyCode.M)&&app.Modal&&Displays("领主宏愿")&&input.Shortcut(KeyCode.Escape),"M did not open milestones");
            yield return WaitForState(()=>Displays("③ 集结巡边或远征"),"Guide did not expose its third step");
            yield return Press("Manor.Patrol");
            Require(!VisibleButton("Manor.FrontierTier.2").IsInteractable()&&!VisibleButton("Manor.FrontierTier.3").IsInteractable(),
                "Elite patrol ranks were initially available");
            yield return Press("Manor.Patrol.Start");
            Require(app.InFrontier&&app.Estate.guideStep==3&&!app.Estate.GuideActive,"Patrol did not complete the guide");
            app.ReturnManor();
            yield return Press("Manor.Settlement.Done");

            string first=app.Estate.Export();
            yield return Press("庄园工坊");yield return Press("Manor.Saves");yield return Press("Manor.SaveSlot.0");
            yield return Press("Manor.SaveSlot.0");
            Require(Displays("覆盖存档 1？"),"Overwrite confirmation missing");
            yield return Press("Manor.Slot.Cancel");
            Require(app.ReadManorSave(0).Export()==first,"Cancel changed the manual save");
            yield return Press("关闭");yield return Press("Manor.Day");
            string second=app.Estate.Export();
            yield return Press("庄园工坊");yield return Press("Manor.Saves");yield return Press("Manor.SaveSlot.1");
            yield return Press("Manor.SaveSlot.2");yield return Press("Manor.SaveSlot.2");yield return Press("Manor.Slot.Overwrite");
            Require(first!=second&&app.ReadManorSave(0).Export()==first&&app.ReadManorSave(1).Export()==second&&
                app.ReadManorSave(2).Export()==second,"Local save slots were not independent");
            yield return Press("Manor.LoadSlot.0");yield return Press("Manor.Slot.Load");
            Require(!app.Modal&&app.Estate.Export()==first&&PlayerPrefs.GetString(FrontierSaveKey)==first&&
                app.ReadManorSave(1).Export()==second,"Load did not update the live and automatic save only");
            localSlotsVerified=true;

            var founding=new Manor();
            app.ImportText(founding.Export());
            yield return WaitForState(()=>Displays("① 建一块麦田并派工"),"Founding guide did not appear after the import notice");
            yield return Press("Manor.Guide.Dismiss");
            Require(app.Estate.guideDismissed&&Manor.Import(PlayerPrefs.GetString(FrontierSaveKey)).guideDismissed,"Guide skip was not saved");
            guideVerified=true;

            var unlocked=Manor.Import(first);unlocked.frontierSwept=true;
            app.ImportText(unlocked.Export());
            yield return Press("Manor.Patrol");yield return Press("Manor.FrontierTier.2");yield return Press("Manor.FrontierTier.3");
            Require(app.Estate.frontierTier==3&&ManorReadout("Manor.FrontierTier.Rewards").text.Contains("物资 ×2"),
                "Elite tier card did not update");
            yield return Press("Manor.Patrol.Start");
            var guard=app.Battle.Units[app.Config.red.Count];
            float baseHp=app.Data.Unit(guard.entity.type).hp;
            Require(app.Config.blue.All(p=>p.level==3)&&guard.entity.maxHp>baseHp&&
                app.View.MaxHp(app.Data.Unit(guard.entity.type),guard.entity.id)==guard.entity.maxHp,
                "Elite health did not reach PhysX and the health bars");
            app.ReturnManor();
            yield return Press("Manor.Settlement.Done");
            tierSelectionVerified=true;
            Debug.Log("FRONTLINE_SLOTS_GUIDE_TIERS_OK slots=3 shortcuts=G/P/M tier=3");
        }

        IEnumerator CheckAutumnWinterLight(bool captures)
        {
            Require(app.Estate.Season==2,"Autumn light test started outside autumn");
            var autumn=app.View.SeasonLightColor;
            var autumnRotation=app.View.SeasonLightRotation;
            if(captures)
            {
                app.View.Focus(new Vector3(FrontierMap.ManorX,.35f,0),34,48,-22);
                yield return ManorShot("141-autumn-light");
            }
            yield return Press("Manor.Day");
            Require(app.View.SeasonLightBlending&&app.Estate.Season==3,"Season switch did not start interpolating");
            yield return WaitForState(()=>!app.View.SeasonLightBlending,"Winter sun transition did not finish");
            var winter=app.View.SeasonLightColor;
            Require(autumn.r>autumn.b&&winter.b>winter.r&&Quaternion.Angle(autumnRotation,app.View.SeasonLightRotation)>15,
                "Seasonal light positions or color temperatures did not differ");
            if(captures)yield return ManorShot("141-winter-light");
            seasonalLightingVerified=true;
            Debug.Log("FRONTLINE_SEASON_LIGHT_OK autumn="+autumn+" winter="+winter);
        }
    }
}
