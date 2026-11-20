using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;

static partial class ManorChecks
{
    static Dictionary<string,object> LegacySchema(Manor manor,int version)
    {
        var old = (Dictionary<string,object>)Json.Parse(manor.Export());
        old["version"] = version;
        if(version<11)old.Remove("nobility");
        if(version<10)
        {
            old.Remove("clearedSectors");
            old.Remove("claimedSectors");
        }
        if(version<9)
            foreach(string key in new[]{"retinue","influence","tithe"})old.Remove(key);
        if(version<8)
            foreach(string key in new[]{"prosperityPeak","developmentMask","chronicle"})old.Remove(key);
        if(version<7)
        {
            old.Remove("frontierTier");
            old.Remove("guideStep");
            old.Remove("guideDismissed");
        }
        if(version<6)
        {
            old.Remove("reclaimedPlots");
            old.Remove("milestoneClaims");
            old.Remove("frontierSwept");
        }
        if(version<5)old.Remove("specialists");
        int plots=version==1?10:version<5?Manor.OriginalPlotCount:version==5?Manor.FirstOuterPlot:
            version==11?Manor.LegacyNoblePlotCount:Manor.FirstNoblePlot;
        old["slots"]=LegacyPlots(old["slots"],plots,version>=8,version>=9);
        if (version < 4) old.Remove("eventDay");
        if (version < 3)
            foreach (string key in new[] { "approval","tax","decree","market" }) old.Remove(key);
        if (version == 1)
        {
            foreach (string key in new[] { "remainder","training","heroes" }) old.Remove(key);
        }
        return old;
    }

    static object[] LegacyPlots(object slots,int count,bool backyards=false,bool fertility=false) =>
        ((List<object>)slots).Take(count).Select(item=>{
            var plot=(Dictionary<string,object>)item;
            if(!backyards)plot.Remove("backyard");
            if(!fertility){plot.Remove("fertility");plot.Remove("rotation");}
            return (object)plot;
        }).ToArray();

    static void ExpectOperationsDefaults(Manor m)
    {
        Expect(m.approval == 60 && m.tax == "normal" && m.decree == "none","new operation defaults");
        Expect(m.eventDay == 0 && m.ActiveEvent == null,"unexpected migrated event");
        foreach (string resource in new[] { "food","timber","arms" })
        {
            var quote = m.QuoteTrade(resource);
            Expect(quote.buyRemaining == m.MarketSeasonLimit && quote.sellRemaining == m.MarketSeasonLimit,"empty market defaults");
        }
    }

    static int Purse(Manor m) => 100 * m.coin + m.remainder.coin;

    public static void RunOperations(Catalog data,Action<string,Action> check)
    {
        check("manor tax changes the daily bill and approval with independent caps",() => {
            var m = new Manor();
            ExpectOperationsDefaults(m);
            Expect(m.Forecast().coin == 864 && m.Forecast().approvalFactors.Total == 0,"normal tax");
            m.SetTax("low");
            var low = m.Forecast();
            Expect(low.coin == 432 && low.approvalFactors.tax == 3 && low.approvalAfter == 63,"low tax");
            m.Advance(1);
            Expect(m.approval == 63 && m.coin == 144,"forecast and daily settlement diverged");
            m.SetTax("high");
            var high = m.Forecast();
            Expect(high.coin == 1253 && high.approvalFactors.tax == -3 && high.approvalAfter == 60,"high tax");
            m.Advance(1);
            Expect(m.approval == 60 && Purse(m) == 15685,"fractional tax lost");
            m.approval = 1;
            m.Advance(1);
            Expect(m.approval == 0,"approval fell below zero");
            m.SetTax("low"); m.approval = 99;
            Expect(m.Forecast().approvalFactors.tax == 3 && m.Forecast().approvalAfter == 100,"approval exceeded 100");
            string before = m.Export();
            Reject(() => m.SetTax("none")); Reject(() => m.SetDecree("high"));
            Expect(m.Export() == before,"invalid policies changed the estate");
        });
        check("manor approval distinguishes shortage, different staffed food sources, and tavern service",() => {
            var m = new Manor { workers = 4, coin = 400, timber = 400 };
            m.Build(1,"farm"); m.Build(2,"farm"); m.Build(3,"irrigated_field"); m.Build(4,"tavern"); m.Build(5,"cottage");
            m.Assign(1,1); m.Assign(2,1);
            var plain = m.Forecast().approvalFactors;
            Expect(plain.food == 0 && plain.tavern == 0,"duplicate grain plots or idle tavern gave a bonus");
            m.Assign(3,1); m.Assign(4,1);
            var varied = m.Forecast();
            Expect(varied.approvalFactors.food == 2 && varied.approvalFactors.tavern == 2 && varied.approvalFactors.market == 1 &&
                varied.approvalFactors.minstrel==1 && varied.approvalAfter == 67,"varied diet or staffed tavern");
            m.food = 0;
            var shortage = m.Forecast();
            Expect(shortage.approvalFactors.food == -5 && shortage.approvalFactors.tavern == 2 && shortage.approvalAfter == 59,"empty granary or combined factors");
            m.Advance(1);
            Expect(m.approval == 59 && m.food > 0 && m.Forecast().approvalFactors.food == 2,"harvest did not relieve shortage");
            var draining = new Manor { food = 1 };
            Expect(draining.Forecast().approvalFactors.food == -5,"forecast missed depletion that day");
        });
        check("40 and 75 approval are the exact household and productive yield thresholds",() => {
            var m = new Manor { workers = 4, food = 40, coin = 500, timber = 500 };
            m.Build(1,"farm"); m.Build(2,"timber_camp"); m.Build(3,"smithy"); m.Build(4,"cottage");
            m.Assign(1,1); m.Assign(2,1); m.Assign(3,1);
            foreach (var expected in new[] {
                new { approval=39, coin=734, food=125, timber=350, arms=187, arrival=false },
                new { approval=40, coin=864, food=0, timber=412, arms=220, arrival=true },
                new { approval=74, coin=864, food=0, timber=412, arms=220, arrival=true },
                new { approval=75, coin=950, food=50, timber=453, arms=242, arrival=true }
            })
            {
                m.approval = expected.approval;
                var f = m.Forecast();
                Expect(f.coin == expected.coin && f.food == expected.food && f.timber == expected.timber &&
                    f.arms == expected.arms && f.newHousehold == expected.arrival,"wrong approval band at "+m.approval);
            }
            m.approval = 39; m.SetTax("low"); m.Advance(1);
            Expect(m.approval == 42 && m.workers == 4 && m.Forecast().newHousehold,"approval gate did not reopen the following day");
        });
        check("spring, summer, autumn and winter modify both crop types and pasture in fixed point",() => {
            var m = new Manor { workers = 4, food = 90 };
            m.Build(1,"farm"); m.Build(2,"irrigated_field"); m.Build(3,"pasture");
            m.Assign(1,1); m.Assign(2,1); m.Assign(3,1);
            m.SetRotation(1,"intensive"); m.SetRotation(2,"intensive");
            int[] harvest = { 1504,1715,1925,722 };
            for (int season=0; season<4; season++)
            {
                m.day = 1 + season * Manor.SeasonDays;
                var f = m.Forecast();
                Expect(m.Season == season && f.food == harvest[season] && !f.newHousehold,"season "+season);
                Expect(f.winterTimberRequired == (season == 3 ? 100 : 0),"winter trigger");
            }
        });
        check("winter heating rounds up, spends fractional stock and precedes smithies",() => {
            var m = new Manor { workers = 5, food = 100, day = 37 };
            m.Build(1,"cottage"); m.Build(2,"smithy"); m.Build(3,"timber_camp"); m.Assign(2,1);
            m.timber = 0; m.remainder.timber = 25;
            var shortDay = m.Forecast();
            Expect(shortDay.winterTimberRequired == 200 && shortDay.winterTimber == 125 && shortDay.timber == -25 &&
                shortDay.forgeTimber == 0 && shortDay.approvalFactors.heating == -3,"partial winter coverage");
            m.Advance(1);
            Expect(m.timber == 0 && m.remainder.timber == 0 && m.approval == 57,"heating debit");
            m.Assign(3,1);
            var warmDay = m.Forecast();
            Expect(warmDay.winterTimber == 200 && warmDay.forgeTimber == 100 && warmDay.arms == 220 &&
                warmDay.timber == 212 && warmDay.approvalFactors.heating == 0,"same-day wood could not cover heat and forge");
            m.Advance(1);
            Expect(m.timber == 2 && m.remainder.timber==12 && m.arms == 14 && m.remainder.arms==20 && m.approval == 57,"winter settlement");
        });
        check("all four decrees change exactly their own output, prices, and approval",() => {
            var m = new Manor { workers = 4, food = 90, coin = 500, timber = 500 };
            m.Build(1,"farm"); m.Build(2,"pasture"); m.Build(3,"smithy"); m.Build(4,"barracks"); m.Build(5,"stables");
            for (int i=1;i<=4;i++) m.Assign(i,1);
            foreach (var expected in new[] {
                new { decree="none", coin=864, food=640, arms=200, morale=0, buy=294, sell=200 },
                new { decree="agrarian", coin=864, food=828, arms=170, morale=0, buy=294, sell=200 },
                new { decree="mobilize", coin=864, food=640, arms=250, morale=-1, buy=294, sell=200 },
                new { decree="trade", coin=1037, food=586, arms=200, morale=0, buy=265, sell=220 }
            })
            {
                m.SetDecree(expected.decree);
                var f = m.Forecast();
                var quote = m.QuoteTrade("food");
                Expect(f.coin == expected.coin && f.food == expected.food && f.arms == expected.arms &&
                    f.approvalFactors.decree == expected.morale && quote.buyPrice == expected.buy &&
                    quote.sellPrice == expected.sell,"wrong effects for "+m.decree);
            }
            m.SetDecree("mobilize");
            Expect(m.RecruitPrice("militia") == "3.46 金币 · 1 军械" && m.RecruitPrice("barricade") == "5 金币 · 1 军械","quote did not stack troop discount, or discounted a prop");
            m.coin = 3; m.remainder.coin = 45;
            string before = m.Export();
            Reject(() => m.Recruit(4,"militia"));
            Expect(m.Export() == before,"unaffordable levy charged");
            m.remainder.coin = 46; m.Recruit(4,"militia");
            Expect(Purse(m) == 0 && m.arms == 11 && m.Stock("militia") == 5,"exact troop price");
        });
        check("season, decree, approval and upgrade rates combine before hundredths round once",() => {
            var m = new Manor { day = 25, approval = 75, cleared = 1, workers = 4, coin = 500, timber = 500, food = 100 };
            m.Build(1,"farm"); m.Build(2,"pasture"); m.Upgrade(0); m.Upgrade(1); m.Upgrade(1);
            m.Assign(0,1); m.Assign(0,1); m.Assign(1,1); m.Assign(1,1);
            m.SetTax("high"); m.SetDecree("trade");
            var f = m.Forecast();
            Expect(f.coin == 2729 && f.food == 1257 && f.approvalAfter == 72,"stacked fixed-point rounding");
            int purse = Purse(m), bread = m.food*100+m.remainder.food;
            m.Advance(1);
            Expect(Purse(m)-purse == 2729 && m.food*100+m.remainder.food-bread == 1257 && m.approval == 72,"settled yields changed");
        });
        check("market prices prevent arbitrage even across policy switches and preserve fractional balances",() => {
            var m = new Manor { coin = 500, timber = 500, food = 500, arms = 500 };
            foreach (var price in new[] { ("food",300,200), ("timber",400,200), ("arms",800,500) })
            {
                m.SetDecree("none");
                var normal = m.QuoteTrade(price.Item1);
                Expect(normal.buyPrice == price.Item2 && normal.sellPrice == price.Item3,"base quote");
                m.SetDecree("trade");
                var discounted = m.QuoteTrade(price.Item1);
                Expect(discounted.buyPrice == price.Item2*9/10 && discounted.sellPrice == price.Item3*11/10 &&
                    discounted.buyPrice > discounted.sellPrice && discounted.buyPrice < normal.buyPrice && discounted.sellPrice > normal.sellPrice,"cross-policy spread");
                int before = Purse(m);
                m.Trade(price.Item1,1); m.Trade(price.Item1,-1);
                Expect(Purse(m) == before-discounted.buyPrice+discounted.sellPrice &&
                    m.QuoteTrade(price.Item1).inventory == normal.inventory,"trade changed inventory net or rounded coin");
            }
            Expect(Purse(m) == 49640 && Manor.Import(m.Export()).Export() == m.Export(),"trade lost change through a save");
        });
        check("market quotas are independent and reset on exactly the next season",() => {
            var m = new Manor { day = 11, coin = 1000 };
            m.Trade("food",20); m.Trade("food",-20);
            var full = m.QuoteTrade("food"); var arms = m.QuoteTrade("arms");
            Expect(full.buyRemaining == 0 && full.sellRemaining == 0 && arms.buyRemaining == 20 && arms.sellRemaining == 20,"shared or netted quotas");
            m.Advance(1); m = Manor.Import(m.Export());
            string before = m.Export();
            Reject(() => m.Trade("food",1)); Reject(() => m.Trade("food",-1));
            Expect(m.day == 12 && m.Export() == before,"quota failed or reset early");
            m.Advance(1);
            Expect(m.day == 13 && m.QuoteTrade("food").buyRemaining == 20 && m.QuoteTrade("food").sellRemaining == 20,"quota did not reset");
            m.Trade("food",1);
        });
        check("market failures are atomic including precise wallet and storage caps",() => {
            var m = new Manor { coin = 2, timber = 999999, arms = 0 };
            m.remainder.coin = 69; m.remainder.timber = 1; m.SetDecree("trade");
            string before = m.Export();
            foreach (var action in new Action[] { () => m.Trade("gold",1), () => m.Trade(null,-1), () => m.Trade("food",int.MinValue),
                () => m.Trade("food",0), () => m.Trade("food",21), () => m.Trade("food",1), () => m.Trade("arms",-1) })
                Reject(action);
            Expect(m.Export() == before && m.QuoteTrade("food").buyLimit == 0,"failed trade changed the ledger");
            m.coin = 999999; m.remainder.coin = 50;
            before = m.Export();
            Reject(() => m.Trade("timber",1)); Reject(() => m.Trade("food",-1));
            Expect(m.Export() == before && m.QuoteTrade("timber").buyLimit == 0 && m.QuoteTrade("food").sellLimit == 0,"bound check changed the ledger");
        });
        check("prosperity tiers derive from households, approval, buildings, and expedition clears",() => {
            var m = new Manor { coin = 5000, timber = 5000 };
            Expect(m.ProsperityTitle == "拓荒营地","founding title");
            for (int i=1;i<=9;i++) m.Build(i,"cottage");
            m.workers = 4;
            Expect(m.ProsperityTitle == "安宁村落","village title");
            m.workers = 8; m.cleared = 1;
            Expect(m.ProsperityTitle == "丰饶市镇","market town title");
            m.workers = 12; m.cleared = 3; m.approval = 75;
            Expect(m.ProsperityTitle == "边疆雄镇","border town title");
            m.approval = 39;
            Expect(m.ProsperityTitle == "拓荒营地" && !m.Export().Contains("边疆雄镇"),"title persisted or ignored approval");
        });
        check("version two imports preserve all old progress and initialize independent operations",() => {
            var m = new Manor { day = 35, workers = 5, cleared = 3, coin = 2000, timber = 500, food = 150, arms = 42, raidReady = true };
            m.Build(1,"cottage"); m.Build(12,"tavern"); m.Assign(12,1); m.Build(13,"drill_yard"); m.Assign(13,1);
            var hero = data.Heroes["hero_vanguard"];
            m.HireHero(12,hero); m.UpgradeHero(12,hero); m.heroes[hero.id].restUntil = 36;
            m.Train(13,"archer"); m.Upgrade(0);
            m.remainder = new ManorRemainders { coin=12, food=34, timber=56, arms=78 };
            m.SetTax("high"); m.SetDecree("trade"); m.approval = 91; m.Trade("food",1);
            var old = LegacySchema(m,2);
            var migrated = Manor.Import(Json.Write(old));
            ExpectOperationsDefaults(migrated);
            var comparable = LegacySchema(migrated,2);
            foreach (string key in old.Keys) Expect(Json.Write(comparable[key]) == Json.Write(old[key]),"migration changed "+key);
            Expect(migrated.HeroRest(hero.id) == 1 && migrated.UnitLevel("archer") == 2 && migrated.version == Manor.SaveVersion,"progress migration");
            Expect(Manor.Import(migrated.Export()).Export() == migrated.Export(),"migrated save not canonical");
            old.Add("approval",60);
            Reject(() => Manor.Import(Json.Write(old)));
        });
        check("saves reject illegal operation enums, numbers, and extra or duplicate keys",() => {
            var m = new Manor();
            var valid = m.Export();
            var raw = (Dictionary<string,object>)Json.Parse(valid);
            void Bad(string key,object value)
            {
                var changed = new Dictionary<string,object>(raw) { [key] = value };
                Reject(() => Manor.Import(Json.Write(changed)));
            }
            Bad("tax","LOW"); Bad("tax",0); Bad("decree","raid"); Bad("decree",null);
            Bad("approval",101); Bad("approval",-1); Bad("approval",49.5); Bad("approval","60");
            Bad("market",null); Bad("market",new { food = new { bought=0,sold=0 } });
            Reject(() => Manor.Import(valid.Replace("\"tax\":","\"tax\":\"low\",\"tax\":")));
            Reject(() => Manor.Import(valid.Replace("\"bought\":0","\"bought\":51")));
            Reject(() => Manor.Import(valid.Replace("\"bought\":0","\"bought\":0.2")));
            Reject(() => Manor.Import(valid.Replace("\"sold\":0","\"sold\":-1")));
            Reject(() => Manor.Import(valid.Replace("\"sold\":0","\"sold\":0,\"sold\":1")));
            Reject(() => Manor.Import(valid.Replace("\"sold\":0","\"sold\":0,\"quota\":1")));
            m.market.food.sold=51;
            Reject(() => m.Export());
        });
    }
}
