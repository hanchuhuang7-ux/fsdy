using System;
using System.Collections.Generic;

namespace Frontline.Core
{
    public enum ManorPerk
    {
        HeavyPlough, SustainableWoods, FreeMarket, GuildSmiths, BurgageMilitia, FortressLogistics
    }

    public readonly struct ManorDevelopment
    {
        public readonly string title, effect;
        internal ManorDevelopment(string title,string effect) {this.title=title;this.effect=effect;}
    }

    public sealed partial class Manor
    {
        public int prosperityPeak, developmentMask;

        public static IReadOnlyList<ManorDevelopment> Developments {get;} = Array.AsReadOnly(new[] {
            new ManorDevelopment("重犁深耕","麦田与水浇麦田产粮 +15%"),
            new ManorDevelopment("林地永续","冬柴需求 -35%（取整至少 1） · 伐木 +15%"),
            new ManorDevelopment("自由集市","每季买卖额度各 +10 · 买价 -5%"),
            new ManorDevelopment("行会精锻","铁匠铺军械产出 +20%"),
            new ManorDevelopment("堡地民兵制","靶场兵员招募金币 -12% · 常备军上限 +4"),
            new ManorDevelopment("要塞后勤","出征与巡边军费 +10% · 英雄升级金币 -10%")
        });

        static int DevelopmentMask => (1<<Developments.Count)-1;
        static int CountFlags(int mask)
        {
            int count=0;
            for(uint bits=(uint)mask;bits!=0;bits&=bits-1)count++;
            return count;
        }

        public int DevelopmentPointsEarned => Math.Max(prosperityPeak,ProsperityTier)+(CountFlags(milestoneClaims)>=3?1:0);
        public int DevelopmentPointsSpent => CountFlags(developmentMask);
        public int DevelopmentPointsAvailable => DevelopmentPointsEarned-DevelopmentPointsSpent;
        public bool HasDevelopment(ManorPerk perk) => (uint)perk<Developments.Count&&(developmentMask&(1<<(int)perk))!=0;

        int DevelopmentArmyBonus => HasDevelopment(ManorPerk.BurgageMilitia)?4:0;
        int DevelopmentMarketBonus => HasDevelopment(ManorPerk.FreeMarket)?10:0;
        int DevelopmentBudgetBonus => HasDevelopment(ManorPerk.FortressLogistics)?10:0;

        public string DevelopmentBlock(ManorPerk perk)
        {
            int index=(int)perk;
            if((uint)index>=Developments.Count)return "未知专精";
            if(HasDevelopment(perk))return "已解锁";
            if((index&1)==1&&!HasDevelopment((ManorPerk)(index-1)))return "需要同路线一阶";
            return DevelopmentPointsAvailable>0?null:"发展点不足";
        }

        public void UnlockDevelopment(ManorPerk perk)
        {
            string blocked=DevelopmentBlock(perk);
            if(blocked!=null)throw new InvalidOperationException(blocked);
            TrackProsperity();
            developmentMask|=1<<(int)perk;
            Remember("development",(int)perk);
        }

        bool ValidDevelopment()
        {
            if((uint)prosperityPeak>3||(uint)developmentMask>DevelopmentMask||DevelopmentPointsSpent>DevelopmentPointsEarned)return false;
            for(int index=1;index<Developments.Count;index+=2)
                if(HasDevelopment((ManorPerk)index)&&!HasDevelopment((ManorPerk)(index-1)))return false;
            return true;
        }

        void ReadDevelopment(Dictionary<string,object> root)
        {
            prosperityPeak=Number(root["prosperityPeak"],0,3);
            developmentMask=Number(root["developmentMask"],0,DevelopmentMask);
        }
    }
}
