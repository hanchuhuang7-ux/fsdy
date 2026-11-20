using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        void MilestonesDialog()
        {
            OpenDialog("领主宏愿",BuildMilestones,680,(bar,w)=>
                ManorButton(bar,"Manor.Milestones.Back","返回政务",0,3,w,40,GovernanceDialog),maxWidth:690,
                header:(p,w)=>ManorButton(p,"Manor.Merit.Open","领主勋绩总览",0,0,w,34,MeritDialog,true));
        }

        void MeritDialog()
        {
            OpenDialog("领主勋绩总览",BuildMerit,680,(bar,w)=>
                ManorButton(bar,"Manor.Merit.Back","返回领主宏愿",0,3,w,40,MilestonesDialog),maxWidth:710,wholeRows:true);
        }

        void BuildMerit(RectTransform p,float w)
        {
            var summary=Panel(p,"Merit overview",0,0,w,105,manorInset,Slice.Inset);
            var title=Label(summary,"",12,7,w-112,37,portrait?20:25,gold);
            title.name="Manor.Merit.Title";
            var percent=Label(summary,"",w-93,8,81,36,20,gold,TextAnchor.MiddleRight);
            percent.name="Manor.Merit.Percent";
            var (track,fill)=MeritBar(summary,12,50,w-24,10);
            var subtotal=Label(summary,"",12,68,w-24,25,portrait?12:14,pale);
            string[] categories={"贵族爵位","外域疆域","领主宏愿","领地专才","英雄武装","领地远征"};
            string[] details={"已解锁阶位","已宣称","已领取","已培养至满级","已锻造至满阶","已通关"};
            int[] limits={Manor.NobleRanks.Count,Manor.Sectors.Count,Manor.Milestones.Count,
                Manor.SpecialistIds.Length,Manor.HeroIds.Length,Manor.Route.Length};
            var values=new Text[categories.Length];
            var fills=new RectTransform[categories.Length];
            float barWidth=portrait?w*.24f:w*.38f;
            for(int i=0;i<categories.Length;i++)
            {
                var row=Panel(p,"Merit "+i,0,114+i*54,w,49,manorInset,Slice.Inset);
                Label(row,categories[i],11,1,w-barWidth-95,26,portrait?14:16,white);
                Label(row,details[i],11,27,w-barWidth-95,19,11,pale);
                values[i]=Label(row,"",w-63,5,52,34,15,gold,TextAnchor.MiddleRight);
                values[i].name="Manor.Merit.Row."+i;
                (_,fills[i])=MeritBar(row,w-barWidth-78,18,barWidth,7);
            }
            int revision=-1;
            manorDialogValues.Add(()=>{
                if(revision==app.ManorRevision)return;
                revision=app.ManorRevision;
                var progress=app.Estate.MeritOverview();
                title.text=progress.Title;
                percent.text=progress.Percent+"%";
                subtotal.text="综合勋绩  "+progress.Completed+" / "+ManorMerit.Total;
                fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,track.sizeDelta.x*progress.Completed/ManorMerit.Total);
                for(int i=0;i<values.Length;i++)
                {
                    int count=i switch {0=>progress.ranks,1=>progress.sectors,2=>progress.milestones,
                        3=>progress.specialists,4=>progress.armaments,_=>progress.expeditions};
                    values[i].text=count+" / "+limits[i];
                    values[i].color=count==limits[i]?thriving:gold;
                    fills[i].SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,barWidth*count/limits[i]);
                }
            });
            p.sizeDelta=new Vector2(w,114+categories.Length*54);
        }

        (RectTransform track,RectTransform fill) MeritBar(RectTransform parent,float x,float y,float width,float height)
        {
            var track=Panel(parent,"Merit track",x,y,width,height,ink,Slice.Badge);
            var fill=Panel(track,"Merit fill",0,0,0,height,gold,Slice.Badge);
            return (track,fill);
        }

        void BuildMilestones(RectTransform p,float w)
        {
            var buttons=new Button[Manor.Milestones.Count];
            var captions=new Text[buttons.Length];
            var badges=new Text[buttons.Length];
            var badgeBackings=new Image[buttons.Length];
            float h=portrait?82:72, actionWidth=portrait?69:93;
            for(int i=0;i<buttons.Length;i++)
            {
                int index=i;
                var entry=Manor.Milestones[i];
                var row=Panel(p,entry.title,0,i*(h+5),w,h,manorInset,Slice.Inset);
                float detailWidth=w-actionWidth-22;
                Label(row,entry.title,9,1,detailWidth,29,portrait?14:16,gold);
                Label(row,entry.condition,9,28,detailWidth,portrait?30:22,portrait?11:13,pale);
                Label(row,MilestoneRewards(entry),9,portrait?56:50,detailWidth,portrait?24:19,portrait?10:12,thriving);
                var button=buttons[i]=ManorButton(row,"Manor.Milestone."+i,"领取",w-actionWidth-7,h-34,actionWidth,27,()=>
                    app.ClaimManorMilestone(index),true);
                captions[i]=button.GetComponentInChildren<Text>();
                badges[i]=Badge(row,"",w-actionWidth-7,6,actionWidth,23,portrait?11:12);
                badgeBackings[i]=badges[i].transform.parent.GetComponent<Image>();
            }
            p.sizeDelta=new Vector2(w,buttons.Length*(h+5)-5);

            int revision=-1;
            manorDialogValues.Add(()=>{
                if(revision==app.ManorRevision)return;
                revision=app.ManorRevision;
                var estate=app.Estate;
                int ready=estate.ReadyMilestones;
                for(int i=0;i<buttons.Length;i++)
                {
                    bool claimed=estate.MilestoneClaimed(i),available=(ready&(1<<i))!=0;
                    buttons[i].interactable=available;
                    captions[i].text=claimed?"已领取":"领取";
                    BadgeLook(badges[i],badgeBackings[i],claimed?"已完成":available?"可领取":"进行中",good:claimed,active:available);
                }
            });
        }

        static string MilestoneRewards(ManorMilestone entry)
        {
            var rewards=new List<string>(5);
            if(entry.coin>0)rewards.Add("金币 "+entry.coin);
            if(entry.food>0)rewards.Add("粮食 "+entry.food);
            if(entry.timber>0)rewards.Add("建材 "+entry.timber);
            if(entry.arms>0)rewards.Add("军械 "+entry.arms);
            if(entry.approval>0)rewards.Add("民心 +"+entry.approval);
            return string.Join(" · ",rewards);
        }
    }
}
