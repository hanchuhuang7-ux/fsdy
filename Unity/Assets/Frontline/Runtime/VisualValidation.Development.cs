using System.Collections;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        bool backyardVerified,developmentVerified,marketNeedsVerified,chronicleVerified;

        IEnumerator CheckManorDevelopment(bool captures)
        {
            var estate=app.Estate;
            yield return CheckFrontierPreset(1);
            yield return PickPlot(1);
            int coin=estate.coin, timber=estate.timber;
            int food=estate.Forecast().food;
            yield return Press("Manor.Backyard");
            yield return Press("Manor.Backyard.garden");
            Require(estate.slots[1].backyard=="garden"&&estate.coin==coin-8&&estate.timber==timber-4&&
                estate.Forecast().food==food+145,"Garden was not settled at the cottage's Lv.3 rate");
            yield return Press("Manor.Backyard.poultry");
            Require(estate.slots[1].backyard=="poultry"&&estate.Forecast().food==food+80&&
                !VisibleButton("Manor.Backyard.poultry").IsInteractable(),"Switching backyard did not replace the old yield");
            yield return Press("关闭");
            Require(Displays("后院 鸡舍  ·  每日粮食 +0.8 · 金币 +0.6 · 独立副食")&&
                PlayerPrefs.GetString(FrontierSaveKey)==estate.Export(),"Cottage card or save did not update");
            backyardVerified=true;
            if(captures)
            {
                var cottage=new Vector3(FrontierMap.ManorX+Manor.SlotX(1)+.28f,.68f,Manor.SlotZ(1)-.53f);
                app.View.Focus(cottage,7.6f,41,-20);
                yield return ManorShot("142-backyard");
            }

            yield return Press("Manor.Governance");
            Require(ManorReadout("Manor.MarketNeeds.Rations").text=="口粮 · 食谱丰富"&&
                ManorReadout("Manor.MarketNeeds.Fuel").text=="薪柴 · 足额"&&
                ManorReadout("Manor.MarketNeeds.Spirit").text=="精神 · 有供给"&&
                ManorReadout("Manor.MarketNeeds.Summary").text=="市集兴旺 · 民心 +1"&&
                estate.Forecast().approvalFactors.market==1,"Market lights did not agree with the forecast");
            marketNeedsVerified=true;
            if(captures)yield return ManorShot("142-market-needs");
            yield return Press("Manor.Development");
            int available=estate.DevelopmentPointsAvailable;
            Require(available>0&&!VisibleButton("Manor.Development.1").IsInteractable(),"Development prerequisite check");
            int before=estate.Forecast().food;
            yield return Press("Manor.Development.0");
            Require(estate.HasDevelopment(ManorPerk.HeavyPlough)&&estate.DevelopmentPointsAvailable==available-1&&
                estate.Forecast().food>before&&PlayerPrefs.GetString(FrontierSaveKey)==estate.Export(),"Development purchase did not settle");
            developmentVerified=true;
            if(captures)yield return ManorShot("145-development");
            yield return Press("Manor.Development.Back");
            yield return Press("Manor.Chronicle");
            Require(Displays("解锁专精 · 重犁深耕")&&Displays("开垦外环建地 18")&&estate.chronicle.Count<=Manor.ChronicleLimit,
                "Chronicle omitted a new event or exceeded capacity");
            chronicleVerified=true;
            if(captures)yield return ManorShot("142-chronicle");
            yield return Press("关闭");
            Debug.Log("FRONTLINE_MANOR_DEVELOPMENT_OK backyard=poultry perk=heavy_plough needs=3 chronicle="+estate.chronicle.Count);
        }
    }
}
