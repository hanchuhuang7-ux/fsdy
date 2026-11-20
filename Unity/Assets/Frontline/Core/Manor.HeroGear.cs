using System;

namespace Frontline.Core
{
    public readonly struct HeroGear
    {
        public readonly string title;
        public readonly int coin, arms, timber, influence;
        public readonly int life, attack, reach, healing, reduction, stability, pierce;

        internal HeroGear(string title,int coin,int arms,int timber,int influence,int life=0,int attack=0,
            int reach=0,int healing=0,int reduction=0,int stability=0,int pierce=0)
        {
            this.title=title; this.coin=coin; this.arms=arms; this.timber=timber; this.influence=influence;
            this.life=life; this.attack=attack; this.reach=reach; this.healing=healing;
            this.reduction=reduction; this.stability=stability; this.pierce=pierce;
        }

        public string Price => coin+" 金币 · "+arms+" 军械 · "+timber+" 建材"+(influence>0?" · "+influence+" 威望":"");
        public string Effect => life>0 && reduction>0
            ? "生命 +"+life+"% · 承伤 −"+reduction+"% · 受击推力 −"+stability+"%"
            : attack>0 ? "攻击 +"+attack+"% · 射程 +"+reach+"% · 破甲 +"+pierce+" 点"
            : "治疗 +"+healing+"% · 生命 +"+life+"%";
    }

    public sealed partial class Manor
    {
        public const int MaxHeroArmament=3;

        // The displayed effects are cumulative. The three branches share the same spending curve.
        static readonly HeroGear[][] Armaments = {
            new[] {
                new HeroGear("精钢重锤",54,8,10,0,life:8,reduction:3,stability:8),
                new HeroGear("符文胸甲",90,14,18,12,life:16,reduction:7,stability:15),
                new HeroGear("御赐金狮重铠",140,23,30,28,life:25,reduction:12,stability:24)
            },
            new[] {
                new HeroGear("紫杉长弓",54,8,10,0,attack:8,reach:4,pierce:2),
                new HeroGear("破甲箭囊",90,14,18,12,attack:15,reach:8,pierce:5),
                new HeroGear("鹰眼精钢弩",140,23,30,28,attack:24,reach:13,pierce:10)
            },
            new[] {
                new HeroGear("圣光权杖",54,8,10,0,life:5,healing:12),
                new HeroGear("救护圣匣",90,14,18,12,life:10,healing:25),
                new HeroGear("金冠战旗",140,23,30,28,life:18,healing:40)
            }
        };

        public static HeroGear Armament(string hero,int tier) => Armaments[Array.IndexOf(HeroIds,hero)][tier-1];
        public int HeroArmament(string id) => heroes.TryGetValue(id,out var hero)?hero.armament:0;

        public string ForgeHeroBlock(string id)
        {
            if(!heroes.TryGetValue(id,out var hero))return "请先在英雄酒馆招募";
            if(hero.armament>=MaxHeroArmament)return "武装已满阶";
            if(!HasBuilding("tavern")&&!HasBuilding("smithy"))return "需要英雄酒馆或铁匠铺";
            var cost=Armament(id,hero.armament+1);
            return coin>=cost.coin&&arms>=cost.arms&&timber>=cost.timber&&influence>=cost.influence?null:"锻造资源不足";
        }

        public void ForgeHero(string id)
        {
            string reason=ForgeHeroBlock(id);
            if(reason!=null)throw new InvalidOperationException(reason);
            var hero=heroes[id];
            var cost=Armament(id,hero.armament+1);
            Pay(cost.coin,cost.timber);
            arms-=cost.arms;
            influence-=cost.influence;
            hero.armament++;
            Remember("armament",Array.IndexOf(HeroIds,id),hero.armament);
        }
    }
}
