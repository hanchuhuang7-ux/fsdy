using System;
using System.Collections.Generic;
using System.Linq;

namespace Frontline.Core
{
    public sealed class ManorHero
    {
        public int level = 1, restUntil;
    }

    public sealed partial class Manor
    {
        public const int HeroMaxLevel = 12, UnitMaxLevel = 8, HeroRestDays = 2;
        public static readonly string[] HeroIds = { "hero_vanguard", "hero_ranger", "hero_commander" };
        public Dictionary<string,int> training = new Dictionary<string,int>(StringComparer.Ordinal);
        public Dictionary<string,ManorHero> heroes = new Dictionary<string,ManorHero>(StringComparer.Ordinal);

        public int UnitLevel(string id) => training.TryGetValue(id,out int level) ? level : 1;
        public int HeroLevel(string id) => heroes.TryGetValue(id,out var hero) ? hero.level : 0;
        public int HeroRest(string id) => heroes.TryGetValue(id,out var hero) ? Math.Max(0,hero.restUntil - day) : 0;
        public int Available(string id) => HeroIds.Contains(id) ? (HeroLevel(id) > 0 && HeroRest(id) == 0 ? 1 : 0) : Stock(id);

        readonly struct TrainingCost
        {
            readonly int coin, food, arms;
            public TrainingCost(int coin,int food,int arms = 0,int coinPercent = 100)
            { this.coin = coin * coinPercent; this.food = food; this.arms = arms; }
            public bool Affordable(Manor manor) => Precise(manor.coin,manor.remainder.coin) >= coin && manor.food >= food && manor.arms >= arms;
            public void Pay(Manor manor)
            {
                if (!Affordable(manor)) throw new InvalidOperationException("金币、粮食或军械不足");
                Credit(ref manor.coin,ref manor.remainder.coin,-coin);
                manor.food -= food; manor.arms -= arms;
            }
            public override string ToString() => FormatHundredths(coin) + " 金币 · " + food + " 粮食" + (arms == 0 ? "" : " · " + arms + " 军械");
        }

        static TrainingCost HireCost(HeroDef hero) => new TrainingCost(hero.hireCoin,hero.hireFood);
        TrainingCost HeroCost(HeroDef hero) => new TrainingCost(Whole(LevelScaling.Scale(24,HeroLevel(hero.id))),
            Whole(LevelScaling.Scale(4,HeroLevel(hero.id))));
        TrainingCost UnitCost(int i,string id)
        {
            var p = Plot(i);
            int level = UnitLevel(id);
            int discount = p.workers == 0 ? 0 : WholeLevelValue(6,p.level);
            return new TrainingCost(Whole(LevelScaling.Scale(16,level)) - discount,
                Whole(LevelScaling.Scale(3,level)),Whole(LevelScaling.Scale(3,level)),100-WatchTrainingDiscount);
        }
        public string HeroHirePrice(HeroDef hero) => HireCost(hero).ToString();
        public string HeroUpgradePrice(HeroDef hero) => HeroCost(hero).ToString();
        public string TrainPrice(int i,string id) => UnitCost(i,id).ToString();

        public string HeroHireBlock(int i,HeroDef hero)
        {
            var p = Plot(i);
            if (p.kind != "tavern") return "请在英雄酒馆招募";
            if (HeroLevel(hero.id) > 0) return "英雄已招募";
            if (p.level < hero.tavernLevel) return "需要酒馆 Lv." + hero.tavernLevel;
            if (p.workers == 0) return "请先指派一户";
            return HireCost(hero).Affordable(this) ? null : "金币或粮食不足";
        }
        public string HeroUpgradeBlock(int i,HeroDef hero)
        {
            var p = Plot(i);
            if (p.kind != "tavern") return "请在英雄酒馆培养";
            int level = HeroLevel(hero.id);
            if (level == 0) return "请先招募英雄";
            if (level >= HeroMaxLevel) return "已达最高等级";
            if (p.workers == 0) return "请先指派一户";
            return HeroCost(hero).Affordable(this) ? null : "金币或粮食不足";
        }
        public string TrainBlock(int i,string id)
        {
            if (Plot(i).kind != "drill_yard") return "请在兵营训练";
            if (Offer(id).prop) return "道具无法训练";
            if (UnitLevel(id) >= UnitMaxLevel) return "已达最高等级";
            return UnitCost(i,id).Affordable(this) ? null : "金币、粮食或军械不足";
        }
        public void HireHero(int i,HeroDef hero)
        {
            string reason = HeroHireBlock(i,hero);
            if (reason != null) throw new InvalidOperationException(reason);
            HireCost(hero).Pay(this);
            heroes.Add(hero.id,new ManorHero());
        }
        public void UpgradeHero(int i,HeroDef hero)
        {
            string reason = HeroUpgradeBlock(i,hero);
            if (reason != null) throw new InvalidOperationException(reason);
            HeroCost(hero).Pay(this);
            heroes[hero.id].level++;
        }
        public void Train(int i,string id)
        {
            string reason = TrainBlock(i,id);
            if (reason != null) throw new InvalidOperationException(reason);
            UnitCost(i,id).Pay(this);
            training[id] = UnitLevel(id) + 1;
        }

        void ReadProgress(object unitLevels,object heroRoster)
        {
            if (!(unitLevels is Dictionary<string,object> units) || !(heroRoster is Dictionary<string,object> roster))
                throw new FormatException("训练或英雄名册无效");
            foreach (var p in units)
            {
                if (!recruits.TryGetValue(p.Key,out var def) || def.prop) throw new FormatException("未知训练兵种");
                training.Add(p.Key,Number(p.Value,2,UnitMaxLevel));
            }
            foreach (var p in roster)
            {
                if (!HeroIds.Contains(p.Key)) throw new FormatException("未知英雄");
                var h = ObjectWith(p.Value,"level","restUntil");
                heroes.Add(p.Key,new ManorHero {
                    level = Number(h["level"],1,HeroMaxLevel), restUntil = Number(h["restUntil"],0,Math.Min(MaxValue,day + HeroRestDays))
                });
            }
        }
    }
}
