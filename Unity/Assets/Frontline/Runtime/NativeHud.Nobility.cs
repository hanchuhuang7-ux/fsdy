using Frontline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Frontline.UnityGame
{
    public sealed partial class NativeHud
    {
        static string NobleGrantText(int rank)
        {
            int end=Manor.GrantedPlotCount(rank);
            return rank==0?"原领地 "+end+" 块":"赐地 "+(end-Manor.NoblePlotsPerRank)+"–"+(end-1)+"（+"+Manor.NoblePlotsPerRank+" 块）";
        }

        static string NoblePrice(ManorNobleRank rank) => rank.coin+" 金币 · "+rank.influence+" 威望";

        static string NobleState(Manor estate,int rank) =>
            rank==estate.nobility?"当前爵位":rank<estate.nobility?"已受封":
            estate.NoblePromotionBlock(rank)==null?"可受封":"未受封";

        void NobilityDialog() => OpenDialog("贵族头衔与赐土",BuildNobility,maxHeight:726,maxWidth:680,footer:NobilityFooter);

        void BuildNobility(RectTransform p,float w)
        {
            var title=Label(p,"",0,0,w,32,18,gold);
            title.name="Manor.Nobility.Current";
            var progress=Label(p,"",0,32,w,39,portrait?11:13,pale);
            manorDialogValues.Add(()=>{
                var estate=app.Estate;
                title.text=estate.NobleTitle+"  ·  已封授 "+estate.GrantedPlots+" / "+Manor.PlotCount+" 块建地";
                progress.text="繁荣曾达 "+estate.BestProsperityTitle+"  ·  远征 "+estate.cleared+"/"+Manor.Route.Length+
                    "  ·  筑哨 "+estate.ClaimedSectorCount+"/"+Manor.Sectors.Count+
                    "\n威望 "+estate.influence+"  ·  金币 "+estate.coin;
            });
            float y=78;
            for(int rank=0;rank<Manor.NobleRanks.Count;rank++)
            {
                int index=rank;
                var entry=Manor.NobleRanks[rank];
                var row=Panel(p,"Nobility "+rank,0,y,w,0,manorInset,Slice.Inset);
                Label(row,entry.title+"  ·  "+NobleGrantText(rank),10,3,w-100,27,portrait?13:16,gold);
                var state=Badge(row,"",w-82,6,74,21,11);
                state.name="Manor.Nobility.State."+rank;
                var background=state.transform.parent.GetComponent<Image>();
                manorDialogValues.Add(()=>{
                    var estate=app.Estate;
                    BadgeLook(state,background,NobleState(estate,index),good:index<estate.nobility,
                        active:index==estate.nobility||estate.NoblePromotionBlock(index)==null);
                });
                float bottom=30;
                bottom+=ReadingLine(row,Manor.NobleRequirement(rank)+(rank==0?"":"  ·  "+NoblePrice(entry)),10,bottom,w-20,portrait?11:13,pale);
                bottom+=ReadingLine(row,Manor.NoblePrivilege(rank),10,bottom+2,w-20,portrait?11:13,white)+2;
                row.sizeDelta=new Vector2(w,bottom+9);
                y+=bottom+15;
            }
            p.sizeDelta=new Vector2(w,y);
        }

        void NobilityFooter(RectTransform p,float w)
        {
            ManorButton(p,"Manor.Nobility.Back","政务",4,3,portrait?50:65,40,GovernanceDialog);
            float left=portrait?58:75,action=portrait?97:126;
            var guidance=Label(p,"",left,0,w-action-left-12,46,portrait?10:12,pale);
            var promote=ManorButton(p,"Manor.Nobility.Promote","受封",w-action-4,3,action,40,()=>{
                int next=app.Estate.nobility+1;
                if(next<Manor.NobleRanks.Count)app.PromoteManorNobility(next);
            },true);
            var caption=promote.GetComponentInChildren<Text>();
            manorDialogValues.Add(()=>{
                var estate=app.Estate;
                int next=estate.nobility+1;
                if(next>=Manor.NobleRanks.Count)
                {
                    caption.text="最高爵位";
                    guidance.text="全部封地已开放";
                    promote.interactable=false;
                    return;
                }
                caption.text="受封"+Manor.NobleRanks[next].title;
                string blocked=estate.NoblePromotionBlock(next);
                guidance.text=blocked??NoblePrice(Manor.NobleRanks[next]);
                promote.interactable=blocked==null;
            });
        }
    }
}
