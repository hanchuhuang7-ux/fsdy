using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed class NativeHud:MonoBehaviour
    {
        FrontlineApp app;Canvas canvas;RectTransform baseRoot,overlay;Font font;Text status,clock,power,progress,commandStatus,brief,pauseLabel;Slider timeline;float rw,rh;int width,height;bool portrait,deckOpen=true;string dialogTitle;Action<RectTransform,float> dialogBuilder;
        readonly Color ink=new Color(.055f,.09f,.125f,.97f),panel=new Color(.08f,.125f,.165f,.97f),muted=new Color(.61f,.7f,.73f),gold=new Color(.96f,.73f,.36f),white=new Color(.9f,.94f,.94f),line=new Color(.2f,.29f,.33f);
        readonly bool[] arenaStyles={true,true,true,false};readonly HashSet<int> arenaFavorites=new HashSet<int>(),arenaImports=new HashSet<int>();readonly HashSet<string> arenaMaps=new HashSet<string>();string seedText="42,77";
        public void Initialize(FrontlineApp value)
        {
            app=value;font=Resources.Load<Font>("NotoSansCJKsc-Regular");var go=new GameObject("Native UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;canvas.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            if(EventSystem.current==null)new GameObject("Event System",typeof(EventSystem),typeof(StandaloneInputModule));baseRoot=Node(canvas.transform,"Game HUD");app.Changed+=Build;app.Notice+=s=>{if(status)status.text=s;};
        }
        RectTransform Node(Transform parent,string name){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return go.GetComponent<RectTransform>();}
        void Rect(RectTransform t,float x,float y,float w,float h){t.anchorMin=t.anchorMax=new Vector2(0,1);t.pivot=new Vector2(0,1);t.anchoredPosition=new Vector2(x,-y);t.sizeDelta=new Vector2(w,h);}
        void Stretch(RectTransform t){t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=t.offsetMax=Vector2.zero;}
        RectTransform Panel(Transform parent,string name,float x,float y,float w,float h,Color color){var t=Node(parent,name);Rect(t,x,y,w,h);var image=t.gameObject.AddComponent<Image>();image.color=color;return t;}
        Text Label(Transform parent,string text,float x,float y,float w,float h,int size=14,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var t=Node(parent,"Text "+text.Substring(0,Math.Min(18,text.Length)));Rect(t,x,y,w,h);var label=t.gameObject.AddComponent<Text>();label.font=font;label.fontSize=size;label.text=text;label.color=color??white;label.alignment=align;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;label.raycastTarget=false;return label;
        }
        Button Button(Transform parent,string text,float x,float y,float w,float h,Action action,bool accent=false,bool selected=false)
        {
            var rect=Panel(parent,text,x,y,w,h,accent?gold:selected?new Color(.18f,.3f,.35f):new Color(.13f,.2f,.25f));var b=rect.gameObject.AddComponent<Button>();var c=b.colors;c.highlightedColor=new Color(.8f,.9f,.95f);c.pressedColor=new Color(.6f,.7f,.74f);b.colors=c;Label(rect,text,6,0,w-12,h,portrait?13:14,accent?ink:white,TextAnchor.MiddleCenter);b.onClick.AddListener(()=>app.Try(action));return b;
        }
        InputField InputBox(Transform parent,string value,float x,float y,float w,float h,Action<string> changed=null,bool multiline=false)
        {
            var rect=Panel(parent,"Input",x,y,w,h,new Color(.04f,.07f,.1f));var input=rect.gameObject.AddComponent<InputField>();var text=Label(rect,value,10,6,w-20,h-12,14,white,multiline?TextAnchor.UpperLeft:TextAnchor.MiddleLeft);text.supportRichText=false;input.textComponent=text;input.text=value;input.characterLimit=1024*1024;input.lineType=multiline?InputField.LineType.MultiLineNewline:InputField.LineType.SingleLine;if(changed!=null)input.onEndEdit.AddListener(s=>app.Try(()=>changed(s)));return input;
        }
        void Clear(Transform t){foreach(Transform child in t)Destroy(child.gameObject);}
        public void Build()
        {
            if(!app||app.Config==null)return;width=Screen.width;height=Screen.height;portrait=Viewport.Compact;rw=portrait?390:1280;rh=height/(float)Math.Max(1,width)*rw;canvas.GetComponent<CanvasScaler>().scaleFactor=width/rw;Stretch(baseRoot);Clear(baseRoot);status=clock=power=progress=commandStatus=brief=pauseLabel=null;timeline=null;
            Panel(baseRoot,"Top bar",0,0,rw,portrait?90:74,ink);Label(baseRoot,"FRONTLINE LAB   /   阵线实验室",18,8,portrait?280:430,36,portrait?17:21,gold);Label(baseRoot,"UNITY EDITION  ·  0.6.0",18,40,260,20,10,muted);
            if(portrait){Button(baseRoot,"策略",294,10,78,32,Strategy);Button(baseRoot,"工坊",294,49,78,30,Workshop);}
            else {Button(baseRoot,"策略与演武擂台",850,17,160,38,Strategy);Button(baseRoot,"工坊 / 回放",1020,17,130,38,Workshop);Button(baseRoot,"说明",1160,17,100,38,Help);}
            if(app.Phase=="deploy")Deploy();else BattleHud();
            status=Label(baseRoot,app.Message,portrait?14:302,rh-30,portrait?362:rw-320,26,12,gold);if(app.Busy){var p=Panel(baseRoot,"Calculation",portrait?12:335,portrait?100:90,portrait?366:610,83,ink);progress=Label(p,app.Progress??"准备计算",14,8,portrait?230:460,35,14,gold);Button(p,"停止",portrait?270:500,20,74,42,app.CancelJob);Label(p,"分片推演中，可随时停止",14,44,230,25,11,muted);}
            if(overlay){Destroy(overlay.gameObject);overlay=null;if(dialogBuilder!=null)OpenDialog(dialogTitle,dialogBuilder);}
        }
        void Deploy()
        {
            var side=Panel(baseRoot,"Deployment",portrait?10:16,portrait?rh-(deckOpen?354:105):90,portrait?370:268,portrait?(deckOpen?316:67):rh-138,panel);float sw=portrait?370:268;
            Label(side,(app.EditingTeam=="red"?"红方":"蓝方")+" · 战前布阵",14,7,sw-120,28,17,gold);Button(side,portrait?(deckOpen?"收起":"展开"):"撤销",sw-94,8,80,30,()=>{if(portrait){deckOpen=!deckOpen;Build();}else app.Undo();});
            if(portrait&&!deckOpen){Label(side,app.Data.Cost(app.PublicConfig,app.EditingTeam).ToString("0")+" / "+app.PlayerLimit+" 军费",14,36,200,25,12,muted);TouchSwitch();return;}
            var scroll=Scroll(side,10,44,sw-20,portrait?210:rh-277);float y=0,w=sw-42;
            Label(scroll,"军费 "+app.Data.Cost(app.PublicConfig,app.EditingTeam).ToString("0")+" / "+app.PlayerLimit+"  ·  "+app.PublicConfig.Army(app.EditingTeam).Count+" / 60",0,y,w,28,13,muted);y+=34;
            int index=0;foreach(var d in app.Data.Units.Values){int col=index%2,row=index/2;string type=d.id;Button(scroll,d.name+"  "+d.cost, col*(w/2+3),y+row*42,w/2-3,36,()=>app.SelectTool(type),selected:app.UnitType==type&&app.PropType==null&&app.Tool=="place");index++;}y+=5*42+8;
            Label(scroll,"战场道具",0,y,w,28,13,muted);y+=32;index=0;foreach(var d in app.Data.Props.Values){string type=d.id;Button(scroll,d.name+" "+d.cost,index%2*(w/2+3),y+index/2*40,w/2-3,34,()=>app.SelectTool(type,true),selected:app.PropType==type);index++;}y+=3*40+8;
            Label(scroll,"兵种姿态 / 选中 "+app.Selected.Count,0,y,w,28,13,muted);y+=32;var stances=new[]{"推进","坚守","迂回","猎杀"};for(int i=0;i<4;i++){string stance=Catalog.Stances[i];Button(scroll,stances[i],i*(w/4+1),y,w/4-2,36,()=>app.SetStance(stance),selected:app.Stance==stance);}y+=45;
            Button(scroll,"选择",0,y,w/3-3,36,()=>{app.Tool="select";app.Refresh();},selected:app.Tool=="select");Button(scroll,"移动",w/3+1,y,w/3-3,36,()=>{app.Tool="move";app.Refresh();},selected:app.Tool=="move");Button(scroll,"删除",w*2/3+2,y,w/3-3,36,app.DeleteSelection);y+=44;
            Button(scroll,"方阵",0,y,w/3-3,36,()=>app.Arrange("square"));Button(scroll,"横列",w/3+1,y,w/3-3,36,()=>app.Arrange("line"));Button(scroll,"楔形",w*2/3+2,y,w/3-3,36,()=>app.Arrange("wedge"));y+=44;
            Button(scroll,app.Mirror?"镜像布兵：开":"镜像布兵：关",0,y,w,36,()=>{app.Mirror=!app.Mirror;app.Refresh();});y+=48;
            Label(scroll,app.Diagnosis,0,y,w,70,12,gold,TextAnchor.UpperLeft);y+=78;Button(scroll,"预设 / 收藏 / 关卡",0,y,w,40,Presets);y+=50;Label(scroll,"左键点选布兵 · 右键拖动视角\n选择工具支持拖框，再切换移动。",0,y,w,54,11,muted,TextAnchor.UpperLeft);y+=65;scroll.sizeDelta=new Vector2(w,y);
            Button(side,"清空",12,portrait?265:rh-222,(sw-34)*.33f,38,app.ClearTeam);Button(side,StartText(),20+(sw-34)*.33f,portrait?265:rh-222,(sw-34)*.67f-8,38,app.RequestStart,true);
            if(!portrait){Label(baseRoot,app.Data.Maps[app.Config.map].name,310,92,480,42,22,white);Label(baseRoot,app.Data.Maps[app.Config.map].hint??"",310,133,510,40,13,muted,TextAnchor.UpperLeft);Button(baseRoot,"视角 · "+CameraName(),rw-170,rh-84,150,38,()=>{app.View.CameraMode=(app.View.CameraMode+1)%3;app.Refresh();});}
            else {TouchSwitch();Label(baseRoot,app.Diagnosis,14,146,362,50,12,gold,TextAnchor.UpperLeft);}
        }
        string StartText(){if(app.Mode=="hotseat"&&app.Hotseat!="revealed")return app.Hotseat=="red"?"封存红方":app.Hotseat=="handoff"?"蓝方接手":"封存蓝方";return "开 战";}
        string CameraName(){return app.View.CameraMode==0?"全景":app.View.CameraMode==1?"俯瞰":"追踪";}
        void TouchSwitch(){Button(baseRoot,app.TouchCamera?"拖动视角  ⇄  切换点选":"点选布阵 / 军令  ⇄  切换视角",10,98,280,40,()=>{app.TouchCamera=!app.TouchCamera;app.Refresh();},selected:!app.TouchCamera);Button(baseRoot,"视角",300,98,80,40,()=>{app.View.CameraMode=(app.View.CameraMode+1)%3;app.Refresh();});}
        void BattleHud()
        {
            clock=Label(baseRoot,"",portrait?12:22,portrait?145:90,portrait?360:570,42,portrait?19:22,gold);power=Label(baseRoot,"",portrait?12:22,portrait?183:132,portrait?362:600,28,13,muted);if(portrait)TouchSwitch();
            float x=portrait?10:24,y=rh-(portrait?174:137),w=portrait?370:680;var p=Panel(baseRoot,"Battle controls",x,y,w,portrait?136:94,panel);
            var pause=Button(p,app.Paused?"继续":"暂停",10,10,portrait?70:90,38,app.TogglePause);pauseLabel=pause.GetComponentInChildren<Text>();pause.interactable=app.Phase!="result";Button(p,app.Speed+"×",portrait?86:110,10,60,38,()=>{app.Speed=app.Speed==.5f?1:app.Speed==1?2:app.Speed==2?4:.5f;app.Refresh();});Button(p,"回布阵",portrait?152:180,10,80,38,app.ReturnToDeploy);Button(p,app.Muted?"静音":"声音",portrait?238:270,10,60,38,()=>{app.Muted=!app.Muted;app.Refresh();});Button(p,"战报",portrait?304:340,10,56,38,ReportDialog);
            if(app.Phase=="result"){Button(p,"看回放",10,56,100,32,app.Watch,true);if(app.Series!=null&&!app.Series.Complete)Button(p,"下一局",120,56,100,32,app.NextRound);Button(p,"导出回放",230,56,120,32,()=>app.ExportReplay());}
            else if(app.Phase=="battle"&&app.Config.commander.enabled){string[] names={"1 鼓舞","2 集火","3 盾阵"};for(int i=0;i<3;i++){string type=Catalog.Orders[i];Button(p,names[i],10+i*116,56,108,32,()=>{app.OrderType=type;app.Refresh();},selected:app.OrderType==type);}commandStatus=Label(p,"",10,94,w-20,32,12,muted);if(!portrait){commandStatus.rectTransform.anchoredPosition=new Vector2(375,-53);commandStatus.rectTransform.sizeDelta=new Vector2(280,34);}}
            if(app.Phase=="replay"&&app.ReplayFile!=null){var tp=Panel(baseRoot,"Timeline",portrait?10:24,rh-(portrait?222:188),portrait?370:680,40,ink);timeline=Slider(tp,10,9,portrait?350:660,22,app.ReplayFile.result.time,v=>app.Seek(v));foreach(var h in app.ReplayFile.highlights){var captured=h;float hx=10+(portrait?330:640)*h.tick/60f/Mathf.Max(.01f,app.ReplayFile.result.time);var mark=Button(tp,"",hx-9,0,22,40,()=>app.SeekHighlight(captured));mark.GetComponent<Image>().color=new Color(0,0,0,0);Panel(mark.transform,"High point",9,1,3,12,gold);}}
            if(!portrait){Button(baseRoot,"视角 · "+CameraName(),rw-180,92,156,38,()=>{app.View.CameraMode=(app.View.CameraMode+1)%3;app.Refresh();});Button(baseRoot,app.BriefOpen?"收起战况":"战况快报",rw-232,rh-78,210,36,()=>{app.BriefOpen=!app.BriefOpen;app.Refresh();});if(app.BriefOpen)brief=Label(Panel(baseRoot,"Brief",rw-270,rh-300,248,210,panel),"",12,12,224,188,13,muted,TextAnchor.UpperLeft);}
            if(app.Series!=null)Label(baseRoot,"Bo3  "+app.Series.Archive.wins["red"]+" : "+app.Series.Archive.wins["blue"]+"  ·  "+(app.Series.Complete?"系列结束":"第 "+(app.Series.Archive.rounds.Count+(app.Phase=="result"?0:1))+" 局"),portrait?12:650,portrait?214:94,340,30,14,white);
        }
        Slider Slider(Transform parent,float x,float y,float w,float h,float max,Action<float> change)
        {
            var rect=Panel(parent,"Replay seek",x,y,w,h,line);var s=rect.gameObject.AddComponent<Slider>();s.minValue=0;s.maxValue=max;var fill=Panel(rect,"Fill",0,0,w,h,gold);Stretch(fill);s.fillRect=fill;s.targetGraphic=fill.GetComponent<Image>();s.direction=UnityEngine.UI.Slider.Direction.LeftToRight;s.onValueChanged.AddListener(v=>change(v));return s;
        }
        RectTransform Scroll(Transform parent,float x,float y,float w,float h)
        {
            var view=Node(parent,"Scroll viewport");Rect(view,x,y,w,h);view.gameObject.AddComponent<Image>().color=new Color(0,0,0,.01f);view.gameObject.AddComponent<RectMask2D>();var content=Node(view,"Content");Rect(content,0,0,w-12,h);var scroll=view.gameObject.AddComponent<ScrollRect>();scroll.viewport=view;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;return content;
        }
        void OpenDialog(string title,Action<RectTransform,float> builder)
        {
            if(overlay)Destroy(overlay.gameObject);app.Modal=true;dialogTitle=title;dialogBuilder=builder;overlay=Panel(canvas.transform,"Dialog overlay",0,0,rw,rh,new Color(0,0,0,.6f));float w=Mathf.Min(rw-24,880),h=Mathf.Min(rh-40,760);var shell=Panel(overlay,title,(rw-w)/2,(rh-h)/2,w,h,panel);Label(shell,title,20,12,w-110,38,20,gold);Button(shell,"关闭",w-84,14,66,34,CloseDialog);var content=Scroll(shell,18,62,w-36,h-80);builder(content,w-60);
        }
        void CloseDialog(){if(overlay)Destroy(overlay.gameObject);overlay=null;dialogBuilder=null;app.Modal=false;EventSystem.current?.SetSelectedGameObject(null);}
        public void Strategy(){OpenDialog("谋算 · 演武擂台",(p,w)=>{
            float y=0;Label(p,"对局模式",0,y,w,28,15,gold);y+=34;string[] modes={"ai","sandbox","hotseat","auto"},names={"人机","沙盘","热座","AI 观战"};for(int i=0;i<4;i++){string mode=modes[i];Button(p,names[i],i*(w/4),y,w/4-5,40,()=>{CloseDialog();app.SetMode(mode);Strategy();},selected:app.Mode==mode);}y+=54;
            Label(p,"地图",0,y,w,28,15,gold);y+=34;int j=0;foreach(var map in app.Data.Maps.Values){string id=map.id;Button(p,map.name,j%2*(w/2),y+j/2*44,w/2-6,38,()=>{CloseDialog();app.SetMap(id);Strategy();},selected:app.Config.map==id);j++;}y+=3*44+12;
            Label(p,"AI 风格",0,y,w,28,15,gold);y+=34;var styles=new[]{"守正 · 稳健","奇袭 · 机动","知敌 · 克制","算策 · 推演"};for(int i=0;i<4;i++){string style=Catalog.Styles[i];Button(p,styles[i],i%2*(w/2),y+i/2*44,w/2-6,38,()=>{app.Style=style;Strategy();},selected:app.Style==style);}y+=100;
            Label(p,"算策会以多套合法候选快筛，再完整验算前两名；只读取公开阵型。封存的热座对手不参与分析。",0,y,w,76,13,muted,TextAnchor.UpperLeft);y+=84;
            Button(p,"为当前方谋算",0,y,w/2-5,40,()=>{CloseDialog();app.PlanArmy(app.EditingTeam);});Button(p,"演武擂台排行",w/2,y,w/2-5,40,ArenaDialog,true);y+=52;
            Label(p,"随机种子",0,y,w/2-6,28,13,muted);Label(p,"双方军费",w/2,y,w/2-6,28,13,muted);y+=32;InputBox(p,app.Config.seed.ToString(),0,y,w/2-6,38,app.SetSeed);InputBox(p,app.Config.budget.ToString(),w/2,y,w/2-6,38,s=>{if(int.TryParse(s,out int b))app.SetBudget(b);});y+=52;
            Button(p,app.Bo3?"Bo3：开":"Bo3：关",0,y,w/2-5,40,()=>{app.SetBo3(!app.Bo3);Strategy();});Button(p,app.Config.commander.enabled?"统帅军令：开":"统帅军令：关",w/2,y,w/2-5,40,()=>{app.SetCommander(!app.Config.commander.enabled);Strategy();});y+=52;
            if(app.Mode=="sandbox"||app.Mode=="hotseat")Button(p,"当前军令 / 编辑阵营："+(app.Team=="red"?"红方":"蓝方"),0,y,w,40,()=>{app.Team=Catalog.Other(app.Team);app.SetTeam(app.Team);Strategy();});y+=52;Button(p,"兵种、道具与操作说明",0,y,w,40,Help);p.sizeDelta=new Vector2(w,y+54);
        });}
        void Presets(){OpenDialog("预设阵型 · 收藏 · 关卡",(p,w)=>{float y=0;int i=0;var names=new[]{"均衡","冲锋","箭雨","枪盾","火炮","巨人","骑兵","军医","攻城"};foreach(string id in app.Data.Formations.Keys){string preset=id;Button(p,names[i],i%2*w/2,y+i/2*44,w/2-6,38,()=>{CloseDialog();app.Preset(preset);});i++;}y+=5*44+16;Label(p,"本机收藏槽",0,y,w,28,16,gold);y+=34;for(i=0;i<5;i++){int slot=i;Button(p,(i+1)+" "+(app.Favorites[i]?.name??"空"),0,y,w,36,()=>{app.FavoriteSlot=slot;Presets();},selected:app.FavoriteSlot==slot);y+=42;}Button(p,"保存当前方",0,y,w/2-5,40,()=>{app.SaveFavorite();Presets();});Button(p,"载入到当前方",w/2,y,w/2-5,40,()=>{CloseDialog();app.LoadFavorite();});y+=58;Label(p,"战术关卡",0,y,w,28,16,gold);y+=36;for(i=0;i<app.Data.Data.missions.Count;i++){int mission=i;Button(p,app.Data.Data.missions[i].name,0,y,w,40,()=>{CloseDialog();app.Mission(mission);});y+=46;}p.sizeDelta=new Vector2(w,y+12);});}
        public void Workshop(){OpenDialog("工坊 · 文件与回放",(p,w)=>{float y=0;Button(p,"导入 JSON 文件（阵型 / 回放）",0,y,w,44,()=>{CloseDialog();app.ImportFile();},true);y+=54;Button(p,"导出当前阵型",0,y,w,40,app.ExportConfig);y+=48;Button(p,"导出完整轨迹回放",0,y,w,40,()=>app.ExportReplay());y+=48;Button(p,"导出 Unity 种子回放",0,y,w,40,()=>app.ExportReplay(true));y+=48;Button(p,"导出 Bo3 战报",0,y,w,40,app.ExportSeries);y+=54;Label(p,"也可以粘贴阵型 JSON",0,y,w,30,15,gold);y+=34;var input=InputBox(p,"",0,y,w,180,null,true);y+=190;Button(p,"校验并导入文本",0,y,w,40,()=>{app.ImportText(input.text);CloseDialog();});y+=54;Label(p,"0.1–0.5 的完整轨迹可直接播放。旧 Rapier 种子包先运行随工程提供的转换工具。Unity 种子包要求同引擎、同平台复现；跨平台请交换完整轨迹。",0,y,w,110,13,muted,TextAnchor.UpperLeft);y+=120;Button(p,"操作说明 / 外部 Bot",0,y,w,40,Help);p.sizeDelta=new Vector2(w,y+54);});}
        void ArenaDialog(){if(arenaMaps.Count==0)arenaMaps.Add(app.Config.map);OpenDialog("演武擂台 · 双循环赛",(p,w)=>{float y=0;Label(p,"选择 2–8 方参赛；每对阵型交换红蓝出场。",0,y,w,44,14,gold);y+=50;for(int i=0;i<4;i++){int index=i;Button(p,(arenaStyles[i]?"[已选] ":"[未选] ")+new[]{"守正","奇袭","知敌","算策"}[i],0,y,w,38,()=>{arenaStyles[index]=!arenaStyles[index];ArenaDialog();});y+=44;}for(int i=0;i<5;i++)if(app.Favorites[i]!=null){int index=i;Button(p,(arenaFavorites.Contains(i)?"[已选] ":"[未选] ")+"收藏 "+(i+1)+" · "+app.Favorites[i].map,0,y,w,38,()=>{if(!arenaFavorites.Add(index))arenaFavorites.Remove(index);ArenaDialog();});y+=44;}for(int i=0;i<app.ImportedEntrants.Count;i++){int index=i;Button(p,(arenaImports.Contains(i)?"[已选] ":"[未选] ")+app.ImportedEntrants[i].name,0,y,w,38,()=>{if(!arenaImports.Add(index))arenaImports.Remove(index);ArenaDialog();});y+=44;}Button(p,"导入参赛阵型 JSON",0,y,w,40,()=>FileBridge.Import(s=>app.Try(()=>{app.AddArenaFormation(s);arenaImports.Add(app.ImportedEntrants.Count-1);ArenaDialog();})));y+=56;
            Label(p,"地图集合（本地阵型需与地图一致）",0,y,w,32,14,gold);y+=38;foreach(var map in app.Data.Maps.Values){string id=map.id;Button(p,(arenaMaps.Contains(id)?"[已选] ":"[未选] ")+map.name,0,y,w,36,()=>{if(!arenaMaps.Add(id))arenaMaps.Remove(id);ArenaDialog();});y+=42;}
            Label(p,"种子列表，逗号分隔，最多 10 个",0,y,w,30,13,muted);y+=34;var seeds=InputBox(p,seedText,0,y,w,38,s=>seedText=s);y+=50;Button(p,"开始循环赛",0,y,w,44,()=>{seedText=seeds.text;var entrants=new List<Entrant>();for(int i=0;i<4;i++)if(arenaStyles[i])entrants.Add(new Entrant{id=Catalog.Styles[i],name=new[]{"守正","奇袭","知敌","算策"}[i],ai=Catalog.Styles[i]});foreach(int index in arenaFavorites.OrderBy(x=>x)){var f=app.Favorites[index];entrants.Add(new Entrant{id="fav-"+index,name="收藏 "+(index+1),map=f.map,team=f.team,units=f.units,props=f.props});}foreach(int index in arenaImports.OrderBy(x=>x))entrants.Add(app.ImportedEntrants[index]);var values=seedText.Split(',').Select(s=>uint.Parse(s.Trim())).ToList();app.Arena(entrants,app.Data.Maps.Keys.Where(arenaMaps.Contains).ToList(),values);CloseDialog();},true);y+=56;Button(p,"查看最近排名 / 胜率矩阵",0,y,w,40,ArenaResults);p.sizeDelta=new Vector2(w,y+54);});}
        void ArenaResults(){OpenDialog("演武擂台 · 排名与矩阵",(p,w)=>{if(app.ArenaReport==null){Label(p,"完成循环赛后显示结果。",0,0,w,50,15,muted);p.sizeDelta=new Vector2(w,70);return;}float y=0;Label(p,"名次  参赛方  ·  胜/平/负  ·  胜率  ·  残余战力 / 用时 / 稳定 MVP",0,y,w,56,13,gold);y+=62;int i=1;foreach(var r in app.ArenaReport.ranking){Label(p,i+++". "+r.name+"  "+r.wins+" / "+r.draws+" / "+r.losses+"   "+r.winRate.ToString("P0")+"\n残余 "+r.averageRemaining.ToString("0.0")+" · "+r.averageTime.ToString("0.0")+"s · "+(r.stableMvp==null?"—":app.Data.Units[r.stableMvp].name),0,y,w,66,14,white);y+=74;}Label(p,"交锋矩阵 · 每行从左方视角统计",0,y,w,38,16,gold);y+=44;for(i=0;i<app.ArenaReport.entrants.Count;i++)for(int j=i+1;j<app.ArenaReport.entrants.Count;j++){var r=app.ArenaReport.matrix[i][j];Label(p,app.ArenaReport.entrants[i].name+" → "+app.ArenaReport.entrants[j].name+"："+r.wins+" 胜 / "+r.draws+" 平 / "+r.losses+" 负",0,y,w,42,14,muted);y+=46;}Button(p,"导出擂台报告 JSON",0,y,w,42,app.ExportArena,true);p.sizeDelta=new Vector2(w,y+56);});}
        public void ReportDialog(){OpenDialog("战果 · 贡献与关键战况",(p,w)=>{float y=0;var result=app.ReplayFile?.result??app.Battle?.Result;var report=result?.report??app.Battle?.Report();if(report==null){Label(p,"开战后查看贡献。",0,0,w,44,14,muted);return;}Label(p,(result==null?"实时战报":result.winner=="draw"?"平局":(result.winner=="red"?"红方":"蓝方")+"获胜")+"  ·  "+(result?.time??app.Battle.Time).ToString("0.0")+"s",0,y,w,38,20,gold);y+=46;if(report.mvp!=null){Label(p,"MVP · "+app.Data.Units[report.mvp.type].name+"   贡献 "+report.mvp.score.ToString("0"),0,y,w,36,15,white);y+=44;}foreach(var team in new[]{"red","blue"}){var r=report.teams[team];Label(p,(team=="red"?"红方":"蓝方")+" · 存活 "+r.survived+" / "+r.initial+" · 残余战力 "+r.remainingValue.ToString("0"),0,y,w,34,16,gold);y+=40;foreach(var row in r.byType){var d=row.Value;Label(p,app.Data.Units[row.Key].name+"  "+d.survived+" / "+d.initial+"  伤害 "+d.damageDealt.ToString("0")+"  治疗 "+d.healing.ToString("0")+"  误伤 "+d.friendlyFire.ToString("0")+"  击杀 "+d.kills,0,y,w,46,13,muted);y+=50;}y+=16;}Label(p,"高光时刻（可点击跳转）",0,y,w,32,16,gold);y+=40;foreach(var h in app.Highlights){var captured=h;Button(p,Replay.HighlightText(h),0,y,w,40,()=>{CloseDialog();app.SeekHighlight(captured);});y+=46;}p.sizeDelta=new Vector2(w,y+16);});}
        void Help(){OpenDialog("阵线手册",(p,w)=>{string text="布阵\n选择兵种后点击己方半场放置。右键拖动视角、滚轮缩放；选择工具拖框，多选后切到移动工具点选新中心。Ctrl/Cmd+Z 撤销，Delete 删除所选。\n\n对战\nSpace 暂停或继续；Tab 依次切换全景、俯瞰、追踪。军令开启时，1 / 2 / 3 选择鼓舞、集火、盾阵，点击地面释放。每方 3 点，冷却 8 秒。输入框或弹窗打开时会屏蔽快捷键。\n\n触屏\n“拖动视角”用于单手转动镜头，切到“点选布阵 / 军令”后才接受落点。超过拖动阈值、双指缩放、取消触摸均不会放兵。\n\n兵种\n游勇：便宜近战；盾卫：正面减伤；长枪：克制骑兵；弓手：高台射程；弩手：穿盾；爆破手：范围误伤；巨人：击退；骑兵：冲锋；军医：治疗；投石车：远程范围打击。\n\n规则\n每局最多 90 秒，按残余战力判定。Bo3 率先两胜或打满三局；小局间可调整布阵。热座封存阶段只显示己方。道具也计入军费。\n\n外部智能体\nWindows 版可用命令行启动两个 stdio 子进程 Bot。每 60 步接收公开观察，合法军令才入战斗；网页平台可导入 Bot 对战回放。详见随包 docs/UNITY_MIGRATION.md 和 AGENT_PROTOCOL.md。\n\n兼容\n原有布阵可导入。旧版本完整轨迹回放直接播放；旧 Rapier 种子回放用 Node 转换工具生成完整轨迹。Unity 版本使用原生 PhysX，跨平台复盘以完整轨迹为准。";Label(p,text,0,0,w,1180,14,white,TextAnchor.UpperLeft);p.sizeDelta=new Vector2(w,1200);});}
        void Update()
        {
            if(!app||app.Config==null)return;if(width!=Screen.width||height!=Screen.height){Build();return;}if(status)status.text=app.Message;if(progress)progress.text=app.Progress??"准备计算";var f=app.CurrentFrame;
            if(clock&&f!=null){int red=f.units.Count(u=>u.alive&&u.team=="red"),blue=f.units.Count(u=>u.alive&&u.team=="blue");clock.text=(app.Phase=="replay"?"回放 ":"")+f.time.ToString("0.0")+"s   红 "+red+" : "+blue+" 蓝";}if(power&&f!=null){float r=0,b=0;foreach(var u in f.units)if(u.alive){float v=u.hp/app.Data.Units[u.type].hp*app.Data.Units[u.type].cost;if(u.team=="red")r+=v;else b+=v;}power.text="实时战力 · 红 "+r.ToString("0")+" / 蓝 "+b.ToString("0");}if(commandStatus&&f!=null){var state=f.commander;commandStatus.text=(app.Team=="red"?"红方":"蓝方")+" · 军令 "+state.points[app.Team]+" / 3 · "+Mathf.Max(0,(state.readyAt[app.Team]-f.tick)/60f).ToString("0.0")+"s 冷却";}if(pauseLabel)pauseLabel.text=app.Paused?"继续":"暂停";if(timeline)timeline.SetValueWithoutNotify(app.ReplayTime);if(brief)brief.text=string.Join("\n\n",app.Highlights.Skip(Math.Max(0,app.Highlights.Count-5)).Select(Replay.HighlightText));
        }
    }
}







