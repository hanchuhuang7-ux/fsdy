using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;

static partial class ManorChecks
{
    public static void RunExpansion(Catalog data,Action<string,Action> check)
    {
        check("outer reclamation costs eight coins and returns four timber atomically",() => {
            var m=new Manor {coin=7};
            m.remainder.timber=23;
            Expect(Enumerable.Range(0,18).All(m.IsPlotCleared)&&!Enumerable.Range(18,6).Any(m.IsPlotCleared),"initial outer gates");
            string before=m.Export();
            Reject(()=>m.Build(18,"farm"));Reject(()=>m.ReclaimPlot(18));Reject(()=>m.ReclaimPlot(17));Reject(()=>m.ReclaimPlot(24));
            Expect(m.Export()==before,"a refused plot operation spent resources");
            m.coin=80;
            m.ReclaimPlot(18);
            Expect(m.IsPlotCleared(18)&&!m.IsPlotCleared(19)&&m.coin==72&&m.timber==84&&m.remainder.timber==23,"reclamation balance");
            m.Build(18,"farm");m.Upgrade(18);m.Assign(18,1);
            m=Manor.Import(m.Export());
            Expect(m.slots[18].kind=="farm"&&m.slots[18].level==2&&m.slots[18].workers==1,"outer build did not persist");
            m.Demolish(18);
            Expect(m.IsPlotCleared(18)&&m.slots[18].kind==null,"demolition returned cleared land to woods");
            before=m.Export();Reject(()=>m.ReclaimPlot(18));
            Expect(m.Export()==before,"repeat reclamation spent resources");
        });

        check("all six milestones enforce their own thresholds and grant the exact reward once",() => {
            var m=new Manor {coin=1500,food=150,timber=1500,arms=30};
            Expect(m.ReadyMilestones==0,"founding estate already qualified");
            Reject(()=>m.ClaimMilestone(-1));Reject(()=>m.ClaimMilestone(6));
            void Claim(int index)
            {
                var reward=Manor.Milestones[index];
                int coin=m.coin,food=m.food,timber=m.timber,arms=m.arms,approval=m.approval;
                Expect((m.ReadyMilestones&(1<<index))!=0,"milestone unavailable "+index);
                m.ClaimMilestone(index);
                Expect(m.MilestoneClaimed(index)&&m.coin==coin+reward.coin&&m.food==food+reward.food&&m.timber==timber+reward.timber&&
                    m.arms==arms+reward.arms&&m.approval==Math.Min(100,approval+reward.approval),"incorrect milestone award "+index);
                string claimed=m.Export();
                m=Manor.Import(claimed);
                Reject(()=>m.ClaimMilestone(index));
                Expect(m.Export()==claimed,"repeat milestone award "+index);
            }
            m.ReclaimPlot(18);Reject(()=>m.ClaimMilestone(0));
            m.ReclaimPlot(23);Claim(0);
            m.Build(1,"cottage");m.Upgrade(1);m.Upgrade(1);
            m.Build(2,"cottage");m.Upgrade(2);m.Upgrade(2);
            m.workers=7;m.approval=75;Reject(()=>m.ClaimMilestone(1));
            m.workers=8;m.approval=74;Reject(()=>m.ClaimMilestone(1));
            m.approval=75;Claim(1);

            m.Build(3,"farm");m.Assign(3,1);
            m.Build(4,"pasture");m.Assign(4,1);m.Build(5,"granary");
            Reject(()=>m.ClaimMilestone(2));
            m.Assign(5,1);Claim(2);

            m.Build(6,"timber_camp");m.Assign(6,1);
            m.Build(7,"smithy");m.Assign(7,1);
            m.Build(8,"chapel");Reject(()=>m.ClaimMilestone(3));
            m.Build(9,"barracks");Claim(3);

            m.workers=12;m.cleared=2;Reject(()=>m.ClaimMilestone(4));
            m.cleared=3;Claim(4);
            m.cleared=5;Reject(()=>m.ClaimMilestone(5));
            var last=m.Prepare(data,5,false,Party(1,0));
            m.Settle(last,Outcome(last,"red",1));
            m.approval=98;Claim(5);
            Expect(m.milestoneClaims==63&&m.ReadyMilestones==0,"completed ledger flags");
        });

        check("a single complete patrol qualifies the final milestone and partial patrols never accumulate",() => {
            var m=new Manor();
            foreach(int defeated in new[]{8,7,15})
            {
                var sortie=m.PrepareFrontier(data,Party(2,0));
                var result=Outcome(sortie,"draw",2);result.reason="withdraw";
                for(int i=0;i<sortie.config.blue.Count;i++)
                    result.report.units.Add(new UnitReport{id=i+3,team="blue",type=sortie.config.blue[i].type,alive=i>=defeated});
                var next=m.ReturnedState(sortie,result,out var receipt);
                Expect(!m.frontierSwept&&next.frontierSwept==(defeated==15)&&receipt.defeated==defeated,"sweep flag");
                m=Manor.Import(next.Export());
                if(defeated<15)Reject(()=>m.ClaimMilestone(5));
            }
            Expect((m.ReadyMilestones&(1<<5))!=0&&m.cleared==0,"patrol qualification did not survive import");
            m.ClaimMilestone(5);
            Expect(Manor.Import(m.Export()).MilestoneClaimed(5),"patrol medal was not saved");
        });

        check("v1–v5 preserve all historical progress and receive six uncleared outer plots",() => {
            var source=EstablishedEstate();
            source.workers=8;source.day=42;source.eventDay=42;source.approval=86;source.cleared=3;source.raidReady=true;
            source.remainder.coin=12;source.remainder.food=37;source.SetTax("high");source.SetDecree("trade");
            source.HireHero(10,data.Heroes["hero_vanguard"]);source.heroes["hero_vanguard"].restUntil=44;
            source.Build(13,"barracks");source.Assign(13,1);source.Build(14,"granary");source.Upgrade(14);source.Assign(14,1);
            source.Build(17,"chapel");source.Upgrade(17);source.Upgrade(17);source.Assign(17,1);
            source.UpgradeSpecialist("steward");source.Trade("food",8);source.Trade("timber",-4);
            for(int version=1;version<=5;version++)
            {
                var old=LegacySchema(source,version);
                var migrated=Manor.Import(Json.Write(old));
                var comparable=LegacySchema(migrated,version);
                foreach(string key in old.Keys)
                    Expect(Json.Write(old[key])==Json.Write(comparable[key]),"v"+version+" lost "+key);
                Expect(migrated.version==Manor.SaveVersion&&migrated.slots.Length==Manor.PlotCount&&migrated.reclaimedPlots==0&&migrated.milestoneClaims==0&&
                    !migrated.frontierSwept&&Enumerable.Range(0,18).All(migrated.IsPlotCleared)&&
                    migrated.slots.Skip(18).All(p=>p.kind==null&&p.level==0&&p.workers==0),"v6 migration defaults");
                if(version==5)Expect(migrated.slots[14].workers==1&&migrated.slots[17].level==3&&
                    migrated.SpecialistLevel("steward")==2&&migrated.ActiveEvent?.title=="寒潮来袭"&&
                    migrated.HeroRest("hero_vanguard")==2,"v5 civic progress");
            }
        });

        check("the current schema rejects bad expansion flags, impossible rewards and construction on uncleared land",() => {
            var m=new Manor();
            m.ReclaimPlot(18);m.Build(18,"farm");m.ReclaimPlot(23);m.ClaimMilestone(0);
            string save=m.Export();
            Expect(Manor.Import(save).Export()==save,"save changed round trip");
            string RawField(string key,string value) => Json.Write(key)+":"+value;
            void RejectField(string key,string original,string invalid) =>
                Reject(()=>Manor.Import(save.Replace(RawField(key,original),RawField(key,invalid))));
            foreach(string value in new[]{"-1","1.5","64","null","true",Json.Write("33"),"0","1"})
                RejectField("reclaimedPlots","33",value);
            foreach(string value in new[]{"-1","64","1.5","{}","33"})
                RejectField("milestoneClaims","1",value);
            RejectField("frontierSwept","false","1");
            RejectField("frontierSwept","false","false,"+RawField("frontierSwept","true"));
            var fields=(Dictionary<string,object>)Json.Parse(save);
            fields.Add("extra",0);Reject(()=>Manor.Import(Json.Write(fields)));
            fields.Remove("extra");fields.Remove("milestoneClaims");Reject(()=>Manor.Import(Json.Write(fields)));
            fields.Add("milestoneClaims",1);
            ((List<object>)fields["slots"]).RemoveAt(23);
            Reject(()=>Manor.Import(Json.Write(fields)));
            m.reclaimedPlots=0;Reject(()=>m.Export());
        });
    }
}
