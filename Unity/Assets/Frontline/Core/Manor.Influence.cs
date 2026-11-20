using System;

namespace Frontline.Core
{
    public sealed partial class Manor
    {
        public const int InfluenceMax = 999, TitheRate = 10, FestivalCost = 25, FestivalApproval = 8, MerchantCost = 20;
        public const int MercenaryCost = 30, MercenaryCoin = 12, MercenaryLevel = 3, MercenarySize = 3;
        public const int ExpeditionInfluence = 12, RaidInfluence = 8, FrontierInfluence = 18;
        static readonly string[] MercenaryRoles = { "shield","spear","crossbow" };
        public int influence, tithe;

        public static string MercenaryRole(int index) => MercenaryRoles[index];
        public bool CanHireMercenaries => influence >= MercenaryCost && coin >= MercenaryCoin;

        public void SetTithe(int percent)
        {
            if (percent != 0 && percent != TitheRate) throw new InvalidOperationException("什一赈济仅支持关闭或 10%");
            tithe = percent;
        }

        void ApplyTithe(ref ManorDay forecast,int storedFood,int households,int minstrel)
        {
            if (tithe == 0 || influence == InfluenceMax || forecast.food <= 0) return;
            int donated = Math.Max(1,forecast.food * tithe / 10000);
            if (storedFood + forecast.food - donated * 100 < households * 200) return;
            forecast.titheFood = donated;
            forecast.influence = Math.Min(InfluenceMax-influence,1+minstrel);
            forecast.food -= donated * 100;
        }

        int GainInfluence(int amount)
        {
            int gained = Math.Min(amount,InfluenceMax - influence);
            influence += gained;
            return gained;
        }

        public void HoldFestival()
        {
            if (influence < FestivalCost) throw new InvalidOperationException("威望不足");
            influence -= FestivalCost;
            approval = ClampApproval(approval + FestivalApproval);
            Remember("festival",0);
            TrackProsperity();
        }

        public void CallMerchant()
        {
            if (influence < MerchantCost) throw new InvalidOperationException("威望不足");
            influence -= MerchantCost;
            market = default;
        }

        // The draft is free. Charge both currencies once the battle has been created.
        public bool SignMercenaryContract(Sortie sortie)
        {
            if (sortie == null || sortie.owner != this || sortie.resolved)
                throw new InvalidOperationException("没有可签订合约的出征名册");
            if (!sortie.Support.mercenaries || sortie.contractPaid) return false;
            if (!CanHireMercenaries) throw new InvalidOperationException("威望或金币不足");
            influence -= MercenaryCost;
            coin -= MercenaryCoin;
            sortie.contractPaid = true;
            return true;
        }
    }
}
