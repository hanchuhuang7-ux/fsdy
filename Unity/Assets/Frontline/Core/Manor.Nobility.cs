using System;
using System.Collections.Generic;

namespace Frontline.Core
{
    public readonly struct ManorNobleRank
    {
        public readonly string title;
        public readonly int prosperity, expeditions, sectors, coin, influence;
        public readonly int armyBonus, fieldBonus, budgetPercent, productionPercent;

        internal ManorNobleRank(string title,int prosperity,int expeditions,int sectors,int coin,int influence,
            int armyBonus,int fieldBonus,int budgetPercent,int productionPercent)
        {
            this.title=title;
            this.prosperity=prosperity; this.expeditions=expeditions; this.sectors=sectors;
            this.coin=coin; this.influence=influence;
            this.armyBonus=armyBonus; this.fieldBonus=fieldBonus;
            this.budgetPercent=budgetPercent; this.productionPercent=productionPercent;
        }
    }

    public sealed partial class Manor
    {
        public const int FirstNoblePlot=24, LegacyNoblePlotCount=32, NoblePlotsPerRank=3;
        const int LegacyNoblePlotsPerRank=2;
        public int nobility;

        public static IReadOnlyList<ManorNobleRank> NobleRanks {get;}=Array.AsReadOnly(new[]{
            new ManorNobleRank("男爵",0,0,0,0,0,0,0,0,0),
            new ManorNobleRank("子爵",1,1,0,80,25,2,0,0,0),
            new ManorNobleRank("伯爵",2,2,0,140,45,2,2,0,0),
            new ManorNobleRank("侯爵",3,3,1,220,70,2,2,8,0),
            new ManorNobleRank("公爵",3,5,3,340,100,2,2,8,5)
        });

        public string NobleTitle => NobleRanks[nobility].title;
        public int GrantedPlots => GrantedPlotCount(nobility);
        public int BestProsperity => Math.Max(prosperityPeak,ProsperityTier);
        public string BestProsperityTitle => ProsperityTitles[BestProsperity];
        public int ClaimedSectorCount => CountFlags(claimedSectors);

        public static int GrantedPlotCount(int rank) => FirstNoblePlot+NoblePlotsPerRank*rank;
        public static int PlotRank(int index)
        {
            if(index<FirstNoblePlot||index>=PlotCount)return 0;
            return index<LegacyNoblePlotCount?1+(index-FirstNoblePlot)/LegacyNoblePlotsPerRank:1+index-LegacyNoblePlotCount;
        }
        public bool IsPlotGranted(int index) => (uint)index<PlotCount&&PlotRank(index)<=nobility;

        public static string NobleGrantPlots(int rank,bool includeOuter=true)
        {
            int start=FirstNoblePlot+(rank-1)*LegacyNoblePlotsPerRank;
            return start+"–"+(start+1)+(includeOuter?"、"+(LegacyNoblePlotCount+rank-1):"");
        }

        public static string NobleRequirement(int rank)
        {
            if(rank==0)return "开局受封";
            var entry=NobleRanks[rank];
            return "曾达"+ProsperityTitles[entry.prosperity]+" · 通关 "+entry.expeditions+" 场远征"+
                (entry.sectors==0?"":" · 筑哨 "+entry.sectors+" 处");
        }

        public static string NoblePrivilege(int rank)
        {
            if(rank==0)return "持有湖心岛原有建地";
            var entry=NobleRanks[rank];
            var previous=NobleRanks[rank-1];
            if(entry.armyBonus>previous.armyBonus)return "常备军上限 +"+entry.armyBonus;
            if(entry.fieldBonus>previous.fieldBonus)return "行军上限 +"+entry.fieldBonus;
            if(entry.budgetPercent>previous.budgetPercent)return "远征与巡边基础军费 +"+entry.budgetPercent+"%";
            return "税收、农牧、伐木、锻造与后院产量 +"+entry.productionPercent+"%";
        }

        bool MeetsNobleProgress(ManorNobleRank entry,int prosperity) =>
            prosperity>=entry.prosperity&&cleared>=entry.expeditions&&ClaimedSectorCount>=entry.sectors;

        public string NoblePromotionBlock(int rank)
        {
            if((uint)rank>=NobleRanks.Count)return "未知爵位";
            if(rank<=nobility)return "已受封";
            if(rank!=nobility+1)return "须逐阶受封";
            var entry=NobleRanks[rank];
            if(!MeetsNobleProgress(entry,BestProsperity))return "受封条件尚未达成";
            if(influence<entry.influence||coin<entry.coin)return "威望或金币不足";
            return null;
        }

        public void PromoteNobility(int rank)
        {
            string blocked=NoblePromotionBlock(rank);
            if(blocked!=null)throw new InvalidOperationException(blocked);
            var entry=NobleRanks[rank];
            TrackProsperity();
            influence-=entry.influence;
            coin-=entry.coin;
            nobility=rank;
            Remember("nobility",rank,1);
        }

        int NobleYield(int amount)
        {
            int bonus=NobleRanks[nobility].productionPercent;
            return bonus==0?amount:(amount*(100+bonus)+50)/100;
        }

        void ReadNobility()
        {
            if(!MeetsNobleProgress(NobleRanks[nobility],prosperityPeak))
                throw new FormatException("爵位与领地进度不一致");
            for(int index=FirstNoblePlot;index<PlotCount;index++)
                if(!IsPlotGranted(index)&&slots[index].kind!=null)throw new FormatException("未封授的贵族建地不能有建筑");
        }
    }
}
