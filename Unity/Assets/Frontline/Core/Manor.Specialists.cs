using System;
using System.Collections.Generic;

namespace Frontline.Core
{
    public sealed partial class Manor
    {
        public const int SpecialistMaxLevel = 3;
        const int LegacySpecialistCount=4;
        public static readonly string[] SpecialistIds = { "steward","merchant","herbalist","watchman",Mason,Minstrel };
        static readonly string[] SpecialistNames = { "庄园管家","行商掌柜","草药医者","巡庄队长","石匠大师","吟游乐师" };
        public Dictionary<string,int> specialists = new Dictionary<string,int>(StringComparer.Ordinal);

        public int BuildingCount
        {
            get
            {
                int count=0;
                foreach(var plot in slots) if(plot.kind!=null)count++;
                return count;
            }
        }
        bool HasBuilding(string kind)
        {
            foreach(var plot in slots) if(plot.kind==kind)return true;
            return false;
        }

        static int SpecialistIndex(string id)
        {
            int index=Array.IndexOf(SpecialistIds,id);
            if(index<0)throw new InvalidOperationException("未知领地专才");
            return index;
        }
        public static string SpecialistName(string id) => SpecialistNames[SpecialistIndex(id)];
        public int SpecialistRank(string id)
        {
            SpecialistIndex(id);
            return specialists.TryGetValue(id,out int level)?level:1;
        }
        public string SpecialistUnlockBlock(string id)
        {
            switch (SpecialistIndex(id))
            {
                case 0: return null;
                case 1: return MerchantUnlocked ? null : "安宁村落或至少 5 座建筑";
                case 2: return HasBuilding("chapel") || HasBuilding("tavern") ? null : "需要石礼拜堂或英雄酒馆";
                case 3: return HasBuilding("drill_yard") || HasBuilding("barracks") ? null : "需要兵营或练兵靶场";
                case 4: return HasBuilding("timber_camp") || HasBuilding("smithy") ? null : "需要伐木采石营或铁匠铺";
                default: return HasBuilding("tavern") || HasBuilding(Bakery) || HasBuilding(Weaver) ? null : "需要酒馆、面包房或织坊";
            }
        }
        bool MerchantUnlocked => BuildingCount>=5 || ProsperityTier>=1;
        public int SpecialistLevel(string id) => SpecialistUnlockBlock(id)==null?SpecialistRank(id):0;

        static int StewardBonus(int rank) => rank==0?0:2+6*rank;
        static int WatchDiscount(int rank) => rank==0?0:1+3*rank;
        int WatchTrainingDiscount => WatchDiscount(SpecialistLevel("watchman"));
        public static string SpecialistEffect(string id,int rank)
        {
            switch (SpecialistIndex(id))
            {
                case 0: return "基础税收 +"+StewardBonus(rank)+"%";
                case 1: return "季买卖额度 +"+(5*rank)+" · 买价 -"+(2*rank)+"%";
                case 2: return "每日民心 +"+rank+" · 伤员救回 +"+(5*rank)+" 个百分点";
                case 3: return "训练、募兵金币 -"+WatchDiscount(rank)+"% · 迎敌军费 +"+(2*rank)+"%";
                case 4: return "建造与升级成本 -"+MasonDiscount(rank)+"% · 采集建材 +"+MasonTimberBonus(rank)+"%";
                default: return "每日民心 +"+rank+" · 赈济日威望再 +"+rank;
            }
        }

        readonly struct SpecialistCost
        {
            public readonly int coin,food,timber,arms;
            public SpecialistCost(int coin,int food=0,int timber=0,int arms=0)
            {
                this.coin=coin;this.food=food;this.timber=timber;this.arms=arms;
            }
            public bool Affordable(Manor estate) =>
                estate.coin>=coin&&estate.food>=food&&estate.timber>=timber&&estate.arms>=arms;
            public void Pay(Manor estate)
            {
                estate.coin-=coin;estate.food-=food;estate.timber-=timber;estate.arms-=arms;
            }
            public override string ToString()
            {
                string resource=food>0?food+" 粮食":timber>0?timber+" 建材":arms+" 军械";
                return coin+" 金币 · "+resource;
            }
        }

        static SpecialistCost UpgradeSpecialistCost(string id,int rank)
        {
            bool senior=rank==2;
            int coins=senior?52:30;
            switch (SpecialistIndex(id))
            {
                case 0: return new SpecialistCost(coins,timber:senior?10:6);
                case 1: return new SpecialistCost(coins,timber:senior?14:8);
                case 2: return new SpecialistCost(coins,food:senior?10:6);
                case 3: return new SpecialistCost(coins,arms:senior?7:4);
                case 4: return new SpecialistCost(coins,timber:senior?16:9);
                default: return new SpecialistCost(coins,food:senior?12:7);
            }
        }
        public string SpecialistUpgradePrice(string id)
        {
            int rank=SpecialistRank(id);
            return rank>=SpecialistMaxLevel?"已满级":UpgradeSpecialistCost(id,rank).ToString();
        }
        public string SpecialistUpgradeBlock(string id)
        {
            string unlock=SpecialistUnlockBlock(id);
            if(unlock!=null)return unlock;
            int rank=SpecialistRank(id);
            if(rank>=SpecialistMaxLevel)return "已满级";
            return UpgradeSpecialistCost(id,rank).Affordable(this)?null:"金币或训练物资不足";
        }
        public void UpgradeSpecialist(string id)
        {
            string reason=SpecialistUpgradeBlock(id);
            if(reason!=null)throw new InvalidOperationException(reason);
            int rank=SpecialistRank(id);
            UpgradeSpecialistCost(id,rank).Pay(this);
            specialists[id]=rank+1;
            int index=SpecialistIndex(id);
            if(index>=LegacySpecialistCount)Remember("specialist",index,rank+1);
        }

        void ReadSpecialists(object value,bool craftSchema)
        {
            if(!(value is Dictionary<string,object> ranks))throw new FormatException("领地专才名册无效");
            foreach(var item in ranks)
            {
                int index=Array.IndexOf(SpecialistIds,item.Key);
                if(index<0||!craftSchema&&index>=LegacySpecialistCount)throw new FormatException("未知领地专才");
                specialists.Add(item.Key,Number(item.Value,2,SpecialistMaxLevel));
            }
        }
    }
}
