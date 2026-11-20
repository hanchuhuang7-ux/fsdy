using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;

static partial class ManorChecks
{
    static Manor FourDevelopmentPoints()
    {
        var m=EstablishedEstate();
        m.workers=4;
        m.Build(11,"farm");
        m.workers=8; m.cleared=1;
        m.Build(12,"granary");
        m.workers=12; m.cleared=3; m.approval=75;
        m.Build(13,"smithy");
        m.Assign(11,1);m.Assign(12,1);m.Assign(13,1);
        m.ReclaimPlot(18);m.ReclaimPlot(19);
        foreach(int i in new[]{0,1,2})m.ClaimMilestone(i);
        return m;
    }

    public static void RunDevelopment(Catalog data,Action<string,Action> check)
    {
        check("each cottage backyard pays once per switch and produces exact fixed-point yields",() => {
            var m=new Manor {coin=500,timber=500,food=100};
            m.Build(1,"cottage");m.Build(2,"farm");
            var baseline=m.Forecast();
            int coin=m.coin, timber=m.timber;
            Reject(()=>m.SetBackyard(2,"garden"));Reject(()=>m.SetBackyard(1,"gardens"));
            m.SetBackyard(1,"garden");
            Expect(m.coin==coin-8&&m.timber==timber-4&&m.Forecast().food-baseline.food==120,"garden price or Lv.1 yield");
            string save=m.Export();
            Reject(()=>m.SetBackyard(1,"garden"));Expect(m.Export()==save,"repeat backyard changed resources");
            m.Upgrade(1);Expect(m.Forecast().food-baseline.food==132,"Lv.2 garden");
            m.Upgrade(1);Expect(m.Forecast().food-baseline.food==145,"Lv.3 garden");
            m.SetBackyard(1,"poultry");
            Expect(m.Forecast().food-baseline.food==80&&m.Forecast().coin-baseline.coin==60,"poultry ledger");
            m.SetBackyard(1,"workshop");
            Expect(m.Forecast().food==baseline.food&&m.Forecast().coin-baseline.coin==150&&m.Forecast().arms-baseline.arms==30,
                "workshop ledger or previous backyard persisted");
            m.Build(3,"cottage");m.SetBackyard(3,"workshop");
            Expect(m.Forecast().arms-baseline.arms==30,"Lv.1 workshop provided arms");
            m.Upgrade(3);Expect(m.Forecast().arms-baseline.arms==60,"Lv.2 workshop missed arms");
            m.coin=0;save=m.Export();
            Reject(()=>m.SetBackyard(1,"militia_yard"));
            Expect(m.Export()==save,"unaffordable switch was not atomic");
        });

        check("backyard militia seats vanish on rebuild and chickens supply one distinct food source",() => {
            var m=new Manor {coin=500,timber=500,food=100};
            m.Build(1,"cottage");m.Build(2,"cottage");m.Build(3,"farm");
            m.SetBackyard(1,"poultry");m.SetBackyard(2,"poultry");
            Expect(!m.Forecast().marketNeeds.variedDiet,"duplicate poultry supplied two kinds of food");
            m.Assign(3,1);
            Expect(m.Forecast().approvalFactors.food==2&&m.Forecast().marketNeeds.variedDiet,"farm and poultry did not diversify food");
            m.SetBackyard(1,"garden");m.SetBackyard(2,"garden");
            Expect(!m.Forecast().marketNeeds.variedDiet,"gardens alone were counted as a food group");
            int army=m.ArmyLimit, field=m.FieldLimit;
            m.SetBackyard(1,"militia_yard");m=Manor.Import(m.Export());
            Expect(m.ArmyLimit==army+2&&m.FieldLimit==field+1,"militia capacity");
            m.Demolish(1);m.Build(1,"cottage");
            Expect(m.slots[1].backyard==null&&m.ArmyLimit==army&&m.FieldLimit==field,"rebuilding retained backyard");
        });

        check("earned prosperity and milestone points remain after demotion, and only four valid perks can unlock",() => {
            var m=FourDevelopmentPoints();
            Expect(m.prosperityPeak==3&&m.DevelopmentPointsEarned==4&&m.chronicle.Count(e=>e.kind=="title")==3,"point sources or title history");
            var save=m.Export();
            foreach(var perk in new[]{ManorPerk.SustainableWoods,ManorPerk.GuildSmiths,ManorPerk.FortressLogistics,(ManorPerk)6})
                Reject(()=>m.UnlockDevelopment(perk));
            Expect(m.Export()==save,"invalid prerequisite spent points");
            m.approval=20;
            m=Manor.Import(m.Export());
            Expect(m.ProsperityTitle=="拓荒营地"&&m.DevelopmentPointsAvailable==4,"demotion reversed earned points");
            foreach(var perk in new[]{ManorPerk.HeavyPlough,ManorPerk.SustainableWoods,ManorPerk.FreeMarket,ManorPerk.GuildSmiths})
                m.UnlockDevelopment(perk);
            save=m.Export();
            Reject(()=>m.UnlockDevelopment(ManorPerk.BurgageMilitia));
            Reject(()=>m.UnlockDevelopment(ManorPerk.HeavyPlough));
            Expect(m.DevelopmentPointsAvailable==0&&m.DevelopmentPointsSpent==4&&m.Export()==save,"overspend or duplicate purchase");
        });

        check("agriculture and guild bonuses compose with approval, winter, crop synergy and exact forge inputs",() => {
            var m=FourDevelopmentPoints();
            m.Build(14,"timber_camp");m.Assign(14,1);
            m.Build(15,"pasture");m.Assign(15,1);
            m.Assign(12,-1);
            m.day=37;
            for(int i=0;i<m.slots.Length;i++)
                if(Manor.IsCrop(m.slots[i].kind))m.SetRotation(i,"intensive");
            var before=m.Forecast();
            m.UnlockDevelopment(ManorPerk.HeavyPlough);
            m.UnlockDevelopment(ManorPerk.SustainableWoods);
            m.UnlockDevelopment(ManorPerk.FreeMarket);
            m.UnlockDevelopment(ManorPerk.GuildSmiths);
            var after=m.Forecast();
            Expect(after.food-before.food==45,"crop boost did not compose with active percentages");
            Expect(before.winterTimberRequired==300&&after.winterTimberRequired==100&&after.timber-before.timber==268,"winter cut or forestry boost");
            Expect(before.arms==242&&after.arms==290&&after.forgeTimber==before.forgeTimber,"forge throughput");
            m.workers=3;
            Expect(m.Forecast().winterTimberRequired==100,"minimum winter fuel");
        });

        check("free market raises seasonal caps to sixty and keeps buy prices above sell prices across policies",() => {
            var m=FourDevelopmentPoints();
            m.UpgradeSpecialist("merchant");m.UpgradeSpecialist("merchant");
            Expect(m.MarketSeasonLimit==50,"merchant baseline");
            m.UnlockDevelopment(ManorPerk.FreeMarket);
            Expect(m.MarketSeasonLimit==60,"expanded trade quota");
            foreach(string id in new[]{"food","timber","arms"})
            {
                m.SetDecree("none");var normal=m.QuoteTrade(id);
                m.SetDecree("trade");var discount=m.QuoteTrade(id);
                Expect(discount.buyPrice>discount.sellPrice&&discount.buyPrice<normal.buyPrice&&normal.buyPrice>discount.sellPrice,"cross-policy arbitrage");
                int purse=Purse(m), inventory=discount.inventory;
                m.Trade(id,60);m.Trade(id,-60);
                Expect(m.QuoteTrade(id).inventory==inventory&&Purse(m)==purse-60*(discount.buyPrice-discount.sellPrice),"unbalanced trade");
                Reject(()=>m.Trade(id,1));Reject(()=>m.Trade(id,-1));
            }
            m=Manor.Import(m.Export());
            Expect(m.market.food.bought==60&&m.market.arms.sold==60&&m.QuoteTrade("food").buyPrice==241,"saved trade cap or price");
        });

        check("militia and fortress specializations affect infantry prices, budget and hero upgrades only in the manor",() => {
            var m=FourDevelopmentPoints();
            m.Build(14,"barracks");m.Build(15,"stables");
            var hero=data.Heroes["hero_vanguard"];
            m.HireHero(10,hero);
            m.SetDecree("mobilize");
            var oldBudget=m.Budget;
            int army=m.ArmyLimit;
            string cavalry=m.RecruitPrice("cavalry"), prop=m.RecruitPrice("barricade"), hire=m.HeroHirePrice(hero);
            string arena=Json.Write(data.Default());
            m.UnlockDevelopment(ManorPerk.BurgageMilitia);
            m.UnlockDevelopment(ManorPerk.FortressLogistics);
            Expect(m.ArmyLimit==army+4&&m.RecruitPrice("militia").StartsWith("3.8 ")&&
                m.RecruitPrice("archer").StartsWith("6.84 ")&&m.RecruitPrice("cavalry")==cavalry&&m.RecruitPrice("barricade")==prop,"militia scope");
            Expect(m.Budget==oldBudget+62&&m.PrepareFrontier(data,Party(1,0)).Logistics.developmentPercent==10,"fortress budget");
            int purse=Purse(m);
            m.UpgradeHero(10,hero);
            Expect(Purse(m)==purse-2160&&m.HeroHirePrice(hero)==hire&&Json.Write(data.Default())==arena,"hero training or duel scope");
        });

        check("market lights require rations, winter fuel and a staffed tavern or chapel for precisely one extra approval",() => {
            var m=new Manor {workers=5,day=37,food=40,coin=400,timber=400};
            m.Build(1,"cottage");m.Build(2,"chapel");
            var idle=m.Forecast();
            Expect(idle.marketNeeds.rations&&idle.marketNeeds.fuel&&!idle.marketNeeds.spirit&&idle.approvalFactors.market==0,"idle chapel supplied spirit");
            m.Assign(2,1);m.timber=0;
            var cold=m.Forecast();
            Expect(cold.marketNeeds.rations&&!cold.marketNeeds.fuel&&cold.marketNeeds.spirit&&!cold.marketNeeds.Prosperous,"partial winter coverage lit fuel");
            m.timber=10;m.food=0;
            var hungry=m.Forecast();
            Expect(!hungry.marketNeeds.rations&&hungry.marketNeeds.fuel&&hungry.marketNeeds.spirit&&hungry.approvalFactors.market==0,"empty stock lit rations");
            m.food=40;
            var good=m.Forecast();
            Expect(good.marketNeeds.Prosperous&&!good.marketNeeds.variedDiet&&good.approvalFactors.market==1,"three fulfilled basics did not pay");
            m.Advance(1);Expect(m.approval==good.approvalAfter,"market bonus did not settle");
        });

        check("chronicle records committed civic and military events and retains exactly the latest twelve",() => {
            var m=FourDevelopmentPoints();
            m.Build(14,"chapel");m.UnlockDevelopment(ManorPerk.FreeMarket);
            m.day=6;m.eventDay=6;
            m.ResolveEvent(6,1);
            var war=m.Prepare(data,0,false,Party(1,0));
            m.Settle(war,Outcome(war,"red",1));
            m.frontierSwept=true;m.SelectFrontierTier(3);
            var frontier=m.PrepareFrontier(data,Party(1,0));
            var returned=m.ReturnedState(frontier,PatrolOutcome(frontier,15),out _);
            Expect(!m.chronicle.Any(e=>e.kind=="frontier"),"uncommitted return changed the source history");
            m=returned;
            foreach(string kind in new[]{"title","reclaim","granary","chapel","development","dispatch","expedition","frontier"})
                Expect(m.chronicle.Any(e=>e.kind==kind),"missing chronicle event: "+kind);
            for(int i=0;i<15;i++)
            {
                m.day++;
                m.Build(17,"granary");m.Demolish(17);
            }
            string saved=m.Export();
            m=Manor.Import(saved);
            Expect(m.chronicle.Count==12&&m.chronicle[0].day==10&&m.chronicle[11].day==21&&
                m.chronicle.All(e=>e.kind=="granary"&&e.subject==17)&&m.Export()==saved,"oldest-first eviction or reload");
        });

        check("v1–v7 round trip the entire original schema with empty backyards, specializations and chronicle",() => {
            var m=FourDevelopmentPoints();
            m.workers=11;
            m.Demolish(12);m.Build(14,"granary");m.Assign(14,1);
            m.day=42;m.eventDay=42;m.frontierSwept=true;m.SelectFrontierTier(3);m.guideStep=2;
            m.SetTax("high");m.SetDecree("trade");m.Trade("food",5);
            m.SetBackyard(1,"garden");m.UnlockDevelopment(ManorPerk.FreeMarket);
            m.Build(18,"cottage");m.SetBackyard(18,"poultry");
            m.HireHero(10,data.Heroes["hero_vanguard"]);m.UpgradeSpecialist("steward");
            for(int version=1;version<=7;version++)
            {
                var old=LegacySchema(m,version);
                var imported=Manor.Import(Json.Write(old));
                var projected=LegacySchema(imported,version);
                foreach(string key in old.Keys)Expect(Json.Write(old[key])==Json.Write(projected[key]),"legacy payload changed "+key);
                Expect(imported.version==Manor.SaveVersion&&imported.slots.All(plot=>plot.backyard==null)&&imported.developmentMask==0&&
                    imported.chronicle.Count==0&&imported.Export()==Manor.Import(imported.Export()).Export(),"new defaults or current roundtrip");
            }
        });

        check("current saves reject wrong backyard types, inconsistent development masks, and invalid chronicle shape or dates",() => {
            var m=new Manor();
            m.Build(1,"cottage");m.SetBackyard(1,"garden");
            m.ReclaimPlot(18);
            string saved=m.Export();
            Dictionary<string,object> Raw()=> (Dictionary<string,object>)Json.Parse(saved);
            void Bad(string field,object value)
            {
                var raw=Raw();raw[field]=value;
                Reject(()=>Manor.Import(Json.Write(raw)));
            }
            foreach(object id in new object[]{"","fisher",false,new object[0]})
            {
                var raw=Raw();var plot=(Dictionary<string,object>)((List<object>)raw["slots"])[1];
                plot["backyard"]=id;
                Reject(()=>Manor.Import(Json.Write(raw)));
            }
            var changed=Raw();
            ((Dictionary<string,object>)((List<object>)changed["slots"])[18])["backyard"]="garden";
            Reject(()=>Manor.Import(Json.Write(changed)));
            Bad("developmentMask",1);Bad("developmentMask",64);Bad("prosperityPeak",4);
            changed=Raw();changed["prosperityPeak"]=3;changed["developmentMask"]=2;
            Reject(()=>Manor.Import(Json.Write(changed)));
            Bad("chronicle",new[]{new {day=2,kind="reclaim",subject=18,choice=0}});
            Bad("chronicle",new[]{new {day=1,kind="fiction",subject=18,choice=0}});
            Bad("chronicle",new[]{new {day=1,kind="reclaim",subject=19,choice=0}});
            Bad("chronicle",Enumerable.Repeat(new {day=1,kind="reclaim",subject=18,choice=0},13).ToArray());
            string key=Json.Write("developmentMask")+":";
            Reject(()=>Manor.Import(saved.Replace(key,key+"0,"+key)));
            m.slots[1].backyard="unknown";Reject(()=>m.Export());
        });
    }
}
