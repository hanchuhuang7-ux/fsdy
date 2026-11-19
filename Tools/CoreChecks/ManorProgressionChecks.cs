using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;

static partial class ManorChecks
{
    static Result PatrolOutcome(Manor.Sortie sortie,int defeated)
    {
        var result=Outcome(sortie,"draw",sortie.config.red.Count);
        result.reason="withdraw";
        for(int i=0;i<sortie.config.blue.Count;i++)
            result.report.units.Add(new UnitReport{id=sortie.config.red.Count+1+i,team="blue",type=sortie.config.blue[i].type,alive=i>=defeated});
        return result;
    }

    public static void RunProgression(Catalog data,Action<string,Action> check)
    {
        check("frontier elite ranks unlock only after a full single sweep and keep the original default guards",() => {
            var m=new Manor();
            var force=Party(2,0);
            Expect(m.frontierTier==1&&m.MaxFrontierTier==1,"initial rank");
            string initial=m.Export();
            Reject(()=>m.SelectFrontierTier(0));Reject(()=>m.SelectFrontierTier(2));Reject(()=>m.SelectFrontierTier(3));
            Expect(m.Export()==initial,"failed tier select changed the estate");
            var basePatrol=m.PrepareFrontier(data,force);
            Expect(basePatrol.FrontierTier==1&&basePatrol.config.seed==1418&&basePatrol.config.blue.Count==15,"base patrol identity");
            for(int i=0;i<FrontierMap.Guards.Count;i++)
            {
                var guard=FrontierMap.Guards[i];
                var placed=basePatrol.config.blue[i];
                Expect(placed.type==guard.unit&&placed.level==0&&placed.x==guard.spawn.x&&placed.z==guard.spawn.z,"base guard changed");
            }
            m.Settle(basePatrol,PatrolOutcome(basePatrol,14));
            Expect(m.MaxFrontierTier==1,"partial patrol unlocked elite ranks");
            var next=m.PrepareFrontier(data,force);
            var result=m.ReturnedState(next,PatrolOutcome(next,15),out var receipt);
            Expect(receipt.frontierTier==1&&m.MaxFrontierTier==1&&result.MaxFrontierTier==3,"sweep was not committed atomically");
            m=Manor.Import(result.Export());
            m.SelectFrontierTier(2);m.SelectFrontierTier(3);m.SelectFrontierTier(1);
            Expect(Manor.Import(m.Export()).MaxFrontierTier==3,"unlocked ranks were lost");
        });

        check("frontier ranks scale live PhysX-independent stats and snapshot the reward rate at launch",() => {
            for(int tier=1;tier<=3;tier++)
            {
                var m=new Manor {frontierSwept=true};
                m.SelectFrontierTier(tier);
                var sortie=m.PrepareFrontier(data,Party(2,0));
                var units=sortie.config.blue;
                Expect(units.Count==15&&units.All(p=>p.level==(tier==1?0:tier)),"incorrect guard levels");
                Expect(units.Select(p=>p.type).SequenceEqual(FrontierMap.Roster(tier))&&
                    (tier<3||units.Count(p=>p.type=="giant")==2),"incorrect elite guard composition");
                using(var battle=new Battle(data,sortie.config,new ReferenceFactory(),true))
                {
                    for(int i=0;i<units.Count;i++)
                    {
                        var guard=battle.Units[sortie.config.red.Count+i];
                        var expected=data.Unit(units[i].type).AtLevel(units[i].level);
                        Expect(guard.entity.maxHp==expected.hp&&guard.definition.damage==expected.damage&&
                            guard.definition.heal==expected.heal,"elite stats did not reach battle entities");
                    }
                }
                m.SelectFrontierTier(tier==1?3:1);
                var next=m.ReturnedState(sortie,PatrolOutcome(sortie,15),out var receipt);
                var awards=(tier,receipt.coin,receipt.timber,receipt.arms,receipt.approvalAfter);
                var expectedAwards=tier==1?(1,45,15,8,66):tier==2?(2,67,22,12,69):(3,90,30,16,72);
                Expect(awards==expectedAwards&&receipt.frontierTier==tier&&next.coin==140+receipt.coin,"patrol paid the wrong rank");
            }
        });

        check("elite levels stay scoped to exact frontier rosters and cannot enter a duel or expedition",() => {
            var m=new Manor {frontierSwept=true};
            m.SelectFrontierTier(3);
            var patrol=m.PrepareFrontier(data,Party(2,0));
            Reject(()=>data.Validate(patrol.config));
            var forged=Json.Clone(patrol.config);forged.blue[0].level=2;
            Reject(()=>data.Validate(forged,true));
            forged=Json.Clone(patrol.config);forged.blue[0].type="catapult";
            Reject(()=>data.Validate(forged,true));
            forged=Json.Clone(patrol.config);forged.blue[0].x+=.01f;
            Reject(()=>data.Validate(forged,true));
            var expedition=m.Prepare(data,0,false,Party(2,0)).config;
            Expect(expedition.blue.All(p=>p.level==0),"guard level leaked to the campaign");
            expedition.blue[0].level=2;
            Reject(()=>data.Validate(expedition,true));
            var arena=data.Default();arena.blue[0].level=2;
            Reject(()=>data.Validate(arena));
        });

        check("opening guidance advances in order and persists completion or dismissal",() => {
            var m=new Manor();
            Expect(m.GuideActive&&!m.CompleteGuideStep(1)&&!m.CompleteGuideStep(0),"guide advanced without farming");
            m.Build(2,"farm");Expect(!m.CompleteGuideStep(0),"idle farm completed guidance");
            m.Assign(2,1);
            Expect(m.CompleteGuideStep(0)&&!m.CompleteGuideStep(2)&&m.CompleteGuideStep(1)&&m.CompleteGuideStep(2),"ordered guide");
            m=Manor.Import(m.Export());
            Expect(!m.GuideActive&&m.guideStep==3&&!m.guideDismissed,"completed guide was lost");
            var skip=new Manor();Expect(skip.DismissGuide()&&!skip.CompleteGuideStep(0),"dismissal allowed further steps");
            Expect(Manor.Import(skip.Export()).guideDismissed,"dismissal did not persist");
        });

        check("v1–v6 preserve their entire save payload and default to regular patrols with the new game guide dismissed",() => {
            var source=EstablishedEstate();
            source.day=42;source.cleared=3;source.workers=9;source.eventDay=42;source.approval=82;
            source.SetTax("high");source.SetDecree("trade");
            source.Build(14,"granary");source.Upgrade(14);source.Assign(14,1);
            source.Build(17,"chapel");source.Upgrade(17);source.Assign(17,1);
            source.UpgradeSpecialist("steward");source.Trade("food",8);source.Trade("timber",-3);
            source.ReclaimPlot(18);source.ReclaimPlot(23);source.Build(23,"farm");source.Assign(23,1);
            source.ClaimMilestone(0);source.frontierSwept=true;
            source.SelectFrontierTier(3);
            for(int version=1;version<=6;version++)
            {
                var old=LegacySchema(source,version);
                var loaded=Manor.Import(Json.Write(old));
                var projected=LegacySchema(loaded,version);
                foreach(string key in old.Keys)Expect(Json.Write(old[key])==Json.Write(projected[key]),"legacy field changed "+key);
                Expect(loaded.version==Manor.SaveVersion&&loaded.frontierTier==1&&loaded.guideStep==0&&loaded.guideDismissed&&
                    loaded.MaxFrontierTier==(version==6?3:1),"migration defaults "+version);
            }
        });

        check("current saves reject invalid tier and guidance schema and round trip",() => {
            var m=new Manor();
            var save=m.Export();
            Expect(Manor.Import(save).Export()==save,"round trip");
            string Field(string key,string raw) => Json.Write(key)+":"+raw;
            void RejectField(string key,string original,string invalid)=>
                Reject(()=>Manor.Import(save.Replace(Field(key,original),Field(key,invalid))));
            foreach(string bad in new[]{"0","2","4","1.5","null"})RejectField("frontierTier","1",bad);
            foreach(string bad in new[]{"-1","4","false","null"})RejectField("guideStep","0",bad);
            RejectField("guideDismissed","false","0");
            RejectField("guideDismissed","false","false,"+Field("guideDismissed","true"));
            var fields=(Dictionary<string,object>)Json.Parse(save);
            fields.Remove("guideStep");Reject(()=>Manor.Import(Json.Write(fields)));
            fields.Add("guideStep",0);fields.Add("guideVersion",1);Reject(()=>Manor.Import(Json.Write(fields)));
            m.frontierTier=3;Reject(()=>m.Export());
        });
    }
}
