using System;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        void EventEntry(Transform parent,string id,float x,float y,float w,float h,List<Action> changes,bool fromGovernance=false)
        {
            var button=ManorButton(parent,id,w<65?"急报":"领地急报",x,y,w,h,()=>EventDialog(fromGovernance),true);
            changes.Add(()=>{
                button.gameObject.SetActive(app.Estate.eventDay!=0);
                if(fromGovernance)SetTodoLook(button,app.Estate.CanResolveDispatch);
            });
        }

        void EventDialog(bool fromGovernance)
        {
            var dispatch=app.Estate.ActiveEvent;
            if(dispatch==null)return;
            int issueDay=app.Estate.eventDay;
            OpenDialog("领地急报 · "+dispatch.title,(p,w)=>{
                Label(p,"第 "+issueDay+" 日送达",0,0,w,22,12,gold);
                Label(p,dispatch.detail,0,24,w,portrait?49:32,15,white,TextAnchor.UpperLeft);
                float y=portrait?78:61;
                EventOption(p,w,y,issueDay,0,dispatch.first,fromGovernance);
                EventOption(p,w,y+78,issueDay,1,dispatch.second,fromGovernance);
                string note="跨季自动采用「"+dispatch.second.title+"」";
                if(dispatch.second.food<0)note+="，余粮不足时跳过赠粮";
                Label(p,note,0,y+156,w,32,12,pale,TextAnchor.UpperLeft);
                p.sizeDelta=new Vector2(w,y+187);
            },480,maxWidth:630);
        }

        void EventOption(RectTransform p,float w,float y,int issueDay,int index,ManorEventChoice choice,bool fromGovernance)
        {
            var row=Panel(p,"Event option "+index,0,y,w,72,manorInset,Slice.Inset);
            Label(row,choice.title,8,2,w-103,23,15,white);
            var effects=Label(row,"",8,26,w-103,23,12,pale);
            var blocked=Label(row,"",8,48,w-103,21,12,danger);
            var state=Badge(row,"",w-86,4,78,21);
            var badge=state.transform.parent.GetComponent<Image>();
            var button=ManorButton(row,"Manor.Event.Choose."+index,"选择",w-86,28,78,35,()=>{
                var estate=app.Estate;
                app.ResolveManorEvent(issueDay,index);
                if(estate==app.Estate&&estate.eventDay!=0)return;
                if(fromGovernance)GovernanceDialog();
                else CloseDialog();
            },index==0);
            manorDialogValues.Add(()=>{
                var m=app.Estate;
                string reason=m.EventChoiceBlock(issueDay,index);
                effects.text=EventEffects(m,choice);
                blocked.text=reason??"";
                BadgeLook(state,badge,reason==null?"可处理":"暂不可选",active:reason==null);
                button.interactable=reason==null;
            });
        }

        static string EventEffects(Manor m,ManorEventChoice choice)
        {
            var effects=new List<string>(4);
            if(choice.coin!=0)effects.Add("金币 "+Change(choice.coin));
            if(choice.food!=0)effects.Add("粮食 "+Change(choice.food));
            if(choice.timber!=0)effects.Add("建材 "+Change(choice.timber));
            if(choice.arms!=0)effects.Add("军械 "+Change(choice.arms));
            if(choice.workers!=0)effects.Add("领民 "+Change(choice.workers)+" 户");
            if(choice.approval!=0)effects.Add("民心 "+Change(Mathf.Clamp(m.approval+choice.approval,0,100)-m.approval));
            return effects.Count==0?"资源与民心照常":string.Join(" · ",effects);
        }
    }
}
