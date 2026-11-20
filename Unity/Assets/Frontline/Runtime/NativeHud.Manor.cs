using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        readonly Color manorPanel = new Color(.25f,.29f,.25f,.975f);
        readonly Color manorInset = new Color(.27f,.30f,.265f,.99f);
        readonly Color pale = new Color(.81f,.82f,.75f);
        readonly Color danger = new Color(1f,.51f,.43f);
        readonly List<Action> manorValues = new List<Action>();
        readonly List<Action> manorDialogValues = new List<Action>();
        Text dialogNotice;
        int shownHover=-1,shownHoverRevision=-1;
        string hoverText;
        bool shownPlotCleared;
        RectTransform dialogContent;
        Action<RectTransform,float> dialogFooter,dialogHeader;
        float dialogHeight = 760, dialogWidth = 880;
        bool dialogWholeRows;
        bool builtManorMode,builtInBase;
        Manor shownEstate;
        int shownPlot,shownPlotLevel,chosenStage;
        int boundRevision=-1,boundSpeed=-1,boundSector=-1;
        string sectorStatus;
        string shownPlotKind,manorDraft = "";
        Manor forecastEstate;
        ManorDay forecastDay;
        int forecastRevision=-1;

        ManorDay PreviewForecast()
        {
            if(forecastEstate!=app.Estate||forecastRevision!=app.ManorRevision)
            {
                forecastEstate=app.Estate;
                forecastRevision=app.ManorRevision;
                forecastDay=forecastEstate.Forecast();
            }
            return forecastDay;
        }
        static readonly string[] Seasons = {"春","夏","秋","冬"};

        Button ManorButton(Transform parent,string id,string caption,float x,float y,float w,float h,
            Action action,bool accent=false,bool selected=false,bool destructive=false)
        {
            var button=Button(parent,caption,x,y,w,h,action,accent,selected,destructive);
            button.name=id;
            return button;
        }

        void TopBar()
        {
            float barHeight=portrait?89:68;
            var bar=Panel(baseRoot,"Top bar",0,0,rw,barHeight,new Color(.086f,.092f,.077f,.975f),null);
            Rule(bar,0,barHeight-1,rw);
            if(portrait)
            {
                Label(bar,"阵线实验室",10,3,76,32,14,gold);
                bool estate=app.Phase=="manor";
                CodexEntryButton(bar,89,3,44,34);
                QualityEntry(bar,138,3,estate?43:68,34);
                if(estate)
                {
                    Button(bar,"手册",186,3,42,34,ManorHelp);
                    Button(bar,"工坊",233,3,42,34,Workshop);
                    GovernanceEntry(bar,280,3,99,34);
                }
                else if(app.InManor)
                {
                    Button(bar,"手册",214,3,75,34,ManorHelp);
                    if(app.Phase=="deploy")Button(bar,"驻军",295,3,84,34,ExpeditionRoster);
                    else Button(bar,"战报",295,3,84,34,ReportDialog);
                }
                else
                {
                    Button(bar,"策略",214,3,75,34,Strategy);
                    Button(bar,"工坊",295,3,84,34,Workshop);
                }
                ModeTab(bar,false,10,43,182,40);
                ModeTab(bar,true,198,43,182,40);
            }
            else
            {
                Label(bar,"阵线实验室",20,5,325,37,23,gold);
                Label(bar,"FRONTLINE LAB   ·   "+Application.version,21,41,280,20,11,muted);
                ModeTab(bar,false,365,12,144,44);
                ModeTab(bar,true,517,12,144,44);
                CodexEntryButton(bar,671,15,53,38);
                QualityEntry(bar,730,15,110,38);
                if(app.InManor)
                {
                    if(app.Phase=="manor")
                    {
                        GovernanceEntry(bar,850,17,123,38);
                        Button(bar,"庄园工坊",985,17,145,38,Workshop);
                    }
                    else if(app.Phase=="deploy")Button(bar,"出征名册",985,17,145,38,ExpeditionRoster);
                    else Button(bar,"战报",985,17,145,38,ReportDialog);
                    Button(bar,"领主手册",1140,17,118,38,ManorHelp);
                }
                else
                {
                    Button(bar,"策略与演武擂台",850,17,160,38,Strategy);
                    Button(bar,"工坊 / 回放",1020,17,130,38,Workshop);
                    Button(bar,"说明",1160,17,100,38,Help);
                }
            }
        }

        void ModeTab(Transform parent,bool manor,float x,float y,float w,float h)
        {
            bool active=app.InManor==manor;
            var b=Button(parent,manor?"领地庄园":"演武斗蛐蛐",x,y,w,h,
                ()=>RequestMode(manor),selected:active);
            b.name=manor?"Mode.Manor":"Mode.Duel";
        }
        void RequestMode(bool manor)
        {
            if(app.InManor==manor)return;
            CloseDialog();
            if(!manor&&app.ConfirmManorExit)
            {
                bool frontier=app.InFrontier;
                OpenDialog(frontier?"结算本次巡边？":"放弃本次远征？",(p,w)=>{
                    string text=frontier?"切换到演武会召回部队，并保存本次巡边结果。":
                        (app.Phase=="battle"?"远征战斗正在进行。":"远征布阵正在进行。")+"切换到演武将撤回本次部队与道具。";
                    Label(p,text,0,0,w,52,15,white);
                },280,(bar,w)=>{
                    ManorButton(bar,"Manor.Stay",frontier?"继续巡边":"继续远征",0,3,w/2-4,40,CloseDialog,true);
                    ManorButton(bar,"Manor.Leave",frontier?"结算并切换":"放弃并切换",w/2+4,3,w/2-4,40,()=>{CloseDialog();app.SetManorMode(false,true);});
                });
                app.HoldExpedition=true;
            }
            else app.SetManorMode(manor);
        }

        static void RefreshBindings(List<Action> changes)
        {
            for(int i=0;i<changes.Count;i++)changes[i]();
        }

        void RefreshManorValues()
        {
            if(!builtInBase)return;
            if(boundRevision!=app.ManorRevision||boundSpeed!=app.ManorSpeed||boundSector!=app.ManorSector)
            {
                RefreshBindings(manorValues);boundRevision=app.ManorRevision;boundSpeed=app.ManorSpeed;
                boundSector=app.ManorSector;
            }
            RefreshBindings(manorDialogValues);
            ShowNotice(app.Message);
        }

        void StateChanged()
        {
            if(builtFrontier&&app.InFrontier&&Screen.width==width&&Screen.height==height&&!overlay)
            {
                RefreshBindings(frontierValues);UpdateReadouts();return;
            }
            if(!app.InManor||app.Phase!="manor"||!builtInBase||shownEstate!=app.Estate||
                width!=Screen.width||height!=Screen.height||shownPlot!=app.ManorSlot)
            {
                Build();
                return;
            }
            var slot=shownPlot>=0?app.Estate.slots[shownPlot]:null;
            if(shownPlotKind!=slot?.kind||shownPlotLevel!=(slot?.level??0)||
                shownPlotCleared!=app.Estate.IsPlotCleared(shownPlot))Build();
            else RefreshManorValues();
        }

        void Value(List<Action> changes,Text label,Func<string> value)
        {
            changes.Add(()=>{
                string text=value();
                if(label.text!=text)label.text=text;
            });
        }

        void ManorHud()
        {
            boundRevision=-1;
            shownHover=-1;
            sectorStatus=null;
            shownEstate=app.Estate;
            shownPlot=app.ManorSlot;
            var slot=shownPlot>=0?shownEstate.slots[shownPlot]:null;
            shownPlotKind=slot?.kind;
            shownPlotLevel=slot?.level??0;
            shownPlotCleared=shownEstate.IsPlotCleared(shownPlot);

            float ledgerW=portrait?374:650, resourcesW=portrait?278:510;
            var ledger=Panel(baseRoot,"Manor ledger",portrait?8:20,portrait?96:80,ledgerW,portrait?49:52,manorPanel);
            ledger.GetComponent<Image>().raycastTarget=false;
            Meter(ledger,0,resourcesW,"金币",gold,()=>app.Estate.coin,fraction:()=>app.Estate.remainder.coin);
            var grain=Meter(ledger,1,resourcesW,"粮食",new Color(.75f,.81f,.4f),()=>app.Estate.food);
            manorValues.Add(()=>grain.color=app.Estate.food==0?danger:white);
            Meter(ledger,2,resourcesW,"建材",new Color(.85f,.65f,.49f),()=>app.Estate.timber);
            Meter(ledger,3,resourcesW,"军械",new Color(.67f,.79f,.83f),()=>app.Estate.arms);
            Meter(ledger,4,resourcesW,portrait?"闲 / 总户":"闲户 / 总户",pale,()=>app.Estate.FreeWorkers,()=>app.Estate.workers);
            ApprovalMeter(ledger,resourcesW,ledgerW-resourcesW);
            SupplyForecast(ledgerW);
            FrontierCameraBar(true);

            int cropRow=Manor.IsCrop(shownPlotKind)?39:0;
            var card=Panel(baseRoot,"Manor plot card",portrait?8:20,rh-(portrait?339:181)-cropRow,
                portrait?374:532,132+cropRow,manorPanel);
            PlotCard(card,card.sizeDelta.x);
            var schedule=Panel(baseRoot,"Manor calendar",portrait?8:rw-456,rh-(portrait?199:171),
                portrait?374:436,portrait?116:122,manorPanel);
            Schedule(schedule,schedule.sizeDelta.x);
            var notice=Panel(baseRoot,"Manor status",portrait?14:208,rh-(portrait?74:40),portrait?362:864,portrait?64:32,ink);
            notice.GetComponent<Image>().raycastTarget=false;
            ManorStatus(notice);
        }

        internal void UpdateManorHover()
        {
            int index=!portrait&&app.Phase=="manor"&&!app.Modal&&app.ManorSector<0?app.View.HoveredPlot:-1;
            if(index<0||!status)
            {
                if(shownHover>=0)ShowNotice(app.Message);
                shownHover=-1;
                return;
            }
            if(shownHover!=index||shownHoverRevision!=app.ManorRevision)
            {
                shownHover=index;
                shownHoverRevision=app.ManorRevision;
                hoverText=app.ManorHoverStatus(index);
            }
            if(status.text!=hoverText)status.text=hoverText;
        }

        Text Meter(RectTransform strip,int i,float w,string name,Color color,Func<int> value,Func<int> total=null,Func<int> fraction=null)
        {
            float cell=w/5,x=i*cell;
            LedgerMark(strip,x,color);
            Label(strip,name,x+11,3,cell-12,18,portrait?10:12,pale);
            var label=Label(strip,"",x+11,20,cell-12,strip.sizeDelta.y-21,portrait?15:20,white);
            label.name="Manor.Ledger."+i;
            if(portrait)
            {
                label.resizeTextForBestFit=true;
                label.resizeTextMaxSize=15;
                label.resizeTextMinSize=9;
            }
            int shown=-1,shownTotal=-1,shownFraction=-1;
            manorValues.Add(()=>{
                int quantity=value(),capacity=total?.Invoke()??-1,hundredths=fraction?.Invoke()??0;
                if(quantity==shown&&capacity==shownTotal&&hundredths==shownFraction)return;
                shown=quantity;shownTotal=capacity;shownFraction=hundredths;
                readoutText.Clear();Number(quantity);
                if(total!=null){readoutText.Append(" / ");Number(capacity);}
                if(hundredths>0)
                {
                    readoutText.Append('.');
                    if(hundredths<10)readoutText.Append('0');
                    Number(hundredths%10==0?hundredths/10:hundredths);
                }
                if(portrait)label.fontSize=readoutText.Length>7?9:readoutText.Length>5?12:15;
                ApplyReadout(label);
            });
            return label;
        }

        void LedgerMark(RectTransform strip,float x,Color color)
        {
            var tick=Panel(strip,"Resource mark",x+5,10,2,strip.sizeDelta.y-20,color,null);
            tick.GetComponent<Image>().raycastTarget=false;
        }

        void Net(string resource,int value)
        {
            readoutText.Append(resource);
            if(value!=0)
                readoutText.Append(value>0?"<color=#A4D8A6>":value<=-300?"<color=#FF9782>":"<color=#F0BB78>");
            if((value/100d).TryFormat(digits,out int length,"+0.##;-0.##;0".AsSpan()))
                readoutText.Append(digits,0,length);
            if(value!=0)readoutText.Append("</color>");
        }
        void SupplyForecast(float w)
        {
            var panel=Panel(baseRoot,"Manor supply",portrait?8:20,portrait?149:136,w,portrait?72:79,ink);
            panel.GetComponent<Image>().raycastTarget=false;
            var net=Label(panel,"",11,3,w-22,22,portrait?11:14,white);
            net.name="Manor.Forecast";
            var limits=Label(panel,"",11,25,w-22,17,portrait?10:11,muted);
            var factors=Label(panel,"",11,41,w-22,17,portrait?10:11,pale);
            factors.name="Manor.Forecast.Factors";
            var synergy=Label(panel,"",11,56,w-22,portrait?15:19,portrait?9:11,thriving);
            synergy.name="Manor.Forecast.Synergy";
            int revision=-1;
            manorValues.Add(()=>{
                var m=app.Estate;
                if(revision==app.ManorRevision)return;
                revision=app.ManorRevision;
                bool shortage=m.food==0;
                limits.color=shortage?danger:muted;
                limits.text="常备军 "+m.ArmySize+"/"+m.ArmyLimit+"  ·  行军上限 "+m.FieldLimit+"  ·  巡边 "+m.FrontierLimit+(shortage?"  ·  缺粮":"");
                var f=PreviewForecast();
                readoutText.Clear();
                readoutText.Append("日净收支  ");
                Net("金 ",f.coin);
                Net("  ·  粮 ",f.food);
                Net("  ·  建材 ",f.timber);
                Net("  ·  军械 ",f.arms);
                net.text=readoutText.ToString();
                bool heatingShortage=f.winterTimber<f.winterTimberRequired;
                factors.color=heatingShortage||m.approval<Manor.UnrestApproval?danger:pale;
                factors.text=ForecastFactors(m,f,heatingShortage);
                string links=EstateSynergyText(f.synergy,portrait);
                synergy.text=f.marketNeeds.Prosperous?(portrait?"市集 +1":"市集兴旺 +1 民心")+(links.Length==0?"":"  ·  "+links):links;
            });
        }

        void PlotCard(RectTransform p,float w)
        {
            var estate=app.Estate;
            int index=app.ManorSlot;
            var slot=index<0?null:estate.slots[index];
            var kind=slot?.kind;
            bool ungranted=index>=0&&!estate.IsPlotGranted(index);
            bool uncleared=index>=0&&!ungranted&&!estate.IsPlotCleared(index);
            int rank=Manor.PlotRank(index);
            string title=index<0?"主基地":index==0?"领主宅邸":
                "建地 "+index+"   "+(ungranted?Manor.NobleRanks[rank].title+"封地":uncleared?"待开垦林地":kind==null?"空置":Manor.Name(kind));
            if(slot!=null&&slot.level>0)title+="   Lv."+slot.level;
            bool recruiting=kind=="barracks"||kind=="stables",heroes=kind=="tavern",training=kind=="drill_yard",cottage=kind=="cottage";
            bool smithy=kind=="smithy";
            float governanceWidth=portrait?64:82,retinueWidth=portrait?69:84;
            float governanceX=w-85-governanceWidth,retinueX=governanceX-5-retinueWidth;
            Label(p,title,12,4,index==0?retinueX-16:w-(recruiting||heroes||training||cottage||smithy?188:111),27,portrait?16:19,gold);
            if(cottage)ManorButton(p,"Manor.Backyard","后院副业",w-169,5,84,29,()=>BackyardDialog(index),true);
            if(index==0)
            {
                ManorButton(p,"Manor.Hall.Governance","领地政务",governanceX,5,governanceWidth,29,GovernanceDialog);
                ManorButton(p,"Manor.Retinue.Open","领主卫队",retinueX,5,retinueWidth,29,RetinueDialog,true);
            }
            if(recruiting)
                ManorButton(p,"Manor.Recruits",kind=="barracks"?"招募":"备军",w-151,5,66,29,Recruitment,true);
            if(heroes)ManorButton(p,"Manor.Heroes","英雄",w-151,5,66,29,HeroTavern,true);
            if(smithy)ManorButton(p,"Manor.Smithy.Gear","武装",w-151,5,66,29,()=>HeroForgeDialog(null),true);
            if(training)ManorButton(p,"Manor.Training","强化",w-151,5,66,29,BarracksTraining,true);
            ManorButton(p,"Manor.Plots","选地",w-80,5,68,29,PlotList);
            string description=index<0?"点击地块查看建筑、派工或建造。":
                ungranted?"晋升"+Manor.NobleRanks[rank].title+"后，此处即为可建造地块。":uncleared?"清理林地后即可建造。":"";
            if(index>=0&&!ungranted&&!uncleared)description=kind==null?"挑选一种建筑，查看所需金币与建材。":Manor.Description(kind);
            Label(p,description,12,33,w-24,34,portrait?12:14,pale);
            string production=kind==null?"":Manor.BuildingOutput(kind,slot.level);
            if(cottage)
                Value(manorValues,Label(p,"",12,63,w-24,24,portrait?11:13,gold),()=>{
                    string backyard=app.Estate.slots[index].backyard;
                    return backyard==null?"后院未启用":"后院 "+Manor.BackyardTitle(backyard)+"  ·  "+Manor.BackyardEffect(backyard,slot.level);
                });
            else if(production!="")
            {
                float left=12;
                if(index==0)
                {
                    var rankBadge=Badge(p,"",12,66,52,20,12);
                    rankBadge.name="Manor.Nobility.Hall";
                    var backdrop=rankBadge.transform.parent.GetComponent<Image>();
                    manorValues.Add(()=>BadgeLook(rankBadge,backdrop,app.Estate.NobleTitle,active:true));
                    left=70;
                }
                Value(manorValues,Label(p,"",left,66,w-left-12,20,portrait?11:13,gold),()=>production+PlotSynergyText(kind,PreviewForecast().synergy));
            }
            bool crop=Manor.IsCrop(kind);
            if(crop)RotationControls(p,w,index);
            float rowY=crop?129:90,h=portrait?33:38;

            if(index<0)
            {
                ManorButton(p,"Manor.Hall","查看宅邸",12,rowY,(w-30)/2,h,()=>app.SelectManorSlot(0));
                ManorButton(p,"Manor.FindLand","选择建地",18+(w-30)/2,rowY,(w-30)/2,h,PlotList,true);
                return;
            }
            if(ungranted)
            {
                ManorButton(p,"Manor.Nobility.Plot","查看爵位",12,rowY,w-24,h,NobilityDialog);
                return;
            }
            if(uncleared)
            {
                Label(p,"花费 "+Manor.ReclaimPrice+" 金币 · 收回 "+Manor.ReclaimTimber+" 建材",12,65,w-24,22,13,gold);
                var reclaim=ManorButton(p,"Manor.Reclaim","开垦建地",12,rowY,w-24,h,app.ReclaimManorPlot,true);
                var caption=reclaim.GetComponentInChildren<Text>();
                manorValues.Add(()=>{
                    var blocked=app.Estate.ReclaimBlock(index);
                    reclaim.interactable=blocked==null;
                    caption.text=blocked??"开垦建地";
                });
                return;
            }
            if(kind==null)
            {
                ManorButton(p,"Manor.Construction","建造",12,rowY,w-24,h,BuildingMenu,true);
                return;
            }

            int cap=Manor.StaffLimit(kind);
            float right=w-12;
            if(index>0)
            {
                right-=52;
                ManorButton(p,"Manor.Demolish","拆除",right,rowY,52,h,Demolition,destructive:true);
                right-=5;
            }
            right-=portrait?68:98;
            var upgrade=ManorButton(p,"Manor.Upgrade",slot.level>=Manor.BuildingMaxLevel?"已满级":"升级",right,rowY,
                portrait?68:98,h,Upgrade);
            upgrade.interactable=slot.level<Manor.BuildingMaxLevel;

            if(cap>0)
            {
                float knob=portrait?37:45;
                float labelWidth=Mathf.Min(right-knob*2-76,portrait?94:128);
                var label=Label(p,"",12,rowY,labelWidth,h,portrait?12:15,white);
                Value(manorValues,label,()=>"派工 "+app.Estate.slots[index].workers+" / "+cap+" 户");
                var idleBadge=Badge(p,"闲置",14+labelWidth,rowY+(h-22)/2,42,22,portrait?11:12);
                idleBadge.name="Manor.Plot.Idle";
                var idleBacking=idleBadge.transform.parent.GetComponent<Image>();
                BadgeLook(idleBadge,idleBacking,"闲置",active:true);
                var less=ManorButton(p,"Manor.Worker.Minus","-",right-knob*2-14,rowY,knob,h,()=>app.AssignWorker(-1));
                var more=ManorButton(p,"Manor.Worker.Plus","＋",right-knob-9,rowY,knob,h,()=>app.AssignWorker(1));
                manorValues.Add(()=>{
                    int assigned=app.Estate.slots[index].workers;
                    idleBacking.gameObject.SetActive(assigned==0);
                    less.interactable=assigned>0;
                    more.interactable=assigned<cap&&app.Estate.FreeWorkers>0;
                });
            }
            else if(kind=="cottage")
                Value(manorValues,Label(p,"",12,rowY,right-27,h,13,white),()=>"已入住 "+app.Estate.workers+" / "+app.Estate.Housing+" 户");
            else
                Value(manorValues,Label(p,"",12,rowY,right-27,h,13,white),()=>"军费 "+app.Estate.Budget+"   行军 "+app.Estate.FieldLimit+" 人");
        }

        string CalendarText()
        {
            int day=app.Estate.day;
            return Seasons[app.Estate.Season]+"季 · 第 "+day+" 日";
        }

        void Schedule(RectTransform p,float w)
        {
            float top=portrait?5:8;
            float x=w*.30f,buttonW=(w-20-x)/3;
            Value(manorValues,Label(p,"",10,top,x-13,32,portrait?13:17,gold),CalendarText);
            ManorButton(p,"Manor.Patrol","边疆巡边",x,top,buttonW,32,()=>MarchDialog(false,true),true);
            ManorButton(p,"Manor.Expedition","领地远征",x+buttonW+5,top,buttonW,32,()=>MarchDialog(false));
            var raid=ManorButton(p,"Manor.Raid","迎战掠夺者",x+(buttonW+5)*2,top,buttonW,32,()=>MarchDialog(true),destructive:true);
            if(portrait)raid.GetComponentInChildren<Text>().fontSize=11;
            float noticeY=portrait?80:43;
            Value(manorValues,Label(p,"",10,noticeY,w-126,30,13,pale),
                ()=>"远征 "+app.Estate.cleared+" / "+Manor.Route.Length+"   ·   "+(app.Estate.raidReady?"掠夺者已抵达":"边境巡守中"));
            EventEntry(p,"Manor.Event",w-114,noticeY,104,30,manorValues);
            float y=portrait?42:78,cell=(w-16)/7;
            ClockButton(p,0,"暂停",8,y,cell-4);
            ClockButton(p,1,"1×",8+cell,y,cell-4);
            ClockButton(p,2,"2×",8+cell*2,y,cell-4);
            ManorButton(p,"Manor.Day","推进一日",8+cell*3,y,cell*2-4,32,()=>app.AdvanceDays(1));
            ManorButton(p,"Manor.Season","推进一季",8+cell*5,y,cell*2-4,32,()=>app.AdvanceDays(Manor.SeasonDays));
        }

        void ClockButton(Transform p,int rate,string caption,float x,float y,float w)
        {
            var b=ManorButton(p,"Manor.Speed."+rate,caption,x,y,w,32,()=>app.SetManorSpeed(rate));
            manorValues.Add(()=>SetButtonTone(b,app.ManorSpeed==rate?HudTone.Selected:HudTone.Secondary));
        }

        void PlotList() => PlotList(0);

        static readonly string[] PlotFilters={"全部","闲置未满员","可升级","空地与封地"};

        void PlotList(int filter)
        {
            OpenDialog("建地清单",(p,w)=>{
                int columns=portrait?2:3;
                float cell=w/columns;
                const int row=51;
                var buttons=new Button[Manor.PlotCount];
                var captions=new Text[buttons.Length];
                for(int i=0;i<buttons.Length;i++)
                {
                    int index=i;
                    buttons[i]=ManorButton(p,"Manor.Slot."+i,"",0,0,cell-5,row-5,
                        ()=>{CloseDialog();app.SelectManorSlot(index,focus:true);},selected:app.ManorSlot==i);
                    captions[i]=buttons[i].GetComponentInChildren<Text>();
                    if(portrait)captions[i].fontSize=12;
                }
                var empty=Label(p,"没有匹配的建地",4,0,w-8,44,14,pale);
                int revision=-1;
                void UpdatePlots()
                {
                    if(revision==app.ManorRevision)return;
                    revision=app.ManorRevision;
                    var estate=app.Estate;
                    int shown=0;
                    for(int i=0;i<buttons.Length;i++)
                    {
                        var plot=estate.slots[i];
                        int capacity=Manor.StaffLimit(plot.kind);
                        bool include=filter switch {
                            1=>plot.workers<capacity, 2=>estate.CanUpgradePlot(i), 3=>plot.kind==null, _=>true
                        };
                        buttons[i].gameObject.SetActive(include);
                        if(!include)continue;
                        string name=(i==0?"中央":"建地 "+i)+(plot.kind==null?"":"  "+Manor.Name(plot.kind));
                        string state=!estate.IsPlotGranted(i)?Manor.NobleRanks[Manor.PlotRank(i)].title+"封地 · 未封授":
                            plot.kind==null?(estate.IsPlotCleared(i)?"空置":"待开垦"):
                            "Lv."+plot.level+(capacity>0?"  ·  派工 "+plot.workers+"/"+capacity:"");
                        captions[i].text=name+"\n"+state;
                        ((RectTransform)buttons[i].transform).anchoredPosition=new Vector2(shown%columns*cell,-(shown/columns*row));
                        shown++;
                    }
                    empty.gameObject.SetActive(shown==0);
                    p.sizeDelta=new Vector2(w,Mathf.Max(46,Mathf.Ceil(shown/(float)columns)*row-5));
                }
                UpdatePlots();
                manorDialogValues.Add(UpdatePlots);
            },570,maxWidth:840,wholeRows:true,header:(p,w)=>{
                float cell=w/PlotFilters.Length;
                for(int i=0;i<PlotFilters.Length;i++)
                {
                    int next=i;
                    var button=ManorButton(p,"Manor.Plots.Filter."+i,PlotFilters[i],i*cell,0,cell-5,33,()=>{
                        if(filter==next)return;
                        if(dialogContent)dialogContent.anchoredPosition=Vector2.zero;
                        PlotList(next);
                    },selected:filter==i);
                    if(portrait)button.GetComponentInChildren<Text>().fontSize=11;
                }
            });
        }

        void BuildingMenu()
        {
            OpenDialog("建造 · 建地 "+app.ManorSlot,(p,w)=>{
                float y=0;
                foreach(string entry in Manor.Buildings)
                {
                    if(entry=="manor_hall")continue;
                    string kind=entry;
                    float actionW=portrait?61:86;
                    string output=Manor.BuildingOutput(kind,1);
                    var row=Panel(p,kind,0,y,w,portrait?91:78,manorInset);
                    Label(row,Manor.Name(kind),9,1,w-actionW-20,25,16,white);
                    Label(row,Manor.Description(kind),9,26,w-18,portrait?32:22,12,pale);
                    Label(row,app.Estate.BuildPrice(kind)+(output==""?"":" · "+output),9,portrait?58:49,w-18,portrait?32:24,12,gold);
                    ManorButton(row,"Manor.Build."+kind,"建造",w-actionW-5,3,actionW,25,
                        ()=>{app.Construct(kind);if(app.Estate.slots[app.ManorSlot].kind==kind)CloseDialog();});
                    y+=row.sizeDelta.y+5;
                }
                p.sizeDelta=new Vector2(w,y);
            },portrait?696:578);
        }

        void Upgrade()
        {
            int index=app.ManorSlot;
            var s=app.Estate.slots[index];
            int before=s.level;
            OpenDialog("升级 · "+Manor.Name(s.kind),(p,w)=>{
                Label(p,"Lv."+before+"  →  Lv."+(before+1),0,0,w,35,20,white);
                string current=Manor.BuildingOutput(s.kind,before), next=Manor.BuildingOutput(s.kind,before+1);
                float y=40;
                if(next!="")
                {
                    Label(p,current+"\n→ "+next,0,y,w,54,13,pale);
                    y+=60;
                }
                Label(p,app.Estate.UpgradePrice(index),0,y,w,42,15,gold);
                p.sizeDelta=new Vector2(w,y+44);
            },320,(bar,w)=>ManorButton(bar,"Manor.ConfirmUpgrade","确认升级",0,3,w,40,()=>{
                app.UpgradeSlot();
                if(app.Estate.slots[index].level>before)CloseDialog();
            },true));
        }

        void Demolition()
        {
            int index=app.ManorSlot;
            var s=app.Estate.slots[index];
            OpenDialog("拆除 · "+Manor.Name(s.kind),(p,w)=>{
                Label(p,"拆除建地 "+index+" 的建筑？指派到此的领民将回到空闲户。",0,0,w,65,15,pale);
                p.sizeDelta=new Vector2(w,70);
            },230,(bar,w)=>ManorButton(bar,"Manor.ConfirmDemolish","拆除建筑",0,3,w,40,()=>{
                app.DemolishSlot();
                if(app.Estate.slots[index].kind==null)CloseDialog();
            },destructive:true));
        }

        string ItemName(string id)
        {
            return app.Data.Props.TryGetValue(id,out var prop)?prop.name:app.Data.Unit(id).name;
        }

        void Recruitment()
        {
            var slot=app.Estate.slots[app.ManorSlot];
            OpenDialog(Manor.Name(slot.kind)+" · 招募",(p,w)=>{
                var reserve=Label(p,"",0,0,w,29,14,gold);
                Value(manorDialogValues,reserve,()=>"金币 "+app.Estate.coin+"   军械 "+app.Estate.arms+"   驻军上限 "+app.Estate.ArmyLimit);
                float y=35;
                foreach(string entry in Manor.Recruits(slot.kind,slot.level))
                {
                    string id=entry;
                    var row=Panel(p,id,0,y,w,54,manorInset);
                    var name=Label(row,"",9,2,w-82,26,15,white);
                    Value(manorDialogValues,name,()=>ItemName(id)+"  ×"+app.Estate.Stock(id));
                    Label(row,app.Estate.RecruitPrice(id),9,28,w-82,22,12,pale);
                    ManorButton(row,"Manor.Recruit."+id,app.Data.Props.ContainsKey(id)?"制造":"招募",w-68,8,61,38,()=>app.Recruit(id));
                    y+=60;
                }
                p.sizeDelta=new Vector2(w,y);
            },590);
        }

        static string StatText(string name,float value,int level,bool next)
        {
            string amount=LevelScaling.Scale(value,level).ToString("0.##");
            return name+" "+amount+(next?"→"+LevelScaling.Scale(value,level+1).ToString("0.##"):"");
        }
        static string CombatStats(UnitDef unit,int level,int cap)
        {
            bool next=level<cap;
            return StatText("生命",unit.hp,level,next)+" · "+StatText("攻击",unit.damage,level,next)+
                (unit.heal>0?" · "+StatText("治疗/秒",unit.heal,level,next):"");
        }
        void HeroTavern()
        {
            int index=app.ManorSlot;
            OpenDialog("英雄酒馆 · 招募与培养",(p,w)=>{
                float forgeWidth=portrait?91:130;
                var supplies=Label(p,"",0,0,w-forgeWidth-7,32,portrait?11:13,gold);
                Value(manorDialogValues,supplies,()=>"金币 "+app.Estate.coin+" · 粮食 "+app.Estate.food+
                    (portrait?"":" · 每级核心数值 ×1.1"));
                ManorButton(p,"Manor.Tavern.Gear","武装锻造",w-forgeWidth,0,forgeWidth,30,()=>HeroForgeDialog(HeroTavern));
                float y=35;
                foreach(var hero in app.Data.Data.heroes)
                {
                    string id=hero.id;
                    float cardHeight=portrait?145:127;
                    var row=Panel(p,id,0,y,w,cardHeight,manorInset);
                    var title=Label(row,"",10,4,w-108,24,portrait?15:17,white);
                    var role=Label(row,hero.subtitle,10,28,w-108,20,12,gold);
                    var action=ManorButton(row,"Manor.Hero."+id,"招募",w-86,9,76,34,()=>{
                        if(app.Estate.HeroLevel(id)>0)app.UpgradeHero(id);
                        else app.HireHero(id);
                    },true);
                    var caption=action.GetComponentInChildren<Text>();
                    Label(row,hero.description,10,51,w-20,portrait?35:26,12,pale,TextAnchor.UpperLeft);
                    var stats=Label(row,"",10,portrait?87:79,w-20,portrait?27:20,12,white,TextAnchor.UpperLeft);
                    var payment=Label(row,"",10,portrait?116:100,w-20,portrait?25:22,12,gold,TextAnchor.UpperLeft);
                    manorDialogValues.Add(()=>{
                        var m=app.Estate;
                        int level=m.HeroLevel(id),rest=m.HeroRest(id);
                        bool hired=level>0;
                        title.text=hero.name+(hired?"  Lv."+level:"");
                        role.text=hero.subtitle+(rest>0?" · 休整 "+rest+" 日":"");
                        stats.text=CombatStats(hero,Math.Max(1,level),hired?Manor.HeroMaxLevel:1);
                        string reason=hired?m.HeroUpgradeBlock(index,hero):m.HeroHireBlock(index,hero);
                        action.interactable=reason==null;
                        caption.text=hired?(level==Manor.HeroMaxLevel?"满级":"升级"):"招募";
                        string price=!hired?m.HeroHirePrice(hero):level<Manor.HeroMaxLevel?m.HeroUpgradePrice(hero):"";
                        payment.text=price==""?reason:reason==null?price:price+" · "+reason;
                    });
                    y+=cardHeight+6;
                }
                p.sizeDelta=new Vector2(w,y);
            },640);
        }

        void BarracksTraining()
        {
            int index=app.ManorSlot;
            OpenDialog("兵营 · 兵种强化",(p,w)=>{
                var supplies=Label(p,"",0,0,w,32,13,gold);
                Value(manorDialogValues,supplies,()=>"金 "+app.Estate.coin+" · 粮 "+app.Estate.food+" · 军械 "+app.Estate.arms+" · 每级 ×1.1");
                float y=35;
                foreach(var unit in app.Data.Data.units)
                {
                    string id=unit.id;
                    var row=Panel(p,id,0,y,w,90,manorInset);
                    var name=Label(row,"",10,3,w-101,29,16,white);
                    var stats=Label(row,"",10,35,w-20,portrait?28:20,12,pale,TextAnchor.UpperLeft);
                    var price=Label(row,"",10,64,w-20,23,12,gold,TextAnchor.UpperLeft);
                    var action=ManorButton(row,"Manor.Train."+id,"升级",w-86,4,76,32,()=>app.TrainUnit(id),true);
                    var caption=action.GetComponentInChildren<Text>();
                    manorDialogValues.Add(()=>{
                        var m=app.Estate;
                        int level=m.UnitLevel(id);
                        bool full=level==Manor.UnitMaxLevel;
                        string reason=m.TrainBlock(index,id);
                        name.text=unit.name+"  Lv."+level+(full?"":" → "+(level+1));
                        stats.text=CombatStats(unit,level,Manor.UnitMaxLevel);
                        price.text=full?reason:m.TrainPrice(index,id)+(reason==null?"":" · "+reason);
                        caption.text=full?"满级":"升级";
                        action.interactable=reason==null;
                    });
                    y+=95;
                }
                p.sizeDelta=new Vector2(w,y);
            },680);
        }

        void MarchDialog(bool raid,bool frontier=false,int? stageToKeep=null)
        {
            chosenStage=stageToKeep??app.Estate.RaidStage;
            OpenDialog(frontier?"边疆巡边 · 集结队伍":raid?"迎敌 · 掠夺者小队":"远征 · 调派驻军",(p,w)=>{
                float y=0;
                if(frontier)
                {
                    y=FrontierEnemyPreview(p,w);
                }
                else if(!raid)
                {
                    for(int i=0;i<Manor.Route.Length;i++)
                    {
                        int stage=i;
                        string map=app.Data.Maps[Manor.Route[i]].name;
                        string caption=(i+1)+"  "+map+(i<=app.Estate.cleared?"":" · 锁");
                        float cell=w/(portrait?2:3);
                        int columns=portrait?2:3;
                        var stageButton=ManorButton(p,"Manor.Stage."+i,caption,(i%columns)*cell,(i/columns)*41,
                            cell-5,36,()=>{chosenStage=stage;RefreshBindings(manorDialogValues);});
                        manorDialogValues.Add(()=>SetButtonTone(stageButton,chosenStage==stage?HudTone.Selected:HudTone.Secondary));
                    }
                    y=portrait?129:88;
                }
                else
                {
                    Value(manorDialogValues,Label(p,"",0,0,w,33,14,pale),
                        ()=>app.Estate.raidReady?"西侧边界发现掠夺者":"派巡逻队主动迎敌");
                    y=39;
                }
                if(!frontier)y+=EnemyPreview(p,w,y,raid);
                var logistics=Label(p,"",0,y,w,portrait?45:27,12,pale);
                logistics.name="Manor.March.Logistics";
                manorDialogValues.Add(()=>{
                    var rules=app.Estate.ExpeditionLogistics(raid);
                    logistics.text=LogisticsText(rules);
                    logistics.color=rules.approvalPercent<0?danger:pale;
                });
                y+=portrait?50:32;
                Label(p,frontier?"巡边名册":"远征名册",0,y,w,29,16,gold);
                ManorButton(p,"Manor.March.Gear","武装锻造",w-103,y,102,28,()=>{
                    int stage=chosenStage;
                    HeroForgeDialog(()=>MarchDialog(raid,frontier,stage));
                });
                y+=34;
                y+=MusterSupport(p,w,y,frontier,raid);
                int availableItems=0;
                foreach(var hero in app.Data.Data.heroes)
                    if(app.Estate.HeroLevel(hero.id)>0){y+=ForceRow(p,w,y,hero.id,frontier,raid);availableItems++;}
                foreach(var unit in app.Data.Units.Values)
                    if(app.Estate.Stock(unit.id)>0){y+=ForceRow(p,w,y,unit.id,frontier,raid);availableItems++;}
                if(!frontier)
                    foreach(var prop in app.Data.Props.Values)
                        if(app.Estate.Stock(prop.id)>0){y+=ForceRow(p,w,y,prop.id,false,raid);availableItems++;}
                if(availableItems==0)
                {
                    Label(p,"常备军尚未集结，可征召兵员或在上方调派增援。",0,y,w,60,14,pale);
                    y+=66;
                }
                p.sizeDelta=new Vector2(w,y+9);
            },portrait?686:627,(bar,w)=>{
                int Limit()=>frontier?app.Estate.FrontierLimit:app.Estate.FieldLimit;
                float actionWidth=portrait?85:115;
                var total=Label(bar,"",6,0,w-2*actionWidth-23,45,13,white);
                Value(manorDialogValues,total,()=>"军费 "+app.PartyCost(frontier,raid)+" / "+app.Estate.ExpeditionLogistics(raid).Budget+
                    "\n兵员 "+app.PartyUnits(frontier,raid)+" / "+Limit());
                var recommend=ManorButton(bar,"Manor.Force.Recommend","一键编队",w-actionWidth*2-10,3,actionWidth,40,
                    ()=>app.RecommendManorForce(chosenStage,raid,frontier));
                manorDialogValues.Add(()=>recommend.interactable=app.Estate.ArmySize>0||Manor.HeroIds.Any(id=>app.Estate.Available(id)>0));
                var march=ManorButton(bar,frontier?"Manor.Patrol.Start":"Manor.March",frontier?"集结巡边":raid?"迎敌布阵":"出征布阵",w-actionWidth-4,3,actionWidth,40,
                    ()=>{if(frontier)app.PrepareFrontier();else app.PrepareExpedition(chosenStage,raid);},accent:!raid,destructive:raid);
                manorDialogValues.Add(()=>march.interactable=(frontier||raid||chosenStage<=app.Estate.cleared)&&app.PartyUnits(frontier,raid)>0&&
                    app.PartyCost(frontier,raid)<=app.Estate.ExpeditionLogistics(raid).Budget&&app.PartyUnits(frontier,raid)<=Limit());
            },wholeRows:true);
        }

        float FrontierEnemyPreview(RectTransform p,float w)
        {
            Label(p,"外域警戒 · 三营 "+FrontierMap.Guards.Count+" 名守军",0,0,w,28,15,gold);
            var labels=new[]{"1 阶 · 常规","2 阶 · 精训","3 阶 · 精锐"};
            float span=w/3;
            for(int i=0;i<labels.Length;i++)
            {
                int tier=i+1;
                var button=ManorButton(p,"Manor.FrontierTier."+tier,labels[i],i*span,31,span-5,33,()=>app.SetFrontierTier(tier));
                manorDialogValues.Add(()=>{
                    button.interactable=tier<=app.Estate.MaxFrontierTier;
                    SetButtonTone(button,tier==app.Estate.frontierTier?HudTone.Selected:HudTone.Secondary);
                });
            }
            var rewards=Label(p,"",0,67,w,portrait?35:25,12,pale,TextAnchor.UpperLeft);
            rewards.name="Manor.FrontierTier.Rewards";
            float y=portrait?104:95;
            var strength=Label(p,"",0,y,w,25,13,white);
            var advice=Label(p,"",0,y+27,w,24,12,pale);
            manorDialogValues.Add(()=>{
                var estate=app.Estate;
                int tier=estate.frontierTier;
                rewards.text=estate.MaxFrontierTier==1?"单次击退全部 15 名守军后解锁 2、3 阶":
                    "守军 Lv."+tier+" · 物资 ×"+Manor.FormatHundredths(FrontierMap.RewardPercent(tier))+
                    " · 每击退 5 人民心 +"+FrontierMap.ApprovalPerGroup(tier);
                int enemy=(int)Math.Round(FrontierMap.Roster(tier).Sum(id=>LevelScaling.Scale(app.Data.Unit(id).cost,tier)));
                strength.text="估算战力  我方 "+app.PartyStrength(true)+"  /  三营合计 "+enemy;
                advice.text=CounterHint(estate.EnemyThreat(app.Data,0,false,true));
            });
            Label(p,"越过桥头金线接战，退回安全区可脱离追击。",0,y+52,w,24,12,pale);
            return y+80;
        }

        float EnemyPreview(RectTransform p,float w,float y,bool raid)
        {
                var enemy=Panel(p,"Manor.EnemyPreview",0,y,w,portrait?99:78,manorInset);
                var strength=Label(enemy,"",9,1,w-18,25,14,gold);
                var roster=Label(enemy,"",9,27,w-18,portrait?43:22,12,pale,TextAnchor.UpperLeft);
                var advice=Label(enemy,"",9,portrait?73:51,w-18,23,12,danger);
                manorDialogValues.Add(()=>{
                    var estate=app.Estate;
                    var foes=estate.EnemyRoster(chosenStage,raid);
                    int budget=estate.EnemyCost(app.Data,chosenStage,raid);
                    strength.text="估算战力  我方 "+app.PartyStrength(false,raid)+"  /  敌军 "+budget+"（"+foes.Count+" 人）";
                    roster.text=string.Join("  ·  ",foes.GroupBy(id=>id).Select(g=>ItemName(g.Key)+" ×"+g.Count()));
                    advice.text=!raid&&chosenStage>estate.cleared?"通关第 "+chosenStage+" 关后可出征":
                        CounterHint(estate.EnemyThreat(app.Data,chosenStage,raid));
                    advice.color=!raid&&chosenStage>estate.cleared?danger:pale;
                });
                return enemy.sizeDelta.y+6;
        }

        static string CounterHint(ManorThreat threat)
        {
            switch(threat)
            {
                case ManorThreat.Mounted:return "敌军有骑兵 · 宜配长枪兵拦截冲锋";
                case ManorThreat.Armored:return "重甲偏多 · 宜配弩手或投石车";
                case ManorThreat.Ranged:return "远程偏多 · 宜配骑兵或盾卫";
                default:return "敌军阵容均衡 · 前排掩护远程与军医";
            }
        }

        string LogisticsText(ManorLogistics rules)
        {
            string budget="军费 "+rules.Budget;
            if(rules.approvalPercent<0)budget+=" · 民心低迷 · 后勤受限（"+Change(rules.approvalPercent)+"%）";
            if(rules.approvalPercent>0)budget+=" · 士气 "+Change(rules.approvalPercent)+"%";
            if(rules.prosperityPercent>0)budget+=" · 雄镇 "+Change(rules.prosperityPercent)+"%";
            if(rules.raidPercent>0)budget+=" · 巡庄 "+Change(rules.raidPercent)+"%";
            if(rules.developmentPercent>0)budget+=" · 要塞后勤 "+Change(rules.developmentPercent)+"%";
            if(rules.nobilityPercent>0)budget+=" · 爵位 "+Change(rules.nobilityPercent)+"%";
            string recovery=rules.recoveryRate*rules.recoveryDivisor==ManorLogistics.RecoveryDenominator?"1/"+rules.recoveryDivisor:
                Manor.FormatHundredths((rules.recoveryRate*100+1)/3)+"%";
            return budget+(portrait?"\n":"   ·   ")+"伤员救回 "+recovery+" · 重伤英雄休整 "+rules.heroRestDays+" 日";
        }

        float ForceRow(RectTransform p,float w,float y,string id,bool frontier,bool raid)
        {
            float h=portrait?54:50;
            var row=Panel(p,id,0,y,w,h,manorInset,Slice.Inset);
            bool hero=app.Data.Heroes.ContainsKey(id),prop=app.Data.Props.ContainsKey(id);
            int cost=(int)(prop?app.Data.Props[id].cost:app.Data.Unit(id).cost);
            int level=hero?app.Estate.HeroLevel(id):app.Estate.UnitLevel(id);
            var title=Label(row,ItemName(id)+(prop?"":"  Lv."+level),10,1,w-168,25,portrait?12:14,white);
            var reason=Label(row,"",10,24,w-110,h-26,portrait?10:11,pale);
            var count=Badge(row,"",w-165,4,61,20,12);
            var badge=count.transform.parent.GetComponent<Image>();
            var minus=ManorButton(row,"Manor.Force.Minus."+id,"-",w-95,6,43,38,()=>app.SelectForce(id,-1,frontier,raid));
            var plus=ManorButton(row,"Manor.Force.Plus."+id,"＋",w-48,6,43,38,()=>app.SelectForce(id,1,frontier,raid));
            manorDialogValues.Add(()=>{
                if(hero)
                {
                    int tier=app.Estate.HeroArmament(id);
                    title.text=ItemName(id)+" Lv."+level+(tier==0?"":" · "+tier+" 阶");
                }
                string blocked=app.ForceAddBlock(id,frontier,raid);
                var unit=prop?null:app.Data.Unit(id);
                reason.text=blocked!=null?cost+" 军费 · "+blocked:unit==null?cost+" 军费":
                    cost+" 军费 · "+CombatStats(unit,Math.Max(1,level),Math.Max(1,level));
                reason.color=blocked==null?pale:danger;
                int selected=app.ForceCount(id,frontier,raid);
                BadgeLook(count,badge,selected+" / "+(hero?1:app.Estate.Stock(id)),active:selected>0);
                minus.interactable=selected>0;
                plus.interactable=blocked==null;
            });
            return h+5;
        }

        void ExpeditionDeploy()
        {
            if(portrait)TouchSwitch();
            float w=portrait?374:358,h=portrait?210:237;
            var p=Panel(baseRoot,"Expedition deployment",portrait?8:16,rh-h-12,w,h,manorPanel);
            string map=app.Data.Maps[app.Config.map].name;
            Label(p,"出征布阵   /   "+map,11,4,w-22,29,17,gold);
            string selected=app.SelectedProp>=0?" · 已选 "+ItemName(app.Config.props[app.SelectedProp].type):" · 已选 "+app.Selected.Count+" 名";
            Label(p,app.Config.red.Count+" 名兵员 · "+app.Config.props.Count+" 件道具"+selected,12,33,w-24,22,12,pale);
            float y=portrait?58:65,step=(w-18)/4;
            ManorButton(p,"Manor.Select","选择",9,y,step-4,32,()=>{app.Tool="select";app.Refresh();},selected:app.Tool=="select");
            ManorButton(p,"Manor.Move","移动",9+step,y,step-4,32,()=>{app.Tool="move";app.Refresh();},selected:app.Tool=="move");
            var undo=ManorButton(p,"Manor.Undo","撤销",9+step*3,y,step-4,32,app.Undo);
            undo.interactable=app.CanUndo;
            ManorButton(p,"Manor.Formation","队形",9+step*2,y,step-4,32,()=>{
                OpenDialog("调整队形",(list,lw)=>{
                    var shapes=new[]{"square","line","wedge"};
                    var names=new[]{"方阵","横列","楔形"};
                    for(int i=0;i<shapes.Length;i++)
                    {
                        string shape=shapes[i];
                        ManorButton(list,"Manor.Shape."+shape,names[i],i*(lw/3),0,lw/3-5,39,()=>{CloseDialog();app.Arrange(shape);});
                    }
                    list.sizeDelta=new Vector2(lw,43);
                },176);
            });
            string[] stances={"推进","坚守","迂回","猎杀"};
            float quarter=(w-18)/4;
            for(int i=0;i<stances.Length;i++)
            {
                string stance=Catalog.Stances[i];
                Button(p,stances[i],9+quarter*i,y+37,quarter-4,32,()=>app.SetStance(stance),selected:app.Stance==stance);
            }
            y+=portrait?79:87;
            ManorButton(p,"Manor.CancelMarch","返回庄园",9,y,w*.35f-5,38,app.ReturnManor);
            ManorButton(p,"Manor.Start","开战",9+w*.35f,y,w*.65f-18,38,app.RequestStart,true);
            status=Label(p,app.Message,10,h-35,w-20,33,12,pale);
            if(!portrait)Button(baseRoot,"视角 · "+CameraName(),rw-171,91,151,36,()=>{
                app.View.CameraMode=(app.View.CameraMode+1)%3;app.Refresh();
            });
        }

        void ExpeditionRoster()
        {
            OpenDialog("出征名册",(p,w)=>{
                var rules=app.PlannedLogistics;
                Label(p,LogisticsText(rules),0,0,w,portrait?45:27,13,rules.approvalPercent<0?danger:gold);
                float y=portrait?50:34;
                var support=app.PlannedSupport;
                foreach(var group in app.Config.red.Take(app.Config.red.Count-support.Count).GroupBy(u=>u.type))
                {
                    Label(p,ItemName(group.Key)+"  Lv."+group.First().level+"  ×"+group.Count(),0,y,w,34,15,white);
                    y+=37;
                }
                if(support.guards>0)
                {
                    Label(p,"领主卫队  Lv."+app.Estate.RetinueBattleLevel+"  ×"+support.guards,0,y,w,34,15,gold);
                    y+=37;
                }
                if(support.mercenaries)
                {
                    Label(p,"自由兵团  Lv."+Manor.MercenaryLevel+"  ×"+Manor.MercenarySize+" · 开战签约",0,y,w,34,15,gold);
                    y+=37;
                }
                foreach(var group in app.Config.props.Where(q=>q.team=="red").GroupBy(q=>q.type))
                {
                    Label(p,ItemName(group.Key)+"  ×"+group.Count(),0,y,w,34,15,white);
                    y+=37;
                }
                p.sizeDelta=new Vector2(w,y);
            },430);
        }

        void SettlementDialog(Manor.Settlement receipt)
        {
            OpenDialog(receipt.frontier?"巡边结算清单":"远征结算清单",(p,w)=>{
                string outcome=receipt.withdrew?"已撤回":receipt.winner=="red"?"胜利":receipt.winner=="blue"?"战败":"平局";
                string location=receipt.frontier?app.Data.Frontier.name+" · 警戒 "+receipt.frontierTier+" 阶":receipt.raid?"迎战掠夺者":app.Data.Maps[Manor.Route[receipt.stage]].name;
                Label(p,location+" · "+outcome,0,0,w,34,17,gold);
                Label(p,"归营 "+receipt.troops.Values.Sum(t=>t.survivors)+" 人  ·  救回伤员 "+receipt.troops.Values.Sum(t=>t.wounded)+
                    " 人  ·  阵亡 "+receipt.troops.Values.Sum(t=>t.fallen)+" 人",0,38,w,28,14,white);
                int approvalChange=receipt.approvalAfter-receipt.approvalBefore;
                var morale=Label(p,"民心 "+receipt.approvalBefore+" → "+receipt.approvalAfter+"（"+Change(approvalChange)+"）",
                    0,69,w,25,15,approvalChange<0?danger:approvalChange>0?thriving:pale);
                morale.name="Manor.Settlement.Approval";
                Label(p,LogisticsText(receipt.logistics),0,98,w,portrait?45:27,12,pale);
                float nameWidth=w*.48f,column=(w-nameWidth)/3;
                float y=portrait?149:132;
                if(receipt.influence>0)
                {
                    var merit=Label(p,"战功威望 +"+receipt.influence,0,y,w,24,14,gold);
                    merit.name="Manor.Settlement.Influence";
                    y+=28;
                }
                if(receipt.newSectors!=0)
                {
                    string names=string.Join("、",Manor.Sectors.Where((_,index)=>(receipt.newSectors&(1<<index))!=0).Select(s=>s.name));
                    var hint=Label(p,"新开放宣称 · "+names,0,y,w,portrait?39:24,14,gold);
                    hint.name="Manor.Settlement.Claims";
                    y+=portrait?43:28;
                }
                Label(p,"归营",nameWidth,y,column,24,12,pale,TextAnchor.MiddleCenter);
                Label(p,"伤员",nameWidth+column,y,column,24,12,gold,TextAnchor.MiddleCenter);
                Label(p,"阵亡",nameWidth+column*2,y,column,24,12,danger,TextAnchor.MiddleCenter);
                y+=25;
                foreach(var pair in receipt.troops)
                {
                    var t=pair.Value;
                    var row=Panel(p,"Manor return row",0,y,w,25,manorInset);
                    Label(row,ItemName(pair.Key)+" · Lv."+app.Estate.UnitLevel(pair.Key),7,0,nameWidth-9,25,13,white);
                    Label(row,t.survivors.ToString(),nameWidth,0,column,25,13,white,TextAnchor.MiddleCenter);
                    Label(row,t.wounded.ToString(),nameWidth+column,0,column,25,13,gold,TextAnchor.MiddleCenter);
                    Label(row,t.fallen.ToString(),nameWidth+column*2,0,column,25,13,danger,TextAnchor.MiddleCenter);
                    y+=27;
                }
                foreach(var pair in receipt.heroes)
                {
                    Label(p,"英雄 "+ItemName(pair.Key)+" · Lv."+app.Estate.HeroLevel(pair.Key)+(pair.Value>0?" · 休整 "+pair.Value+" 日":" · 已归营"),0,y,w,25,13,pale);
                    y+=27;
                }
                if(receipt.guards>0)
                {
                    Label(p,"领主卫队 · 归营 "+(receipt.guards-receipt.guardLosses)+" · 待整补 "+receipt.guardLosses,0,y,w,25,13,gold);
                    y+=27;
                }
                if(receipt.mercenaries>0)
                {
                    Label(p,"自由兵团 · "+receipt.mercenaries+" 人参战 · 战损 "+receipt.mercenaryLosses+" · 合约已结束",0,y,w,25,13,gold);
                    y+=27;
                }
                if(receipt.frontier)
                {
                    Label(p,"击退守军 "+receipt.defeated+" / "+FrontierMap.Guards.Count,0,y,w,30,14,white);
                    y+=35;
                    if(receipt.defeated==FrontierMap.Guards.Count)
                    {
                        Label(p,"外域警戒 2、3 阶已开放",0,y,w,28,13,gold);
                        y+=30;
                    }
                }
                else
                {
                    string spent=receipt.spent.Count==0?"无":string.Join(" · ",receipt.spent.Select(item=>ItemName(item.Key)+" ×"+item.Value));
                    Label(p,"消耗道具 · "+spent,0,y+4,w,receipt.spent.Count>3&&portrait?50:30,13,white);
                    y+=receipt.spent.Count>3&&portrait?56:36;
                }
                Label(p,"获得物资 · 金币 +"+receipt.coin+" · 建材 +"+receipt.timber+" · 军械 +"+receipt.arms,0,y,w,34,13,white);
                y+=40;
                string unlock=receipt.frontier?"":receipt.clearedAfter>receipt.clearedBefore ?
                    (receipt.clearedAfter==Manor.Route.Length?"全部关卡已通关":"解锁第 "+(receipt.clearedAfter+1)+" 关 · "+
                        app.Data.Maps[Manor.Route[receipt.clearedAfter]].name) : "已通关 "+receipt.clearedAfter+" / "+Manor.Route.Length;
                if(receipt.clearedAfter>receipt.clearedBefore&&(receipt.clearedAfter==1||receipt.clearedAfter==3))
                    unlock+="\n宅邸 Lv."+(receipt.clearedAfter==1?2:3)+" 升级条件已达成";
                if(!receipt.frontier)Label(p,unlock,0,y,w,50,14,gold,TextAnchor.UpperLeft);
                p.sizeDelta=new Vector2(w,y+(receipt.frontier?2:51));
            },620,(bar,w)=>ManorButton(bar,"Manor.Settlement.Done","确认结算",0,3,w,40,CloseDialog,true));
        }

        void OpenManorWorkshop()
        {
            if(app.Phase!="manor")return;
            OpenDialog("庄园工坊",(p,w)=>{
                ManorButton(p,"Manor.Import","导入庄园存档 / 远征回放",0,0,w,42,()=>{CloseDialog();app.ImportFile();});
                ManorButton(p,"Manor.Export","导出庄园 JSON",0,49,w/2-4,42,app.ExportManor);
                ManorButton(p,"Manor.Saves","本机存档槽",w/2+4,49,w/2-4,42,ManorSaves,true);
                Label(p,"粘贴庄园存档或回放 JSON",0,103,w,31,15,gold);
                var input=InputBox(p,manorDraft,0,140,w,150,null,true);
                input.onValueChanged.AddListener(s=>manorDraft=s);
                ManorButton(p,"Manor.ImportText","校验并导入文本",0,298,w,40,()=>{
                    app.ImportText(input.text);manorDraft="";CloseDialog();
                });
                p.sizeDelta=new Vector2(w,345);
            },474);
        }

        void ManorHelp()
        {
            OpenDialog("领主手册",(p,w)=>{
                var keys=Panel(p,"Manor.Help.Shortcuts",0,0,w,portrait?108:93,manorInset,Slice.Inset);
                Label(keys,"桌面快捷键",11,2,w-22,25,16,gold);
                Label(keys,"G 政务　P 选地　M 宏愿\nL 收支账本　C 游戏图鉴　N 贵族头衔与赐土",
                    11,29,w-22,portrait?50:44,portrait?12:14,white,TextAnchor.UpperLeft).name="Manor.Help.Keys";
                Label(keys,"演武同样可按 C 查阅图鉴。",11,portrait?77:67,w-22,25,12,pale);
                string text="建地与领民\n半岛初始有 24 块建地，晋爵可扩至 36 块。宅邸三级依次发展为木石堡、外郭城和公爵主堡。18–23 号外环林地花 8 金币开垦，收回 4 建材。「选地」清单可选中地块并聚焦。农舍和牧场提供住房，有余粮时迎来新户。选择农舍，可花金币和建材启用或切换一项后院副业，每日结算无需派工。\n\n发展与供给\n历次称号晋升和领取三项宏愿共可获得最多四点发展点。在政务按路线解锁专精。口粮、薪柴和精神供给齐备时，市集兴旺每天额外提高 1 点民心。编年史保留最近十二件领地大事。\n\n领主宏愿\n在领地政务查看六项里程碑，达成即可领取一次奖励。完成全部远征，或在一次巡边击退全部守军，可达成「平定四方」。\n\n生产协同\n运转中的谷仓提振产粮，应急时保住粮底。礼拜堂提升民心和伤员救回率。农田搭配牧场或谷仓可增产，伐木营搭配铁匠铺可增产军械。农舍累计 3 级并建有礼拜堂或酒馆可获得安居民心。\n\n领地专才\n在政务中查看管家、掌柜、医者和巡庄队长。四位分别改善税收、贸易、医护与战备。解锁后从 Lv.1 起，可用金币与物资培养到 Lv.3。\n\n领地运营\n低税、食谱丰富与酒馆能提高民心，低于 40 暂停迁入。夏秋田产增加，冬季按每四户一份建材取暖。政务可切换税率和政令，账本预览收支。集市每季刷新额度，急报可自行打开处理。\n\n边疆巡边\n木桥连接半岛与外域；山丘、海湾和石崖按实际地形判定。用全景辨认三处营地，再缩放到目的地。集结后用「推进」点选外域陆地，非接战时部队会加速行军。桥头金线外会接战，撤回线内即可脱离追击。可发军令、撤回半岛或结算归营。缺粮时最多带 3 人。一次巡边击退全部 15 名守军后，可在集结时选警戒 2、3 阶，挑战更强守军并获得更多奖励。\n\n培养与远征\n酒馆派工后开放英雄培养。兵营训练兵种，练兵靶场和马厩工坊征召与备货。战前可一键编队，布阵时用「移动」调整兵员或道具。战后可看回放并查看结算。\n\n地力与轮作\n新田地力 100%，产粮按地力缩放，范围 60%–120%。自动轮作冬季或地力低于 80% 时休耕。自动耕作每季减 8，精耕四季生产、每季减 12，休耕不产粮、每季回养 25。牧场派工后，自动轮作和休耕田每季再回养 5。\n\n领主卫队\n在宅邸招募亲兵并强化披甲。宅邸三级分别可养 2、4、6 人。卫队在门前值守，随军占行军名额，战损回营后可用金币整补。\n\n威望与自由兵团\n政务可开启什一赈济。粮食净产为正且留足两日口粮时，捐出约一成净粮（至少一份），每日得 1 威望。远征凯旋、击退掠夺者、清空巡边三营都有战功威望。花 25 威望办庆典，民心 +8。花 20 威望刷新集市额度。出征或巡边可选三人兵团，开战消耗 30 威望和 12 金币，归营解散。\n\n边疆疆域\n一座营地的 5 名守军全灭并归营结算后，在领地政务的「边疆疆域」花威望与建材宣称。西桥前哨每日送 1.5 建材，北林猎场送 2 粮并提供猎物，东岭铁脉送 1 军械。宣称后在地图插起红金旗。再次巡边仍可挑战各营守军。\n\n四季天气\n春夏多晴，偶有细雨。秋日落叶，冬日飘雪。庄园和外域视角的天气随日历变化，微粒密度随画质调整。庄园风声和雨雪音色受环境声音量控制。\n\n日程与视角\n1× 约四秒一日，一季十二日。右键或触屏拖动旋转，滚轮或双指缩放，WASD、方向键、Shift 加左键拖动、中键或双指均可平移。庄园按 1 / 2 / 3 切换全景、半岛和外域，Space 暂停或继续，R 重置，Esc 关闭窗口。庄园按 G 打开政务、P 选地、M 查看宏愿。可在工坊使用 3 个本机存档槽。巡边时 1 / 2 / 3 选择军令。";
                text=text.Replace("半岛有 24 块建地","男爵起始有 24 块建地，晋爵后最多 36 块")
                    .Replace("管家、掌柜、医者和巡庄队长。四位分别改善税收、贸易、医护与战备。","管家、掌柜、医者、巡庄队长、石匠和乐师，分别协助税收、贸易、医护、战备、营建与民心。");
                text+="\n\n专属武装\n招募后可在酒馆、铁匠铺或集结名单打开锻造，逐阶支付金币、军械、建材与高阶威望。武装随领地战斗生效，满阶在盔甲上显示金色纹章。"+
                    "\n\n季候调配与行商\n启用季候自适应后，每次换季重新调配，冬季优先储备、取暖与口粮，暖季侧重高产农田、面包房、锻造与税收。手动派工可退出自动调配。收支账本分列每日生产与消耗，行商每季有一项特惠，选地清单可筛出缺工、可升级及空地与封地。";
                var help=Label(p,text,0,keys.sizeDelta.y+10,w,portrait?504:340,14,white,TextAnchor.UpperLeft);
                help.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,help.preferredHeight+8);
            },portrait?575:430);
        }
    }
}
