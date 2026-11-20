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
            float todoWidth=portrait?0:184;
            var title=Label(p,"",0,0,w-actionWidth-8-todoWidth,28,portrait?14:18,gold);
            Text next=portrait?null:Label(p,"",w-actionWidth-todoWidth-9,0,todoWidth,28,12,gold,TextAnchor.MiddleRight);
            if(next)next.name="Manor.Todos.Priority";
            var status=Label(p,"",0,27,w*.59f,24,portrait?10:13,pale);
            var marketStatus=Label(p,"",w*.59f,27,w*.41f-1,24,portrait?10:13,thriving,TextAnchor.MiddleRight);
            marketStatus.name="Manor.MarketNeeds.Summary";
            EventEntry(p,"Manor.Governance.Event",w-actionWidth,1,actionWidth,26,manorDialogValues,fromGovernance:true);
            float nav=w/7;
            ManorButton(p,"Manor.Specialists",portrait?"专才":"领地专才",0,52,nav-5,28,SpecialistsDialog);
            var development=ManorButton(p,"Manor.Development",portrait?"发展":"领地发展",nav,52,nav-5,28,DevelopmentDialog);
            var milestones=ManorButton(p,"Manor.Milestones",portrait?"宏愿":"领主宏愿",2*nav,52,nav-5,28,MilestonesDialog);
            var claims=ManorButton(p,"Manor.Claims",portrait?"疆域":"边疆疆域",3*nav,52,nav-5,28,TerritoryDialog);
            var nobility=ManorButton(p,"Manor.Nobility",portrait?"爵位":"贵族爵位",4*nav,52,nav-5,28,NobilityDialog);
            var records=ManorButton(p,"Manor.Chronicle","编年史",5*nav,52,nav-5,28,ChronicleDialog);
            var ledger=ManorButton(p,"Manor.Ledger.Open",portrait?"账本":"收支账本",6*nav,52,nav-1,28,LedgerDialog);
            if(portrait)
            {
                records.GetComponentInChildren<Text>().fontSize=11;
                ledger.GetComponentInChildren<Text>().fontSize=11;
            }
            manorDialogValues.Add(()=>{
                var todo=app.Estate.Todos;
                SetTodoLook(development,(todo&ManorTodo.Development)!=0);
                SetTodoLook(milestones,(todo&ManorTodo.Milestone)!=0);
                SetTodoLook(claims,(todo&ManorTodo.Claim)!=0);
                SetTodoLook(nobility,(todo&ManorTodo.Nobility)!=0);
                if(next)next.text=NextTodoText(todo);
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
                int height=portrait?24:28;
                var cell=Panel(p,factorNames[i],i%columns*span,(portrait?119:115)+i/columns*27,span-5,height,manorInset,Slice.Inset);
                notes[i]=Label(cell,"",3,0,span-11,height,portrait?11:13,pale,TextAnchor.MiddleCenter);
            }
            var support=Label(p,"",0,portrait?175:146,w,portrait?43:35,portrait?10:12,pale,TextAnchor.UpperLeft);
            manorDialogValues.Add(()=>{
                var m=app.Estate;
                var forecast=PreviewForecast();
                var factors=forecast.approvalFactors;
                int today=forecast.approvalAfter-m.approval;
                title.text=m.NobleTitle+" · "+m.ProsperityTitle+" · 民心 "+m.approval+" / 100";
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
                string links=EstateSynergyText(forecast.synergy,portrait);
                support.text=(links.Length==0?"":links+"\n")+"每日民心  礼拜堂 "+Change(factors.chapel)+" · 安居 "+
                    Change(factors.settled)+" · 医者 "+Change(factors.herbalist)+(factors.minstrel>0?" · 乐师 "+Change(factors.minstrel):"")+
                    (forecast.reserveFood>0?" · 储备 "+Manor.FormatHundredths(forecast.reserveFood)+" 粮":"");
            });

            float y=portrait?220:183;
            const int policyButtons=20, policyEffect=51;
            Label(p,"税率政策",0,y,w,20,14,gold);
            PolicyChoices(p,w,y+policyButtons,"Manor.Tax.",TaxPolicies,TaxLabels,()=>app.Estate.tax,app.SetManorTax);
            Value(manorDialogValues,Label(p,"",1,y+policyEffect,w-2,21,12,pale),
                ()=>"税收 ×"+Manor.FormatHundredths(app.Estate.TaxRevenuePercent)+"  ·  民心 "+Change(app.Estate.TaxApprovalChange)+"/日");
            y+=portrait?67:72;
            Label(p,"领地政令",0,y,w,20,14,gold);
            PolicyChoices(p,w,y+policyButtons,"Manor.Decree.",DecreePolicies,DecreeLabels,()=>app.Estate.decree,app.SetManorDecree);
            Value(manorDialogValues,Label(p,"",1,y+policyEffect,w-2,portrait?26:21,portrait?11:12,pale,TextAnchor.UpperLeft),()=>DecreeEffect(app.Estate.decree));
            y+=portrait?78:72;

            Label(p,"快捷调配",0,y,w*.4f,20,14,gold);
            Value(manorDialogValues,Label(p,"",w*.4f,y,w*.6f-5,20,portrait?10:12,pale,TextAnchor.MiddleRight),
                ()=>app.Estate.seasonalWork?"季候自适应 · 换季自动调配":"季候方案可在换季自动调配");
            float presetWidth=w/4;
            PolicyChoices(p,w-presetWidth,y+22,"Manor.Work.",new[]{"food","smithing","balanced"},
                new[]{"务农备粮","伐木锻造","均衡分配"},()=>null,app.AssignManorPreset);
            var seasonal=ManorButton(p,"Manor.Work.seasonal","季候自适应",presetWidth*3,y+22,presetWidth-1,31,
                ()=>app.AssignManorPreset("seasonal"));
            if(portrait)seasonal.GetComponentInChildren<Text>().fontSize=11;
            manorDialogValues.Add(()=>SetButtonTone(seasonal,app.Estate.seasonalWork?HudTone.Selected:HudTone.Secondary));
            y+=portrait?57:60;
            y+=InfluenceActions(p,w,y);

            Label(p,"行商集市",0,y,w*.45f,26,16,gold);
            Value(manorDialogValues,Label(p,"",w*.45f,y,w*.55f-5,26,13,white,TextAnchor.MiddleRight),
                ()=>"金币 "+Manor.FormatHundredths(app.Estate.coin*100+app.Estate.remainder.coin));
            float quotaWidth=portrait?w*.46f:w*.54f;
            Value(manorDialogValues,Label(p,"",0,y+23,quotaWidth-5,19,portrait?10:12,pale),
                ()=>"各限 "+app.Estate.MarketSeasonLimit+" 份 · "+(Manor.SeasonDays-(app.Estate.day-1)%Manor.SeasonDays)+" 日刷新");
            var caravan=Badge(p,"",quotaWidth,y+23,w-quotaWidth-1,19,portrait?11:12);
            caravan.name="Manor.Caravan";
            manorDialogValues.Add(()=>{
                var offer=app.Estate.SeasonalCaravan;
                string deal=offer.buyPercent<0?"购粮 −"+(-offer.buyPercent)+"%":
                    (offer.resource=="timber"?"售材":"售军械")+" +"+offer.sellPercent+"%";
                BadgeLook(caravan,caravan.transform.parent.GetComponent<Image>(),offer.title+" · "+deal,active:true);
            });
            y+=portrait?45:43;
            int rowStep=portrait?53:60;
            MarketRow(p,w,y,"food","粮食");
            MarketRow(p,w,y+rowStep,"timber","建材");
            MarketRow(p,w,y+rowStep*2,"arms","军械");
            p.sizeDelta=new Vector2(w,y+rowStep*3-(portrait?0:4));
        }

        float InfluenceActions(RectTransform p,float width,float y)
        {
            float labelWidth=portrait?width:width*.25f;
            var influence=Label(p,"",0,y,labelWidth-5,portrait?24:31,portrait?12:13,gold);
            influence.name="Manor.Influence";
            float x=portrait?0:labelWidth, row=portrait?y+22:y, cell=(width-x)/3;
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
                SetButtonTone(tithe,estate.tithe==0?HudTone.Secondary:HudTone.Selected);
                festival.interactable=estate.influence>=Manor.FestivalCost;
                merchant.interactable=estate.influence>=Manor.MerchantCost;
            });
            return portrait?55:35;
        }

        Text MarketNeed(Transform parent,float width,int index,string key)
        {
            float cell=width/3;
            int height=portrait?30:28;
            var label=Badge(parent,"",index*cell,portrait?86:83,cell-5,height,portrait?11:13);
            label.name="Manor.MarketNeeds."+key;
            return label;
        }

        void SetMarketNeed(Text label,bool fulfilled,string text)
        {
            BadgeLook(label,label.transform.parent.GetComponent<Image>(),text,good:fulfilled,blocked:!fulfilled);
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
                var label=button.GetComponentInChildren<Text>();
                if(portrait)label.fontSize=11;
                manorDialogValues.Add(()=>{
                    SetButtonTone(button,selected()==id?HudTone.Selected:HudTone.Secondary);
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
            int height=portrait?49:55;
            var row=Panel(parent,resource,0,y,w,height,manorInset,Slice.Inset);
            float left=portrait?68:140, cell=(w-left)/2, buttonW=Mathf.Min(105,(cell-13)/2);
            var stock=Label(row,"",8,0,left-14,height,portrait?12:14,white);
            var buyPrice=Label(row,"",left,1,cell-8,21,portrait?10:12,pale);
            var sellPrice=Label(row,"",left+cell,1,cell-8,21,portrait?10:12,pale);
            int top=portrait?21:23, buttonHeight=portrait?24:27;
            var buy=ManorButton(row,"Manor.Trade.Buy."+resource,"买 1",left,top,buttonW,buttonHeight,()=>app.TradeManor(resource,1));
            var buyFive=ManorButton(row,"Manor.Trade.Buy5."+resource,"买 5",left+buttonW+5,top,buttonW,buttonHeight,()=>app.TradeManor(resource,5));
            var sell=ManorButton(row,"Manor.Trade.Sell."+resource,"卖 1",left+cell,top,buttonW,buttonHeight,()=>app.TradeManor(resource,-1));
            var sellFive=ManorButton(row,"Manor.Trade.Sell5."+resource,"卖 5",left+cell+buttonW+5,top,buttonW,buttonHeight,()=>app.TradeManor(resource,-5));
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
