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
        int forgedTiers,forgeClips;
        bool ledgerVerified,plotFiltersVerified,caravanVerified,portraitEconomyVerified,armedPatrolVerified,landscapeVerified;

        IEnumerator CheckArmaments(bool captures)
        {
            var estate=Manor.Import(app.Estate.Export());
            estate.arms=Math.Max(estate.arms,160);
            estate.coin=Math.Max(estate.coin,1400);
            estate.influence=Math.Max(estate.influence,180);
            app.ImportText(estate.Export());
            estate=app.Estate;
            app.SelectManorSlot(3);
            yield return Press("Manor.Heroes");
            yield return Press("Manor.Tavern.Gear");
            CheckModalSkin("英雄专属武装锻造");
            void Hear(string cue){if(cue=="forge")forgeClips++;}
            app.ManorSound+=Hear;
            try
            {
                foreach(string id in Manor.HeroIds)
                    for(int tier=1;tier<=3;tier++)
                    {
                        var gear=Manor.Armament(id,tier);
                        int arms=estate.arms, merit=estate.influence;
                        yield return Press("Manor.Gear.Forge."+id);
                        Require(estate.HeroArmament(id)==tier&&estate.arms==arms-gear.arms&&estate.influence==merit-gear.influence&&
                            estate.chronicle.Last().kind=="armament","Forge did not charge and retain its exact tier");
                        forgedTiers++;
                    }
            }
            finally {app.ManorSound-=Hear;}
            Require(forgeClips==9&&Manor.HeroIds.All(id=>!VisibleButton("Manor.Gear.Forge."+id).IsInteractable()&&
                ManorReadout("Manor.Gear.Tier."+id).text=="3 / 3 阶"),"Forge cap or audio cue failed");
            CheckEconomyLayout("英雄专属武装锻造");
            if(captures)yield return ManorShot("170-hero-forge");
            yield return Press("Manor.Gear.Back");
            yield return Press("关闭");
            app.SelectManorSlot(Manor.FirstNoblePlot);
            yield return Press("Manor.Smithy.Gear");
            Require(Displays(Manor.Armament(Manor.HeroIds[0],3).title),"Smithy could not open the forged sets");
            yield return Press("关闭");

            string saved=estate.Export();
            yield return Press("Manor.Patrol");
            yield return Press("Manor.March.Gear");
            Require(Displays(Manor.Armament(Manor.HeroIds[2],3).title),"Patrol roster lost the forged sets");
            yield return Press("Manor.Gear.Back");
            yield return Press("Manor.Force.Recommend");
            yield return Press("Manor.Patrol.Start");
            yield return WaitForState(()=>app.InFrontier&&app.Battle.Tick>=12,"Armed patrol did not start");
            Require(app.Config.red.Count(unit=>unit.armament==3)==3&&app.Config.blue.All(unit=>unit.armament==0),
                "Player loadout did not survive real patrol deployment or affected the opposing side");
            foreach(string id in Manor.HeroIds)
            {
                var soldier=app.Battle.Units.Single(unit=>unit.entity.type==id);
                var baseDef=app.Data.Heroes[id].AtLevel(estate.HeroLevel(id));
                var equipped=Manor.Armament(id,3);
                Require(Mathf.Abs(soldier.entity.maxHp-baseDef.WithLife(equipped))<.001f&&
                    Mathf.Abs(soldier.definition.damage-baseDef.damage*(100+equipped.attack)/100)<.001f&&
                    Mathf.Abs(soldier.definition.heal-baseDef.heal*(100+equipped.healing)/100)<.001f&&
                    soldier.definition.mass==baseDef.mass&&soldier.definition.scale==baseDef.scale,"Real armed patrol stats diverged");
            }
            armedPatrolVerified=true;
            app.ReturnManor();
            if(app.Modal)yield return Press("Manor.Settlement.Done");
            app.ImportText(saved);
        }

        IEnumerator CheckEconomy(bool captures,bool compact)
        {
            yield return Press("Manor.Governance");
            CheckGovernanceViewport();
            var offer=app.Estate.SeasonalCaravan;
            Require(ManorReadout("Manor.Caravan").text.StartsWith(offer.title,StringComparison.Ordinal)&&
                app.Estate.QuoteTrade(offer.resource).buyPrice>app.Estate.QuoteTrade(offer.resource).sellPrice,
                "Seasonal trading row or premium missing");
            if(!compact)
            {
                yield return Press("Manor.Work.seasonal");
                Require(app.Estate.seasonalWork&&((NativeHudButton)VisibleButton("Manor.Work.seasonal")).Tone==HudTone.Selected,
                    "Seasonal staffing was not activated");
                if(captures)yield return ManorShot("170-seasonal-market");
                caravanVerified=true;
            }
            yield return Press("Manor.Ledger.Open");
            for(int tab=0;tab<5;tab++)
            {
                yield return Press("Manor.Ledger.Tab."+tab);
                int sum=app.Estate.DailyLedger().Sum(line=>line.Amount((ManorLedgerResource)tab));
                string amount=(sum>0?"+":"")+(tab==4?sum.ToString():Manor.FormatHundredths(sum));
                Require(ManorReadout("Manor.Ledger.Total").text==amount,"Displayed ledger does not reconcile");
            }
            yield return Press("Manor.Ledger.Tab.1");
            CheckEconomyLayout("领地收支账本");
            CheckButtonHit("Manor.Ledger.Back");
            if(captures)yield return ManorShot(compact?"170-portrait-ledger":"170-ledger");
            yield return Press("Manor.Ledger.Back");
            yield return Press("关闭");

            app.SelectManorSlot(0);
            yield return Press("Manor.Plots");
            var m=app.Estate;
            int CountPlots()=>app.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Manor.Slot.",StringComparison.Ordinal));
            Require(CountPlots()==Manor.PlotCount,"Plot list lost a district");
            yield return Press("Manor.Plots.Filter.1");
            Require(CountPlots()==m.slots.Count(p=>p.workers<Manor.StaffLimit(p.kind)),"Understaffed filter disagrees with estate");
            yield return Press("Manor.Plots.Filter.2");
            Require(CountPlots()==Enumerable.Range(0,Manor.PlotCount).Count(m.CanUpgradePlot),"Upgrade filter included ineligible land");
            yield return Press("Manor.Plots.Filter.3");
            int index=Array.FindIndex(m.slots,plot=>plot.kind==null);
            Require(index>=0&&CountPlots()==m.slots.Count(plot=>plot.kind==null),"Empty or grant filter lost a plot");
            CheckEconomyLayout("建地清单");
            for(int i=0;i<4;i++)CheckButtonHit("Manor.Plots.Filter."+i);
            CheckButtonHit("Manor.Slot."+index);
            if(captures)yield return ManorShot(compact?"170-portrait-plots":"170-plot-filters");
            yield return Press("Manor.Slot."+index);
            Require(app.ManorSlot==index&&!app.Modal,"Filtered plot did not select and focus");
            if(compact)portraitEconomyVerified=true;
            else ledgerVerified=plotFiltersVerified=true;
        }

        void CheckEconomyLayout(string title)
        {
            var modal=HudElement<RectTransform>(title);
            var box=ScreenRect(modal);
            Require(box.xMin>=9&&box.yMin>=12&&box.xMax<=Screen.width-9&&box.yMax<=Screen.height-12,
                title+" exceeds the screen");
            foreach(var text in modal.GetComponentsInChildren<Text>())
                Require(text.preferredHeight<=text.rectTransform.rect.height+1,"Clipped text in "+title+": "+text.text);
        }

        IEnumerator CaptureArmamentArt(bool captures)
        {
            if(!captures)yield break;
            for(int hero=0;hero<Manor.HeroIds.Length;hero++)
            {
                int mask=1<<hero;
                string id=Manor.HeroIds[hero];
                var tavern=new Vector3(FrontierMap.ManorX+Manor.SlotX(3),.93f,Manor.SlotZ(3));
                app.View.Focus(tavern,10.3f,29,-11);
                yield return WaitForState(()=>(app.View.VisibleHeroMask&mask)!=0,"Missing standing hero "+id);
                app.View.Focus(app.View.ManorHeroPoint(hero)+Vector3.up*1.12f,5.5f,16,app.View.ManorHeroYaw(hero)+(hero==0?128:153));
                yield return new WaitForEndOfFrame();
                Require(!app.View.LowActorLod&&(app.View.VisibleHeroMask&mask)!=0&&app.Estate.HeroArmament(id)==3,"Hero close-up lost its armor");
                yield return ManorShot("170-hero-"+id);
            }
            app.View.Focus(BattleView.RetinuePost(1)+Vector3.up*.90f,5.6f,20,2);
            yield return new WaitForEndOfFrame();
            Require(!app.View.LowActorLod&&app.View.VisibleRetinue>0,"Retinue close-up lost its detailed mesh");
            yield return ManorShot("170-retinue");
            app.View.Focus(BattleView.QuayPoint,10.1f,40,-22);
            yield return new WaitForEndOfFrame();
            Require(!app.View.LowSceneryLod,"Quay close-up did not show the detail mesh");
            yield return ManorShot("170-quay-skiff");
        }

        IEnumerator CheckArenaLandscape(bool captures)
        {
            string original=Json.Write(app.Config);
            var c=new Deployment();
            void Place(string team,string type,float x,float z,bool prop=false)
            {
                for(int column=0;column<5;column++)for(int i=0;i<12;i++)
                {
                    float xx=x+(team=="red"?-1:1)*column*.83f;
                    float zz=z+(i+1)/2*(i%2==0?-1:1)*1.1f;
                    if(!app.Data.CanPlace(c,team,type,xx,zz,prop))continue;
                    if(prop)c.props.Add(new PropPlacement{type=type,team=team,x=xx,z=zz});
                    else c.Army(team).Add(new UnitPlacement(type,xx,zz));
                    return;
                }
                throw new InvalidOperationException("No capture slot for "+type);
            }
            Place("red","catapult",-5.1f,-7.3f);
            Place("red","barricade",-3.5f,-10.1f,true);
            Place("red","barrel",-5.8f,-9.1f,true);
            Place("blue","banner",3.1f,-8.8f,true);
            for(int i=0;i<4;i++)
            {
                Place("red",i<2?"shield":"spear",-2.8f,-5.7f-i*1.15f);
                Place("blue",i<2?"militia":"giant",2.7f,-5.8f-i*1.1f);
            }
            app.ImportText(Json.Write(c));
            yield return Press("开 战");
            yield return WaitForState(()=>app.Phase=="battle"&&app.Battle.Tick>=101,"Arena scenery fixture did not enter combat");
            yield return Press("暂停");
            app.View.Focus(new Vector3(-1.25f,1f,-7.7f),16.6f,22,154);
            yield return new WaitForEndOfFrame();
            Require(!app.View.LowActorLod&&!app.View.LowSceneryLod&&app.Battle.Props.Any(p=>p.entity.type=="barricade")&&
                app.Battle.Units.Any(u=>u.entity.type=="catapult")&&app.Config.red.All(unit=>unit.armament==0),
                "Battle scenery or fair army scope was lost");
            if(captures)yield return ManorShot("170-arena-siege");
            landscapeVerified=true;
            app.ReturnToDeploy();
            app.ImportText(original);
        }
    }
}
