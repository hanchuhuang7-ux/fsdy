using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;

static partial class ManorChecks
{
    static Manor EstablishedEstate()
    {
        var m = new Manor { coin=5000, timber=5000, food=200 };
        for (int i=1;i<=9;i++) m.Build(i,"cottage");
        m.Build(10,"tavern");
        m.Upgrade(10); m.Upgrade(10); m.Assign(10,1);
        return m;
    }

    static Manor DispatchOn(int issueDay)
    {
        var m = new Manor { day=issueDay-1, coin=500, timber=100, food=100 };
        m.Advance(1);
        Expect(m.eventDay==issueDay,"dispatch failed to trigger");
        return m;
    }

    public static void RunCampaign(Catalog data,Action<string,Action> check)
    {
        check("morale budgets gate all three estate sorties without changing the fair arena or replay schema",() => {
            var m = new Manor { approval=39 };
            m.stock["giant"]=1;
            var force = new Dictionary<string,int> { ["giant"]=1,["archer"]=2,["militia"]=2 };
            var arena = data.Default();
            string before=Json.Write(arena);
            Expect(m.Budget==558,"low morale budget");
            Reject(()=>m.Prepare(data,0,false,force));
            Reject(()=>m.Prepare(data,0,true,force));
            Reject(()=>m.PrepareFrontier(data,force));
            m.approval=40;
            Expect(m.Budget==620&&m.Prepare(data,0,false,force).Logistics.recoveryDivisor==3,"neutral budget");
            force["militia"]=3;
            m.approval=74;
            Reject(()=>m.Prepare(data,0,false,force));
            m.approval=75;
            var sortie=m.Prepare(data,0,false,force);
            Expect(m.Budget==670&&sortie.Logistics.recoveryDivisor==2&&sortie.config.red.Count==6&&sortie.config.budget==670,"high morale budget");
            Expect(m.Prepare(data,0,true,force).Logistics.Budget==670 && m.PrepareFrontier(data,force).Logistics.Budget==670,"raid or patrol ignored morale");
            Reject(()=>data.Validate(sortie.config));
            var seed=Replay.Create(data,sortie.config,null,new Result(),true,Replay.RefinedExpeditionEngine);
            Expect(Json.Write(Replay.Import(data,Json.Write(seed)).config)==Json.Write(sortie.config)&&
                Json.Write(data.Validate(arena))==before,"expedition state leaked into the old schemas");
        });

        check("prosperity tiers add 5/10/15 market units over the active merchant and preserve earned trades",() => {
            var m=EstablishedEstate();
            Expect(m.MarketSeasonLimit==25 && m.Logistics.heroRestDays==2,"founding privileges with merchant");
            m.workers=4;
            Expect(m.MarketSeasonLimit==30 && m.Logistics.heroRestDays==2,"village privileges");
            m.workers=8; m.cleared=1;
            Expect(m.MarketSeasonLimit==35 && m.Logistics.heroRestDays==1 && m.Budget==620,"town privileges");
            m.workers=12; m.cleared=3; m.approval=75;
            Expect(m.MarketSeasonLimit==40 && m.Logistics.heroRestDays==1 && m.Budget==732 &&
                m.Logistics.approvalPercent==8 && m.Logistics.prosperityPercent==10,"border town privileges");
            m.Trade("food",40); m.Trade("food",-40);
            m.approval=39;
            m=Manor.Import(m.Export());
            var quote=m.QuoteTrade("food");
            Expect(m.market.food.bought==40&&m.market.food.sold==40&&quote.buyRemaining==0&&quote.sellRemaining==0,"demotion lost history or reopened the quota");
            Reject(()=>m.Trade("food",1)); Reject(()=>m.Trade("food",-1));
            m.SetTax("high");
            m.food=0;
            m.Advance(12);
            Expect(m.market.food.bought==0&&m.QuoteTrade("food").buyRemaining==25,"seasonal reset after title change");
        });

        check("casualty and hero recovery use the logistics rostered when the expedition departed",() => {
            foreach (int morale in new[] { 74,75 })
            {
                var m=EstablishedEstate();
                m.workers=8; m.cleared=1; m.approval=morale;
                m.HireHero(10,data.Heroes["hero_vanguard"]);
                var force=Party();
                force.Add("hero_vanguard",1);
                var sortie=m.Prepare(data,1,false,force);
                m.approval=39;
                var next=m.ReturnedState(sortie,Outcome(sortie,"blue",0),out var note);
                int expected=morale==75?4:3;
                Expect(note.troops.Values.Sum(row=>row.wounded)==expected&&note.troops.Values.Sum(row=>row.fallen)==6-expected&&
                    next.ArmySize==expected&&next.HeroRest("hero_vanguard")==1&&note.heroes["hero_vanguard"]==1,"departure recovery rules");
                Expect(note.approvalBefore==39&&note.approvalAfter==35&&next.approval==35&&m.approval==39,"return state was partially committed");
                Reject(()=>m.ReturnedState(sortie,Outcome(sortie,"blue",0),out _));
            }
        });

        check("first clears earn eight approval, repeat wins and raids earn four, and losses floor at zero",() => {
            var m=new Manor { approval=90 };
            var force=Party(1,0);
            Manor.Settlement Finish(bool raid,string winner)
            {
                var sortie=m.Prepare(data,0,raid,force);
                return m.Settle(sortie,Outcome(sortie,winner,winner=="red"?1:0));
            }
            var first=Finish(false,"red");
            Expect(first.approvalBefore==90&&first.approvalAfter==98&&first.clearedAfter==1,"first clear");
            Expect(Finish(false,"red").approvalAfter==100&&m.cleared==1,"repeat or upper cap");
            m.approval=70;
            Expect(Finish(true,"red").approvalAfter==74&&m.cleared==1,"raid reward");
            Expect(Finish(false,"red").approvalAfter==78,"normal victory reward");
            Expect(Finish(false,"draw").approvalAfter==78&&Finish(true,"blue").approvalAfter==78,"draw or raid loss changed approval");
            m.approval=2;
            Expect(Finish(false,"blue").approvalAfter==0,"defeat lower cap");
        });

        check("patrol approval uses complete groups of five defeated guards and caps at six",() => {
            foreach (var target in new[] { (4,0),(5,2),(9,2),(10,4),(15,6) })
            {
                var m=new Manor { cleared=2 };
                var s=m.PrepareFrontier(data,Party(2,0));
                var result=Outcome(s,"draw",2); result.reason="withdraw";
                for(int i=0;i<s.config.blue.Count;i++)
                    result.report.units.Add(new UnitReport { id=3+i, team="blue", type=s.config.blue[i].type, alive=i>=target.Item1 });
                var saved=m.ReturnedState(s,result,out var note);
                Expect(note.defeated==target.Item1&&note.approvalAfter-note.approvalBefore==target.Item2&&
                    saved.approval==60+target.Item2&&saved.cleared==2,"patrol reward at "+target.Item1);
            }
        });

        check("all eight seasonal dispatches trigger deterministically on day six without blocking the calendar",() => {
            var daily=new Manor { food=500,timber=1000 };
            var bulk=Manor.Import(daily.Export());
            string[] order={ "流民投奔","工匠改良","丰收庆典","寒潮来袭","行商试市","边疆斥候急报","商队收粮","军备紧急征购" };
            for(int i=0;i<order.Length;i++)
            {
                daily.Advance(4);
                Expect(daily.eventDay==0,"early dispatch or previous report still open");
                daily.Advance(1);
                Expect(daily.eventDay==6+i*12&&daily.ActiveEvent?.title==order[i],"dispatch schedule changed");
                var reopened=Manor.Import(daily.Export());
                Expect(reopened.Export()==daily.Export()&&reopened.ActiveEvent?.title==order[i],"pending dispatch was not restored");
                daily.Advance(7); bulk.Advance(12);
                Expect(daily.Export()==bulk.Export()&&daily.eventDay==0,"silent default or batched calendar drift");
            }
        });

        check("dispatch options are atomic and guard both stale choices and housing",() => {
            var m=DispatchOn(6);
            string before=m.Export();
            Expect(m.EventChoiceBlock(6,0)=="没有空房","housing requirement");
            Reject(()=>m.ResolveEvent(6,0)); Reject(()=>m.ResolveEvent(5,1)); Reject(()=>m.ResolveEvent(6,2));
            Expect(m.Export()==before,"invalid choice mutated the estate");
            m.Build(1,"cottage"); m.food=3;
            before=m.Export();
            Expect(m.EventChoiceBlock(6,0)=="粮食不足","resource requirement");
            Reject(()=>m.ResolveEvent(6,0));
            Expect(m.Export()==before,"partial event payment");
            var accepted=Manor.Import(before);
            accepted.food=4;
            accepted.ResolveEvent(6,0);
            Expect(accepted.workers==3&&accepted.food==0&&accepted.eventDay==0,"refugee settlement");
            m.ResolveEvent(6,1);
            Expect(m.workers==2&&m.food==2&&m.approval==65&&m.eventDay==0,"farewell choice");
            Reject(()=>m.ResolveEvent(6,1));
        });

        check("default dispatch resolution preserves the last ration and applies winter's stated tradeoff",() => {
            foreach (int initialFood in new[] { 0,2,3 })
            {
                var m=new Manor { day=12,eventDay=6,food=initialFood };
                int approval=m.Forecast().approvalAfter;
                m.Advance(1);
                int gift=initialFood==3?1:0;
                Expect(m.eventDay==0&&m.approval==approval+5*gift&&m.food==Math.Max(0,initialFood-1)-gift,"spring fallback");
            }
            var winter=DispatchOn(42); winter.approval=97;
            var left=Manor.Import(winter.Export());
            int wood=winter.timber;
            winter.ResolveEvent(42,0); left.ResolveEvent(42,1);
            Expect(winter.approval==100&&winter.timber==wood-4&&left.approval==93&&left.timber==wood,"winter choice");
            var merchant=DispatchOn(54); merchant.coin=1000000; merchant.remainder.coin=0;
            string before=merchant.Export();
            Expect(merchant.EventChoiceBlock(54,1)=="物资将超出仓储上限","event income cap");
            Reject(()=>merchant.ResolveEvent(54,1));
            Expect(merchant.Export()==before,"resource cap partially applied");
        });

        check("all three staffing presets respect priority, household conservation and plot capacities",() => {
            var m=new Manor { workers=8, coin=500, timber=500, food=100 };
            string[] sites={"cottage","irrigated_field","farm","pasture","tavern","timber_camp","smithy","barracks"};
            for(int i=0;i<sites.Length;i++)m.Build(i+1,sites[i]);
            m.Upgrade(1); m.Upgrade(1);
            for(int i=0;i<3;i++)m.Assign(8,1);
            void CheckPlan(string id,params int[] expected)
            {
                m.AssignPreset(id);
                Expect(m.slots.Take(9).Select(plot=>plot.workers).SequenceEqual(expected),"assignment priority for "+id);
                Expect(m.FreeWorkers+m.slots.Sum(plot=>plot.workers)==m.workers&&m.slots.All(plot=>plot.workers<=Manor.StaffLimit(plot.kind)),"invalid capacity");
                var unchanged=m.Export();
                m.AssignPreset(id); Reject(()=>m.AssignPreset("unknown"));
                Expect(m.Export()==unchanged,"assignment was not deterministic and atomic");
            }
            CheckPlan("food",0,0,3,3,2,0,0,0,0);
            CheckPlan("smithing",0,0,1,0,0,1,3,3,0);
            CheckPlan("balanced",1,0,1,1,0,1,2,2,0);
            var small=new Manor(); small.AssignPreset("balanced");
            Expect(small.slots[0].workers==2 && small.FreeWorkers==0,"missing production plots");
        });

        check("buy five and sell five share exact quotes and reject an insufficient remaining quantity atomically",() => {
            var m=new Manor { coin=500,timber=200 };
            m.SetDecree("trade");
            int purse=Purse(m), food=m.food;
            m.Trade("food",5); m.Trade("food",-5);
            Expect(m.food==food&&Purse(m)==purse-250&&m.market.food.bought==5&&m.market.food.sold==5,"batch trade rounding or spread");
            m.Trade("food",11); m.Trade("food",-11);
            var before=m.Export();
            Expect(m.QuoteTrade("food").buyLimit==4&&m.QuoteTrade("food").sellLimit==4,"batch quota threshold");
            Reject(()=>m.Trade("food",5)); Reject(()=>m.Trade("food",-5));
            Expect(m.Export()==before,"partial batch trade");
        });

        check("recommendations are deterministic and balance roles within stock, budget and emergency supply",() => {
            var m=EstablishedEstate();
            m.cleared=3; m.approval=39;
            foreach(string id in Manor.HeroIds)m.HireHero(10,data.Heroes[id]);
            m.heroes["hero_commander"].restUntil=m.day+1;
            m.stock["spear"]=4; m.stock["crossbow"]=2; m.stock["cavalry"]=1; m.stock["spikes"]=4;
            var plan=m.RecommendForce(data,3,false);
            Expect(m.EnemyThreat(data,3,false)==ManorThreat.Mounted&&m.EnemyThreat(data,2,false)==ManorThreat.Armored&&
                m.EnemyThreat(data,0,false,true)==ManorThreat.Ranged,"counter preview");
            Expect(plan.ContainsKey("hero_vanguard")&&plan.ContainsKey("hero_ranger")&&plan.ContainsKey("spear")&&
                !plan.ContainsKey("hero_commander")&&!plan.ContainsKey("spikes"),"recommendation roles or availability");
            m.ValidateForce(data,plan,true);
            Expect(Json.Write(plan)==Json.Write(m.RecommendForce(data,3,false)),"unstable recommendation");
            m.food=0;
            var patrol=m.RecommendForce(data,0,false,true);
            Expect(Manor.ForceSize(data,patrol)==3,"emergency patrol limit");
            m.ValidateForce(data,patrol,true,true);
            var empty=new Manor();
            empty.stock["militia"]=0; empty.stock["archer"]=0;
            Expect(empty.RecommendForce(data,0,true).Count==0,"invented recruits");
        });

        check("v3 migration preserves all progress, policy, approval, fractions and market counters",() => {
            var m=EstablishedEstate();
            m.day=18; m.eventDay=18; m.approval=78; m.workers=8; m.cleared=3; m.raidReady=true;
            m.SetTax("high"); m.SetDecree("trade");
            m.HireHero(10,data.Heroes["hero_vanguard"]);
            m.heroes["hero_vanguard"].restUntil=m.day+2;
            m.remainder.food=37;
            m.Trade("food",5); m.Trade("timber",-5);
            var old=LegacySchema(m,3);
            var migrated=Manor.Import(Json.Write(old));
            var preserved=LegacySchema(migrated,3);
            foreach(string key in old.Keys)Expect(Json.Write(preserved[key])==Json.Write(old[key]),"migration changed "+key);
            Expect(migrated.version==Manor.SaveVersion&&migrated.eventDay==0&&migrated.ActiveEvent==null&&migrated.HeroRest("hero_vanguard")==2,
                "v3 did not migrate smoothly");
            string v3=Json.Write(old);
            Reject(()=>Manor.Import(v3.Replace("\"bought\":5","\"bought\":21")));
            old.Add("eventDay",0);
            Reject(()=>Manor.Import(Json.Write(old)));
        });

        check("saves reject malformed event state, wrong season, extra and duplicate fields",() => {
            var m=new Manor { day=43,eventDay=42 };
            string valid=m.Export();
            Expect(Manor.Import(valid).ActiveEvent?.title=="寒潮来袭","valid event import");
            foreach(string bad in new[]{ "-1","6","43","54","3.5","null","\"42\"","{}" })
                Reject(()=>Manor.Import(valid.Replace("\"eventDay\":42","\"eventDay\":"+bad)));
            Reject(()=>Manor.Import(valid.Replace("\"eventDay\":42","\"eventDay\":42,\"eventDay\":0")));
            Reject(()=>Manor.Import(valid.Replace("\"eventDay\":42","\"eventDay\":42,\"eventId\":\"winter\"")));
            Reject(()=>Manor.Import(valid.Replace(",\"eventDay\":42","").Replace("\"eventDay\":42,","")));
            m.eventDay=30;
            Reject(()=>m.Export());
        });
    }
}
