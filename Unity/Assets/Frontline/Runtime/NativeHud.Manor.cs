using System;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        readonly Color manorPanel = new Color(.075f,.145f,.13f,.97f);
        readonly Color manorInset = new Color(.105f,.19f,.17f,.98f);
        readonly Color manorButton = new Color(.16f,.27f,.23f);
        readonly Color pale = new Color(.76f,.83f,.75f);
        readonly List<Action> manorValues = new List<Action>();
        readonly List<Action> manorDialogValues = new List<Action>();
        Text dialogNotice;
        RectTransform dialogContent;
        Action<RectTransform,float> dialogFooter;
        float dialogHeight = 760;
        bool builtManorMode,builtInBase;
        Manor shownEstate;
        int shownPlot,shownPlotLevel,chosenStage;
        string shownPlotKind,manorDraft = "";
        static readonly string[] Seasons = {"春","夏","秋","冬"};

        Button ManorButton(Transform parent,string id,string caption,float x,float y,float w,float h,
            Action action,bool accent=false)
        {
            var button=Button(parent,caption,x,y,w,h,action,accent);
            button.name=id;
            if(!accent)button.GetComponent<Image>().color=manorButton;
            return button;
        }

        void TopBar()
        {
            var bar=Panel(baseRoot,"Top bar",0,0,rw,portrait?90:74,app.InManor?manorPanel:ink);
            if(portrait)
            {
                Label(bar,"阵线实验室",12,3,112,32,16,gold);
                Label(bar,"UNITY  1.0.0",119,5,89,29,10,muted);
                if(app.InManor)
                {
                    Button(bar,"手册",214,3,75,34,ManorHelp);
                    if(app.Phase=="manor")Button(bar,"工坊",295,3,84,34,Workshop);
                    else if(app.Phase=="deploy")Button(bar,"驻军",295,3,84,34,ExpeditionRoster);
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
                Label(bar,"FRONTLINE LAB   /   阵线实验室",18,6,344,37,21,gold);
                Label(bar,"UNITY EDITION  ·  1.0.0",18,41,270,20,11,muted);
                ModeTab(bar,false,365,14,144,44);
                ModeTab(bar,true,517,14,144,44);
                if(app.InManor)
                {
                    if(app.Phase=="manor")Button(bar,"庄园工坊",985,17,145,38,Workshop);
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
                ()=>{if(app.InManor!=manor){CloseDialog();app.SetManorMode(manor);}},accent:active);
            b.name=manor?"Mode.Manor":"Mode.Duel";
        }

        static void RefreshBindings(List<Action> changes)
        {
            for(int i=0;i<changes.Count;i++)changes[i]();
        }

        void RefreshManorValues()
        {
            if(!builtInBase)return;
            RefreshBindings(manorValues);
            RefreshBindings(manorDialogValues);
            ShowNotice(app.Message);
        }

        void StateChanged()
        {
            if(!app.InManor||app.Phase!="manor"||!builtInBase||shownEstate!=app.Estate||
                width!=Screen.width||height!=Screen.height||shownPlot!=app.ManorSlot)
            {
                Build();
                return;
            }
            var slot=shownPlot>=0?app.Estate.slots[shownPlot]:null;
            if(shownPlotKind!=slot?.kind||shownPlotLevel!=(slot?.level??0))Build();
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
            shownEstate=app.Estate;
            shownPlot=app.ManorSlot;
            var slot=shownPlot>=0?shownEstate.slots[shownPlot]:null;
            shownPlotKind=slot?.kind;
            shownPlotLevel=slot?.level??0;

            float ledgerW=portrait?374:660;
            var ledger=Panel(baseRoot,"Manor ledger",portrait?8:16,portrait?94:83,ledgerW,portrait?48:55,manorPanel);
            Meter(ledger,0,ledgerW,"金币",gold,()=>app.Estate.coin.ToString());
            Meter(ledger,1,ledgerW,"粮食",new Color(.75f,.81f,.4f),()=>app.Estate.food.ToString());
            Meter(ledger,2,ledgerW,"建材",new Color(.85f,.65f,.49f),()=>app.Estate.timber.ToString());
            Meter(ledger,3,ledgerW,"军械",new Color(.67f,.79f,.83f),()=>app.Estate.arms.ToString());
            Meter(ledger,4,ledgerW,"闲户 / 总户",pale,()=>app.Estate.FreeWorkers+" / "+app.Estate.workers);

            var card=Panel(baseRoot,"Manor plot card",portrait?8:16,rh-(portrait?231:170),
                portrait?374:540,portrait?112:126,manorPanel);
            PlotCard(card,card.sizeDelta.x);
            var schedule=Panel(baseRoot,"Manor calendar",portrait?8:rw-472,rh-(portrait?115:170),
                portrait?374:456,portrait?83:126,manorPanel);
            Schedule(schedule,schedule.sizeDelta.x);
            var notice=Panel(baseRoot,"Manor status",portrait?8:16,rh-(portrait?29:36),rw-(portrait?16:32),portrait?25:29,manorPanel);
            notice.GetComponent<Image>().raycastTarget=false;
            status=Label(notice,app.Message,9,0,notice.sizeDelta.x-18,notice.sizeDelta.y,12,pale);
        }

        void Meter(RectTransform strip,int i,float w,string name,Color color,Func<string> value)
        {
            float cell=w/5,x=i*cell;
            if(i>0)Panel(strip,"Divider",x,8,1,strip.sizeDelta.y-16,manorButton).GetComponent<Image>().raycastTarget=false;
            Label(strip,name,x+9,2,cell-11,18,portrait?10:12,color);
            var label=Label(strip,"",x+9,20,cell-12,strip.sizeDelta.y-21,portrait?15:19,white);
            Value(manorValues,label,value);
        }

        void PlotCard(RectTransform p,float w)
        {
            var estate=app.Estate;
            int index=app.ManorSlot;
            var slot=index<0?null:estate.slots[index];
            var kind=slot?.kind;
            string title=index<0?"主基地":index==0?"领主宅邸":
                "建地 "+index+"   "+(kind==null?"空置":Manor.Name(kind));
            if(slot!=null&&slot.level>0)title+="   Lv."+slot.level;
            bool recruiting=kind=="barracks"||kind=="stables";
            Label(p,title,12,4,w-(recruiting?181:111),27,portrait?16:19,gold);
            if(recruiting)
                ManorButton(p,"Manor.Recruits",kind=="barracks"?"招募":"备军",w-151,5,66,29,Recruitment,true);
            ManorButton(p,"Manor.Plots","选地",w-80,5,68,29,PlotList);
            string description=index<0?"点击地块查看建筑、派工或建造。":
                kind==null?"挑选一种建筑，查看所需金币与建材。":Manor.Description(kind);
            Label(p,description,12,33,w-24,34,portrait?12:14,pale);
            float rowY=portrait?72:76,h=portrait?33:38;

            if(index<0)
            {
                ManorButton(p,"Manor.Hall","查看宅邸",12,rowY,(w-30)/2,h,()=>app.SelectManorSlot(0));
                ManorButton(p,"Manor.FindLand","选择建地",18+(w-30)/2,rowY,(w-30)/2,h,PlotList,true);
                return;
            }
            if(kind==null)
            {
                ManorButton(p,"Manor.Construction","建造",12,rowY,w-24,h,BuildingMenu,true);
                return;
            }

            int cap=Manor.StaffLimit(kind,slot.level);
            float right=w-12;
            if(index>0)
            {
                right-=52;
                ManorButton(p,"Manor.Demolish","拆除",right,rowY,52,h,Demolition);
                right-=5;
            }
            right-=portrait?68:98;
            var upgrade=ManorButton(p,"Manor.Upgrade",slot.level>=3?"已满级":"升级",right,rowY,
                portrait?68:98,h,Upgrade);
            upgrade.interactable=slot.level<3;

            if(cap>0)
            {
                float knob=portrait?37:45;
                var label=Label(p,"",12,rowY,right-knob*2-28,h,portrait?12:15,white);
                Value(manorValues,label,()=>"派工 "+app.Estate.slots[index].workers+" / "+cap+" 户");
                var less=ManorButton(p,"Manor.Worker.Minus","-",right-knob*2-14,rowY,knob,h,()=>app.AssignWorker(-1));
                var more=ManorButton(p,"Manor.Worker.Plus","＋",right-knob-9,rowY,knob,h,()=>app.AssignWorker(1));
                manorValues.Add(()=>{
                    int assigned=app.Estate.slots[index].workers;
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
            return Seasons[Math.Max(0,day-1)/Manor.SeasonDays%4]+"季 · 第 "+day+" 日";
        }

        void Schedule(RectTransform p,float w)
        {
            float top=portrait?5:8;
            Value(manorValues,Label(p,"",10,top,w*.34f,32,portrait?14:18,gold),CalendarText);
            float x=w*.36f,buttonW=(w-12-x-6)/2;
            ManorButton(p,"Manor.Expedition","领地远征",x,top,buttonW,32,()=>MarchDialog(false),true);
            var raid=ManorButton(p,"Manor.Raid","迎战掠夺者",x+buttonW+6,top,buttonW,32,()=>MarchDialog(true));
            manorValues.Add(()=>raid.GetComponent<Image>().color=app.Estate.raidReady?new Color(.5f,.28f,.17f):manorButton);
            if(!portrait)
                Value(manorValues,Label(p,"",10,43,w-20,25,13,pale),
                    ()=>"远征 "+app.Estate.cleared+" / "+Manor.Route.Length+"   ·   "+(app.Estate.raidReady?"掠夺者已抵达":"边境巡守中"));
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
            var image=b.GetComponent<Image>();
            var label=b.GetComponentInChildren<Text>();
            manorValues.Add(()=>{
                bool on=app.ManorSpeed==rate;
                image.color=on?pale:manorButton;
                label.color=on?ink:white;
            });
        }

        void PlotList()
        {
            OpenDialog("建地清单",(p,w)=>{
                for(int i=0;i<10;i++)
                {
                    int n=i;
                    var s=app.Estate.slots[i];
                    string name=(i==0?"中央":("建地 "+i))+"  "+(s.kind==null?"空置":Manor.Name(s.kind));
                    float cell=w/2;
                    ManorButton(p,"Manor.Slot."+i,name,(i%2)*cell,(i/2)*49,cell-5,43,
                        ()=>{CloseDialog();app.SelectManorSlot(n);},app.ManorSlot==i);
                }
                p.sizeDelta=new Vector2(w,250);
            },375);
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
                    var row=Panel(p,kind,0,y,w,portrait?86:68,manorInset);
                    Label(row,Manor.Name(kind),9,1,w-actionW-20,25,16,white);
                    Label(row,Manor.Description(kind),9,26,w-18,portrait?32:22,12,pale);
                    Label(row,app.Estate.BuildPrice(kind),9,portrait?56:45,w-18,21,12,gold);
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
                Label(p,app.Estate.UpgradePrice(index),0,38,w,57,15,pale);
                p.sizeDelta=new Vector2(w,100);
            },255,(bar,w)=>ManorButton(bar,"Manor.ConfirmUpgrade","确认升级",0,3,w,40,()=>{
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
            }));
        }

        string ItemName(string id)
        {
            return app.Data.Units.TryGetValue(id,out var unit)?unit.name:app.Data.Props[id].name;
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

        void MarchDialog(bool raid)
        {
            chosenStage=Math.Min(app.Estate.cleared,Manor.Route.Length-1);
            OpenDialog(raid?"迎敌 · 掠夺者小队":"远征 · 调派驻军",(p,w)=>{
                float y=0;
                if(!raid)
                {
                    for(int i=0;i<Manor.Route.Length;i++)
                    {
                        int stage=i;
                        string map=app.Data.Maps[Manor.Route[i]].name;
                        bool available=i<=app.Estate.cleared;
                        string caption=(i+1)+"  "+map+(available?"":" · 锁");
                        float cell=w/(portrait?2:3);
                        int columns=portrait?2:3;
                        var stageButton=ManorButton(p,"Manor.Stage."+i,caption,(i%columns)*cell,(i/columns)*41,
                            cell-5,36,()=>{chosenStage=stage;RefreshBindings(manorDialogValues);});
                        stageButton.interactable=available;
                        var background=stageButton.GetComponent<Image>();
                        manorDialogValues.Add(()=>background.color=chosenStage==stage?new Color(.3f,.43f,.28f):manorButton);
                    }
                    y=portrait?129:88;
                }
                else
                {
                    Label(p,app.Estate.raidReady?"边境发现掠夺者，可以立即迎战。":"派出巡逻队，主动迎战边境的掠夺者。",0,0,w,50,15,pale);
                    y=55;
                }
                Label(p,"常备军与道具",0,y,w,29,16,gold);
                y+=34;
                int availableItems=0;
                foreach(var unit in app.Data.Units.Values)
                    if(app.Estate.Stock(unit.id)>0){ForceRow(p,w,y,unit.id);y+=47;availableItems++;}
                foreach(var prop in app.Data.Props.Values)
                    if(app.Estate.Stock(prop.id)>0){ForceRow(p,w,y,prop.id);y+=47;availableItems++;}
                if(availableItems==0)
                {
                    Label(p,"驻军尚未集结。在练兵靶场或马厩工坊征召，再来调派。",0,y,w,60,14,pale);
                    y+=66;
                }
                p.sizeDelta=new Vector2(w,y+4);
            },portrait?686:627,(bar,w)=>{
                var total=Label(bar,"",6,0,w-129,45,13,white);
                Value(manorDialogValues,total,()=>"军费 "+app.ForceCost+" / "+app.Estate.Budget+"\n兵员 "+app.ForceUnits+" / "+app.Estate.FieldLimit);
                var march=ManorButton(bar,"Manor.March",raid?"迎敌布阵":"出征布阵",w-119,3,115,40,
                    ()=>app.PrepareExpedition(chosenStage,raid),true);
                manorDialogValues.Add(()=>march.interactable=app.ForceUnits>0&&app.ForceCost<=app.Estate.Budget&&app.ForceUnits<=app.Estate.FieldLimit);
            });
        }

        void ForceRow(RectTransform p,float w,float y,string id)
        {
            var row=Panel(p,id,0,y,w,42,manorInset);
            Label(row,ItemName(id),8,0,w-173,42,14,white);
            var count=Label(row,"",w-166,0,64,42,14,pale,TextAnchor.MiddleCenter);
            Value(manorDialogValues,count,()=>app.ForceCount(id)+" / "+app.Estate.Stock(id));
            var minus=ManorButton(row,"Manor.Force.Minus."+id,"-",w-95,3,43,36,()=>app.SelectForce(id,-1));
            var plus=ManorButton(row,"Manor.Force.Plus."+id,"＋",w-48,3,43,36,()=>app.SelectForce(id,1));
            manorDialogValues.Add(()=>{
                int number=app.ForceCount(id);
                minus.interactable=number>0;
                plus.interactable=number<app.Estate.Stock(id);
            });
        }

        void ExpeditionDeploy()
        {
            if(portrait)TouchSwitch();
            float w=portrait?374:358,h=portrait?210:237;
            var p=Panel(baseRoot,"Expedition deployment",portrait?8:16,rh-h-12,w,h,manorPanel);
            string map=app.Data.Maps[app.Config.map].name;
            Label(p,"出征布阵   /   "+map,11,4,w-22,29,17,gold);
            Label(p,app.Config.red.Count+" 名兵员 · 已选择 "+app.Selected.Count+" 名",12,33,w-24,22,12,pale);
            float y=portrait?58:65,step=(w-18)/3;
            ManorButton(p,"Manor.Select","选择",9,y,step-4,32,()=>{app.Tool="select";app.Refresh();});
            ManorButton(p,"Manor.Move","移动",9+step,y,step-4,32,()=>{app.Tool="move";app.Refresh();});
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
                var counts=new Dictionary<string,int>();
                foreach(var unit in app.Config.red)
                {
                    counts.TryGetValue(unit.type,out int n);
                    counts[unit.type]=n+1;
                }
                foreach(var prop in app.Config.props)if(prop.team=="red")
                {
                    counts.TryGetValue(prop.type,out int n);
                    counts[prop.type]=n+1;
                }
                float y=0;
                foreach(var pair in counts)
                {
                    Label(p,ItemName(pair.Key)+"  ×"+pair.Value,0,y,w,34,15,white);
                    y+=37;
                }
                p.sizeDelta=new Vector2(w,y);
            },430);
        }

        void OpenManorWorkshop()
        {
            if(app.Phase!="manor")return;
            OpenDialog("庄园工坊",(p,w)=>{
                ManorButton(p,"Manor.Import","导入庄园存档",0,0,w,42,()=>{CloseDialog();app.ImportFile();});
                ManorButton(p,"Manor.Export","导出 frontline-manor.json",0,49,w,42,app.ExportManor,true);
                Label(p,"粘贴庄园存档 JSON",0,103,w,31,15,gold);
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
                string text="建地与领民\n点击台地上的地块，或从「选地」打开地籍。麦田、采集营和铁匠铺需要指派领民；用 + / - 调整。农舍提高可容纳户数，粮食有余时会迎来新户。\n\n领地日程\n1× 约四秒经过一日。可以暂停、加速、推进一日或一季。一季十二日。\n\n驻军与远征\n在练兵靶场、马厩工坊征召并储备道具。出征前选好本次部队，再在战场调整站位和姿态。高等级宅邸提供更多军费和军令。战后点「返回庄园结算」领回伤员与物资。\n\n视角\n右键拖动旋转、滚轮缩放。触屏可拖动旋转、双指缩放，轻点地块可选中。";
                Label(p,text,0,0,w,portrait?425:300,14,white,TextAnchor.UpperLeft);
                p.sizeDelta=new Vector2(w,portrait?437:310);
            },portrait?575:430);
        }
    }
}
