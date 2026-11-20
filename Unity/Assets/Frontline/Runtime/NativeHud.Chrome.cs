using Frontline.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        static readonly Glyph[] ResourceGlyphs={Glyph.Coin,Glyph.Grain,Glyph.Timber,Glyph.Arms};
        static readonly string[] ResourceTitles={"金币","粮食","建材","军械"};
        static readonly Color[] ResourceColors={
            new Color(.98f,.84f,.53f),new Color(.89f,.88f,.61f),new Color(.93f,.74f,.60f),new Color(.71f,.85f,.89f)
        };
        readonly Text[] balances=new Text[4],deltas=new Text[4];
        GameObject supplyHint;
        CanvasGroup toast;
        float toastUntil,guideUntil;
        Manor lastGuidedEstate;
        int lastGuideStep=-1;

        void TopBar()
        {
            bool estate=app.Phase=="manor";
            float modeX=portrait?8:estate?573:244;
            float modeW=portrait?121:166;
            var modes=Panel(baseRoot,"Mode switch",modeX,portrait?8:15,modeW,portrait?36:37,Color.white,Slice.Rail);
            float half=(modeW-10)/2;
            ModeTab(modes,false,4,4,half,portrait?28:29);
            ModeTab(modes,true,half+6,4,half,portrait?28:29);
            if(!portrait&&!estate)
            {
                var title=Float(baseRoot,"Game identity",15,15,184,37,Slice.Rail);
                Mark(title,Glyph.Crown,11,9,19,gold);
                Label(title,"阵线实验室",39,0,92,37,16,white);
                Label(title,Application.version,133,0,43,37,11,pale);
            }

            float toolsW=portrait?246:estate?454:app.InManor?394:464;
            float toolsH=portrait?36:37;
            var tools=Panel(baseRoot,"Top tools",rw-toolsW-(portrait?8:15),portrait?8:15,toolsW,toolsH,Color.white,Slice.Rail);
            if(portrait)
            {
                if(estate)
                {
                    GovernanceEntry(tools,4,4,48,28);
                    CodexEntryButton(tools,52,4,47,28);
                    QualityEntry(tools,99,4,47,28);
                    ToolButton(tools,"工坊",146,4,47,28,Workshop,Glyph.Workshop);
                    ToolButton(tools,"手册",193,4,49,28,ManorHelp,Glyph.Guide);
                }
                else
                {
                    CodexEntryButton(tools,4,4,54,28);
                    QualityEntry(tools,60,4,54,28);
                    if(app.InManor)
                    {
                        Button(tools,app.Phase=="deploy"?"驻军":"战报",119,4,53,28,
                            app.Phase=="deploy"?ExpeditionRoster:ReportDialog);
                        ToolButton(tools,"手册",174,4,66,28,ManorHelp,Glyph.Guide);
                    }
                    else
                    {
                        Button(tools,"策略",117,4,52,28,Strategy);
                        ToolButton(tools,"工坊",171,4,70,28,Workshop,Glyph.Workshop);
                    }
                }
            }
            else if(estate)
            {
                GovernanceEntry(tools,4,4,110,29);
                CodexEntryButton(tools,115,4,72,29);
                ToolButton(tools,"工坊",188,4,74,29,Workshop,Glyph.Workshop).name="庄园工坊";
                QualityEntry(tools,263,4,105,29);
                ToolButton(tools,"手册",369,4,80,29,ManorHelp,Glyph.Guide).name="领主手册";
            }
            else
            {
                CodexEntryButton(tools,4,4,75,29);
                QualityEntry(tools,80,4,114,29);
                if(app.InManor)
                {
                    Button(tools,app.Phase=="deploy"?"出征名册":"战报",196,4,98,29,
                        app.Phase=="deploy"?ExpeditionRoster:ReportDialog);
                    ToolButton(tools,"手册",296,4,93,29,ManorHelp,Glyph.Guide).name="领主手册";
                }
                else
                {
                    Button(tools,"策略与演武擂台",197,4,113,29,Strategy);
                    ToolButton(tools,"工坊",312,4,72,29,Workshop,Glyph.Workshop);
                    ToolButton(tools,"说明",386,4,72,29,Help,Glyph.Guide);
                }
            }
        }

        Button ToolButton(Transform parent,string caption,float x,float y,float w,float h,System.Action action,Glyph glyph)
        {
            var button=Button(parent,caption,x,y,w,h,action);
            MarkButton(button,glyph,portrait?12:15,portrait?4:9,portrait?11:13);
            return button;
        }

        void BuildLedger()
        {
            float w=portrait?374:528,h=portrait?46:47;
            float origin=portrait?8:15,top=portrait?51:15;
            var ledger=Float(baseRoot,"Manor ledger",origin,top,w,h,Slice.Rail);
            var quick=ledger.gameObject.AddComponent<Button>();
            quick.name="Manor.Ledger.Quick";
            quick.targetGraphic=ledger.GetComponent<Image>();
            quick.navigation=new Navigation{mode=Navigation.Mode.None};
            quick.onClick.AddListener(LedgerDialog);
            var hover=ledger.gameObject.AddComponent<NativeHudLedgerHover>();
            hover.Owner=this;

            float cell=portrait?56:82;
            for(int i=0;i<balances.Length;i++)
            {
                float x=7+cell*i;
                Mark(ledger,ResourceGlyphs[i],x,6,portrait?13:17,ResourceColors[i]);
                if(!portrait)Label(ledger,ResourceTitles[i],x,26,26,15,10,pale,TextAnchor.MiddleCenter);
                float textLeft=x+(portrait?14:25);
                balances[i]=Label(ledger,"",textLeft,3,cell-(portrait?15:27),24,portrait?13:19,white);
                balances[i].name="Manor.Ledger."+i;
                balances[i].resizeTextForBestFit=true;
                balances[i].resizeTextMinSize=portrait?8:11;
                balances[i].resizeTextMaxSize=portrait?13:19;
                deltas[i]=Label(ledger,"",textLeft,24,cell-(portrait?15:25),16,portrait?9:11,thriving);
                deltas[i].name="Manor.Ledger."+i+".Net";
                if(i<balances.Length-1)VerticalRule(ledger,x+cell-4,10,26);
            }
            float side=portrait?229:337;
            VerticalRule(ledger,side-1,9,28);
            Mark(ledger,Glyph.People,side+6,6,portrait?14:17,pale);
            var homes=Label(ledger,"",side+(portrait?21:30),5,portrait?42:60,20,portrait?11:15,white);
            homes.name="Manor.Ledger.4";
            homes.resizeTextForBestFit=true;homes.resizeTextMinSize=8;homes.resizeTextMaxSize=portrait?11:15;
            Label(ledger,"闲户 / 总户",side+(portrait?11:24),24,portrait?58:70,16,portrait?9:10,pale);
            float civic=portrait?300:431;
            VerticalRule(ledger,civic-3,9,28);
            Mark(ledger,Glyph.Crown,civic+5,4,15,gold);
            var rank=Label(ledger,"",civic+25,1,w-civic-27,22,portrait?12:14,gold);
            rank.name="Manor.Nobility.Ledger";
            Mark(ledger,Glyph.Heart,civic+5,25,14,new Color(.96f,.70f,.65f));
            var morale=Label(ledger,"",civic+25,23,w-civic-27,20,portrait?11:13,pale);
            morale.name="Manor.Ledger.Approval";
            BuildSupplyHint(origin,top+h+7);
            manorValues.Add(()=>{
                var m=app.Estate;var day=PreviewForecast();
                SetBalance(0,m.coin,m.remainder.coin,day.coin);
                SetBalance(1,m.food,0,day.food);
                SetBalance(2,m.timber,0,day.timber);
                SetBalance(3,m.arms,0,day.arms);
                balances[1].color=m.food==0?danger:white;
                readoutText.Clear();Number(m.FreeWorkers);readoutText.Append('/');Number(m.workers);ApplyReadout(homes);
                if(rank.text!=m.NobleTitle)rank.text=m.NobleTitle;
                readoutText.Clear().Append("民心 ");Number(m.approval);ApplyReadout(morale);
                morale.color=ApprovalColor(m.approval);
            });
        }

        void SetBalance(int i,int stock,int fraction,int change)
        {
            readoutText.Clear();Number(stock);
            if(fraction>0)
            {
                readoutText.Append('.');
                if(fraction<10)readoutText.Append('0');
                Number(fraction%10==0?fraction/10:fraction);
            }
            ApplyReadout(balances[i]);
            readoutText.Clear().Append(change>0?"▲":change<0?"▼":"—");
            if(change!=0)Number(change*.01f,"+0.#;-0.#;0");
            ApplyReadout(deltas[i]);
            deltas[i].color=change>0?thriving:change<0?new Color(1,.65f,.55f):pale;
        }

        void BuildSupplyHint(float x,float y)
        {
            float w=portrait?374:473;
            var hint=Panel(baseRoot,"Resource details on hover",x,y,w,86,Color.white,Slice.Sheet);
            hint.GetComponent<Image>().raycastTarget=false;
            var values=Label(hint,"",13,3,w-26,20,12,gold);
            values.name="Manor.Forecast";
            var factors=Label(hint,"",13,24,w-26,26,11,pale);
            factors.name="Manor.Forecast.Factors";
            var synergy=Label(hint,"",13,49,w-26,33,11,thriving);
            synergy.name="Manor.Forecast.Synergy";
            supplyHint=hint.gameObject;
            supplyHint.SetActive(false);
            manorValues.Add(()=>{
                var m=app.Estate;var f=PreviewForecast();
                values.text="收支账本 · 点击资源区打开   |   "+m.ProsperityTitle;
                factors.text=ForecastFactors(m,f,f.winterTimber<f.winterTimberRequired);
                string links=EstateSynergyText(f.synergy,portrait);
                synergy.text=f.marketNeeds.Prosperous?"市集兴旺 +1 民心"+(links.Length==0?"":"  ·  "+links):links;
            });
        }

        internal void SetLedgerHover(bool hovered)
        {
            if(supplyHint)supplyHint.SetActive(hovered&&!portrait&&!app.Modal);
        }

        void VerticalRule(Transform parent,float x,float y,float h)
        {
            var line=Panel(parent,"Glass separator",x,y,1,h,new Color(.9f,.92f,.83f,.23f),null);
            line.GetComponent<Image>().raycastTarget=false;
        }

        void BuildManorDock()
        {
            if(portrait)
            {
                var dock=Float(baseRoot,"Manor calendar",8,rh-88,374,78,Slice.Rail);
                Value(manorValues,Label(dock,"",10,5,86,30,12,gold),CalendarText);
                TimeControls(dock,100,6,true);
                PlotEntry(dock,7,40,61);
                MarchEntries(dock,72,40,75,76,85);
                EventEntry(dock,"Manor.Event",322,40,45,30,manorValues);
                return;
            }
            const float width=736;
            float y=rh-58;
            var bar=Panel(baseRoot,"Manor calendar",(rw-width)*.5f,y,width,44,Color.white,Slice.Rail);
            PlotEntry(bar,6,7,68);
            VerticalRule(bar,81,9,24);
            Value(manorValues,Label(bar,"",92,7,105,30,14,gold),CalendarText);
            TimeControls(bar,197,7,false);
            VerticalRule(bar,457,9,24);
            MarchEntries(bar,467,7,80,80,87);
            EventEntry(bar,"Manor.Event",737,7,84,30,manorValues);
            manorValues.Add(()=>{
                float actualWidth=app.Estate.eventDay!=0?827:width;
                if(bar.sizeDelta.x!=actualWidth)Rect(bar,(rw-actualWidth)*.5f,y,actualWidth,44);
            });
        }

        void PlotEntry(Transform p,float x,float y,float w)
        {
            var button=ToolButton(p,"选地",x,y,w,30,PlotList,Glyph.Plot);
            button.name="Manor.Plots";
        }

        void TimeControls(Transform p,float x,float y,bool compact)
        {
            float pauseWidth=compact?36:37, normalWidth=compact?40:38, fastWidth=compact?40:39;
            var pause=ClockButton(p,0,"",x,y,pauseWidth);
            MarkButton(pause,Glyph.Pause,15);
            var normal=ClockButton(p,1,"1×",x+pauseWidth+2,y,normalWidth);
            MarkButton(normal,Glyph.Play,11,3,10);
            var fast=ClockButton(p,2,"2×",x+pauseWidth+normalWidth+4,y,fastWidth);
            MarkButton(fast,Glyph.Speed,11,3,10);
            ((NativeHudButton)normal).Caption.horizontalOverflow=HorizontalWrapMode.Overflow;
            ((NativeHudButton)fast).Caption.horizontalOverflow=HorizontalWrapMode.Overflow;
            float dayX=x+pauseWidth+normalWidth+fastWidth+9;
            ManorButton(p,"Manor.Day","推一日",dayX,y,compact?62:64,30,()=>app.AdvanceDays(1));
            ManorButton(p,"Manor.Season","推一季",dayX+(compact?65:68),y,compact?68:66,30,()=>app.AdvanceDays(Manor.SeasonDays));
        }

        Button ClockButton(Transform p,int rate,string caption,float x,float y,float w)
        {
            var b=ManorButton(p,"Manor.Speed."+rate,caption,x,y,w,30,()=>app.SetManorSpeed(rate));
            manorValues.Add(()=>SetButtonTone(b,app.ManorSpeed==rate?HudTone.Selected:HudTone.Secondary));
            return b;
        }

        void MarchEntries(Transform p,float x,float y,float patrolW,float expeditionW,float raidW)
        {
            ManorButton(p,"Manor.Patrol","边疆巡边",x,y,patrolW,30,()=>MarchDialog(false,true));
            ManorButton(p,"Manor.Expedition","领地远征",x+patrolW+5,y,expeditionW,30,()=>MarchDialog(false));
            ManorButton(p,"Manor.Raid",portrait?"迎敌":"迎战掠夺者",x+patrolW+expeditionW+10,y,raidW,30,
                ()=>MarchDialog(true),destructive:true);
        }

        void BuildToast(bool estate,float? top=null)
        {
            float w=portrait?374:estate?524:420;
            float y=top??(estate?rh-(portrait?133:103):rh-(portrait?64:60));
            var card=Panel(baseRoot,estate?"Manor status":"Status",(rw-w)/2,y,w,34,Color.white,Slice.Rail);
            card.GetComponent<Image>().raycastTarget=false;
            toast=card.gameObject.AddComponent<CanvasGroup>();
            toast.alpha=0;toast.interactable=false;toast.blocksRaycasts=false;
            if(estate)BindManorToast(card);
            else status=Label(card,app.Message,13,0,w-26,34,12,white,TextAnchor.MiddleCenter);
        }

        internal void UpdateToast()
        {
            if(!toast)return;
            float now=Time.unscaledTime;
            bool contextual=builtInBase&&(shownHover>=0||sectorStatus!=null);
            bool guide=builtInBase&&app.Estate.GuideActive&&now<guideUntil&&sectorStatus==null&&shownHover<0&&now>=toastUntil;
            bool visible=(now<toastUntil||contextual||guide)&&!app.Modal;
            float remaining=guide?guideUntil-now:toastUntil-now;
            float alpha=!visible?0:contextual?1:Mathf.Clamp01(remaining*5);
            if(toast.alpha!=alpha)toast.alpha=alpha;
            if(builtInBase)ApplyManorToastMode(guide,visible);
            bool interactive=visible&&builtInBase&&(guide||app.ManorSector>=0);
            if(toast.blocksRaycasts!=interactive)toast.blocksRaycasts=toast.interactable=interactive;
        }
    }

    internal sealed class NativeHudLedgerHover : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        internal NativeHud Owner;
        public void OnPointerEnter(PointerEventData eventData) => Owner.SetLedgerHover(true);
        public void OnPointerExit(PointerEventData eventData) => Owner.SetLedgerHover(false);
    }
}
