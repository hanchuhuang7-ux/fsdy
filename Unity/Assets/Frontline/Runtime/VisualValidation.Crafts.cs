using System.Collections;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        bool craftBuildingsVerified,codexLineBreaksVerified,arenaCloseup;
        int specialistCloseupMask,heroCloseupMask,retinueCloseup;

        IEnumerator CheckCraftBuildings(bool captures)
        {
            var state=Manor.Import(app.Estate.Export());
            state.workers=Mathf.Min(state.Housing,state.workers+3);
            if(state.slots[9].workers==0)state.Assign(9,1);
            app.ImportText(state.Export());
            int start=Manor.LegacyNoblePlotCount;
            for(int index=start;index<start+2;index++)
            {
                app.SelectManorSlot(index);
                for(int level=2;level<=3;level++)
                {
                    yield return Press("Manor.Upgrade");
                    yield return Press("Manor.ConfirmUpgrade");
                    Require(app.Estate.slots[index].level==level,"Craft building did not upgrade");
                }
                yield return Press("Manor.Worker.Plus");
                var world=new Vector3(FrontierMap.ManorX+Manor.SlotX(index),1.03f,Manor.SlotZ(index));
                app.View.Focus(world,8.8f,32,-79);
                yield return new WaitForEndOfFrame();
                Require(!app.View.LowSceneryLod&&app.Estate.slots[index].workers>0,"Craft close-up missed its LOD0 or worker");
                if(captures)yield return ManorShot(index==start?"160-bakery-lv3":"160-weaver-lv3");
            }
            var forecast=app.Estate.Forecast();
            Require(forecast.bakeryGrain>0&&forecast.synergy.breadPercent>0&&forecast.synergy.weavePercent>0&&
                forecast.synergy.winterClothing>0&&forecast.approvalFactors.minstrel>0&&
                app.Estate.SpecialistLevel(Manor.Mason)>0&&app.Estate.SpecialistLevel(Manor.Minstrel)>0,
                "New jobs are missing from the estate forecast");
            craftBuildingsVerified=true;
        }

        IEnumerator CheckResidentCloseups(bool captures,string series="160")
        {
            while(app.Estate.RetinueReady<3)
            {
                int before=app.Estate.RetinueReady;
                app.RecruitManorGuard();
                Require(app.Estate.RetinueReady==before+1,"Retinue portrait fixture could not recruit");
            }
            app.View.FocusManorRetinue();
            yield return WaitForState(()=>!app.View.CameraMoving&&app.View.VisibleRetinue>=3,"Retinue close-up was obscured");
            retinueCloseup=app.View.VisibleRetinue;
            if(captures)yield return ManorShot(series+"-retinue");

            var tavern=new Vector3(FrontierMap.ManorX+Manor.SlotX(3),.93f,Manor.SlotZ(3)-.15f);
            app.View.Focus(tavern,10.3f,29,-11);
            yield return new WaitForEndOfFrame();
            heroCloseupMask=app.View.VisibleHeroMask;
            Require(heroCloseupMask==(1<<Manor.HeroIds.Length)-1,"Hero portrait group was obscured");
            if(captures)yield return ManorShot(series+"-heroes");

            for(int role=0;role<Manor.SpecialistIds.Length;role++)
            {
                int mask=1<<role;
                yield return new WaitForEndOfFrame();
                var previous=app.View.SpecialistPoint(role);
                yield return null;
                yield return new WaitForEndOfFrame();
                var position=app.View.SpecialistPoint(role);
                var motion=previous-position;
                float yaw=motion.sqrMagnitude>.000001f?Mathf.Atan2(motion.x,motion.z)*Mathf.Rad2Deg:0;
                app.View.Focus(position+Vector3.up*.88f,6.8f,22,yaw);
                yield return WaitForState(()=>(app.View.VisibleSpecialistMask&mask)!=0,"Missing specialist close-up "+Manor.SpecialistIds[role]);
                specialistCloseupMask|=mask;
                if(captures)yield return ManorShot(series+"-specialist-"+Manor.SpecialistIds[role]);
            }
            Debug.Log("FRONTLINE_CRAFTS_ART_OK crafts=2 specialists="+specialistCloseupMask+" heroes="+heroCloseupMask+" retinue="+retinueCloseup);
        }

        void CheckCodexReading()
        {
            foreach(var label in CodexScroll.content.GetComponentsInChildren<Text>())
            {
                var settings=label.GetGenerationSettings(new Vector2(label.rectTransform.rect.width,0));
                settings.verticalOverflow=VerticalWrapMode.Overflow;
                using var generator=new TextGenerator(label.text.Length);
                generator.Populate(label.text,settings);
                Require(label.preferredHeight<=label.rectTransform.rect.height+1,"Codex label clipped: "+label.text);
                foreach(var line in generator.lines)
                {
                    int i=line.startCharIdx;
                    if(i>0&&i<label.text.Length)
                        Require("。、，；：！？）》】」』％…".IndexOf(label.text[i])<0,"Codex hanging punctuation: "+label.text);
                }
            }
            codexLineBreaksVerified=true;
        }

        IEnumerator RevealCodexEntry(string id)
        {
            var scroll=CodexScroll;
            var row=HudElement<RectTransform>("Codex.Entry."+id);
            scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(-row.anchoredPosition.y-8,0,
                Mathf.Max(0,scroll.content.rect.height-scroll.viewport.rect.height)));
            scroll.StopMovement();
            yield return new WaitForEndOfFrame();
            var entry=ScreenRect(row);
            var viewport=ScreenRect(scroll.viewport);
            Require(entry.yMin>=viewport.yMin&&entry.yMax<=viewport.yMax,"Codex card not visible after scrolling: "+id);
        }
    }
}
