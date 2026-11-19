using System.Collections;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        bool fertilityVerified,retinueVerified,influenceVerified,mercenaryVerified;
        float governanceBottomMargin;

        void CheckGovernanceViewport()
        {
            Canvas.ForceUpdateCanvases();
            var scroll=VisibleButton("Manor.Trade.Buy.food").GetComponentInParent<ScrollRect>();
            Require(scroll.content.anchoredPosition.y<.5f&&scroll.content.rect.height<=scroll.viewport.rect.height+.5f,
                "Governance requires scrolling at 1280×800");
            governanceBottomMargin=float.PositiveInfinity;
            var corners=new Vector3[4];
            foreach(var resource in new[]{"food","timber","arms"})
                foreach(var action in new[]{"Buy","Buy5","Sell","Sell5"})
                {
                    string name="Manor.Trade."+action+"."+resource;
                    CheckButtonHit(name);
                    ((RectTransform)VisibleButton(name).transform).GetWorldCorners(corners);
                    foreach(var corner in corners)
                    {
                        var point=scroll.viewport.InverseTransformPoint(corner);
                        Require(scroll.viewport.rect.Contains(point),"Market button clipped: "+name);
                        governanceBottomMargin=Mathf.Min(governanceBottomMargin,point.y-scroll.viewport.rect.yMin);
                    }
                }
            Require(governanceBottomMargin>=5,"Market needs an inset below the last trade row");
        }

        IEnumerator CheckFertilityAndSupport(bool captures)
        {
            string original=app.Estate.Export();
            var estate=Manor.Import(original);
            estate.day=48; estate.eventDay=0; estate.food=400; estate.arms=100; estate.influence=150; estate.approval=65;
            estate.slots[2].fertility=Manor.DefaultFertility;
            estate.SetRotation(2,"auto");
            app.ImportText(estate.Export());
            estate=app.Estate;

            yield return CheckFrontierPreset(1);
            yield return PickPlot(2);
            Require(ManorReadout("Manor.Fertility").text.Contains("地力 100%")&&estate.IsRestingField(2)&&
                app.View.DisplayedCrop(2)==ManorPlotArt.CropAppearance.Fallow,"Auto winter fallow card or model");
            yield return Press("Manor.Rotation.intensive");
            Require(estate.slots[2].rotation=="intensive"&&!estate.IsRestingField(2),"Intensive button did not enable winter cropping");
            yield return Press("Manor.Rotation.fallow");
            var field=new Vector3(FrontierMap.ManorX+Manor.SlotX(2),.63f,Manor.SlotZ(2));
            app.View.Focus(field,8.2f,44,-19);
            if(captures)yield return ManorShot("143-farm-rotation");
            yield return Press("Manor.Day");
            Require(estate.slots[2].fertility==120&&estate.IsRestingField(2),"Fallow did not restore the field across seasons");
            yield return Press("Manor.Rotation.auto");
            yield return new WaitForEndOfFrame();
            Require(ManorReadout("Manor.Fertility").text.Contains("地力 120%")&&
                app.View.DisplayedCrop(2)==ManorPlotArt.CropAppearance.Rich,"Fertile field card or gold crop variant");
            fertilityVerified=true;

            yield return Press("Manor.Governance");
            CheckGovernanceViewport();
            yield return Press("Manor.Tithe");
            Require(estate.tithe==10&&estate.Forecast().titheFood>=1,"Tithe toggle or crop surplus");
            int approval=estate.approval;
            yield return Press("Manor.Festival");
            Require(estate.approval==approval+8&&estate.influence==125&&estate.chronicle.Exists(x=>x.kind=="festival"),
                "Festival did not charge and record approval");
            yield return Press("Manor.Trade.Buy.food");
            Require(estate.market.food.bought==1,"Trade precondition");
            yield return Press("Manor.Merchant");
            Require(estate.market.food.bought==0&&estate.influence==105,"Chartered merchant did not reset the market");
            if(captures)yield return ManorShot("143-governance");
            yield return Press("关闭");
            yield return Press("Manor.Day");
            Require(estate.influence==106,"Daily tithe did not grant influence");
            influenceVerified=true;

            yield return CheckFrontierPreset(1);
            yield return PickPlot(0);
            yield return Press("Manor.Retinue.Open");
            yield return Press("Manor.Retinue.Recruit");
            yield return Press("Manor.Retinue.Recruit");
            yield return Press("Manor.Retinue.Upgrade");
            Require(estate.RetinueReady==2&&estate.retinue.level==2&&
                ManorReadout("Manor.Retinue.Armor").text=="披甲 Lv.2  ·  战斗 Lv.4","Retinue summary did not show both levels");
            Require(PlayerPrefs.GetString(FrontierSaveKey)==estate.Export(),"Retinue recruitment or armor not saved");
            yield return Press("Manor.Retinue.View");
            yield return WaitForState(()=>!app.View.CameraMoving,"Retinue camera did not settle");
            yield return new WaitForEndOfFrame();
            Require(app.View.VisibleRetinue==2&&!app.View.LowActorLod,"Retinue close-up lost either guard or its high model");
            for(int i=0;i<2;i++)
            {
                var p=app.View.ViewCamera.WorldToViewportPoint(BattleView.RetinuePost(i)+Vector3.up);
                Require(p.x>.1f&&p.x<.9f&&p.y>.23f&&p.y<.78f&&p.z>0,"Retinue post clipped in close-up");
            }
            if(captures)yield return ManorShot("143-retinue-closeup");
            retinueVerified=true;

            string stock=Json.Write(estate.stock);
            int influence=estate.influence,coin=estate.coin;
            yield return Press("Manor.Patrol");
            yield return Press("Manor.Support.Guard.Plus");
            yield return Press("Manor.Support.Guard.Plus");
            yield return Press("Manor.Support.Mercenaries");
            Require(estate.influence==influence&&estate.coin==coin&&app.PartyUnits(true)<=estate.FrontierLimit,
                "The support draft charged early or exceeded its limit");
            yield return Press("Manor.Patrol.Start");
            yield return new WaitForEndOfFrame();
            Require(app.InFrontier&&app.PlannedSupport.guards==2&&app.PlannedSupport.mercenaries&&app.View.VisibleRetinue==0&&
                estate.influence==influence-Manor.MercenaryCost&&estate.coin==coin-Manor.MercenaryCoin,"The patrol contract or guard departure did not apply");
            yield return Press("Frontier.Settle");
            Require(app.Phase=="manor"&&Displays("自由兵团 · 3 人参战 · 战损 0 · 合约已结束")&&app.Estate.RetinueReady==2&&
                app.Estate.influence==influence-Manor.MercenaryCost&&Json.Write(app.Estate.stock)==stock&&
                PlayerPrefs.GetString(FrontierSaveKey)==app.Estate.Export(),"Support return did not settle and save");
            yield return Press("Manor.Settlement.Done");
            mercenaryVerified=true;
            Debug.Log("FRONTLINE_FERTILITY_SUPPORT_OK fertility=120 retinue=2 armor=2 contract=3 marketInset="+governanceBottomMargin);
            app.ImportText(original);
        }
    }
}
