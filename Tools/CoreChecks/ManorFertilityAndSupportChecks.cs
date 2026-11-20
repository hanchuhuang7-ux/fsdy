using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;

static partial class ManorChecks
{
    public static void RunFertilityAndSupport(Catalog data,Action<string,Action> check)
    {
        check("crop fertility scales fixed-point harvest and auto rests for winter and below eighty",()=>{
            var manor=new Manor { day=12 };
            manor.Build(1,"farm"); manor.Assign(1,1);
            Expect(manor.slots[1].fertility==100&&manor.slots[1].rotation=="auto"&&manor.Forecast().food==400,"new field or baseline");
            manor.slots[1].fertility=80;
            Expect(!manor.IsRestingField(1)&&manor.Forecast().food==300,"eighty was not a growing field");
            manor.Advance(1);
            Expect(manor.Season==1&&manor.slots[1].fertility==72&&manor.IsRestingField(1)&&manor.Forecast().food==-100,"auto did not rest the next season");
            manor.Advance(Manor.SeasonDays);
            Expect(manor.Season==2&&manor.slots[1].fertility==97&&!manor.IsRestingField(1),"one season of recovery");
            manor.Advance(Manor.SeasonDays);
            Expect(manor.Season==3&&manor.slots[1].fertility==89&&manor.IsRestingField(1),"winter soil cover");
            manor.Advance(Manor.SeasonDays);
            Expect(manor.Season==0&&manor.slots[1].fertility==114&&!manor.IsRestingField(1),"winter recovery");
            manor.approval=60;
            Expect(manor.Forecast().food==470,"rich farm output did not use fertility percent");
        });

        check("intensive wears down to sixty while fallow recovers to 120 with one staffed pasture bonus",()=>{
            var manor=new Manor { day=48,food=0 };
            manor.Build(1,"irrigated_field"); manor.Build(2,"farm"); manor.Build(3,"pasture");
            manor.Build(4,"pasture");
            manor.SetRotation(1,"intensive"); manor.Assign(1,1);
            manor.SetRotation(2,"fallow");
            manor.slots[1].fertility=65; manor.slots[2].fertility=70;
            Expect(manor.Forecast().food==160&&!manor.IsRestingField(1)&&manor.IsRestingField(2),"intensive winter harvest");
            manor.Advance(1);
            Expect(manor.slots[1].fertility==60&&manor.slots[2].fertility==95,"idle pasture affected recovery or crop floor");
            manor.Assign(3,1);
            manor.Advance(12);
            Expect(manor.slots[1].fertility==60&&manor.slots[2].fertility==120,"staffed pasture recovery or cap");
            manor.SetRotation(1,"auto"); manor.SetRotation(2,"auto");
            manor.slots[2].fertility=85;
            manor.Advance(12);
            Expect(manor.slots[1].fertility==90&&manor.slots[2].fertility==90,"pasture should add five to both auto fields");
            manor.Demolish(2); manor.Build(2,"farm");
            Expect(manor.slots[2].fertility==100&&manor.slots[2].rotation=="auto","new soil after rebuilding");
        });

        check("retinue recruits independently at 2/4/6 berths and unlocks two armor increases atomically",()=>{
            var manor=new Manor { coin=2000,timber=1000,arms=90,cleared=3 };
            var stock=Json.Write(manor.stock);
            foreach(var limit in new[]{2,4,6})
            {
                Expect(manor.RetinueLimit==limit,"hall retinue capacity");
                while(manor.retinue.count<limit) manor.RecruitGuard();
                string full=manor.Export();
                Reject(manor.RecruitGuard);
                Expect(full==manor.Export(),"full berths charged for a guard");
                if(limit<6)manor.Upgrade(0);
            }
            int coins=manor.coin, weapons=manor.arms;
            manor.UpgradeRetinue(); manor.UpgradeRetinue();
            Reject(manor.UpgradeRetinue);
            Expect(manor.retinue.level==3&&manor.RetinueBattleLevel==5&&manor.coin==coins-74&&manor.arms==weapons-16&&
                Json.Write(manor.stock)==stock,"armor or standing-army ledger");
            var expedition=manor.Prepare(data,0,false,Party(0,0),new ManorSupport(6,false));
            Expect(expedition.config.red.Select(x=>x.type).SequenceEqual(new[]{"shield","spear","cavalry","shield","spear","cavalry"})&&
                expedition.config.red.All(x=>x.level==5),"retinue combat role or armor level");
        });

        check("sortie provenance distinguishes guards, mercenaries and identical regulars without changing arena validation",()=>{
            var manor=new Manor { coin=1000,arms=30,influence=50 };
            manor.RecruitGuard(); manor.RecruitGuard(); manor.UpgradeRetinue();
            manor.stock["shield"]=1;
            var normal=new Dictionary<string,int>{{"shield",1}};
            var support=new ManorSupport(2,true);
            var sortie=manor.Prepare(data,0,false,normal,support);
            Expect(sortie.config.red.Count==6&&sortie.config.red[0].type=="shield"&&sortie.config.red[0].level==1&&
                sortie.config.red[1].type=="shield"&&sortie.config.red[1].level==4&&sortie.config.red[3].level==3,"mixed levels");
            Reject(()=>data.Import(Json.Write(sortie.config)));
            var formation=Json.Clone(sortie.config);
            formation.manorHall=0;
            Reject(()=>data.Validate(formation));
            (formation.red[0],formation.red[1])=(formation.red[1],formation.red[0]);
            formation.manorHall=sortie.config.manorHall;
            Expect(!sortie.Matches(formation)&&sortie.Matches(Json.Clone(sortie.config)),"guard/regular roster substitution");
            manor.food=0;
            Reject(()=>manor.PrepareFrontier(data,normal,support));
            Reject(()=>manor.Prepare(data,0,true,normal,support));
            manor.food=100;
            var raid=manor.Prepare(data,0,true,normal,new ManorSupport(2,false));
            Expect(raid.raid&&raid.config.red.Count==3,"defensive muster lost guards");

            var outcome=Outcome(sortie,"red",6);
            outcome.report.units[1].alive=false;
            outcome.report.units[3].alive=false;
            Reject(()=>manor.Settle(sortie,outcome));
            Expect(manor.SignMercenaryContract(sortie),"contract was not signed");
            var next=manor.ReturnedState(sortie,outcome,out var receipt);
            Expect(next.Stock("shield")==1&&receipt.troops["shield"].survivors==1&&receipt.guardLosses==1&&
                next.retinue.count==2&&next.RetinueReady==1&&receipt.mercenaries==3&&receipt.mercenaryLosses==1&&
                next.Stock("crossbow")==0&&receipt.influence==Manor.ExpeditionInfluence,"allied losses leaked into the wrong ledger");
            Reject(()=>manor.Settle(sortie,outcome));
            int gold=next.coin;
            next.RefillRetinue();
            Expect(next.coin==gold-Manor.GuardRefillCoin&&next.RetinueReady==2&&next.retinue.level==2,"retinue replenishment");
        });

        check("mercenary contracts reserve roster limits and pay both currencies once at launch",()=>{
            var manor=new Manor { coin=1000,influence=40 };
            var support=new ManorSupport(0,true);
            var before=manor.Export();
            Reject(()=>manor.Prepare(data,0,false,Party(),support));
            var roster=manor.RecommendForce(data,0,false,true,support);
            Expect(Manor.ForceSize(data,roster,support)<=manor.FrontierLimit,"recommendation consumed the reserved seats");
            var draft=manor.PrepareFrontier(data,roster,support);
            Expect(manor.Export()==before,"planning charged a contract");
            manor.coin=Manor.MercenaryCoin-1;
            var poor=manor.Export();
            Reject(()=>manor.SignMercenaryContract(draft));
            Expect(manor.Export()==poor,"failed contract partially debited");
            manor.coin=1000;
            Expect(manor.SignMercenaryContract(draft)&&!manor.SignMercenaryContract(draft)&&
                manor.coin==1000-Manor.MercenaryCoin&&manor.influence==40-Manor.MercenaryCost,"duplicate or incorrect debit");
            var outcome=PatrolOutcome(draft,0);
            var beforeReturn=manor.Export();
            var malformed=PatrolOutcome(draft,0);
            malformed.report.units[0].id=99;
            Reject(()=>manor.Settle(draft,malformed));
            Expect(manor.Export()==beforeReturn,"bad report mutated the signed ledger");
            var result=manor.Settle(draft,outcome);
            Expect(result.mercenaries==3&&manor.influence==10&&manor.coin==1000-Manor.MercenaryCoin,"return charged the contract again");
        });

        check("daily tithe consumes a tenth of net grain, protects the reserve and stops at the influence cap",()=>{
            var manor=new Manor { food=0 };
            manor.Build(1,"farm"); manor.Assign(1,1);
            Expect(manor.tithe==0&&manor.Forecast().titheFood==0,"tithe did not default off");
            manor.SetTithe(10);
            Expect(manor.Forecast().titheFood==0,"donated from household emergency rations");
            manor.food=30;
            var f=manor.Forecast();
            Expect(f.titheFood==1&&f.food==300&&f.influence==1&&manor.influence==0,"whole-ration minimum or forecast mutation");
            manor.Advance(1);
            Expect(manor.food==33&&manor.influence==1,"tithe did not settle exactly once");
            manor.Build(2,"irrigated_field"); manor.Assign(1,-1);
            manor.Assign(2,1); manor.Assign(2,1); manor.Upgrade(2); manor.Upgrade(2); manor.SetDecree("agrarian");
            manor.influence=998;
            f=manor.Forecast();
            Expect(f.titheFood==2&&f.food==2023,"larger net did not use the ten-percent rate");
            manor.Advance(1);
            Expect(manor.influence==999&&manor.Forecast().titheFood==0,"capped influence continued accepting the tithe");
            Reject(()=>manor.SetTithe(5));
        });

        check("civic privileges and victories update influence and chronicle exactly once",()=>{
            var manor=new Manor { coin=2000,influence=19,approval=95 };
            manor.Trade("food",5); manor.Trade("timber",-1); manor.Trade("arms",3);
            string before=manor.Export();
            Reject(manor.HoldFestival); Reject(manor.CallMerchant);
            Expect(manor.Export()==before,"failed privilege changed state");
            manor.influence=45;
            manor.HoldFestival(); manor.CallMerchant();
            Expect(manor.approval==100&&manor.influence==0&&manor.chronicle.Last().kind=="festival"&&
                new[]{"food","timber","arms"}.All(id=>manor.QuoteTrade(id).buyRemaining==manor.MarketSeasonLimit&&
                    manor.QuoteTrade(id).sellRemaining==manor.MarketSeasonLimit),"festival or merchant privilege");
            var war=manor.Prepare(data,0,false,Party(1,0));
            Expect(manor.Settle(war,Outcome(war,"red",1)).influence==12,"expedition merit");
            var raid=manor.Prepare(data,0,true,Party(1,0));
            Expect(manor.Settle(raid,Outcome(raid,"red",1)).influence==8,"raid merit");
            var incomplete=manor.PrepareFrontier(data,Party(1,0));
            Expect(manor.Settle(incomplete,PatrolOutcome(incomplete,14)).influence==0,"partial patrol paid merit");
            var patrol=manor.PrepareFrontier(data,Party(1,0));
            manor.influence=990;
            var receipt=manor.Settle(patrol,PatrolOutcome(patrol,15));
            var kinds=manor.chronicle.Skip(1).Select(x=>x.kind);
            Expect(receipt.influence==9&&manor.influence==999&&kinds.SequenceEqual(new[]{"expedition","raid","frontier"})&&
                Manor.ChronicleText(manor.chronicle.Last()).Contains("威望 +9"),"full patrol or capped merit in chronicle");
            Expect(Manor.Import(manor.Export()).chronicle.Count==4,"merit chronology roundtrip");
        });

        check("v1-v8 preserve their projected payload with default crop, retinue and tithe state",()=>{
            var manor=new Manor { coin=1000,timber=600,food=200,cleared=3,day=41,workers=4 };
            manor.Build(1,"farm"); manor.Assign(1,1); manor.Upgrade(1);
            manor.Build(2,"cottage"); manor.Upgrade(2); manor.SetBackyard(2,"poultry");
            manor.Build(3,"cottage"); manor.Build(4,"farm"); manor.Build(5,"timber_camp");
            manor.Upgrade(0); manor.ReclaimPlot(18); manor.ReclaimPlot(19); manor.Trade("food",2);
            manor.ClaimMilestone(0); manor.UnlockDevelopment(ManorPerk.HeavyPlough);
            manor.remainder.food=42;
            for(int version=1;version<=8;version++)
            {
                var old=LegacySchema(manor,version);
                var migrated=Manor.Import(Json.Write(old));
                var projected=LegacySchema(migrated,version);
                foreach(var item in old)Expect(Json.Write(item.Value)==Json.Write(projected[item.Key]),"v"+version+" lost "+item.Key);
                Expect(migrated.version==Manor.SaveVersion&&migrated.slots[1].fertility==100&&migrated.slots[1].rotation=="auto"&&
                    migrated.retinue.count==0&&migrated.retinue.level==1&&migrated.retinue.wounded==0&&migrated.influence==0&&migrated.tithe==0,"crop and retinue defaults");
                string save=migrated.Export();
                Expect(Manor.Import(save).Export()==save,"current roundtrip after v"+version);
            }
        });

        check("current export and import reject malformed crop, tithe, influence and retinue state",()=>{
            var manor=new Manor();
            manor.Build(1,"farm");
            string valid=manor.Export();
            Dictionary<string,object> Raw()=> (Dictionary<string,object>)Json.Parse(valid);
            void Field(int index,string name,object value)
            {
                var raw=Raw();
                var plot=(Dictionary<string,object>)((List<object>)raw["slots"])[index];
                plot[name]=value;
                Reject(()=>Manor.Import(Json.Write(raw)));
            }
            void Root(string name,object value)
            {
                var raw=Raw(); raw[name]=value;
                Reject(()=>Manor.Import(Json.Write(raw)));
            }
            Field(1,"fertility",59); Field(1,"fertility",121); Field(1,"fertility",100.5);
            Field(1,"rotation","crop-menu"); Field(1,"rotation",null); Field(0,"rotation","auto"); Field(0,"fertility",100);
            Root("retinue",new {count=3,level=2,wounded=0});
            Root("retinue",new {count=1,level=1,wounded=2});
            Root("retinue",new {count=0,level=2,wounded=0});
            Root("retinue",new {count=1,level=4,wounded=0});
            Root("tithe",5); Root("influence",1000); Root("influence",-1);
            var incomplete=Raw(); incomplete.Remove("influence");
            Reject(()=>Manor.Import(Json.Write(incomplete)));
            manor.slots[1].fertility=121; Reject(()=>manor.Export());
            manor.slots[1].fertility=120; manor.retinue.count=3; Reject(()=>manor.Export());
        });
    }
}
