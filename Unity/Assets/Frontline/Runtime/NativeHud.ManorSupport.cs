using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        static readonly string[] RotationIds = { "auto","intensive","fallow" };
        static readonly string[] RotationLabels = { "自动轮作","精耕","休耕" };

        void RotationControls(RectTransform panel,float width,int plot)
        {
            float left = portrait ? 134 : 182, cell = (width-left-7)/3;
            var soil = Label(panel,"",12,90,left-15,31,portrait?12:14,gold);
            soil.name = "Manor.Fertility";
            Value(manorValues,soil,()=>{
                var estate = app.Estate;
                int fertility = estate.slots[plot].fertility;
                return "地力 "+fertility+"%"+(estate.IsRestingField(plot)?" · 休耕中":fertility>=Manor.RichFertility?" · 沃田":"");
            });
            for (int i = 0; i < RotationIds.Length; i++)
            {
                string id = RotationIds[i];
                var control = ManorButton(panel,"Manor.Rotation."+id,RotationLabels[i],left+i*cell,90,cell-5,31,()=>{
                    if (app.Estate.slots[plot].rotation!=id) app.SetManorRotation(plot,id);
                });
                var caption = control.GetComponentInChildren<Text>();
                if (portrait) caption.fontSize = 12;
                manorValues.Add(()=>SetButtonTone(control,app.Estate.slots[plot].rotation==id?HudTone.Selected:HudTone.Secondary));
            }
        }

        void RetinueDialog()
        {
            OpenDialog("领主卫队",(panel,width)=>{
                var count = Label(panel,"",0,0,width,29,16,gold);
                count.name = "Manor.Retinue.Count";
                Value(manorDialogValues,count,()=>{
                    var estate = app.Estate;
                    return "编制 "+estate.retinue.count+" / "+estate.RetinueLimit+"  ·  在岗 "+estate.RetinueReady+
                        "  ·  待整补 "+estate.retinue.wounded;
                });
                var armor = Label(panel,"",0,31,width,24,14,white);
                armor.name = "Manor.Retinue.Armor";
                Value(manorDialogValues,armor,()=>"披甲 Lv."+app.Estate.retinue.level+"  ·  战斗 Lv."+app.Estate.RetinueBattleLevel);
                var recruit = ManorButton(panel,"Manor.Retinue.Recruit","招募亲兵",0,66,width,32,app.RecruitManorGuard,true);
                var upgrade = ManorButton(panel,"Manor.Retinue.Upgrade","强化披甲",0,104,width,32,app.UpgradeManorRetinue);
                var refill = ManorButton(panel,"Manor.Retinue.Refill","整补卫队",0,142,width,32,app.RefillManorRetinue);
                var focus = ManorButton(panel,"Manor.Retinue.View","查看宅邸门前卫队",0,185,width,32,()=>{
                    CloseDialog(); app.View.FocusManorRetinue();
                });
                var recruitText = recruit.GetComponentInChildren<Text>();
                var upgradeText = upgrade.GetComponentInChildren<Text>();
                var refillText = refill.GetComponentInChildren<Text>();
                manorDialogValues.Add(()=>{
                    var estate = app.Estate;
                    recruit.interactable = estate.CanRecruitGuard;
                    upgrade.interactable = estate.CanUpgradeRetinue;
                    refill.interactable = estate.CanRefillRetinue;
                    focus.interactable = estate.RetinueReady>0;
                    recruitText.text = "招募亲兵 · "+Manor.GuardCoin+" 金币 / "+Manor.GuardArms+" 军械";
                    upgradeText.text = estate.retinue.level==Manor.RetinueMaxLevel ? "披甲已满级" :
                        "强化披甲 · "+estate.RetinueUpgradeCoin+" 金币 / "+estate.RetinueUpgradeArms+" 军械";
                    refillText.text = estate.retinue.wounded==0 ? "卫队整齐" : "整补卫队 · "+estate.RetinueRefillCoin+" 金币";
                });
            },maxWidth:500);
        }

        float MusterSupport(RectTransform panel,float width,float y,bool frontier,bool raid)
        {
            ManorSupport Draft() => app.PartySupport(frontier,raid);
            float row = portrait ? 61 : 53;
            var guards = Panel(panel,"Retinue",0,y,width,row-5,manorInset);
            var guardName = Label(guards,"",8,1,width-110,24,14,white);
            var guardHint = Label(guards,"",8,24,width-110,row-29,11,pale);
            var minus = ManorButton(guards,"Manor.Support.Guard.Minus","-",width-94,6,42,34,()=>{
                var draft = Draft();
                app.SelectManorSupport(new ManorSupport(draft.guards-1,draft.mercenaries),frontier,raid);
            });
            var plus = ManorButton(guards,"Manor.Support.Guard.Plus","＋",width-47,6,42,34,()=>{
                var draft = Draft();
                app.SelectManorSupport(new ManorSupport(draft.guards+1,draft.mercenaries),frontier,raid);
            });
            var company = Panel(panel,"Mercenaries",0,y+row,width,row-5,manorInset);
            Label(company,"自由兵团 · "+Manor.MercenarySize+" 人",8,1,width-112,24,14,white);
            var mercHint = Label(company,"",8,24,width-112,row-29,11,pale);
            var hire = ManorButton(company,"Manor.Support.Mercenaries","加入名册",width-101,6,96,34,()=>{
                var draft = Draft();
                app.SelectManorSupport(new ManorSupport(draft.guards,!draft.mercenaries),frontier,raid);
            },true);
            var caption = hire.GetComponentInChildren<Text>();
            var hint = Label(panel,"增援占用行军席位和军费，必要时会腾出普通名册席位。",0,y+row*2,width,portrait?32:22,11,pale);
            hint.alignment = TextAnchor.UpperLeft;
            manorDialogValues.Add(()=>{
                var draft = Draft();
                var estate = app.Estate;
                string guardBlock = app.PartySupportBlock(new ManorSupport(draft.guards+1,draft.mercenaries),frontier,raid);
                string mercBlock = app.PartySupportBlock(new ManorSupport(draft.guards,true),frontier,raid);
                guardName.text = "领主卫队  "+draft.guards+" / "+estate.RetinueReady;
                guardHint.text = estate.retinue.count==0 ? "在宅邸可招募亲兵" : guardBlock ?? "披甲 Lv."+estate.retinue.level+" · 战损回营可整补";
                mercHint.text = Manor.MercenaryCost+" 威望 / "+Manor.MercenaryCoin+" 金币"+
                    (!draft.mercenaries&&mercBlock!=null ? " · "+mercBlock : " · 开战签约");
                minus.interactable = draft.guards>0;
                plus.interactable = guardBlock==null;
                caption.text = draft.mercenaries ? "取消预选" : "加入名册";
                hire.interactable = draft.mercenaries||mercBlock==null;
            });
            return row*2+(portrait?34:25);
        }
    }
}
