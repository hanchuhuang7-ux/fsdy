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
                ManorButton(bar,"Manor.Milestones.Back","返回政务",0,3,w,40,GovernanceDialog),maxWidth:690);
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
