using System;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        static readonly string[] LedgerResources={"金币","粮食","建材","军械","民心"};
        static readonly string[] LedgerSources={
            "宅邸税收","基础供给与安居","农田收成","牧场产出","林场采集","铁匠锻造","面包房出炉","织坊布匹",
            "农舍后院","外域特产","建筑协同","发展专精","专才助益","爵位特权","各户口粮","冬季取暖","锻造耗材","烘焙投粮",
            "谷仓储备","新户安置","什一赈济","税率","口粮与食谱","酒馆服务","礼拜堂","领地政令","市集兴旺","库存与民心限值"
        };
        int ledgerTab;

        void LedgerDialog() => OpenDialog("领地收支账本",BuildLedger,675,
            (p,w)=>ManorButton(p,"Manor.Ledger.Back","返回领地政务",0,3,w,40,GovernanceDialog),
            maxWidth:704,wholeRows:true,header:LedgerHeader);

        void LedgerHeader(RectTransform p,float w)
        {
            float cell=w/LedgerResources.Length;
            for(int i=0;i<LedgerResources.Length;i++)
            {
                int tab=i;
                ManorButton(p,"Manor.Ledger.Tab."+i,LedgerResources[i],cell*i,0,cell-5,34,()=>{
                    if(ledgerTab==tab)return;
                    ledgerTab=tab;
                    if(dialogContent)dialogContent.anchoredPosition=Vector2.zero;
                    LedgerDialog();
                },selected:ledgerTab==i);
            }
        }

        void BuildLedger(RectTransform p,float w)
        {
            int tab=ledgerTab;
            string Amount(int delta) => (delta>0?"+":"")+(tab==4?delta.ToString():Manor.FormatHundredths(delta));
            float right=portrait?96:138;
            var total=Panel(p,"Ledger summary",0,0,w,73,manorInset);
            Label(total,LedgerResources[tab]+" · 每日净收支",12,6,w-right-17,27,portrait?15:17,gold);
            var sumLabel=Label(total,"",w-right,4,right-12,29,20,thriving,TextAnchor.MiddleRight);
            sumLabel.name="Manor.Ledger.Total";
            Label(total,portrait?"按当前派工与政策预估下一日。\n交易、建造及武装锻造当次扣费。":
                "按当前派工、季候与政策预估下一日。集市交易、建造及武装锻造当次扣费。",
                12,35,w-24,35,portrait?11:13,pale,TextAnchor.UpperLeft);
            var rows=new RectTransform[LedgerSources.Length];
            var amounts=new Text[rows.Length];
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i]=Panel(p,"Ledger row "+i,0,0,w,36,manorInset,Slice.Inset);
                Label(row,LedgerSources[i],11,0,w-right-15,36,14,white);
                var label=amounts[i]=Label(row,"",w-right,0,right-12,36,15,white,TextAnchor.MiddleRight);
                label.name="Manor.Ledger.Line."+((ManorLedgerSource)i);
            }
            var empty=Label(p,"本日无变动",3,83,w-6,39,14,pale);
            int revision=-1;
            void UpdateLedger()
            {
                if(revision==app.ManorRevision)return;
                revision=app.ManorRevision;
                var ledger=app.Estate.DailyLedger();
                int sum=0;
                float y=83;
                for(int i=0;i<ledger.Length;i++)
                {
                    int delta=ledger[i].Amount((ManorLedgerResource)tab);
                    if(rows[i].gameObject.activeSelf!=(delta!=0))rows[i].gameObject.SetActive(delta!=0);
                    if(delta==0)continue;
                    sum+=delta;
                    amounts[i].text=Amount(delta);
                    amounts[i].color=delta<0?danger:thriving;
                    rows[i].anchoredPosition=new Vector2(0,-y);
                    y+=41;
                }
                empty.gameObject.SetActive(y==83);
                sumLabel.text=Amount(sum);
                sumLabel.color=sum<0?danger:sum>0?thriving:pale;
                p.sizeDelta=new Vector2(w,y==83?126:y);
            }
            UpdateLedger();
            manorDialogValues.Add(UpdateLedger);
        }

        void HeroForgeDialog(Action back)
        {
            Action<RectTransform,float> footer=null;
            if(back!=null)footer=(p,w)=>ManorButton(p,"Manor.Gear.Back","返回",0,3,w,40,back);
            OpenDialog("英雄专属武装锻造",BuildHeroForge,738,footer,maxWidth:790,wholeRows:true);
        }

        void BuildHeroForge(RectTransform p,float w)
        {
            var resources=Label(p,"",0,0,w,27,portrait?12:14,gold);
            Value(manorDialogValues,resources,()=>{
                var estate=app.Estate;
                return "金币 "+estate.coin+" · 军械 "+estate.arms+" · 建材 "+estate.timber+" · 威望 "+estate.influence;
            });
            Label(p,portrait?"先招募英雄。武装随远征、巡边与迎敌生效。":
                "在英雄酒馆或铁匠铺锻造。武装随领地远征、边疆巡边和迎战掠夺者生效。",
                0,28,w,27,portrait?11:13,pale);
            float y=60, cardHeight=portrait?146:127;
            foreach(var hero in app.Data.Data.heroes)
            {
                string id=hero.id;
                var row=Panel(p,id,0,y,w,cardHeight,manorInset,Slice.Inset);
                var title=Label(row,hero.name+" · "+hero.subtitle,11,6,w-101,25,portrait?14:17,white);
                title.name="Manor.Gear.Title."+id;
                var state=Badge(row,"",w-91,9,80,20,11);
                state.name="Manor.Gear.Tier."+id;
                var name=Label(row,"",11,34,w-22,23,15,gold);
                var effect=Label(row,"",11,58,w-22,portrait?31:25,portrait?11:13,pale,TextAnchor.UpperLeft);
                var pay=Label(row,"",11,portrait?91:87,w-(portrait?92:125),portrait?49:35,portrait?11:12,white,TextAnchor.UpperLeft);
                float actionWidth=portrait?74:104;
                var action=ManorButton(row,"Manor.Gear.Forge."+id,"锻造",w-actionWidth-10,cardHeight-41,actionWidth,31,
                    ()=>app.ForgeHero(id),true);
                var buttonText=action.GetComponentInChildren<Text>();
                manorDialogValues.Add(()=>{
                    var estate=app.Estate;
                    int tier=estate.HeroArmament(id);
                    bool hired=estate.HeroLevel(id)>0, full=tier==Manor.MaxHeroArmament;
                    var item=Manor.Armament(id,full?tier:tier+1);
                    var block=estate.ForgeHeroBlock(id);
                    BadgeLook(state,state.transform.parent.GetComponent<Image>(),hired?tier+" / "+Manor.MaxHeroArmament+" 阶":"尚未招募",active:tier>0);
                    name.text=full?item.title:tier==0?"初锻 · "+item.title:
                        Manor.Armament(id,tier).title+"  →  "+item.title;
                    effect.text=(full?"":"下一阶 · ")+item.Effect;
                    pay.text=full?"武装已臻满阶":item.Price+(block==null?"":"\n"+block);
                    action.interactable=block==null;
                    buttonText.text=full?"已满阶":"锻造";
                });
                y+=cardHeight+7;
            }
            p.sizeDelta=new Vector2(w,y);
        }
    }
}
