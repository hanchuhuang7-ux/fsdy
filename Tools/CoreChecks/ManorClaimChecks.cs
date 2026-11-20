using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;

static partial class ManorChecks
{
    public static void RunClaims(Catalog data,Action<string,Action> check)
    {
        check("patrol settlement unlocks whole camps by guard identity and keeps every tier replayable",()=>{
            var manor=new Manor { influence=200 };
            var party=Party(1,0);
            var split=manor.PrepareFrontier(data,party);
            var splitReport=PatrolOutcome(split,0);
            foreach(int index in new[]{0,1,5,10,11})splitReport.report.units[index+1].alive=false;
            Expect(manor.Settle(split,splitReport).newSectors==0&&manor.clearedSectors==0,"partial casualties were counted as a camp");

            foreach(int camp in new[]{1,0,2})
            {
                var patrol=manor.PrepareFrontier(data,party);
                var report=PatrolOutcome(patrol,0);
                for(int i=0;i<FrontierMap.GroupSize;i++)report.report.units[1+camp*FrontierMap.GroupSize+i].alive=false;
                report.report.units.Reverse();
                int before=manor.clearedSectors;
                var next=manor.ReturnedState(patrol,report,out var receipt);
                Expect(receipt.defeated==5&&receipt.newSectors==(1<<camp)&&manor.clearedSectors==before&&
                    next.clearedSectors==(before|(1<<camp))&&next.claimedSectors==0,"camp clearance or early mutation");
                Reject(()=>manor.Settle(patrol,report));
                manor=next;
            }
            Expect(manor.clearedSectors==7&&!manor.frontierSwept&&manor.MaxFrontierTier==1,"separate camps unlocked elite tiers");
            manor.ClaimSector(1);
            var sweep=manor.PrepareFrontier(data,party);
            Expect(sweep.config.blue.Count==15,"ownership removed a patrol garrison");
            var forged=PatrolOutcome(sweep,15);
            forged.report.units[1].id=99;
            string save=manor.Export();
            Reject(()=>manor.Settle(sweep,forged));
            Expect(manor.Export()==save,"invalid patrol report changed clearance or stocks");
            var full=manor.Settle(sweep,PatrolOutcome(sweep,15));
            Expect(full.newSectors==0&&full.influence==Manor.FrontierInfluence&&manor.frontierSwept,"repeat sweep loot or first-unlock alert");
            for(int tier=1;tier<=3;tier++)
            {
                manor.SelectFrontierTier(tier);
                var challenge=manor.PrepareFrontier(data,party);
                Expect(challenge.config.blue.Select(unit=>unit.type).SequenceEqual(FrontierMap.Roster(tier)),"claimed camp changed tier roster");
            }
        });

        check("three claims cost both resources once and supply exact fixed-point production",()=>{
            var manor=new Manor { influence=200,timber=130,food=40 };
            manor.Build(1,"farm"); manor.Assign(1,1);
            var before=manor.Forecast();
            int coins=manor.coin, timber=manor.timber;
            Reject(()=>manor.ClaimSector(0));
            manor.clearedSectors=7;
            manor.influence=34;
            string blocked=manor.Export();
            Reject(()=>manor.ClaimSector(0)); Expect(manor.Export()==blocked,"insufficient influence still charged timber");
            manor.influence=200; manor.timber=15;
            blocked=manor.Export();
            Reject(()=>manor.ClaimSector(0)); Expect(manor.Export()==blocked,"insufficient timber charged influence");
            manor.timber=timber;
            for(int i=0;i<3;i++)manor.ClaimSector(i);
            string claimed=manor.Export();
            Reject(()=>manor.ClaimSector(1)); Reject(()=>manor.ClaimSector(3));
            Expect(manor.Export()==claimed&&manor.coin==coins&&manor.influence==65&&manor.timber==timber-60&&manor.claimedSectors==7,
                "claim price, replay, or bitmask");
            var after=manor.Forecast();
            Expect(after.timber-before.timber==150&&after.food-before.food==200&&after.arms-before.arms==100,"daily specialty output");
            Expect(!before.marketNeeds.variedDiet&&after.marketNeeds.variedDiet&&after.approvalFactors.food==2,"game failed to count as an independent food");
            Expect(manor.chronicle.Count==3&&manor.chronicle.Select(Manor.ChronicleText).SequenceEqual(Manor.Sectors.Select(s=>"宣称并筑哨 · "+s.name))&&
                Manor.Import(claimed).Export()==claimed,"claim chronicle or current migration");
            int wood=manor.timber, food=manor.food, arms=manor.arms;
            manor.Advance(1);
            Expect(manor.timber==wood+2&&manor.remainder.timber==50&&manor.food==food+6&&manor.arms==arms+1,"daily production did not settle");
        });

        check("North Glade game pairs with other food and respects consumption and ration availability",()=>{
            var manor=new Manor { clearedSectors=2,claimedSectors=2,food=20 };
            var wild=manor.Forecast();
            Expect(wild.food==100&&!wild.marketNeeds.variedDiet,"one wildlife source was counted twice");
            manor.Build(1,"farm"); manor.Assign(1,1); manor.SetRotation(1,"fallow");
            Expect(!manor.Forecast().marketNeeds.variedDiet,"fallow land counted as food");
            manor.Build(2,"cottage"); manor.SetBackyard(2,"poultry");
            Expect(manor.Forecast().marketNeeds.variedDiet,"poultry and wild game should diversify rations");
            manor.food=0;
            Expect(!manor.Forecast().marketNeeds.variedDiet,"empty larder lit the diet");
        });

        check("manor todos expose only actionable dispatch, development, milestones and claims",()=>{
            var manor=new Manor { coin=1000,timber=200,day=6 };
            Expect(manor.Todos==ManorTodo.None,"empty estate generated todos");
            manor.eventDay=6; manor.prosperityPeak=1;
            manor.ReclaimPlot(18); manor.ReclaimPlot(19); manor.clearedSectors=1;
            Expect(manor.Todos==(ManorTodo.Dispatch|ManorTodo.Development|ManorTodo.Milestone),"unaffordable claim alerted");
            manor.influence=35;
            Expect(manor.Todos==(ManorTodo.Dispatch|ManorTodo.Development|ManorTodo.Milestone|ManorTodo.Claim),"simultaneous priorities were lost");
            manor.ResolveEvent(6,1);
            manor.UnlockDevelopment(ManorPerk.HeavyPlough);
            manor.ClaimMilestone(0);
            Expect(manor.Todos==ManorTodo.Claim,"resolved todo remained");
            manor.ClaimSector(0);
            Expect(manor.Todos==ManorTodo.None,"owned territory still requested attention");
        });

        check("v1-v9 preserve historic estate fields and all three new sectors default locked",()=>{
            var manor=new Manor { coin=800,timber=400,arms=55,food=60,day=42,cleared=3 };
            manor.Build(1,"farm"); manor.Assign(1,1);
            manor.Build(2,"cottage"); manor.SetBackyard(2,"poultry");
            manor.ReclaimPlot(18); manor.ReclaimPlot(19); manor.ClaimMilestone(0);
            manor.frontierSwept=true; manor.SelectFrontierTier(3);
            for(int version=1;version<=9;version++)
            {
                if(version==9)
                {
                    manor.slots[1].fertility=84;
                    manor.SetRotation(1,"intensive");
                    manor.RecruitGuard(); manor.UpgradeRetinue(); manor.retinue.wounded=1;
                    manor.influence=98; manor.SetTithe(10);
                    var raid=manor.Prepare(data,0,true,Party(1,0));
                    manor.Settle(raid,Outcome(raid,"red",1));
                    manor.HoldFestival();
                }
                var old=LegacySchema(manor,version);
                var upgraded=Manor.Import(Json.Write(old));
                var projected=LegacySchema(upgraded,version);
                foreach(var field in old)
                    Expect(Json.Write(field.Value)==Json.Write(projected[field.Key]),"migration lost "+field.Key+" from v"+version);
                Expect(upgraded.version==Manor.SaveVersion&&upgraded.clearedSectors==0&&upgraded.claimedSectors==0&&
                    (upgraded.Todos&ManorTodo.Claim)==0,"old save created a sector entitlement");
                var current=upgraded.Export();
                Expect(Manor.Import(current).Export()==current,"canonical migration from v"+version);
            }
        });

        check("save validation rejects malformed, impossible or obsolete claim records",()=>{
            var manor=new Manor();
            string valid=manor.Export();
            Dictionary<string,object> Raw()=> (Dictionary<string,object>)Json.Parse(valid);
            void Bad(string key,object value)
            {
                var draft=Raw(); draft[key]=value;
                Reject(()=>Manor.Import(Json.Write(draft)));
            }
            Bad("clearedSectors",8); Bad("clearedSectors",-1); Bad("clearedSectors",1.5);
            Bad("claimedSectors",1); Bad("claimedSectors","0");
            Bad("chronicle",new[]{new {day=1,kind="claim",subject=0,choice=0}});
            var missing=Raw(); missing.Remove("clearedSectors");
            Reject(()=>Manor.Import(Json.Write(missing)));
            var old=LegacySchema(manor,9);
            old["claimedSectors"]=0;
            Reject(()=>Manor.Import(Json.Write(old)));
            manor.clearedSectors=1; manor.claimedSectors=2;
            Reject(()=>manor.Export());
        });
    }
}
