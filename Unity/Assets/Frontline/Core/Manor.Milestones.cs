using System;
using System.Collections.Generic;

namespace Frontline.Core
{
    public readonly struct ManorMilestone
    {
        public readonly string title, condition;
        public readonly int coin, food, timber, arms, approval;

        internal ManorMilestone(string title,string condition,int coin=0,int food=0,int timber=0,int arms=0,int approval=0)
        {
            this.title=title; this.condition=condition;
            this.coin=coin; this.food=food; this.timber=timber; this.arms=arms; this.approval=approval;
        }
    }

    public sealed partial class Manor
    {
        public const int ReclaimPrice = 8, ReclaimTimber = 4;
        const int OuterPlotMask = (1 << (PlotCount - FirstOuterPlot)) - 1;
        public int reclaimedPlots, milestoneClaims;
        public bool frontierSwept;

        public static IReadOnlyList<ManorMilestone> Milestones { get; } = Array.AsReadOnly(new[] {
            new ManorMilestone("开荒拓土","开垦至少 2 块外环建地",coin:20,timber:12),
            new ManorMilestone("安居乐业","达到 8 户且民心至少 75",coin:24,food:18),
            new ManorMilestone("五谷丰登","激活耕牧互补并派工谷仓",coin:20,food:26),
            new ManorMilestone("百业兴旺","激活林铁联动并解锁全部 4 位专才",timber:20,arms:14),
            new ManorMilestone("边疆雄镇","达成边疆雄镇繁荣称号",coin:40,arms:16,approval:4),
            new ManorMilestone("平定四方","通关全部 6 场远征，或一次巡边击退全部 15 名守军",
                coin:70,food:30,timber:30,arms:24,approval:8)
        });
        static readonly int MilestoneMask = (1 << Milestones.Count) - 1;

        public bool IsPlotCleared(int index) =>
            (uint)index < PlotCount && (index < FirstOuterPlot || (reclaimedPlots & (1 << (index - FirstOuterPlot))) != 0);

        public string ReclaimBlock(int index)
        {
            if (index < FirstOuterPlot || index >= PlotCount || IsPlotCleared(index)) return "此处无需开垦";
            return coin < ReclaimPrice ? "金币不足" : null;
        }

        public void ReclaimPlot(int index)
        {
            string blocked=ReclaimBlock(index);
            if (blocked != null) throw new InvalidOperationException(blocked);
            coin -= ReclaimPrice;
            Credit(ref timber,ref remainder.timber,ReclaimTimber * 100);
            reclaimedPlots |= 1 << (index - FirstOuterPlot);
            Remember("reclaim",index);
        }

        bool TwoOuterPlotsCleared => (reclaimedPlots & (reclaimedPlots - 1)) != 0;
        bool FrontiersSecured => cleared == Route.Length || frontierSwept;

        public int ReadyMilestones
        {
            get
            {
                if (milestoneClaims == MilestoneMask) return 0;
                int ready=0;
                if (TwoOuterPlotsCleared) ready|=1<<0;
                if (workers>=8 && approval>=FlourishingApproval) ready|=1<<1;
                var synergy=ReadSynergies(out bool hasClinic);
                if (synergy.cropPercent>0 && synergy.granaryLevel>0) ready|=1<<2;
                if (synergy.forgePercent>0 && MerchantUnlocked && hasClinic &&
                    (HasBuilding("drill_yard") || HasBuilding("barracks"))) ready|=1<<3;
                if (ProsperityTier==3) ready|=1<<4;
                if (FrontiersSecured) ready|=1<<5;
                return ready & ~milestoneClaims;
            }
        }

        public bool MilestoneClaimed(int index) =>
            (uint)index < Milestones.Count && (milestoneClaims & (1 << index)) != 0;

        public void ClaimMilestone(int index)
        {
            if ((uint)index >= Milestones.Count || (ReadyMilestones & (1 << index)) == 0)
                throw new InvalidOperationException("宏愿尚未达成或奖励已领取");
            var reward=Milestones[index];
            Credit(ref coin,ref remainder.coin,reward.coin*100);
            Credit(ref food,ref remainder.food,reward.food*100);
            Credit(ref timber,ref remainder.timber,reward.timber*100);
            Credit(ref arms,ref remainder.arms,reward.arms*100);
            approval=ClampApproval(approval+reward.approval);
            milestoneClaims |= 1<<index;
            TrackProsperity();
        }

        bool ValidExpansion()
        {
            if ((uint)reclaimedPlots > OuterPlotMask || (uint)milestoneClaims > MilestoneMask ||
                (MilestoneClaimed(0) && !TwoOuterPlotsCleared) || (MilestoneClaimed(5) && !FrontiersSecured)) return false;
            for (int i=FirstOuterPlot;i<PlotCount;i++)
            {
                var plot=slots[i];
                if (plot==null || !IsPlotCleared(i) && (plot.kind!=null || plot.level!=0 || plot.workers!=0)) return false;
            }
            return true;
        }

        void ReadExpansion(Dictionary<string,object> root)
        {
            reclaimedPlots=Number(root["reclaimedPlots"],0,OuterPlotMask);
            milestoneClaims=Number(root["milestoneClaims"],0,MilestoneMask);
            if (!(root["frontierSwept"] is bool swept)) throw new FormatException("外域巡边记录无效");
            frontierSwept=swept;
            if (!ValidExpansion()) throw new FormatException("拓荒建地或宏愿记录不一致");
        }
    }
}
