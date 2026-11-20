using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Frontline.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud:MonoBehaviour
    {
        FrontlineApp app;Canvas canvas;RectTransform baseRoot,overlay;Font font;Text status,clock,power,progress,commandStatus,brief,pauseLabel;Slider timeline;RectTransform redPower;float powerWidth;float rw,rh,nextRefresh;int width,height;bool portrait,deckOpen=true;string dialogTitle;Action<RectTransform,float> dialogBuilder;
        readonly StringBuilder readoutText=new StringBuilder(512);
        readonly char[] digits=new char[48];
        Frame shownFrame;
        IReadOnlyList<Highlight> shownHighlights;
        string shownPhase,shownMessage,shownProgress,shownCommandTeam;
        int shownTick,shownHighlightCount,shownCommandPoints,shownCooldownTenths;
        RectTransform selectionBox;
        Button dismissDialog;
        Text qualityLabel;
        static readonly string[] QualityNames={"流畅","均衡","精细"};
        float shownReplayTime;
        bool shownPaused,readoutsDirty;
        readonly Color ink=Color.white;
        readonly Color muted=new Color(.79f,.82f,.77f),gold=titleGold,white=new Color(.985f,.957f,.886f),line=new Color(.52f,.59f,.56f,.65f);
        readonly bool[] arenaStyles={true,true,true,false};readonly HashSet<int> arenaFavorites=new HashSet<int>(),arenaImports=new HashSet<int>();readonly HashSet<string> arenaMaps=new HashSet<string>();string seedText="42,77";
        public void Initialize(FrontlineApp value)
        {
            app=value;font=Resources.Load<Font>("NotoSansCJKsc-Regular");InitializeTheme();var go=new GameObject("Native UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;canvas.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            if(EventSystem.current==null)new GameObject("Event System",typeof(EventSystem),typeof(StandaloneInputModule));baseRoot=Node(canvas.transform,"Game HUD");app.Changed+=StateChanged;app.Notice+=ReceiveNotice;app.ExpeditionReturned+=SettlementDialog;
            app.View.FrontierFocusChanged+=SyncCameraBindings;
        }
        void OnDestroy()
        {
            if(app)
            {
                app.Changed-=StateChanged;
                app.Notice-=ReceiveNotice;
                app.ExpeditionReturned-=SettlementDialog;
                if(app.View)app.View.FrontierFocusChanged-=SyncCameraBindings;
            }
            ReleaseTheme();
        }
        internal void SelectionBox(Vector2 a,Vector2 b,bool show)
        {
            if(!show){if(selectionBox)selectionBox.gameObject.SetActive(false);return;}
            if(!selectionBox)
            {
                selectionBox=Panel(canvas.transform,"Selection rectangle",0,0,0,0,new Color(.42f,.77f,.94f,.18f),null);
                selectionBox.GetComponent<Image>().raycastTarget=false;
                var outline=selectionBox.gameObject.AddComponent<Outline>();
                outline.effectColor=new Color(.69f,.90f,1,.9f);outline.effectDistance=new Vector2(1,-1);
                outline.useGraphicAlpha=false;
            }
            selectionBox.gameObject.SetActive(true);
            float scale=rw/Screen.width;
            Rect(selectionBox,Mathf.Min(a.x,b.x)*scale,rh-Mathf.Max(a.y,b.y)*scale,Mathf.Abs(a.x-b.x)*scale,Mathf.Abs(a.y-b.y)*scale);
        }
        RectTransform Node(Transform parent,string name){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return go.GetComponent<RectTransform>();}
        void Rect(RectTransform t,float x,float y,float w,float h){t.anchorMin=t.anchorMax=new Vector2(0,1);t.pivot=new Vector2(0,1);t.anchoredPosition=new Vector2(x,-y);t.sizeDelta=new Vector2(w,h);}
        void Stretch(RectTransform t){t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=t.offsetMax=Vector2.zero;}
        RectTransform Panel(Transform parent,string name,float x,float y,float w,float h,Color color,Slice? slice=Slice.Frame)
        {
            var rect=Node(parent,name);
            Rect(rect,x,y,w,h);
            var image=rect.gameObject.AddComponent<Image>();
            image.color=color;
            if(slice.HasValue)
            {
                image.sprite=slices[(int)slice.Value];
                image.type=Image.Type.Sliced;
            }
            return rect;
        }
        Text Label(Transform parent,string text,float x,float y,float w,float h,int size=14,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var t=Node(parent,"Text "+text.Substring(0,Math.Min(18,text.Length)));Rect(t,x,y,w,h);var label=t.gameObject.AddComponent<Text>();label.font=font;label.fontSize=size;label.text=text;label.color=color??white;label.alignment=align;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;label.raycastTarget=false;return label;
        }
        Button Button(Transform parent,string text,float x,float y,float w,float h,Action action,bool accent=false,bool selected=false,bool destructive=false)
        {
            var rect=Panel(parent,text,x,y,w,h,Color.white,Slice.Secondary);
            var b=rect.gameObject.AddComponent<NativeHudButton>();
            b.targetGraphic=rect.GetComponent<Image>();
            b.colors=new ColorBlock
            {
                normalColor=new Color(.94f,.94f,.94f),
                highlightedColor=Color.white,
                selectedColor=Color.white,
                pressedColor=new Color(.78f,.78f,.75f),
                disabledColor=Color.white,
                colorMultiplier=1,
                fadeDuration=.075f
            };
            // Space belongs to the game while the HUD is open. Dialogs retain keyboard navigation.
            if(!overlay||!rect.IsChildOf(overlay))b.navigation=new Navigation{mode=Navigation.Mode.None};
            var label=Label(rect,text,5,0,w-10,h,portrait?13:14,white,TextAnchor.MiddleCenter);
            ApplyButtonLook(b,label,destructive?HudTone.Danger:accent?HudTone.Primary:selected?HudTone.Selected:HudTone.Secondary);
            b.onClick.AddListener(()=>app.Try(action));
            return b;
        }
        InputField InputBox(Transform parent,string value,float x,float y,float w,float h,Action<string> changed=null,bool multiline=false)
        {
            var rect=Panel(parent,"Input",x,y,w,h,new Color(.04f,.07f,.1f));var input=rect.gameObject.AddComponent<InputField>();var text=Label(rect,value,10,6,w-20,h-12,14,white,multiline?TextAnchor.UpperLeft:TextAnchor.MiddleLeft);text.supportRichText=false;input.textComponent=text;input.text=value;input.characterLimit=1024*1024;input.lineType=multiline?InputField.LineType.MultiLineNewline:InputField.LineType.SingleLine;if(changed!=null)input.onEndEdit.AddListener(s=>app.Try(()=>changed(s)));return input;
        }
        void Clear(Transform t){foreach(Transform child in t){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
        public void Build()
        {
            if(!app||app.Config==null)return;
            bool resized=width!=Screen.width||height!=Screen.height;
            if(overlay&&(builtManorMode!=app.InManor||builtInBase!=(app.Phase=="manor")))CloseDialog();
            width=Screen.width;height=Screen.height;portrait=Viewport.Compact;rw=portrait?390:1280;rh=height/(float)Math.Max(1,width)*rw;canvas.GetComponent<CanvasScaler>().scaleFactor=width/rw;Stretch(baseRoot);Clear(baseRoot);status=clock=power=progress=commandStatus=brief=pauseLabel=null;timeline=null;redPower=null;
            builtManorMode=app.InManor;builtInBase=app.Phase=="manor";builtFrontier=app.InFrontier;manorValues.Clear();frontierValues.Clear();
            toast=null;supplyHint=null;
            TopBar();
            if(builtInBase)ManorHud();else if(app.InFrontier)FrontierHud();else if(app.Phase=="deploy"){if(app.InManor)ExpeditionDeploy();else Deploy();}else BattleHud();
            if(!status)BuildToast(false);
            if(app.Busy){var p=Float(baseRoot,"Calculation",portrait?12:335,portrait?145:72,portrait?366:610,83);progress=Label(p,app.Progress??"准备计算",14,8,portrait?230:460,35,14,gold);Button(p,"停止",portrait?270:500,20,74,42,app.CancelJob);Label(p,"分片推演中，可随时停止",14,44,230,25,11,muted);}
            readoutsDirty=true;UpdateReadouts();
            if(overlay&&(resized||!builtInBase)&&dialogBuilder!=null)
                OpenDialog(dialogTitle,dialogBuilder,dialogHeight,dialogFooter,true,dialogWidth,dialogWholeRows,dialogHeader);
            RefreshManorValues();
            if(!builtInBase)UpdateToast();
        }
        void Deploy()
        {
            var publicConfig=app.PublicConfig;string team=app.EditingTeam;
            var side=Float(baseRoot,"Deployment",portrait?10:16,portrait?rh-(deckOpen?343:105):71,portrait?370:268,portrait?(deckOpen?306:67):rh-119);float sw=portrait?370:268;
            Label(side,(app.EditingTeam=="red"?"红方":"蓝方")+" · 战前布阵",14,7,sw-120,28,17,gold);Button(side,portrait?(deckOpen?"收起":"展开"):"撤销",sw-94,8,80,30,()=>{if(portrait){deckOpen=!deckOpen;Build();}else app.Undo();});
            if(portrait&&!deckOpen){Label(side,app.Data.Cost(publicConfig,team).ToString("0")+" / "+app.PlayerLimit+" 军费",14,36,200,25,12,muted);TouchSwitch();return;}
            float footerY=portrait?265:side.rect.height-52;float stanceY=footerY-86;float visible=portrait?202:Mathf.Max(40,stanceY-52);var scroll=Scroll(side,10,48,sw-20,visible);float y=4,w=sw-42;
            Label(scroll,"军费 "+app.Data.Cost(publicConfig,team).ToString("0")+" / "+app.PlayerLimit+"  ·  "+publicConfig.Army(team).Count+" / 60",0,y,w,28,13,muted);y+=34;
            int index=0;foreach(var d in app.Data.Units.Values){int col=index%2,row=index/2;string type=d.id;Button(scroll,d.name+"  "+d.cost, col*(w/2+3),y+row*42,w/2-3,36,()=>app.SelectTool(type),selected:app.UnitType==type&&app.PropType==null&&app.Tool=="place");index++;}y+=5*42+8;
            Label(scroll,"战场道具",0,y,w,28,13,muted);y+=32;index=0;foreach(var d in app.Data.Props.Values){string type=d.id;Button(scroll,d.name+" "+d.cost,index%2*(w/2+3),y+index/2*40,w/2-3,34,()=>app.SelectTool(type,true),selected:app.PropType==type);index++;}y+=3*40+8;
            Transform stanceParent=portrait?scroll:side;float stanceTop=portrait?y:stanceY;float stanceLeft=portrait?0:12;float stanceWidth=portrait?w:sw-24;
            Label(stanceParent,"兵种姿态 / 选中 "+app.Selected.Count,stanceLeft,stanceTop,stanceWidth,28,13,muted);var stances=new[]{"推进","坚守","迂回","猎杀"};for(int i=0;i<4;i++){string stance=Catalog.Stances[i];Button(stanceParent,stances[i],stanceLeft+i*(stanceWidth/4+1),stanceTop+32,stanceWidth/4-2,36,()=>app.SetStance(stance),selected:app.Stance==stance);}if(portrait)y+=77;
            Button(scroll,"选择",0,y,w/3-3,36,()=>{app.Tool="select";app.Refresh();},selected:app.Tool=="select");Button(scroll,"移动",w/3+1,y,w/3-3,36,()=>{app.Tool="move";app.Refresh();},selected:app.Tool=="move");Button(scroll,"删除",w*2/3+2,y,w/3-3,36,app.DeleteSelection,destructive:true);y+=44;
            Button(scroll,"方阵",0,y,w/3-3,36,()=>app.Arrange("square"));Button(scroll,"横列",w/3+1,y,w/3-3,36,()=>app.Arrange("line"));Button(scroll,"楔形",w*2/3+2,y,w/3-3,36,()=>app.Arrange("wedge"));y+=44;
            Button(scroll,app.Mirror?"镜像布兵：开":"镜像布兵：关",0,y,w,36,()=>{app.Mirror=!app.Mirror;app.Refresh();});y+=48;
            string diagnosis=app.Data.Diagnose(publicConfig,team);
            Label(scroll,diagnosis,0,y,w,70,12,gold,TextAnchor.UpperLeft);y+=78;Button(scroll,"预设 / 收藏 / 关卡",0,y,w,40,Presets);y+=50;Label(scroll,"左键点选布兵 · 右键拖动视角\n选择工具支持拖框，再切换移动。",0,y,w,54,11,muted,TextAnchor.UpperLeft);y+=81;scroll.sizeDelta=new Vector2(w,y);
            if(!portrait)FitDeploymentViewport(scroll,visible);
            Button(side,"清空",12,footerY,(sw-34)*.33f,38,app.ClearTeam,destructive:true);Button(side,StartText(),20+(sw-34)*.33f,footerY,(sw-34)*.67f-8,38,app.RequestStart,true);
            if(!portrait)
            {
                var map=app.Data.Maps[app.Config.map];
                var mapTitle=Float(baseRoot,"Map title",299,71,336,58,Slice.Rail);
                mapTitle.GetComponent<Image>().raycastTarget=false;
                Label(mapTitle,map.name,15,3,307,29,19,gold);
                Rule(mapTitle,15,33,304);
                Label(mapTitle,map.hint??"",15,35,307,20,12,white);
                Button(baseRoot,"视角 · "+CameraName(),rw-170,rh-84,150,38,()=>{app.View.CameraMode=(app.View.CameraMode+1)%3;app.Refresh();});
            }
            else {TouchSwitch();Label(baseRoot,diagnosis,14,91,362,47,12,gold,TextAnchor.UpperLeft);}
        }
        static void FitDeploymentViewport(RectTransform content,float available)
        {
            float last=LastFullRow(content,available-4);
            if(last>0)((RectTransform)content.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,last+4);
        }

        static float LastFullRow(RectTransform content,float available)
        {
            float last=0;
            foreach(RectTransform row in content)
            {
                float bottom=-row.anchoredPosition.y+row.sizeDelta.y;
                if(bottom<=available)last=Mathf.Max(last,bottom);
            }
            return last;
        }
        string StartText(){if(app.Mode=="hotseat"&&app.Hotseat!="revealed")return app.Hotseat=="red"?"封存红方":app.Hotseat=="handoff"?"蓝方接手":"封存蓝方";return "开 战";}
        string CameraName(){return app.View.CameraMode==0?"全景":app.View.CameraMode==1?"俯瞰":"追踪";}
        void TouchSwitch(){Button(baseRoot,app.TouchCamera?"拖动视角  ⇄  切换点选":"点选布阵 / 军令  ⇄  切换视角",10,52,280,32,()=>{app.TouchCamera=!app.TouchCamera;app.Refresh();},selected:!app.TouchCamera);Button(baseRoot,"视角",300,52,80,32,()=>{app.View.CameraMode=(app.View.CameraMode+1)%3;app.Refresh();});}
        void BattleHud()
        {

            var score=Float(baseRoot,"Live power",portrait?10:16,portrait?91:69,portrait?370:300,73,Slice.Rail);
            score.GetComponent<Image>().raycastTarget=false;
            clock=Label(score,"",14,3,score.sizeDelta.x-28,31,portrait?18:19,gold);power=Label(score,"",14,31,score.sizeDelta.x-28,22,12,white);
            powerWidth=score.sizeDelta.x-28;var blueBar=Panel(score,"Blue power",14,57,powerWidth,6,new Color(.47f,.66f,.84f),Slice.Badge);blueBar.GetComponent<Image>().raycastTarget=false;redPower=Panel(blueBar,"Red power",0,0,powerWidth/2,6,new Color(.94f,.52f,.43f),Slice.Badge);redPower.GetComponent<Image>().raycastTarget=false;if(portrait)TouchSwitch();
            bool commands=app.Phase=="battle"&&app.Config.commander.enabled,results=app.Phase=="result";float controlsHeight=commands?(portrait?130:100):results?104:57;
            float w=portrait?370:commands?690:416,x=(rw-w)*.5f,y=rh-controlsHeight-14;
            var p=Float(baseRoot,"Battle controls",x,y,w,controlsHeight,Slice.Rail);
            var pause=Button(p,app.Paused?"继续":"暂停",10,10,portrait?70:90,38,app.TogglePause);pauseLabel=pause.GetComponentInChildren<Text>();pause.interactable=app.Phase!="result";Button(p,app.Speed+"×",portrait?86:110,10,60,38,()=>{app.Speed=app.Speed==.5f?1:app.Speed==1?2:app.Speed==2?4:.5f;app.Refresh();});
            if(app.InManor)
            {
                if(!results)ManorButton(p,"Manor.Retreat",app.Phase=="battle"?"撤回庄园":app.PendingSettlement?"结算归营":"返回庄园",portrait?152:180,10,80,38,app.ReturnManor);
            }
            else Button(p,"回布阵",portrait?152:180,10,80,38,app.ReturnToDeploy);
            Button(p,app.Muted?"静音":"声音",portrait?238:270,10,60,38,()=>{app.Muted=!app.Muted;app.Refresh();});Button(p,"战报",portrait?304:340,10,56,38,ReportDialog);
            if(results)
            {
                if(app.InManor)
                {
                    ManorButton(p,"Manor.Return","返回庄园结算",10,54,142,38,app.ReturnManor,true);
                    ManorButton(p,"Manor.Watch","看回放",159,54,86,38,app.Watch);
                    ManorButton(p,"Manor.ExportReplay","导出回放",252,54,108,38,()=>app.ExportReplay());
                }
                else
                {
                    Button(p,"看回放",10,56,100,32,app.Watch,true);
                    if(app.Series!=null&&!app.Series.Complete)Button(p,"下一局",120,56,100,32,app.NextRound);
                    Button(p,"导出回放",230,56,120,32,()=>app.ExportReplay());
                }
            }
            else if(commands)
            {
                string[] names={"1 鼓舞","2 集火","3 盾阵"};
                for(int i=0;i<3;i++)
                {
                    string type=Catalog.Orders[i];
                    bool enabled=app.AllowsOrder(type);
                    string caption=enabled?names[i]:names[i]+" · Lv."+Manor.OrderLevel(type);
                    var order=Button(p,caption,10+i*116,56,108,32,()=>{app.OrderType=type;app.Refresh();},selected:app.OrderType==type);
                    order.interactable=enabled;
                }
                commandStatus=Label(p,"",10,94,w-20,32,12,muted);
                if(!portrait){commandStatus.rectTransform.anchoredPosition=new Vector2(375,-53);commandStatus.rectTransform.sizeDelta=new Vector2(280,34);}
            }
            BuildToast(false,y-(app.Phase=="replay"?87:40));
            if(app.Phase=="replay"&&app.ReplayFile?.kind=="trajectory"){var tp=Panel(baseRoot,"Timeline",x,y-48,w,40,ink);timeline=Slider(tp,10,9,w-20,22,app.ReplayFile.result.time,v=>app.Seek(v));foreach(var h in app.ReplayFile.highlights){var captured=h;float hx=10+(w-40)*h.tick/60f/Mathf.Max(.01f,app.ReplayFile.result.time);var mark=Button(tp,"",hx-9,0,22,40,()=>app.SeekHighlight(captured));mark.GetComponent<Image>().color=new Color(0,0,0,0);Panel(mark.transform,"High point",9,1,3,12,gold);}}
            if(!portrait){Button(baseRoot,"视角 · "+CameraName(),rw-175,69,156,34,()=>{app.View.CameraMode=(app.View.CameraMode+1)%3;app.Refresh();});Button(baseRoot,app.BriefOpen?"收起战况":"战况快报",rw-171,rh-56,153,33,()=>{app.BriefOpen=!app.BriefOpen;app.Refresh();});if(app.BriefOpen)brief=Label(Float(baseRoot,"Brief",rw-267,rh-285,248,211),"",12,12,224,188,13,muted,TextAnchor.UpperLeft);}
            if(app.Series!=null)Label(baseRoot,"Bo3  "+app.Series.Archive.wins["red"]+" : "+app.Series.Archive.wins["blue"]+"  ·  "+(app.Series.Complete?"系列结束":"第 "+(app.Series.Archive.rounds.Count+(app.Phase=="result"?0:1))+" 局"),portrait?12:650,portrait?168:69,340,30,14,white);
        }
        Slider Slider(Transform parent,float x,float y,float w,float h,float max,Action<float> change)
        {
            var rect=Panel(parent,"Replay seek",x,y,w,h,line,null);var s=rect.gameObject.AddComponent<Slider>();s.minValue=0;s.maxValue=max;var fill=Panel(rect,"Fill",0,0,w,h,gold,null);Stretch(fill);s.fillRect=fill;s.targetGraphic=fill.GetComponent<Image>();s.direction=UnityEngine.UI.Slider.Direction.LeftToRight;s.onValueChanged.AddListener(v=>change(v));return s;
        }
        RectTransform Scroll(Transform parent,float x,float y,float w,float h)
        {
            var view=Node(parent,"Scroll viewport");
            Rect(view,x,y,w,h);
            view.gameObject.AddComponent<Image>().color=Color.clear;
            view.gameObject.AddComponent<RectMask2D>();
            var content=Node(view,"Content");
            Rect(content,0,0,w-12,h);
            var track=Panel(view,"Scroll track",0,0,7,h,new Color(.31f,.38f,.35f,.56f),Slice.Badge);
            track.anchorMin=new Vector2(1,0);
            track.anchorMax=Vector2.one;
            track.offsetMin=new Vector2(-8,3);
            track.offsetMax=new Vector2(-1,-3);
            var slider=Node(track,"Scroll sliding area");
            Stretch(slider);
            slider.offsetMin=Vector2.one;
            slider.offsetMax=-Vector2.one;
            var grip=Panel(slider,"Scroll thumb",0,0,5,h,new Color(.88f,.79f,.61f,.85f),Slice.Badge);
            var bar=track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect=grip;
            bar.targetGraphic=grip.GetComponent<Image>();
            bar.direction=Scrollbar.Direction.BottomToTop;
            bar.navigation=new Navigation{mode=Navigation.Mode.None};
            var scroll=view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport=view;
            scroll.content=content;
            scroll.horizontal=false;
            scroll.movementType=ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity=27;
            scroll.verticalScrollbar=bar;
            scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }
        void OpenDialog(string title,Action<RectTransform,float> builder,float maxHeight=760,Action<RectTransform,float> footer=null,
            bool rebuild=false,float maxWidth=880,bool wholeRows=false,Action<RectTransform,float> header=null)
        {
            float scrollY=overlay&&dialogTitle==title&&dialogContent?dialogContent.anchoredPosition.y:0;
            string notice=rebuild&&dialogNotice?dialogNotice.text:"";
            if(overlay){overlay.gameObject.SetActive(false);Destroy(overlay.gameObject);}
            manorDialogValues.Clear();dialogNotice=null;
            app.Modal=true;dialogTitle=title;dialogBuilder=builder;dialogFooter=footer;dialogHeight=maxHeight;dialogWidth=maxWidth;
            SetLedgerHover(false);
            dialogWholeRows=wholeRows;
            dialogHeader=header;
            app.View.HoveredPlot=-1;
            overlay=Panel(canvas.transform,"Dialog overlay",0,0,rw,rh,new Color(.04f,.072f,.063f,.51f),null);
            float w=Mathf.Min(rw-(portrait?20:36),maxWidth),h=Mathf.Min(rh-32,maxHeight);
            var shadow=Panel(overlay,"Dialog shadow",0,0,0,0,Color.white,Slice.Shadow);
            shadow.GetComponent<Image>().raycastTarget=false;
            var shell=Panel(overlay,title,(rw-w)/2,(rh-h)/2,w,h,Color.white,Slice.Sheet);
            Label(shell,title,22,9,w-82,43,portrait?18:23,white);
            dismissDialog=Button(shell,"关闭",w-55,13,35,33,CloseDialog);
            var closeCaption=((NativeHudButton)dismissDialog).Caption;
            closeCaption.text="×";closeCaption.fontSize=22;
            Rule(shell,20,54,w-40);
            float noticeSpace=app.InManor?32:0,footerSpace=footer==null?0:52;
            float headerSpace=header==null?0:42;
            if(header!=null)
            {
                var tabs=Panel(shell,"Dialog tabs",18,59,w-36,38,Color.white,Slice.Rail);
                var contents=Node(tabs,"Segmented choices");
                Rect(contents,4,2,w-44,34);
                header(contents,w-44);
            }
            float chrome=80+noticeSpace+footerSpace+headerSpace;
            dialogContent=Scroll(shell,18,62+headerSpace,w-36,0);
            builder(dialogContent,w-60);
            float contentHeight=dialogContent.sizeDelta.y;
            foreach(RectTransform child in dialogContent)contentHeight=Mathf.Max(contentHeight,-child.anchoredPosition.y+child.sizeDelta.y);
            contentHeight+=6;
            dialogContent.sizeDelta=new Vector2(w-60,contentHeight);
            h=Mathf.Min(h,contentHeight+chrome);
            float visible=Mathf.Max(0,h-chrome);
            if(wholeRows&&visible<contentHeight)
            {
                float bottom=LastFullRow(dialogContent,visible-4);
                if(bottom>0){visible=bottom+4;h=visible+chrome;}
            }
            Rect(shell,(rw-w)/2,(rh-h)/2,w,h);
            Rect(shadow,(rw-w)/2-14,(rh-h)/2-8,w+28,h+27);
            ((RectTransform)dialogContent.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,visible);
            dialogContent.anchoredPosition=new Vector2(0,Mathf.Clamp(scrollY,0,Mathf.Max(0,dialogContent.sizeDelta.y-visible)));
            if(footer!=null){var bar=Panel(shell,"Footer",18,h-58-noticeSpace,w-36,46,Color.white,Slice.Inset);footer(bar,w-36);}
            if(app.InManor)dialogNotice=Label(shell,notice,18,h-38,w-36,32,12,gold);
            RefreshBindings(manorDialogValues);
        }
        internal bool TryDismissDialog()
        {
            if(!app.Modal||!overlay||!dismissDialog||!dismissDialog.isActiveAndEnabled||!dismissDialog.IsInteractable())return false;
            CloseDialog();return true;
        }
        void CloseDialog(){app.Presentation?.Flush();if(overlay){overlay.gameObject.SetActive(false);Destroy(overlay.gameObject);}overlay=null;dismissDialog=null;dialogContent=null;dialogBuilder=null;dialogFooter=dialogHeader=null;dialogNotice=null;manorDialogValues.Clear();app.Modal=app.HoldExpedition=false;EventSystem.current?.SetSelectedGameObject(null);}
        void QualityEntry(RectTransform bar,float x,float y,float w,float h)
        {
            var entry=Button(bar,portrait?"设置":"显示 · "+QualityNames[app.View.QualityLevel],x,y,w,h,QualityDialog);
            entry.name="Settings.Quality";
            qualityLabel=entry.GetComponentInChildren<Text>();
            MarkButton(entry,Glyph.Settings,portrait?12:15,portrait?4:7,portrait?11:12);
        }
        void QualityChoices(RectTransform p,float w,float y,Action redraw)
        {
            for(int i=0;i<QualityNames.Length;i++)
            {
                int level=i;
                var option=Button(p,QualityNames[i],i*w/3,y,w/3-5,40,()=>{
                    app.View.SetQuality(level);
                    if(qualityLabel&&!portrait)qualityLabel.text="显示 · "+QualityNames[level];
                    redraw();
                },selected:app.View.QualityLevel==level);
                option.name="Quality."+level;
            }
        }
        public void Strategy(){OpenDialog("谋算 · 演武擂台",(p,w)=>{
            float y=0;Label(p,"对局模式",0,y,w,28,15,gold);y+=34;string[] modes={"ai","sandbox","hotseat","auto"};for(int i=0;i<modes.Length;i++){string mode=modes[i];Button(p,FrontlineApp.ModeName(mode),i*(w/4),y,w/4-5,40,()=>{CloseDialog();app.SetMode(mode);Strategy();},selected:app.Mode==mode);}y+=54;
            Label(p,"地图",0,y,w,28,15,gold);y+=34;int j=0;foreach(var map in app.Data.Maps.Values){string id=map.id;Button(p,map.name,j%2*(w/2),y+j/2*44,w/2-6,38,()=>{CloseDialog();app.SetMap(id);Strategy();},selected:app.Config.map==id);j++;}y+=3*44+12;
            Label(p,"画质",0,y,w,28,15,gold);y+=34;QualityChoices(p,w,y,Strategy);y+=52;
            Label(p,"AI 风格",0,y,w,28,15,gold);y+=34;var styles=new[]{"守正 · 稳健","奇袭 · 机动","知敌 · 克制","算策 · 推演"};for(int i=0;i<4;i++){string style=Catalog.Styles[i];Button(p,styles[i],i%2*(w/2),y+i/2*44,w/2-6,38,()=>{app.Style=style;Strategy();},selected:app.Style==style);}y+=100;
            Label(p,"算策会以多套合法候选快筛，再完整验算前两名；只读取公开阵型。封存的热座对手不参与分析。",0,y,w,76,13,muted,TextAnchor.UpperLeft);y+=84;
            Button(p,"为当前方谋算",0,y,w/2-5,40,()=>{CloseDialog();app.PlanArmy(app.EditingTeam);});Button(p,"演武擂台排行",w/2,y,w/2-5,40,ArenaDialog,true);y+=52;
            Label(p,"随机种子",0,y,w/2-6,28,13,muted);Label(p,"双方军费",w/2,y,w/2-6,28,13,muted);y+=32;InputBox(p,app.Config.seed.ToString(),0,y,w/2-6,38,app.SetSeed);InputBox(p,app.Config.budget.ToString(),w/2,y,w/2-6,38,s=>{if(int.TryParse(s,out int b))app.SetBudget(b);});y+=52;
            Button(p,app.Bo3?"Bo3：开":"Bo3：关",0,y,w/2-5,40,()=>{app.SetBo3(!app.Bo3);Strategy();});Button(p,app.Config.commander.enabled?"统帅军令：开":"统帅军令：关",w/2,y,w/2-5,40,()=>{app.SetCommander(!app.Config.commander.enabled);Strategy();});y+=52;
            if(app.Mode=="sandbox"||app.Mode=="hotseat")Button(p,"当前军令 / 编辑阵营："+(app.Team=="red"?"红方":"蓝方"),0,y,w,40,()=>{app.Team=Catalog.Other(app.Team);app.SetTeam(app.Team);Strategy();});y+=52;Button(p,"兵种、道具与操作说明",0,y,w,40,Help);p.sizeDelta=new Vector2(w,y+54);
        });}
        void Presets(){OpenDialog("预设阵型 · 收藏 · 关卡",(p,w)=>{float y=0;int i=0;var names=new[]{"均衡","冲锋","箭雨","枪盾","火炮","巨人","骑兵","军医","攻城"};foreach(string id in app.Data.Formations.Keys){string preset=id;Button(p,names[i],i%2*w/2,y+i/2*44,w/2-6,38,()=>{CloseDialog();app.Preset(preset);});i++;}y+=5*44+16;Label(p,"本机收藏槽",0,y,w,28,16,gold);y+=34;for(i=0;i<5;i++){int slot=i;Button(p,(i+1)+" "+(app.Favorites[i]?.name??"空"),0,y,w,36,()=>{app.FavoriteSlot=slot;Presets();},selected:app.FavoriteSlot==slot);y+=42;}Button(p,"保存当前方",0,y,w/2-5,40,()=>{app.SaveFavorite();Presets();});Button(p,"载入到当前方",w/2,y,w/2-5,40,()=>{CloseDialog();app.LoadFavorite();});y+=58;Label(p,"战术关卡",0,y,w,28,16,gold);y+=36;for(i=0;i<app.Data.Data.missions.Count;i++){int mission=i;Button(p,app.Data.Data.missions[i].name,0,y,w,40,()=>{CloseDialog();app.Mission(mission);});y+=46;}p.sizeDelta=new Vector2(w,y+12);});}
        public void Workshop()
        {
            if(app.InManor){OpenManorWorkshop();return;}
            OpenDialog("工坊 · 文件与回放",(p,w)=>{
                float y=0;
                Button(p,"导入 JSON 文件（阵型 / 回放 / 庄园）",0,y,w,44,()=>{CloseDialog();app.ImportFile();},true);y+=54;
                Button(p,"导出当前阵型",0,y,w,40,app.ExportConfig);y+=48;
                Button(p,"导出完整轨迹回放",0,y,w,40,()=>app.ExportReplay());y+=48;
                Button(p,"导出 Unity 种子回放",0,y,w,40,()=>app.ExportReplay(true));y+=48;
                Button(p,"导出 Bo3 战报",0,y,w,40,app.ExportSeries);y+=54;
                Label(p,"也可以粘贴阵型、回放或庄园存档 JSON",0,y,w,30,15,gold);y+=34;
                var input=InputBox(p,"",0,y,w,180,null,true);y+=190;
                Button(p,"校验并导入文本",0,y,w,40,()=>{app.ImportText(input.text);CloseDialog();});y+=54;
                Label(p,"0.1–0.8 的完整轨迹可直接播放。旧 Rapier 种子包先运行随工程提供的转换工具。Unity 种子包要求同引擎、同平台复现；跨平台请交换完整轨迹。",0,y,w,110,13,muted,TextAnchor.UpperLeft);y+=120;
                Button(p,"操作说明 / 外部 Bot",0,y,w,40,Help);p.sizeDelta=new Vector2(w,y+54);
            });
        }
        void ArenaDialog(){if(arenaMaps.Count==0)arenaMaps.Add(app.Config.map);OpenDialog("演武擂台 · 双循环赛",(p,w)=>{float y=0;Label(p,"选择 2–8 方参赛；每对阵型交换红蓝出场。",0,y,w,44,14,gold);y+=50;for(int i=0;i<4;i++){int index=i;Button(p,(arenaStyles[i]?"[已选] ":"[未选] ")+new[]{"守正","奇袭","知敌","算策"}[i],0,y,w,38,()=>{arenaStyles[index]=!arenaStyles[index];ArenaDialog();});y+=44;}for(int i=0;i<5;i++)if(app.Favorites[i]!=null){int index=i;Button(p,(arenaFavorites.Contains(i)?"[已选] ":"[未选] ")+"收藏 "+(i+1)+" · "+app.Favorites[i].map,0,y,w,38,()=>{if(!arenaFavorites.Add(index))arenaFavorites.Remove(index);ArenaDialog();});y+=44;}for(int i=0;i<app.ImportedEntrants.Count;i++){int index=i;Button(p,(arenaImports.Contains(i)?"[已选] ":"[未选] ")+app.ImportedEntrants[i].name,0,y,w,38,()=>{if(!arenaImports.Add(index))arenaImports.Remove(index);ArenaDialog();});y+=44;}Button(p,"导入参赛阵型 JSON",0,y,w,40,()=>FileBridge.Import(s=>app.Try(()=>{app.AddArenaFormation(s);arenaImports.Add(app.ImportedEntrants.Count-1);ArenaDialog();})));y+=56;
            Label(p,"地图集合（本地阵型需与地图一致）",0,y,w,32,14,gold);y+=38;foreach(var map in app.Data.Maps.Values){string id=map.id;Button(p,(arenaMaps.Contains(id)?"[已选] ":"[未选] ")+map.name,0,y,w,36,()=>{if(!arenaMaps.Add(id))arenaMaps.Remove(id);ArenaDialog();});y+=42;}
            Label(p,"种子列表，逗号分隔，最多 10 个",0,y,w,30,13,muted);y+=34;var seeds=InputBox(p,seedText,0,y,w,38,s=>seedText=s);y+=50;Button(p,"开始循环赛",0,y,w,44,()=>{seedText=seeds.text;var entrants=new List<Entrant>();for(int i=0;i<4;i++)if(arenaStyles[i])entrants.Add(new Entrant{id=Catalog.Styles[i],name=new[]{"守正","奇袭","知敌","算策"}[i],ai=Catalog.Styles[i]});foreach(int index in arenaFavorites.OrderBy(x=>x)){var f=app.Favorites[index];entrants.Add(new Entrant{id="fav-"+index,name="收藏 "+(index+1),map=f.map,team=f.team,units=f.units,props=f.props});}foreach(int index in arenaImports.OrderBy(x=>x))entrants.Add(app.ImportedEntrants[index]);var values=seedText.Split(',').Select(s=>uint.Parse(s.Trim())).ToList();app.Arena(entrants,app.Data.Maps.Keys.Where(arenaMaps.Contains).ToList(),values);CloseDialog();},true);y+=56;Button(p,"查看最近排名 / 胜率矩阵",0,y,w,40,ArenaResults);p.sizeDelta=new Vector2(w,y+54);});}
        void ArenaResults(){OpenDialog("演武擂台 · 排名与矩阵",(p,w)=>{if(app.ArenaReport==null){Label(p,"完成循环赛后显示结果。",0,0,w,50,15,muted);p.sizeDelta=new Vector2(w,70);return;}float y=0;Label(p,"名次  参赛方  ·  胜/平/负  ·  胜率  ·  残余战力 / 用时 / 稳定 MVP",0,y,w,56,13,gold);y+=62;int i=1;foreach(var r in app.ArenaReport.ranking){Label(p,i+++". "+r.name+"  "+r.wins+" / "+r.draws+" / "+r.losses+"   "+r.winRate.ToString("P0")+"\n残余 "+r.averageRemaining.ToString("0.0")+" · "+r.averageTime.ToString("0.0")+"s · "+(r.stableMvp==null?"—":app.Data.Units[r.stableMvp].name),0,y,w,66,14,white);y+=74;}Label(p,"交锋矩阵 · 每行从左方视角统计",0,y,w,38,16,gold);y+=44;for(i=0;i<app.ArenaReport.entrants.Count;i++)for(int j=i+1;j<app.ArenaReport.entrants.Count;j++){var r=app.ArenaReport.matrix[i][j];Label(p,app.ArenaReport.entrants[i].name+" → "+app.ArenaReport.entrants[j].name+"："+r.wins+" 胜 / "+r.draws+" 平 / "+r.losses+" 负",0,y,w,42,14,muted);y+=46;}Button(p,"导出擂台报告 JSON",0,y,w,42,app.ExportArena,true);p.sizeDelta=new Vector2(w,y+56);});}
        void Help(){OpenDialog("阵线手册",(p,w)=>{string text="布阵\n选择兵种后点击己方半场放置。右键拖动视角、滚轮缩放；选择工具拖框，多选后切到移动工具点选新中心。Ctrl/Cmd+Z 撤销，Delete 删除所选。\n\n对战\nSpace 暂停或继续；Tab 依次切换全景、俯瞰、追踪。R 重置视角，Esc 关闭窗口。军令开启时，1 / 2 / 3 选择鼓舞、集火、盾阵，点击地面释放。每方 3 点，冷却 8 秒。输入框或弹窗打开时会屏蔽快捷键。\n\n触屏\n“拖动视角”用于单手转动镜头，切到“点选布阵 / 军令”后才接受落点。超过拖动阈值、双指缩放、取消触摸均不会放兵。\n\n兵种\n游勇：便宜近战；盾卫：正面减伤；长枪：克制骑兵；弓手：高台射程；弩手：穿盾；爆破手：范围误伤；巨人：击退；骑兵：冲锋；军医：治疗；投石车：远程范围打击。\n\n规则\n每局最多 90 秒，按残余战力判定。Bo3 率先两胜或打满三局；小局间可调整布阵。热座封存阶段只显示己方。道具也计入军费。\n\n外部智能体\nWindows 版可用命令行启动两个 stdio 子进程 Bot。每 60 步接收公开观察，合法军令才入战斗；网页平台可导入 Bot 对战回放。详见随包 docs/UNITY_MIGRATION.md 和 AGENT_PROTOCOL.md。\n\n兼容\n原有布阵可导入。旧版本完整轨迹回放直接播放；旧 Rapier 种子回放用 Node 转换工具生成完整轨迹。Unity 版本使用原生 PhysX，跨平台复盘以完整轨迹为准。";Label(p,text,0,0,w,1180,14,white,TextAnchor.UpperLeft);p.sizeDelta=new Vector2(w,1200);});}
        void ShowNotice(string message)
        {
            string visible=builtInBase&&sectorStatus!=null?sectorStatus:message;
            if(status&&status.text!=visible)status.text=visible;
            shownMessage=message;
        }
        void ReceiveNotice(string message)
        {
            toastUntil=Time.unscaledTime+3.4f;
            ShowNotice(message);
            if(dialogNotice&&dialogNotice.text!=message)dialogNotice.text=message;
        }
        void Number(float value,string format)
        {
            if(value.TryFormat(digits,out int length,format.AsSpan()))readoutText.Append(digits,0,length);
        }
        void Number(int value)
        {
            if(value.TryFormat(digits,out int length))readoutText.Append(digits,0,length);
        }
        void ApplyReadout(Text label)
        {
            string current=label.text;
            // UGUI needs a string when the displayed characters change.
            if(current.Length==readoutText.Length)
            {
                int i=0;
                while(i<current.Length&&current[i]==readoutText[i])i++;
                if(i==current.Length)return;
            }
            label.text=readoutText.ToString();
        }
        int shownTenth,shownRed,shownBlue,shownRedValue,shownBlueValue;
        void FrameReadouts(Frame frame,string phase)
        {
            int red=0,blue=0;
            float redValue=0,blueValue=0;
            for(int i=0;i<frame.units.Count;i++)
            {
                var unit=frame.units[i];
                if(!unit.alive)continue;
                var def=app.View.Definition(unit.type);
                float value=Mathf.Max(0,unit.hp)/app.View.MaxHp(def,unit.id)*def.cost;
                if(unit.team=="red"){red++;redValue+=value;}
                else {blue++;blueValue+=value;}
            }
            int tenth=Mathf.RoundToInt(frame.time*10),rp=Mathf.RoundToInt(redValue),bp=Mathf.RoundToInt(blueValue);
            if(readoutsDirty||shownPhase!=phase||shownTenth!=tenth||shownRed!=red||shownBlue!=blue)
            {
                readoutText.Clear();
                if(phase=="replay")readoutText.Append("回放 ");
                Number(tenth/10f,"0.0");readoutText.Append(phase=="frontier"?"s   我方 ":"s   红 ");Number(red);readoutText.Append(" : ");Number(blue);readoutText.Append(phase=="frontier"?" 守军":" 蓝");
                ApplyReadout(clock);shownTenth=tenth;shownRed=red;shownBlue=blue;
            }
            if(readoutsDirty||shownRedValue!=rp||shownBlueValue!=bp)
            {
                readoutText.Clear().Append("实时战力 · 红 ");Number(rp);readoutText.Append(" / 蓝 ");Number(bp);
                ApplyReadout(power);shownRedValue=rp;shownBlueValue=bp;
            }
            float redWidth=powerWidth*(redValue+blueValue>0?redValue/(redValue+blueValue):.5f);
            if(redPower&&Mathf.Abs(redPower.sizeDelta.x-redWidth)>.25f)redPower.sizeDelta=new Vector2(redWidth,6);
        }
        void UpdateReadouts()
        {
            bool dirty=readoutsDirty;
            var frame=app.CurrentFrame;
            int tick=frame?.tick??-1;
            string phase=app.Phase;
            if(dirty||shownMessage!=app.Message)ShowNotice(app.Message);
            string calculation=app.Progress??"准备计算";
            if(progress&&(dirty||shownProgress!=calculation)&&progress.text!=calculation)progress.text=calculation;
            shownProgress=calculation;
            if(clock&&frame!=null&&(dirty||!ReferenceEquals(shownFrame,frame)||shownTick!=tick||shownPhase!=phase))FrameReadouts(frame,phase);
            if(commandStatus&&frame!=null)
            {
                string team=app.Team;
                int points=frame.commander.points[team],readyIn=Math.Max(0,frame.commander.readyAt[team]-tick);
                int tenths=(readyIn+5)/6;
                if(dirty||team!=shownCommandTeam||points!=shownCommandPoints||tenths!=shownCooldownTenths)
                {
                    readoutText.Clear().Append(team=="red"?"红方":"蓝方").Append(" · 军令 ");
                    Number(points);readoutText.Append(" / 3 · ");Number(tenths/10f,"0.0");readoutText.Append("s 冷却");
                    ApplyReadout(commandStatus);
                    shownCommandTeam=team;shownCommandPoints=points;shownCooldownTenths=tenths;
                }
            }
            if(pauseLabel&&(dirty||shownPaused!=app.Paused))
            {
                string label=app.Paused?"继续":"暂停";
                if(pauseLabel.text!=label)pauseLabel.text=label;
            }
            if(timeline&&(dirty||shownReplayTime!=app.ReplayTime))timeline.SetValueWithoutNotify(app.ReplayTime);
            if(brief)
            {
                var highlights=app.Highlights;
                if(dirty||!ReferenceEquals(shownHighlights,highlights)||shownHighlightCount!=highlights.Count)
                {
                    readoutText.Clear();
                    for(int i=Math.Max(0,highlights.Count-5);i<highlights.Count;i++)
                    {
                        if(readoutText.Length>0)readoutText.Append("\n\n");
                        readoutText.Append(Replay.HighlightText(highlights[i]));
                    }
                    ApplyReadout(brief);
                }
                shownHighlights=highlights;shownHighlightCount=highlights.Count;
            }
            else {shownHighlights=null;shownHighlightCount=0;}
            shownFrame=frame;shownTick=tick;shownPhase=phase;shownPaused=app.Paused;shownReplayTime=app.ReplayTime;readoutsDirty=false;
        }
        void Update()
        {
            if(!app||app.Config==null)return;
            if(width!=Screen.width||height!=Screen.height){Build();return;}
            if(Time.unscaledTime<nextRefresh){UpdateToast();return;}
            nextRefresh=Time.unscaledTime+.1f;
            UpdateReadouts();
            UpdateManorHover();
            UpdateToast();
        }
    }
}
