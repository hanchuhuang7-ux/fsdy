using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontline.Core
{
    public enum ManorThreat { Mixed, Mounted, Armored, Ranged }

    public readonly struct ManorLogistics
    {
        public const int RecoveryDenominator=300, MaxRecoveryPercent=65;
        public readonly int baseBudget, approvalPercent, prosperityPercent, raidPercent, developmentPercent, nobilityPercent;
        public readonly int recoveryDivisor, recoveryRate, heroRestDays;
        public int Budget => (baseBudget * (100 + approvalPercent + prosperityPercent + raidPercent + developmentPercent + nobilityPercent) + 50) / 100;

        internal ManorLogistics(int baseBudget,int approvalPercent,int prosperityPercent,int raidPercent,int developmentPercent,
            int nobilityPercent,int recoveryDivisor,int bonusRecovery,int heroRestDays)
        {
            this.baseBudget=baseBudget; this.approvalPercent=approvalPercent; this.prosperityPercent=prosperityPercent;
            this.raidPercent=raidPercent;
            this.developmentPercent=developmentPercent;
            this.nobilityPercent=nobilityPercent;
            this.recoveryDivisor=recoveryDivisor; this.heroRestDays=heroRestDays;
            recoveryRate=Math.Min(MaxRecoveryPercent*3,RecoveryDenominator/recoveryDivisor+bonusRecovery*3);
        }
        int RecoverCount(int casualties) => (casualties*recoveryRate+RecoveryDenominator-1)/RecoveryDenominator;
        internal bool RecoversWound(int casualtyIndex) => RecoverCount(casualtyIndex+1)>RecoverCount(casualtyIndex);
    }

    public sealed partial class Manor
    {
        public ManorLogistics Logistics => ExpeditionLogistics(false);
        public ManorLogistics ExpeditionLogistics(bool raid)
        {
            int tier = ProsperityTier;
            bool morale = approval >= FlourishingApproval;
            int modifier = morale ? 8 : approval < UnrestApproval ? -10 : 0;
            int recovery = Synergies().chapelRecoveryPercent + 5*SpecialistLevel("herbalist");
            return new ManorLogistics(Whole(LevelScaling.Scale(620,slots[0].level)),modifier,tier==3?10:0,
                raid?2*SpecialistLevel("watchman"):0,DevelopmentBudgetBonus,NobleRanks[nobility].budgetPercent,
                morale?2:3,recovery,tier>=2?1:HeroRestDays);
        }

        static readonly string[] FrontlinePicks = { "shield","spear","militia","giant","cavalry" };
        static readonly string[] AntiCavalry = { "spear","shield","militia","giant","cavalry" };
        static readonly string[] AntiRanged = { "cavalry","shield","spear","militia","giant" };
        static readonly string[] RangedPicks = { "archer","crossbow","bomber","catapult" };
        static readonly string[] AntiArmor = { "crossbow","catapult","archer","bomber" };

        public ManorThreat EnemyThreat(Catalog data,int stage,bool raid,bool frontier = false)
        {
            var enemies = frontier ? FrontierMap.Roster(frontierTier) : EnemyRoster(stage,raid);
            int mounted=0, armored=0, ranged=0;
            foreach (string id in enemies)
            {
                if (id=="cavalry") mounted++;
                if (id=="shield" || id=="giant") armored++;
                if (data.Unit(id).ranged) ranged++;
            }
            // One cavalry unit can threaten a small squad. Ranged and armor matter when several guards share that role.
            int cavalryPressure=mounted*6, armorPressure=armored*4, rangePressure=ranged*4;
            int pressure=Math.Max(cavalryPressure,Math.Max(armorPressure,rangePressure));
            if (pressure<enemies.Count) return ManorThreat.Mixed;
            if (pressure==cavalryPressure) return ManorThreat.Mounted;
            return pressure==armorPressure?ManorThreat.Armored:ManorThreat.Ranged;
        }

        public Dictionary<string,int> RecommendForce(Catalog data,int stage,bool raid,bool frontier = false,ManorSupport support=default)
        {
            ManorThreat threat=EnemyThreat(data,stage,raid,frontier);
            var front=threat==ManorThreat.Mounted?AntiCavalry:threat==ManorThreat.Ranged?AntiRanged:FrontlinePicks;
            var rear=threat==ManorThreat.Armored?AntiArmor:RangedPicks;
            var roster=new Dictionary<string,int>(StringComparer.Ordinal);
            ValidateForce(data,roster,false,frontier,raid,support);
            int budget=ExpeditionLogistics(raid&&!frontier).Budget-SupportCost(data,support);
            int seats=(frontier?FrontierLimit:FieldLimit)-support.Count;

            bool Add(string id)
            {
                roster.TryGetValue(id,out int count);
                int price=ItemCost(data,id);
                if (seats==0 || count>=Available(id) || price>budget) return false;
                roster[id]=count+1;
                budget-=price;
                seats--;
                return true;
            }
            bool Pick(string[] options)
            {
                foreach (string id in options) if (Add(id)) return true;
                return false;
            }

            foreach (string id in HeroIds) Add(id);
            int frontline=roster.ContainsKey("hero_vanguard")?1:0;
            int output=roster.ContainsKey("hero_ranger")?1:0;
            if (frontline==0 && Pick(front)) frontline++;
            if (output==0 && Pick(rear)) output++;
            if (frontline>0 && output>0 && !roster.ContainsKey("hero_commander")) Add("medic");

            while (seats>0)
            {
                if (frontline>=output*2 && Pick(rear)) output++;
                else if (Pick(front)) frontline++;
                else if (Pick(rear)) output++;
                else if (!Add("medic")) break;
            }
            ValidateForce(data,roster,false,frontier,raid,support);
            return roster;
        }
    }
}
