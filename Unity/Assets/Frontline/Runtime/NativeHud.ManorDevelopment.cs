using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        void BackyardDialog(int plot)
        {
            OpenDialog("农舍后院 · 建地 "+plot,(p,w)=>{
                Label(p,"每座农舍可经营一项副业。选中即启用或切换。",0,0,w,28,portrait?11:14,pale);
                float action=portrait?71:91;
                for(int i=0;i<Manor.Backyards.Count;i++)
                {
                    var item=Manor.Backyards[i];
                    var row=Panel(p,item.title,0,34+i*82,w,77,manorInset,Slice.Inset);
                    Label(row,item.title,9,2,w-action-22,27,portrait?14:17,gold);
                    Label(row,Manor.BackyardEffect(item.id,app.Estate.slots[plot].level),9,30,w-action-22,
                        portrait?24:21,portrait?10:13,white);
                    Label(row,item.coin+" 金币 · "+item.timber+" 建材",9,52,w-action-22,24,12,pale);
                    var button=ManorButton(row,"Manor.Backyard."+item.id,"启用",w-action-8,35,action,31,
                        ()=>app.SetManorBackyard(plot,item.id),true);
                    var caption=button.GetComponentInChildren<Text>();
                    if(portrait)caption.fontSize=11;
                    manorDialogValues.Add(()=>{
                        var estate=app.Estate;
                        string blocked=estate.BackyardBlock(plot,item.id);
                        button.interactable=blocked==null;
                        caption.text=blocked==null?(estate.slots[plot].backyard==null?"启用":"切换"):
                            blocked=="已启用"?"已启用":"物资不足";
                    });
                }
                p.sizeDelta=new Vector2(w,34+Manor.Backyards.Count*82);
            },560,maxWidth:610);
        }

        static readonly string[] DevelopmentLines={"农耕林牧","商贸工艺","军备边防"};
        void DevelopmentDialog()
        {
            OpenDialog("领地发展",(p,w)=>{
                var points=Label(p,"",0,0,w,30,portrait?15:18,gold);
                points.name="Manor.Development.Points";
                Value(manorDialogValues,points,()=>"可用 "+app.Estate.DevelopmentPointsAvailable+" 点  ·  已用 "+
                    app.Estate.DevelopmentPointsSpent+" / 已得 "+app.Estate.DevelopmentPointsEarned);
                Label(p,"称号晋升和领取三项宏愿可获得发展点。每项专精消耗 1 点。",0,32,w,portrait?34:25,portrait?11:13,pale);
                float y=portrait?72:62,cell=w/2;
                for(int line=0;line<DevelopmentLines.Length;line++)
                {
                    Label(p,DevelopmentLines[line],0,y,w,25,14,gold);
                    for(int tier=0;tier<2;tier++)
                    {
                        var perk=(ManorPerk)(2*line+tier);
                        var definition=Manor.Developments[(int)perk];
                        var card=Panel(p,definition.title,tier*cell,y+29,cell-5,111,manorInset,Slice.Inset);
                        Label(card,definition.title,9,2,cell-22,28,portrait?14:16,gold);
                        Label(card,definition.effect,9,32,cell-23,38,portrait?10:12,pale,TextAnchor.UpperLeft);
                        float badgeWidth=portrait?53:80;
                        var state=Badge(card,"",9,78,badgeWidth,23,portrait?10:12);
                        var badge=state.transform.parent.GetComponent<Image>();
                        var button=ManorButton(card,"Manor.Development."+(int)perk,"解锁",badgeWidth+15,75,cell-badgeWidth-28,29,
                            ()=>app.UnlockManorDevelopment(perk),true);
                        var caption=button.GetComponentInChildren<Text>();
                        manorDialogValues.Add(()=>{
                            string blocked=app.Estate.DevelopmentBlock(perk);
                            bool learned=app.Estate.HasDevelopment(perk);
                            BadgeLook(state,badge,learned?"已解锁":blocked==null?"可解锁":"未解锁",good:learned,active:blocked==null);
                            button.interactable=blocked==null;
                            caption.text=learned?"已研习":blocked??"解锁";
                            caption.fontSize=portrait?10:13;
                        });
                    }
                    y+=147;
                }
                p.sizeDelta=new Vector2(w,y);
            },700,(bar,w)=>ManorButton(bar,"Manor.Development.Back","返回政务",0,3,w,40,GovernanceDialog),maxWidth:720);
        }

        void ChronicleDialog()
        {
            OpenDialog("领地编年史",(p,w)=>{
                var entries=app.Estate.chronicle;
                if(entries.Count==0)Label(p,"领地大事将在这里按日期记录。",0,0,w,40,14,pale);
                float dateWidth=portrait?69:106;
                for(int i=entries.Count-1,rowIndex=0;i>=0;i--,rowIndex++)
                {
                    var entry=entries[i];
                    var row=Panel(p,"日 "+entry.day,0,rowIndex*37,w,33,manorInset);
                    Label(row,"第 "+entry.day+" 日",8,0,dateWidth-12,33,portrait?10:12,gold);
                    Label(row,Manor.ChronicleText(entry),dateWidth,0,w-dateWidth-7,33,portrait?11:14,white);
                }
                p.sizeDelta=new Vector2(w,Mathf.Max(45,entries.Count*37-4));
            },670,(bar,w)=>ManorButton(bar,"Manor.Chronicle.Back","返回政务",0,3,w,40,GovernanceDialog),maxWidth:720);
        }
    }
}
