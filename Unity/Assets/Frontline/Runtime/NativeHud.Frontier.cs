using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        bool builtFrontier;
        readonly List<Action> frontierValues=new List<Action>();
        void SyncCameraBindings()
        {
            if(builtFrontier&&app.InFrontier||builtInBase&&app.Phase=="manor")RefreshBindings(frontierValues);
        }
        void BindFrontier(Button button,Func<bool> selected=null,Func<string> caption=null,bool allowed=true,bool navigation=false)
        {
            var label=button.GetComponentInChildren<Text>();
            void RefreshControl()
            {
                button.interactable=allowed&&(navigation||app.Battle.Result==null);
                if(selected!=null)SetButtonTone(button,selected()?HudTone.Selected:HudTone.Secondary);
                if(caption!=null){var value=caption();if(label.text!=value)label.text=value;}
            }
            RefreshControl();frontierValues.Add(RefreshControl);
        }
        void FrontierCameraBar(bool estate)
        {
            const float w=217;
            float x=portrait?(rw-w)*.5f:rw-w-15;
            float y=portrait?(estate?106:51):63;
            var bar=Float(baseRoot,"Frontier camera",x,y,w,35,Slice.Rail);
            string[] names={"全景","湖心","外域"};
            float cell=(w-10)/3;
            for(int i=0;i<names.Length;i++)
            {
                int focus=i;
                bool shown=app.View.FrontierFocus==focus;
                var button=ManorButton(bar,"Frontier.View."+focus,names[i],5+cell*i,4,cell-4,27,
                    ()=>app.View.FrameFrontier(focus,true),selected:shown);
                frontierValues.Add(()=>SetButtonTone(button,app.View.FrontierFocus==focus?HudTone.Selected:HudTone.Secondary));
            }
        }

        void FrontierHud()
        {
            FrontierCameraBar(false);
            float x=portrait?8:20;
            float w=portrait?374:848;
            float y=portrait?96:64;
            float readoutW=portrait?w:310;
            var readout=Float(baseRoot,"Frontier readout",x,y,readoutW,56,Slice.Rail);
            readout.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            clock=Label(readout,"",10,2,readoutW-(portrait?122:17),26,16,gold);
            power=Label(readout,"",10,27,readoutW-20,25,12,pale);
            if(portrait)
            {
                var touch=ManorButton(readout,"Frontier.Touch","切到拖视角",w-107,3,101,24,
                    ()=>{app.TouchCamera=!app.TouchCamera;app.Refresh();});
                BindFrontier(touch,()=>app.TouchCamera,()=>app.TouchCamera?"切到点选":"切到拖视角",navigation:true);
            }
            float dockTop=rh-(portrait?127:84);
            var controls=Float(baseRoot,"Frontier controls",portrait?x:(rw-w)/2,dockTop,w,portrait?115:70,Slice.Rail);
            float part=(w-16)/(portrait?4:8);
            float orderX=portrait?8:8+part*4,orderY=portrait?48:7;
            var advance=ManorButton(controls,"Frontier.Advance","推进",8,7,part-4,35,()=>{
                app.Tool="march";app.Tell("点选地面推进，遇敌自动接战");app.Refresh();
            });
            BindFrontier(advance,()=>app.Tool=="march");
            BindFrontier(ManorButton(controls,"Frontier.Recall","撤回湖心",8+part,7,part-4,35,app.RecallFrontier));
            BindFrontier(ManorButton(controls,"Frontier.Pause","暂停",8+part*2,7,part-4,35,app.TogglePause),caption:()=>app.Paused?"继续":"暂停");
            ManorButton(controls,"Frontier.Settle","结算归营",8+part*3,7,part-4,35,app.ReturnManor,true);

            string[] orders={"1 鼓舞","2 集火","3 盾阵"};
            for(int i=0;i<orders.Length;i++)
            {
                string type=Catalog.Orders[i];
                bool allowed=app.AllowsOrder(type);
                string caption=orders[i]+(allowed?"":" Lv."+Manor.OrderLevel(type));
                var order=ManorButton(controls,"Frontier.Order."+type,caption,orderX+part*i,orderY,part-4,portrait?32:35,
                    ()=>app.ChooseOrder(type),selected:app.Tool=="order"&&app.OrderType==type);
                BindFrontier(order,()=>app.Tool=="order"&&app.OrderType==type,allowed:allowed);
                if(!allowed)order.GetComponentInChildren<UnityEngine.UI.Text>().fontSize=11;
            }
            var speed=ManorButton(controls,"Frontier.Speed","1×",orderX+part*3,orderY,part-4,portrait?32:35,
                ()=>{app.Speed=app.Speed==1?2:app.Speed==2?4:1;app.Refresh();});
            BindFrontier(speed,caption:()=>app.Speed==1?"1×":app.Speed==2?"2×":"4×");
            commandStatus=Label(controls,"",11,portrait?82:46,portrait?w-22:241,24,12,pale);
            BuildToast(false,dockTop-40);
        }
    }
}
