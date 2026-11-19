using System;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        static readonly Color thriving = new Color(.58f,.90f,.63f);
        static readonly string[] TaxPolicies = {"low","normal","high"};
        static readonly string[] TaxLabels = {"免税","常规轻税","重税"};
        static readonly string[] DecreePolicies = {"none","agrarian","mobilize","trade"};
        static readonly string[] DecreeLabels = {"休养生息","重农囤粮","战时总动员","商贸兴镇"};

        Color ApprovalColor(int approval) => approval<Manor.UnrestApproval?danger:approval>=Manor.FlourishingApproval?thriving:gold;
        static string ApprovalStatus(int approval) =>
            approval<Manor.UnrestApproval?"怨声载道":approval>=Manor.FlourishingApproval?"安居乐业":"平稳运转";
        static string Change(int points) => points>0?"+"+points:points.ToString();

        void ApprovalMeter(RectTransform ledger,float x,float w)
        {
            Panel(ledger,"Divider",x,8,1,ledger.sizeDelta.y-16,manorButton).GetComponent<Image>().raycastTarget=false;
            var title=Label(ledger,"",x+9,2,w-18,18,portrait?11:12,pale);
            var score=Label(ledger,"",x+9,20,w-18,ledger.sizeDelta.y-21,portrait?15:17,gold);
            Value(manorValues,title,()=>app.Estate.ProsperityTitle);
            Value(manorValues,score,()=>"民心 "+app.Estate.approval);
            manorValues.Add(()=>score.color=ApprovalColor(app.Estate.approval));
        }

        string ForecastFactors(Manor m,ManorDay forecast,bool heatingShortage)
        {
            string season;
            switch(m.Season)
            {
                case 0: season="春播 田产 100%";break;
                case 1: season="盛夏 田产 +"+(m.CropSeasonPercent-100)+"%";break;
                case 2: season="秋收 田产 +"+(m.CropSeasonPercent-100)+"%";break;
                default:
                    season="冬季 自动休耕 · 精耕 "+(m.CropSeasonPercent-100)+"% · 牧场 "+(m.PastureSeasonPercent-100)+"% · "+
                        (heatingShortage?"缺柴 ":"耗柴 ")+Manor.FormatHundredths(forecast.winterTimberRequired-(heatingShortage?forecast.winterTimber:0));
                    break;
            }
            int percent=m.ApprovalProductionPercent;
            return season+" · 民心 "+(percent==100?"标准产出":Change(percent-100)+"%")+
                (percent<100?" · 暂停迁入":!portrait&&forecast.forgeTimber>0?" · 锻造耗材 "+(forecast.forgeTimber/100):"");
        }

        void GovernanceDialog()
        {
            app.SeeGovernance();
            OpenDialog("领地政务与集市",BuildGovernance,maxHeight:800,maxWidth:760);
        }

        void BuildGovernance(RectTransform p,float w)
        {
            float actionWidth=portrait?80:100;
            var title=Label(p,"",0,0,w-actionWidth-8,28,portrait?14:18,gold);
            var status=Label(p,"",0,27,w*.59f,24,portrait?10:13,pale);
            var marketStatus=Label(p,"",w*.59f,27,w*.41f-1,24,portrait?10:13,thriving,TextAnchor.MiddleRight);
            marketStatus.name="Manor.MarketNeeds.Summary";
            EventEntry(p,"Manor.Governance.Event",w-actionWidth,1,actionWidth,26,manorDialogValues,fromGovernance:true);
            float nav=w/4;
            ManorButton(p,"Manor.Specialists","领地专才",0,52,nav-5,28,SpecialistsDialog);
            var milestones=ManorButton(p,"Manor.Milestones","领主宏愿",nav,52,nav-5,28,MilestonesDialog);
            var development=ManorButton(p,"Manor.Development","领地发展",2*nav,52,nav-5,28,DevelopmentDialog);
            var developmentImage=development.GetComponent<Image>();
            var developmentTitle=development.GetComponentInChildren<Text>();
            ManorButton(p,"Manor.Chronicle","编年史",3*nav,52,nav-1,28,ChronicleDialog);
            var milestoneImage=milestones.GetComponent<Image>();
            var milestoneTitle=milestones.GetComponentInChildren<Text>();
            var milestoneColor=milestoneTitle.color;
            manorDialogValues.Add(()=>{
                bool ready=app.Estate.ReadyMilestones!=0;
                milestoneImage.color=ready?gold:manorButton;
                milestoneTitle.color=ready?ink:milestoneColor;
                bool hasPoint=app.Estate.DevelopmentPointsAvailable>0;
                developmentImage.color=hasPoint?gold:manorButton;
                developmentTitle.color=hasPoint?ink:white;
            });
            var rations=MarketNeed(p,w,0,"Rations");
            var fuel=MarketNeed(p,w,1,"Fuel");
            var spirit=MarketNeed(p,w,2,"Spirit");
            string[] factorNames={"税率","食谱","酒馆","冬柴","政令"};
            var notes=new Text[factorNames.Length];
            int columns=portrait?3:factorNames.Length;
            float span=w/columns;
            for(int i=0;i<notes.Length;i++)
            {
                int height=portrait?34:28;
                var cell=Panel(p,factorNames[i],i%columns*span,(portrait?122:115)+i/columns*38,span-5,height,manorInset);
                notes[i]=Label(cell,"",3,0,span-11,height,portrait?11:13,pale,TextAnchor.MiddleCenter);
            }
            var support=Label(p,"",0,portrait?197:146,w,portrait?42:35,portrait?10:12,pale,TextAnchor.UpperLeft);
            manorDialogValues.Add(()=>{
                var m=app.Estate;
                var forecast=PreviewForecast();
                var factors=forecast.approvalFactors;
                int today=forecast.approvalAfter-m.approval;
                title.text=m.ProsperityTitle+"  ·  民心 "+m.approval+" / 100";
                title.color=ApprovalColor(m.approval);
                status.text=ApprovalStatus(m.approval)+"  ·  日变化 "+Change(today)+" → "+forecast.approvalAfter;
                if(today!=factors.Total&&!portrait)status.text+="（触及限值）";
                var needs=forecast.marketNeeds;
                SetMarketNeed(rations,needs.rations,needs.variedDiet?"口粮 · 食谱丰富":needs.rations?"口粮 · 足粮":"口粮 · 缺粮");
                SetMarketNeed(fuel,needs.fuel,m.Season!=3?"薪柴 · 非冬":needs.fuel?"薪柴 · 足额":"薪柴 · 不足");
                SetMarketNeed(spirit,needs.spirit,needs.spirit?"精神 · 有供给":"精神 · 待派工");
                marketStatus.text=needs.Prosperous?"市集兴旺 · 民心 +1":"";

                Factor(notes[0],"税率",factors.tax);
                Factor(notes[1],factors.food<0?"缺粮":factors.food>0?"食谱丰富":"食谱",factors.food);
                Factor(notes[2],factors.tavern>0?"酒馆运转":"酒馆",factors.tavern);
                Factor(notes[3],forecast.winterTimber<forecast.winterTimberRequired?"冬柴不足":forecast.winterTimberRequired>0?"冬柴足额":"冬柴",factors.heating);
                Factor(notes[4],"政令",factors.decree);
                string links=EstateSynergyText(forecast.synergy);
                support.text=(links.Length==0?"":links+"\n")+"每日民心  礼拜堂 "+Change(factors.chapel)+" · 安居 "+
                    Change(factors.settled)+" · 医者 "+Change(factors.herbalist)+
                    (forecast.reserveFood>0?" · 储备 "+Manor.FormatHundredths(forecast.reserveFood)+" 粮":"");
            });

            float y=portrait?244:183;
            int policyButtons=portrait?23:20, policyEffect=portrait?59:51;
            Label(p,"税率政策",0,y,w,20,14,gold);
            PolicyChoices(p,w,y+policyButtons,"Manor.Tax.",TaxPolicies,TaxLabels,()=>app.Estate.tax,app.SetManorTax);
            Value(manorDialogValues,Label(p,"",1,y+policyEffect,w-2,21,12,pale),
                ()=>"税收 ×"+Manor.FormatHundredths(app.Estate.TaxRevenuePercent)+"  ·  民心 "+Change(app.Estate.TaxApprovalChange)+"/日");
            y+=portrait?86:72;
            Label(p,"领地政令",0,y,w,20,14,gold);
            PolicyChoices(p,w,y+policyButtons,"Manor.Decree.",DecreePolicies,DecreeLabels,()=>app.Estate.decree,app.SetManorDecree);
            Value(manorDialogValues,Label(p,"",1,y+policyEffect,w-2,portrait?34:21,12,pale,TextAnchor.UpperLeft),()=>DecreeEffect(app.Estate.decree));
            y+=portrait?101:72;

            Label(p,"快捷调配",0,y,w,20,14,gold);
            float presetWidth=w/3;
            ManorButton(p,"Manor.Work.food","务农备粮",0,y+23,presetWidth-5,31,()=>app.AssignManorPreset("food"));
            ManorButton(p,"Manor.Work.smithing","伐木锻造",presetWidth,y+23,presetWidth-5,31,()=>app.AssignManorPreset("smithing"));
            ManorButton(p,"Manor.Work.balanced","均衡分配",presetWidth*2,y+23,presetWidth-5,31,()=>app.AssignManorPreset("balanced"));
            y+=portrait?66:60;
            y+=InfluenceActions(p,w,y);

            Label(p,"行商集市",0,y,w*.45f,26,16,gold);
            Value(manorDialogValues,Label(p,"",w*.45f,y,w*.55f-5,26,13,white,TextAnchor.MiddleRight),
                ()=>"金币 "+Manor.FormatHundredths(app.Estate.coin*100+app.Estate.remainder.coin));
            Value(manorDialogValues,Label(p,"",0,y+(portrait?25:22),w,21,12,pale),
                ()=>"每种物资每季各买卖 "+app.Estate.MarketSeasonLimit+" 份 · 余 "+(Manor.SeasonDays-(app.Estate.day-1)%Manor.SeasonDays)+" 日刷新");
            y+=portrait?50:43;
            MarketRow(p,w,y,"food","粮食");
            MarketRow(p,w,y+60,"timber","建材");
            MarketRow(p,w,y+120,"arms","军械");
            p.sizeDelta=new Vector2(w,y+176);
        }

        float InfluenceActions(RectTransform p,float width,float y)
        {
            float labelWidth=portrait?width:width*.25f;
            var influence=Label(p,"",0,y,labelWidth-5,portrait?24:31,portrait?12:13,gold);
            influence.name="Manor.Influence";
            float x=portrait?0:labelWidth, row=portrait?y+26:y, cell=(width-x)/3;
            var tithe=ManorButton(p,"Manor.Tithe","什一赈济",x,row,cell-5,30,app.ToggleManorTithe);
            var festival=ManorButton(p,"Manor.Festival","领地庆典 · "+Manor.FestivalCost,x+cell,row,cell-5,30,app.HoldManorFestival);
            var merchant=ManorButton(p,"Manor.Merchant","特许行商 · "+Manor.MerchantCost,x+cell*2,row,cell-1,30,app.CallManorMerchant);
            var caption=tithe.GetComponentInChildren<Text>();
            if(portrait)
                foreach(var button in new[]{tithe,festival,merchant})button.GetComponentInChildren<Text>().fontSize=11;
            manorDialogValues.Add(()=>{
                var estate=app.Estate;
                var forecast=PreviewForecast();
                influence.text="威望 "+estate.influence+" / "+Manor.InfluenceMax+
                    (forecast.titheFood>0?" · 赈 "+forecast.titheFood+" 粮":"");
                caption.text="什一赈济 · "+(estate.tithe==0?"关":estate.tithe+"%");
                festival.interactable=estate.influence>=Manor.FestivalCost;
                merchant.interactable=estate.influence>=Manor.MerchantCost;
            });
            return portrait?62:35;
        }

        Text MarketNeed(Transform parent,float width,int index,string key)
        {
            float cell=width/3;
            int height=portrait?30:28;
            var panel=Panel(parent,key,index*cell,portrait?87:83,cell-5,height,manorInset);
            var label=Label(panel,"",4,0,cell-13,height,portrait?10:13,pale,TextAnchor.MiddleCenter);
            label.name="Manor.MarketNeeds."+key;
            return label;
        }

        void SetMarketNeed(Text label,bool fulfilled,string text)
        {
            label.text=text;
            label.color=fulfilled?thriving:danger;
            label.transform.parent.GetComponent<Image>().color=fulfilled?
                new Color(.14f,.28f,.19f):new Color(.32f,.18f,.15f);
        }

        void Factor(Text label,string title,int points)
        {
            label.text=title+" "+Change(points);
            label.color=points<0?danger:points>0?thriving:pale;
        }

        void PolicyChoices(Transform p,float w,float y,string prefix,string[] ids,string[] labels,Func<string> selected,Action<string> select)
        {
            float cell=w/ids.Length;
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i];
                var button=ManorButton(p,prefix+id,labels[i],i*cell,y,cell-5,31,()=>{
                    if(selected()!=id)select(id);
                });
                var image=button.GetComponent<Image>();
                var label=button.GetComponentInChildren<Text>();
                if(portrait)label.fontSize=11;
                manorDialogValues.Add(()=>{
                    bool active=selected()==id;
                    image.color=active?gold:manorButton;
                    label.color=active?ink:white;
                });
            }
        }

        static string DecreeEffect(string rule)
        {
            switch(rule)
            {
                case "agrarian": return "农田与牧场 +20% · 军械 -15%";
                case "mobilize": return "军械 +25% · 募兵金币 -10% · 民心 -1/日";
                case "trade": return "税收 +20% · 买价 -10% · 卖价 +10% · 田产 -10%";
                default: return "按常规产出与价格结算";
            }
        }

        void MarketRow(RectTransform parent,float w,float y,string resource,string name)
        {
            var row=Panel(parent,resource,0,y,w,55,manorInset);
            float left=portrait?68:140, cell=(w-left)/2, buttonW=Mathf.Min(105,(cell-13)/2);
            var stock=Label(row,"",8,0,left-14,55,portrait?12:14,white);
            var buyPrice=Label(row,"",left,1,cell-8,21,portrait?10:12,pale);
            var sellPrice=Label(row,"",left+cell,1,cell-8,21,portrait?10:12,pale);
            var buy=ManorButton(row,"Manor.Trade.Buy."+resource,"买 1",left,23,buttonW,27,()=>app.TradeManor(resource,1));
            var buyFive=ManorButton(row,"Manor.Trade.Buy5."+resource,"买 5",left+buttonW+5,23,buttonW,27,()=>app.TradeManor(resource,5));
            var sell=ManorButton(row,"Manor.Trade.Sell."+resource,"卖 1",left+cell,23,buttonW,27,()=>app.TradeManor(resource,-1));
            var sellFive=ManorButton(row,"Manor.Trade.Sell5."+resource,"卖 5",left+cell+buttonW+5,23,buttonW,27,()=>app.TradeManor(resource,-5));
            manorDialogValues.Add(()=>{
                var quote=app.Estate.QuoteTrade(resource);
                stock.text=name+(portrait?"\n":"  ")+quote.inventory;
                buyPrice.text=Manor.FormatHundredths(quote.buyPrice)+" 金  ·  余 "+quote.buyRemaining;
                sellPrice.text=Manor.FormatHundredths(quote.sellPrice)+" 金  ·  余 "+quote.sellRemaining;
                buy.interactable=quote.buyLimit>0;
                buyFive.interactable=quote.buyLimit>=5;
                sell.interactable=quote.sellLimit>0;
                sellFive.interactable=quote.sellLimit>=5;
            });
        }
    }
}
