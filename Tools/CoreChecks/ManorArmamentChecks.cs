using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;

static partial class ManorChecks
{
    public static void RunArmamentsAndEconomy(Catalog data,Action<string,Action> check)
    {
        check("hero armaments pay four resources atomically and record nine charges",()=>{
            var m=new Manor {coin=2200,food=500,timber=1100,arms=240,influence=220};
            Reject(()=>m.ForgeHero(Manor.HeroIds[0]));
            m.Build(1,"tavern");m.Upgrade(1);m.Upgrade(1);m.Assign(1,1);
            foreach(string id in Manor.HeroIds)
            {
                m.HireHero(1,data.Heroes[id]);
                for(int tier=1;tier<=3;tier++)
                {
                    var gear=Manor.Armament(id,tier);
                    int coin=m.coin,steel=m.arms,wood=m.timber,merit=m.influence;
                    m.arms=gear.arms-1;
                    string snapshot=m.Export();
                    Reject(()=>m.ForgeHero(id));
                    Expect(m.Export()==snapshot,"unaffordable forge altered state");
                    m.arms=steel;
                    m.ForgeHero(id);
                    Expect(m.coin==coin-gear.coin&&m.arms==steel-gear.arms&&m.timber==wood-gear.timber&&
                        m.influence==merit-gear.influence&&m.HeroArmament(id)==tier,"wrong forge debit");
                    Expect(Manor.ChronicleText(m.chronicle.Last()).Contains(gear.title),"forging was not recorded");
                }
                Reject(()=>m.ForgeHero(id));
            }
            Expect(m.chronicle.Count(entry=>entry.kind=="armament")==9&&Manor.Import(m.Export()).Export()==m.Export(),"armaments did not roundtrip");
            m.Demolish(1);
            Expect(m.HeroArmament(Manor.HeroIds[2])==3,"equipment lost when its workshop closed");
        });

        check("muster snapshots gear for expedition patrol and raid while Duel and old replay rules stay isolated",()=>{
            var m=new Manor {coin=2200,food=500,timber=1100,arms=240,influence=220,cleared=3};
            m.Upgrade(0);m.Upgrade(0);m.Build(1,"tavern");m.Upgrade(1);m.Upgrade(1);m.Assign(1,1);
            var army=new Dictionary<string,int>();
            foreach(var id in Manor.HeroIds)
            {
                m.HireHero(1,data.Heroes[id]);
                army.Add(id,1);
            }
            var prior=m.Prepare(data,0,false,army);
            foreach(var id in Manor.HeroIds)for(int tier=1;tier<=3;tier++)m.ForgeHero(id);
            var sortie=m.Prepare(data,0,false,army);
            var patrol=m.PrepareFrontier(data,army);
            var raid=m.Prepare(data,0,true,army);
            Expect(prior.config.red.All(unit=>unit.armament==0)&&new[]{sortie,patrol,raid}.All(s=>s.config.red.All(unit=>unit.armament==3)),
                "weapon scope or draft snapshot");
            var forged=Json.Clone(sortie.config);
            forged.red[0].armament=1;
            Expect(!sortie.Matches(forged),"draft loadout could be rewritten");
            forged.red[0].armament=4;
            Reject(()=>data.Validate(forged,true));
            var arena=data.Default();
            var baseline=Json.Write(arena);
            arena.red[0].armament=1;
            Reject(()=>data.Validate(arena));
            Reject(()=>new Battle(data,arena,new ReferenceFactory()));
            Expect(Json.Write(data.Default())==baseline,"arena catalog was changed");
            var seed=new ReplayPackage {kind="seed",engineVersion=Replay.ArmamentExpeditionEngine,config=sortie.config};
            Expect(Replay.Import(data,Json.Write(seed)).config.red[1].armament==3,"modern seed lost weapon tiers");
            seed.engineVersion=Replay.RefinedExpeditionEngine;
            Reject(()=>Replay.Import(data,Json.Write(seed)));
            seed.config=prior.config;
            Expect(Replay.Import(data,Json.Write(seed)).config.red.All(unit=>unit.armament==0),"old expedition rejected");

            using var oldBattle=new Battle(data,prior.config,new ReferenceFactory {freezeUnits=true},true);
            using var newBattle=new Battle(data,sortie.config,new ReferenceFactory {freezeUnits=true},true);
            var v=newBattle.Units[0];var r=newBattle.Units[1];var c=newBattle.Units[2];
            Expect(Math.Abs(v.entity.hp/oldBattle.Units[0].entity.hp-1.25f)<.001f&&
                Math.Abs(r.definition.damage/oldBattle.Units[1].definition.damage-1.24f)<.001f&&
                Math.Abs(r.definition.range/oldBattle.Units[1].definition.range-1.13f)<.001f&&r.armamentPierce==10&&
                Math.Abs(c.definition.heal/oldBattle.Units[2].definition.heal-1.4f)<.001f&&
                Math.Abs(c.entity.hp/oldBattle.Units[2].entity.hp-1.18f)<.001f,"hero-specific battle effects");
            Expect(v.definition.scale==oldBattle.Units[0].definition.scale&&v.definition.mass==oldBattle.Units[0].definition.mass&&
                Math.Abs(data.Unit(v.entity.type).hp-oldBattle.Units[0].entity.hp)<.001f,"catalog or collider stats were changed");
            newBattle.Hit(v,100,null,3);
            Expect(Math.Abs(v.entity.stats.damageTaken-66)<.001f&&v.armamentStability==24,"armor hit modifier");
        });

        check("seasonal staffing respects rested fields, fills winter essentials, and hands control back on manual changes",()=>{
            var m=new Manor {coin=2500,food=200,timber=1100,workers=7,day=36};
            string[] buildings={"farm","farm","irrigated_field","pasture","granary","chapel",Manor.Weaver,"timber_camp",
                Manor.Bakery,"smithy","cottage","cottage"};
            for(int i=0;i<buildings.Length;i++)m.Build(i+1,buildings[i]);
            m.SetRotation(3,"fallow");
            m.slots[2].fertility=120;
            m.slots[1].fertility=82;
            m.AssignPreset("seasonal");
            Expect(m.seasonalWork&&m.slots[2].workers>0&&m.slots[3].workers==0&&m.slots[9].workers>0&&
                m.slots[10].workers>0&&m.slots[0].workers>0,"warm priorities");
            m=Manor.Import(m.Export());
            m.Advance(1);
            Expect(m.Season==3&&m.slots[1].workers==0&&m.slots[2].workers==0&&m.slots[3].workers==0&&
                Enumerable.Range(4,5).All(i=>m.slots[i].workers>0)&&m.FreeWorkers>=0,"winter transition or food/heat priorities");
            m.Advance(Manor.SeasonDays);
            Expect(m.Season==0&&m.slots[2].workers>0&&m.slots[9].workers>0&&m.slots[10].workers>0,"spring did not rebalance");
            m.Assign(2,-1);
            Expect(!m.seasonalWork,"manual assign retained automation");
            m.AssignPreset("seasonal");
            m.AssignPreset("balanced");
            Expect(!m.seasonalWork,"another plan retained automation");
        });

        check("caravan is stable within each season with no profitable spread across days policies or perks",()=>{
            var plain=new Manor {coin=800,food=800,timber=800,arms=500};
            var rich=PromotionEstate();
            rich.developmentMask=1<<(int)ManorPerk.FreeMarket;
            rich.Build(10,Manor.Weaver);
            rich.specialists["merchant"]=3;
            string[] resources={"food","timber","arms"};
            int[] lowestBuy={int.MaxValue,int.MaxValue,int.MaxValue},highestSell={0,0,0};
            var offers=new HashSet<string>();
            for(int season=0;season<240;season++)
            {
                plain.day=rich.day=1+season*Manor.SeasonDays;
                var offer=plain.SeasonalCaravan;
                offers.Add(offer.resource);
                Expect(rich.SeasonalCaravan.title==offer.title,"capital or progress seeded the offer");
                foreach(var manor in new[]{plain,rich})
                {
                    foreach(string decree in new[]{"none","trade"})
                    {
                        manor.SetDecree(decree);
                        for(int i=0;i<resources.Length;i++)
                        {
                            var q=manor.QuoteTrade(resources[i]);
                            lowestBuy[i]=Math.Min(lowestBuy[i],q.buyPrice);
                            highestSell[i]=Math.Max(highestSell[i],q.sellPrice);
                            Expect(q.buyPrice>q.sellPrice,"same season arbitrage");
                        }
                    }
                    manor.day+=Manor.SeasonDays-1;
                    Expect(manor.SeasonalCaravan.title==offer.title,"offer moved midseason");
                }
            }
            Expect(offers.Count==3&&Enumerable.Range(0,3).All(i=>lowestBuy[i]>highestSell[i]),"inter-season arbitrage");
        });

        check("daily ledger reconciles every settled resource and morale in abundant and scarce seasons",()=>{
            var estate=PromotionEstate();
            estate.coin=2200;estate.food=250;estate.timber=1100;estate.arms=110;
            estate.clearedSectors=Manor.SectorMask;estate.prosperityPeak=3;estate.approval=99;
            estate.SetTithe(Manor.TitheRate);
            estate.Build(1,"cottage");estate.SetBackyard(1,"poultry");estate.workers=5;
            estate.Build(10,Manor.Weaver);estate.Build(11,Manor.Bakery);estate.Build(12,"smithy");estate.Build(13,"farm");
            estate.Build(15,"granary");estate.Build(16,"chapel");
            estate.SetRotation(13,"intensive");
            for(int i=10;i<=13;i++)estate.Assign(i,1);
            for(int rank=1;rank<=4;rank++)estate.PromoteNobility(rank);
            foreach(int day in new[]{1,25,37})
            {
                var m=Manor.Import(estate.Export());
                m.day=day;
                if(day==25){m.timber=1000000;m.coin=1000000;m.food=1000000;m.arms=1000000;}
                if(day==37){m.timber=0;m.food=0;}
                var ledger=m.DailyLedger();
                int Net(ManorLedgerResource r)=>
                    ledger.Sum(row=>row.Amount(r));
                int coin=Purse(m),food=m.food*100+m.remainder.food,timber=m.timber*100+m.remainder.timber,
                    arms=m.arms*100+m.remainder.arms,approval=m.approval;
                Expect(ledger[(int)ManorLedgerSource.Frontier].food==200&&ledger[(int)ManorLedgerSource.Frontier].timber==150&&
                    ledger[(int)ManorLedgerSource.Backyard].coin>0&&ledger[(int)ManorLedgerSource.Rations].food==-100*m.workers&&
                    ledger[(int)ManorLedgerSource.Nobility].coin>0,"source classification");
                if(day==1)Expect(ledger[(int)ManorLedgerSource.BakeryInput].food<0&&ledger[(int)ManorLedgerSource.ForgeInput].timber<0&&
                    ledger[(int)ManorLedgerSource.Tithe].food<0,"operating costs missing");
                if(day==37)Expect(ledger[(int)ManorLedgerSource.Winter].timber<0,"winter debit missing");
                m.Advance(1);
                Expect(Purse(m)-coin==Net(ManorLedgerResource.Coin)&&m.food*100+m.remainder.food-food==Net(ManorLedgerResource.Food)&&
                    m.timber*100+m.remainder.timber-timber==Net(ManorLedgerResource.Timber)&&
                    m.arms*100+m.remainder.arms-arms==Net(ManorLedgerResource.Arms)&&m.approval-approval==Net(ManorLedgerResource.Approval),
                    "ledger differs from actual settlement on day "+day);
            }
        });

        check("v1 through v12 project losslessly into v13 and invalid hero tiers or staffing toggles are refused",()=>{
            for(int version=1;version<=12;version++)
            {
                var m=version==12?PromotionEstate():new Manor {coin=600,timber=300,arms=40,food=80};
                m.day=17;
                m.Build(1,"farm");m.Assign(1,1);m.Build(2,"cottage");
                if(version>=2)
                {
                    m.Build(3,"tavern");m.Assign(3,1);
                    var hero=data.Heroes[Manor.HeroIds[0]];
                    m.HireHero(3,hero);m.UpgradeHero(3,hero);
                    m.heroes[hero.id].restUntil=m.day+1;
                }
                if(version==12)
                {
                    for(int rank=1;rank<=4;rank++)m.PromoteNobility(rank);
                    m.Build(35,Manor.Weaver);
                }
                var snapshot=LegacySchema(m,version);
                var imported=Manor.Import(Json.Write(snapshot));
                Expect(imported.version==13&&!imported.seasonalWork&&Manor.HeroIds.All(id=>imported.HeroArmament(id)==0)&&
                    Json.Write(LegacySchema(imported,version))==Json.Write(snapshot),"migration from v"+version);
                if(version==12)Expect(imported.slots[35].kind==Manor.Weaver,"last plot lost");
            }
            var estate=new Manor();
            string id=Manor.HeroIds[0];
            estate.heroes[id]=new ManorHero();
            foreach(var invalid in new object[]{-1,4,1.5,"2",null})
            {
                var raw=(Dictionary<string,object>)Json.Parse(estate.Export());
                var heroes=(Dictionary<string,object>)raw["heroes"];
                ((Dictionary<string,object>)heroes[id])["armament"]=invalid;
                Reject(()=>Manor.Import(Json.Write(raw)));
            }
            var legacy=LegacySchema(estate,12);
            ((Dictionary<string,object>)((Dictionary<string,object>)legacy["heroes"])[id])["armament"]=1;
            Reject(()=>Manor.Import(Json.Write(legacy)));
            var current=(Dictionary<string,object>)Json.Parse(estate.Export());
            current["seasonalWork"]="yes";
            Reject(()=>Manor.Import(Json.Write(current)));
            current.Remove("seasonalWork");
            Reject(()=>Manor.Import(Json.Write(current)));
        });
    }
}
