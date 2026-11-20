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
        bool nobilityVerified,codexTabsVerified,arenaCodexVerified,portraitCodexVerified;
        int dukePlotsBuilt,rankSounds,nobleDetails;

        IEnumerator CheckNobilityAndCodex(bool captures)
        {
            var estate=Manor.Import(app.Estate.Export());
            estate.coin=3300;estate.timber=1400;estate.influence=Manor.InfluenceMax;
            estate.cleared=5;estate.food=280;estate.prosperityPeak=3;estate.clearedSectors=Manor.SectorMask;
            for(int index=0;index<Manor.Sectors.Count;index++)
                if(!estate.ClaimedSector(index))estate.ClaimSector(index);
            app.ImportText(estate.Export());
            estate=app.Estate;
            yield return CheckFrontierPreset(1);
            Require(app.View.NobleBoundaryCount==Manor.PlotCount-Manor.FirstNoblePlot&&app.View.NobleDetailCount==0&&estate.nobility==0&&
                ManorReadout("Manor.Nobility.Ledger").text.StartsWith("男爵",StringComparison.Ordinal),"Baron did not show unsealed plots");
            app.View.FocusManorPlot(Manor.FirstNoblePlot);
            yield return WaitForState(()=>!app.View.CameraMoving,"Crown boundary camera did not settle");
            yield return new WaitForEndOfFrame();
            CheckHoverDetails(Manor.FirstNoblePlot,"子爵封地","晋升子爵后开放");
            yield return PickPlot(Manor.FirstNoblePlot);
            Require(!app.GetComponentsInChildren<Button>().Any(button=>button.name=="Manor.Construction"),
                "Unsealed plot offered a build button");
            yield return Press("Manor.Nobility.Plot");
            Require(Displays("贵族头衔与赐土"),"Unsealed plot did not open the rank requirements");
            yield return Press("关闭");
            Require(((NativeHudButton)VisibleButton("Manor.Governance")).Tone==HudTone.Primary,"Promotion did not reach top-level todos");
            yield return Press("Manor.Governance");
            CheckGovernanceViewport();
            Require(((NativeHudButton)VisibleButton("Manor.Nobility")).Tone==HudTone.Primary,"Nobility tab did not show a ready promotion");
            yield return Press("Manor.Nobility");
            CheckModalSkin("贵族头衔与赐土");
            if(captures)yield return ManorShot("150-nobility");

            void Sound(string cue){if(cue=="coronation")rankSounds++;}
            app.ManorSound+=Sound;
            try
            {
                int firstGrant=Manor.FirstNoblePlot;
                for(int rank=1;rank<Manor.NobleRanks.Count;rank++)
                {
                    var offer=Manor.NobleRanks[rank];
                    int coin=estate.coin,merit=estate.influence;
                    Require(ManorReadout("Manor.Nobility.State."+rank).text=="可受封","Next-rank badge missing");
                    yield return Press("Manor.Nobility.Promote");
                    yield return new WaitForEndOfFrame();
                    Require(estate.nobility==rank&&estate.GrantedPlots==firstGrant+Manor.NoblePlotsPerRank*rank&&
                        estate.coin==coin-offer.coin&&estate.influence==merit-offer.influence&&
                        app.View.NobleBoundaryCount==Manor.PlotCount-estate.GrantedPlots&&
                        estate.chronicle.Last().kind=="nobility"&&PlayerPrefs.GetString(FrontierSaveKey)==estate.Export(),
                        "Promotion did not grant the correct plots, costs, chronicle and save");
                }
            }
            finally {app.ManorSound-=Sound;}
            Require(rankSounds==4&&!VisibleButton("Manor.Nobility.Promote").IsInteractable(),"Rank cap or ceremony audio failed");
            nobleDetails=app.View.NobleDetailCount;
            Require(nobleDetails>=6,"Duke's civic details were missing");
            yield return Press("关闭");

            string[] newBuildings={"smithy","cottage","farm","pasture","cottage","timber_camp","granary","farm",
                Manor.Bakery,Manor.Weaver,"cottage","timber_camp"};
            for(int index=Manor.FirstNoblePlot;index<Manor.PlotCount;index++)
            {
                app.SelectManorSlot(index,focus:true);
                yield return WaitForState(()=>!app.View.CameraMoving,"Granted plot camera did not settle");
                yield return Press("Manor.Construction");
                string kind=newBuildings[index-Manor.FirstNoblePlot];
                yield return Press("Manor.Build."+kind);
                Require(estate.slots[index].kind==kind,"Granted estate could not build on "+index);
                dukePlotsBuilt++;
            }
            yield return CheckCraftBuildings(captures);
            estate=app.Estate;
            app.SelectManorSlot(0);
            Require(ManorReadout("Manor.Nobility.Hall").text=="公爵","Lord's hall is missing its ranked badge");
            yield return CheckFrontierPreset(1);
            if(captures)
            {
                yield return ManorShot("160-duke-coast");
                app.View.Focus(new Vector3(FrontierMap.ManorX-2.10f,1.03f,-.10f),13.9f,35,-7);
                yield return ManorShot("160-manor-details");
            }
            yield return Press("Manor.Governance");
            yield return Press("Manor.Chronicle");
            Require(Displays("受封公爵 · 赐地 "+Manor.NobleGrantPlots(4))&&
                Displays("建地 32 建成"+Manor.Name(Manor.Bakery))&&Displays("建地 33 建成"+Manor.Name(Manor.Weaver)),
                "Expansion or craft chronicle entry missing");
            yield return Press("关闭");
            nobilityVerified=true;
            yield return CheckArmaments(captures);
            yield return CheckResidentCloseups(captures);
            yield return CaptureArmamentArt(captures);
            yield return CheckEconomy(captures,false);
            estate=app.Estate;

            yield return Press("Codex.Open");
            CheckModalSkin("游戏图鉴");
            yield return Press("Codex.Tab.0");
            Require(CountCodexEntries()==15&&Displays("军费 40 · 生命 80 · 攻击 18\n移速 3.2 · 射程 1.35m · 间隔 0.8s")&&
                Displays("训练 Lv.1"),"Unit or prop sources did not reach the manor codex");
            if(captures)yield return ManorShot("150-codex-troops");
            yield return CheckCodexBottom(app.Data.Data.props.Last().id);
            yield return Press("Codex.Tab.1");
            Require(CountCodexEntries()==25&&HudElement<Image>("Codex.Entry.market")&&HudElement<Image>("Codex.Entry.manor_hall"),
                "Building, backyard or synergy entry missing");
            CheckCodexReading();
            if(captures)
            {
                yield return ManorShot("160-codex-buildings");
                yield return RevealCodexEntry(Manor.Bakery);
                yield return ManorShot("160-codex-crafts");
            }
            yield return Press("Codex.Tab.2");
            Require(CountCodexEntries()==14&&Displays("当前爵位")&&Displays("每日 +2 粮食"),
                "Rank, region or development records incomplete");
            if(captures)yield return ManorShot("150-codex-nobility");
            yield return Press("Codex.Tab.3");
            Require(CountCodexEntries()==12&&HudElement<Image>("Codex.Entry."+Manor.Mason)&&HudElement<Image>("Codex.Entry."+Manor.Minstrel)&&
                Displays(Manor.Armament(Manor.HeroIds[0],3).title)&&
                Displays("当前 Lv."+estate.SpecialistLevel("watchman"))&&
                Displays("圣辉 · "+app.Data.Heroes["hero_commander"].subtitle),
                "Heroes or specialist ranks missing");
            CheckCodexReading();
            if(captures)
            {
                yield return ManorShot("160-codex-people");
                yield return RevealCodexEntry("gear_"+Manor.HeroIds[0]);
                yield return ManorShot("170-codex-gear");
                yield return RevealCodexEntry(Manor.Mason);
                yield return ManorShot("160-codex-specialists");
            }
            yield return Press("关闭");
            codexTabsVerified=true;
            Debug.Log("FRONTLINE_NOBILITY_CODEX_OK dukePlots="+dukePlotsBuilt+" details="+nobleDetails+" sounds="+rankSounds);
        }

        int CountCodexEntries() => app.GetComponentsInChildren<Image>().Count(image=>image.name.StartsWith("Codex.Entry.",StringComparison.Ordinal));
        ScrollRect CodexScroll => HudElement<RectTransform>("游戏图鉴").GetComponentInChildren<ScrollRect>();

        IEnumerator CheckCodexBottom(string id)
        {
            var scroll=CodexScroll;
            Require(scroll.content.anchoredPosition.y<.5f&&scroll.verticalScrollbar.gameObject.activeInHierarchy,
                "Codex tab did not start at the top with a visible scrollbar");
            scroll.verticalScrollbar.value=0;
            yield return new WaitForEndOfFrame();
            var entry=ScreenRect(HudElement<RectTransform>("Codex.Entry."+id));
            var viewport=ScreenRect(scroll.viewport);
            Require(entry.yMin>=viewport.yMin+5&&entry.yMax<=viewport.yMax,"Codex final row is clipped");
            CheckButtonHit("Codex.Tab.1");
        }

        IEnumerator CheckArenaCodex(bool captures)
        {
            string arena=Json.Write(app.Config);
            yield return Press("Codex.Open");
            yield return Press("Codex.Tab.0");
            var labels=CodexScroll.GetComponentsInChildren<Text>();
            Require(CountCodexEntries()==15&&labels.All(label=>!label.text.Contains("库存")&&!label.text.Contains("训练 Lv.")&&
                !label.text.Contains("靶场 Lv."))&&Json.Write(app.Config)==arena,"Duel codex exposed manor progression");
            if(captures)yield return ManorShot("150-arena-codex");
            yield return Press("Codex.Tab.3");
            Require(CountCodexEntries()==9&&!Displays(Manor.Armament(Manor.HeroIds[0],3).title),"Duel codex exposed equipped armament");
            yield return Press("关闭");
            arenaCodexVerified=true;
        }

        IEnumerator CheckPortraitCodex(bool captures)
        {
            yield return Press("Codex.Open");
            yield return Press("Codex.Tab.3");
            yield return new WaitForEndOfFrame();
            var modal=HudElement<RectTransform>("游戏图鉴");
            var rect=ScreenRect(modal);
            Require(rect.xMin>=9&&rect.yMin>=12&&rect.xMax<=Screen.width-9&&rect.yMax<=Screen.height-12,
                "Portrait codex extends past the display");
            for(int i=0;i<4;i++)CheckButtonHit("Codex.Tab."+i);
            var scroll=CodexScroll;
            var labels=scroll.content.GetComponentsInChildren<Text>();
            foreach(var label in labels)
                Require(label.preferredHeight<=label.rectTransform.rect.height+1,"Portrait codex clipped text: "+label.text);
            CheckCodexReading();
            if(captures)
            {
                yield return RevealCodexEntry(Manor.Mason);
                yield return ManorShot("160-portrait-codex");
            }
            yield return Press("关闭");
            portraitCodexVerified=true;
        }
    }
}
