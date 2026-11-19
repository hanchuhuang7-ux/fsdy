using System;
using System.Collections.Generic;

namespace Frontline.Core
{
    public struct ManorRetinue
    {
        public int count, level, wounded;
    }

    public readonly struct ManorSupport
    {
        public readonly int guards;
        public readonly bool mercenaries;

        public ManorSupport(int guards,bool mercenaries)
        {
            this.guards = guards;
            this.mercenaries = mercenaries;
        }

        public int Count => guards + (mercenaries ? Manor.MercenarySize : 0);
    }

    public sealed partial class Manor
    {
        public const int RetinueMaxLevel = 3, GuardCoin = 18, GuardArms = 4, GuardRefillCoin = 4;
        static readonly string[] GuardRoles = { "shield","spear","cavalry" };
        public ManorRetinue retinue = new ManorRetinue { level = 1 };
        public int RetinueLimit => 2 * slots[0].level;
        public int RetinueReady => retinue.count - retinue.wounded;
        public int RetinueBattleLevel => retinue.level + 2;
        public static string RetinueRole(int index) => GuardRoles[index % GuardRoles.Length];
        public int RetinueUpgradeCoin => retinue.level == 1 ? 30 : 44;
        public int RetinueUpgradeArms => retinue.level == 1 ? 6 : 10;
        public int RetinueRefillCoin => retinue.wounded * GuardRefillCoin;
        public bool CanRecruitGuard => retinue.count < RetinueLimit && coin >= GuardCoin && arms >= GuardArms;
        public bool CanUpgradeRetinue => retinue.count > 0 && retinue.level < RetinueMaxLevel &&
            coin >= RetinueUpgradeCoin && arms >= RetinueUpgradeArms;
        public bool CanRefillRetinue => retinue.wounded > 0 && coin >= RetinueRefillCoin;

        public void RecruitGuard()
        {
            if (!CanRecruitGuard) throw new InvalidOperationException("卫队编制已满或金币、军械不足");
            coin -= GuardCoin;
            arms -= GuardArms;
            retinue.count++;
        }

        public void UpgradeRetinue()
        {
            if (!CanUpgradeRetinue) throw new InvalidOperationException("卫队等级、金币或军械不足");
            coin -= RetinueUpgradeCoin;
            arms -= RetinueUpgradeArms;
            retinue.level++;
        }

        public void RefillRetinue()
        {
            if (!CanRefillRetinue) throw new InvalidOperationException("没有待整补卫队或金币不足");
            coin -= RetinueRefillCoin;
            retinue.wounded = 0;
        }

        public int SupportCost(Catalog data,ManorSupport support)
        {
            int cost = 0;
            for (int i = 0; i < support.guards; i++) cost += ItemCost(data,RetinueRole(i));
            if (support.mercenaries)
                foreach (var role in MercenaryRoles) cost += ItemCost(data,role);
            return cost;
        }

        void ReadRetinue(object value)
        {
            var row = ObjectWith(value,"count","level","wounded");
            retinue.count = Number(row["count"],0,RetinueLimit);
            retinue.level = Number(row["level"],1,retinue.count == 0 ? 1 : RetinueMaxLevel);
            retinue.wounded = Number(row["wounded"],0,retinue.count);
        }
    }
}
