using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        static string EstateSynergyText(ManorSynergy link,bool compact=false)
        {
            var parts=new List<string>(6);
            if(link.cropPercent>0)parts.Add((compact?"耕牧 +":"耕牧互补 +")+link.cropPercent+"%");
            if(link.grainPercent>0)parts.Add((compact?"谷仓 +":"谷仓产粮 +")+link.grainPercent+"%");
            if(link.forgePercent>0)parts.Add((compact?"林铁 +":"林铁联动 +")+link.forgePercent+"%");
            if(link.settledApproval>0)parts.Add((compact?"安居 +":"安居民心 +")+link.settledApproval);
            if(link.breadPercent>0)parts.Add((compact?"麦香 +":"麦香联动 +")+link.breadPercent+"%");
            if(link.weavePercent>0)parts.Add((compact?"牧织 +":"牧织联动 +")+link.weavePercent+"%");
            return string.Join(" · ",parts);
        }

        static string PlotSynergyText(string kind,ManorSynergy link)
        {
            switch(kind)
            {
                case "farm":case "irrigated_field":return link.cropPercent>0?" · 耕牧 +"+link.cropPercent+"%":"";
                case "timber_camp":case "smithy":return link.forgePercent>0?" · 林铁 +"+link.forgePercent+"%":"";
                case "chapel":case "tavern":case "cottage":return link.settledApproval>0?" · 安居民心 +"+link.settledApproval:"";
                case Manor.Bakery:return link.breadPercent>0?" · 麦香联动 +"+link.breadPercent+"%":"";
                case Manor.Weaver:return link.weavePercent>0?" · 牧织联动 +"+link.weavePercent+"%":"";
                default:return "";
            }
        }

        void SpecialistsDialog()
        {
            OpenDialog("领地专才",(p,w)=>{
                float y=0;
                foreach(string id in Manor.SpecialistIds)
                {
                    var row=Panel(p,id,0,y,w,portrait?105:84,manorInset);
                    var title=Label(row,"",10,3,w-98,26,16,gold);
                    title.name="Manor.Specialist.Level."+id;
                    var effect=Label(row,"",10,31,w-20,portrait?36:24,12,pale,TextAnchor.UpperLeft);
                    var cost=Label(row,"",10,portrait?72:56,w-20,25,12,white,TextAnchor.UpperLeft);
                    var upgrade=ManorButton(row,"Manor.Specialist.Upgrade."+id,"升级",w-79,4,69,28,()=>app.UpgradeSpecialist(id),true);
                    var caption=upgrade.GetComponentInChildren<Text>();
                    manorDialogValues.Add(()=>{
                        var estate=app.Estate;
                        int rank=estate.SpecialistRank(id),level=estate.SpecialistLevel(id);
                        string blocked=estate.SpecialistUpgradeBlock(id);
                        title.text=Manor.SpecialistName(id)+(level>0?" · Lv."+level:rank>1?" · 暂离 Lv."+rank:" · 未解锁");
                        effect.text=Manor.SpecialistEffect(id,rank);
                        effect.color=level>0?pale:muted;
                        cost.text=level==0?estate.SpecialistUnlockBlock(id):rank>=Manor.SpecialistMaxLevel?"已达最高等级":
                            estate.SpecialistUpgradePrice(id)+(blocked==null?"":" · "+blocked);
                        cost.color=blocked!=null&&rank<Manor.SpecialistMaxLevel?danger:white;
                        caption.text=rank>=Manor.SpecialistMaxLevel?"满级":"升 Lv."+(rank+1);
                        upgrade.interactable=blocked==null;
                    });
                    y+=row.sizeDelta.y+7;
                }
                p.sizeDelta=new Vector2(w,y);
            },720,(bar,w)=>ManorButton(bar,"Manor.Specialists.Back","返回政务",0,3,w,40,GovernanceDialog),maxWidth:650,wholeRows:true);
        }
    }
}
