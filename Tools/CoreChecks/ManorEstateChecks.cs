using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;

static partial class ManorChecks
{
    public static void RunEstate(Catalog data,Action<string,Action> check)
    {
        check("all 36 save slots and the dock retain the island survey and west bridge abutment",() => {
            var bridge=data.Frontier.platforms.Single();
            Expect(Manor.PlotCount==36&&Manor.PlateauHalfWidth==42&&Manor.PlateauHalfDepth==28&&FrontierMap.IslandWest==-106,"shelf bounds");
            Expect(FrontierMap.BridgeStart==-22&&FrontierMap.Boundary==45&&Math.Abs(bridge.x-11.5f)<.001f&&bridge.hz==3.6f,"bridge abutments");
            Expect(FrontierMap.HasGround(-100,19)&&!FrontierMap.HasGround(-105,23)&&!FrontierMap.HasGround(-17,4)&&
                FrontierMap.HasGround(-17,3.58f)&&FrontierMap.Safe(44.99f,0)&&!FrontierMap.Safe(45,0),"ground or safe line");
            for(int i=0;i<Manor.PlotCount;i++)
            {
                float x=Manor.SlotX(i),z=Manor.SlotZ(i);
                Expect(Manor.PickSlot(x,z)==i&&FrontierMap.HasGround(FrontierMap.ManorX+x,z),"unpickable plot "+i);
                for(int j=0;j<i;j++)
                    Expect(Math.Pow(x-Manor.SlotX(j),2)+Math.Pow(z-Manor.SlotZ(j),2)>4.2*4.2,"ambiguous pick regions");
            }
            var order=FrontierMap.Formation(0,1,new V3(-90,0,16),1);
            Expect(order.z==16&&FrontierMap.HasGround(order.x,order.z),"patrol could not use the expanded shelf");
        });

        check("surveyed continent encloses the lake on every bearing and exceeds twenty times the island area",() => {
            Expect(FrontierMap.ManorArea==4325&&FrontierMap.LakeArea>=21000&&
                FrontierMap.FrontierArea/FrontierMap.ManorArea>=20,"coastline area budget");
            var origin=new V3(FrontierMap.ManorX,0,0);
            for(int i=0;i<FrontierMap.ShoreStations;i++)
            {
                int next=(i+1)%FrontierMap.ShoreStations;
                var lake=(FrontierMap.LakeBank[i]+FrontierMap.LakeBank[next])*.5f;
                var outer=(FrontierMap.OuterBank[i]+FrontierMap.OuterBank[next])*.5f;
                var outward=(lake-origin).Normalized*.15f;
                Expect(FrontierMap.OnMainland(lake.x+outward.x,lake.z+outward.z)&&
                    !FrontierMap.OnMainland(lake.x-outward.x,lake.z-outward.z)&&
                    FrontierMap.OnMainland(outer.x-outward.x,outer.z-outward.z)&&
                    !FrontierMap.HasGround(outer.x+outward.x,outer.z+outward.z),"bank crosses at station "+i);
                var shelf=FrontierMap.RingRoad((i+.5)*2*Math.PI/FrontierMap.ShoreStations,.24f);
                Expect(Catalog.Finite(FrontierMap.SurfaceHeight(shelf.x,shelf.z))&&!FrontierMap.Safe(shelf),"ring terrain or protection is discontinuous");
            }
            Expect(!FrontierMap.HasGround(FrontierMap.ManorX,55)&&!FrontierMap.HasGround(-129,0)&&
                FrontierMap.HasGround(FrontierMap.ManorX,122)&&FrontierMap.Safe(FrontierMap.Rally),
                "island, water and inland coast are not distinct");
        });

        check("patrols skirt the lake on either bank, cross only at the bridge and return to the island",() => {
            var endpoints=new[]{new V3(-92,0,12),FrontierMap.Rally,FrontierMap.CampCenter(0),FrontierMap.CampCenter(1),
                FrontierMap.CampCenter(2),FrontierMap.RingRoad(2.57,.10f),FrontierMap.RingRoad(3.95,.72f),
                FrontierMap.RingRoad(.6,.03f),FrontierMap.RingRoad(4.78,.045f)};
            int marches=0;
            foreach(var start in endpoints)
                foreach(var target in endpoints)
                {
                    if(V3.DistanceXZ(start,target)<.1f)continue;
                    var p=start;
                    var goal=FrontierMap.Formation(0,1,target,1);
                    for(int step=0;step<7500&&V3.DistanceXZ(p,goal)>.12f;step++)
                    {
                        var waypoint=FrontierMap.Waypoint(p,goal,0);
                        if(FrontierMap.IsIsland(p.x,p.z))waypoint=FrontierMap.AroundFence(p,waypoint,FrontierMap.CastleFootprint);
                        var leg=waypoint-p;
                        p+=leg.Normalized*Math.Min(.125f,leg.Planar);
                        if(!FrontierMap.HasGround(p.x,p.z))throw new Exception("march leaves land at "+p.x+","+p.z);
                    }
                    Expect(V3.DistanceXZ(p,goal)<=.12f,"a march did not arrive at "+goal.x+","+goal.z+" from "+start.x+","+start.z+
                        " stalled at "+p.x+","+p.z);
                    marches++;
                }
            Expect(marches==72,"march sample count");
        });

        check("granary, crops and forges stack exact production multipliers and all building grades fit the economy",() => {
            var m=new Manor {coin=700,timber=500,food=100,workers=7};
            string[] kinds={"cottage","farm","pasture","timber_camp","smithy"};
            for(int i=0;i<kinds.Length;i++)m.Build(i+1,kinds[i]);
            m.Build(14,"granary");m.Build(16,"chapel");
            for(int i=2;i<=5;i++)m.Assign(i,1);
            Expect(m.Forecast().synergy.grainPercent==0&&m.Forecast().synergy.cropPercent==8,"idle granary or working pasture");
            m.Assign(14,1);
            var f=m.Forecast();
            Expect(f.food==415&&f.arms==220&&f.synergy.grainPercent==8&&f.synergy.forgePercent==10&&f.synergy.chapelApproval==3,"Lv.1 building effects");
            Reject(()=>m.Assign(14,1));
            m.Upgrade(14); m.Upgrade(16);
            Expect(m.Synergies().grainPercent==10&&m.Synergies().chapelApproval==4,"Lv.2 building effects");
            m.Upgrade(14); m.Upgrade(16); m.Assign(16,1);
            f=m.Forecast();
            Expect(f.food==453&&f.synergy.grainPercent==12&&f.synergy.chapelApproval==6&&
                f.approvalFactors.chapel==6&&f.approvalFactors.herbalist==1,"Lv.3 building effects");
            m.Assign(3,-1);
            Expect(m.Synergies().cropPercent==8,"granary alone did not support working crops");
            m.Assign(14,-1);m.Assign(4,-1);
            Expect(m.Synergies().cropPercent==0&&m.Synergies().forgePercent==0,"idle support still paid a bonus");
        });

        check("granary reserve preserves a ration without minting stock and mitigates winter or famine",() => {
            var m=new Manor {day=37,food=1,coin=500,timber=500};
            m.Build(14,"granary");m.Assign(14,1);
            Expect(m.Forecast().reserveFood==100&&m.Forecast().food==0,"reserve did not preserve final grain");
            m.Advance(1);Expect(m.food==1,"reserve altered ledger improperly");
            m.food=0;
            m.Advance(1);Expect(m.food==0,"reserve created tradeable grain");
            m.Build(1,"cottage");m.Upgrade(1);m.Upgrade(14);m.Upgrade(14);
            m.workers=6;m.food=1;m.timber=0;
            var f=m.Forecast();
            Expect(f.reserveFood==300&&f.approvalFactors.food==-2&&f.approvalFactors.heating==0&&
                f.winterTimberRequired==200&&f.winterTimber==100,"winter mitigation spent or invented fuel");
        });

        check("chapels count an extra civic building and settle the village only with enough cottage levels",() => {
            var m=new Manor {workers=4,coin=500,timber=500};
            m.Build(1,"cottage");m.Build(16,"chapel");
            Expect(m.BuildingCount==3&&m.ProsperityTitle=="安宁村落"&&m.Synergies().settledApproval==0,"chapel scoring");
            m.Upgrade(1);m.Upgrade(1);
            Expect(m.Forecast().approvalFactors.settled==1&&m.Logistics.recoveryRate==124,"settled village or chapel relief");
            m.Assign(16,1);
            Expect(m.Logistics.recoveryRate==130&&m.Forecast().approvalFactors.chapel==4,"staffed chapel bonus");
            m.Demolish(16);
            Expect(m.Synergies().settledApproval==0&&m.ProsperityTitle=="拓荒营地","closed chapel still contributed");
        });

        check("specialists unlock from buildings, upgrade atomically and retain trained ranks on demolition",() => {
            var m=new Manor {coin=1000,timber=500,food=200,arms=100,workers=4};
            Expect(m.SpecialistLevel("steward")==1&&m.Forecast().coin==864,"founding steward");
            foreach(string id in Manor.SpecialistIds.Skip(1))
            {
                Expect(m.SpecialistLevel(id)==0,"premature specialist "+id);
                Reject(()=>m.UpgradeSpecialist(id));
            }
            m.Build(1,"cottage");m.Build(2,"farm");m.Build(3,"tavern");
            Expect(m.SpecialistLevel("merchant")==1,"village market unlock with four buildings");
            m.Build(4,"drill_yard");
            int[] lv2={6,8,6,4},lv3={10,14,10,7};
            for(int i=0;i<lv2.Length;i++)
            {
                string id=Manor.SpecialistIds[i];
                int Funds()=>i==2?m.food:i==3?m.arms:m.timber;
                int coin=m.coin,funds=Funds();
                m.UpgradeSpecialist(id);
                Expect(m.SpecialistLevel(id)==2&&m.coin==coin-30&&Funds()==funds-lv2[i],"Lv.2 price "+id);
                if(i==0)Expect(m.Forecast().coin==912,"Lv.2 tax");
                int wallet=m.coin;
                m.coin=0;string before=m.Export();
                Reject(()=>m.UpgradeSpecialist(id));
                Expect(m.Export()==before,"unfunded upgrade charged");
                m.coin=wallet;
                m.UpgradeSpecialist(id);
                Expect(m.SpecialistLevel(id)==3&&m.coin==coin-82&&Funds()==funds-lv2[i]-lv3[i],"Lv.3 price "+id);
                Reject(()=>m.UpgradeSpecialist(id));
            }
            Expect(m.Forecast().coin==960&&m.Forecast().approvalFactors.herbalist==3,"max rank output");
            int arms=m.arms;m.arms=0;
            var other=Manor.Import(m.Export());other.specialists["watchman"]=1;
            Reject(()=>other.Export());
            m.arms=arms;
            m.Demolish(3);m=Manor.Import(m.Export());
            Expect(m.SpecialistLevel("herbalist")==0&&m.SpecialistRank("herbalist")==3,"inactive training lost");
            m.Build(3,"chapel");
            Expect(m.SpecialistLevel("herbalist")==3,"reopened clinic lost progression");
        });

        check("merchant quota and discount coexist with all title tiers and every policy preserves the market spread",() => {
            var m=EstablishedEstate();m.workers=12;m.approval=75;m.cleared=3;
            Expect(m.MarketSeasonLimit==40,"Lv.1 merchant quota");
            m.UpgradeSpecialist("merchant");Expect(m.MarketSeasonLimit==45,"Lv.2 merchant quota");
            m.UpgradeSpecialist("merchant");Expect(m.MarketSeasonLimit==50,"Lv.3 merchant quota");
            foreach(var resource in new[]{"food","timber","arms"})
            {
                m.SetDecree("none");var full=m.QuoteTrade(resource);
                m.SetDecree("trade");var cheap=m.QuoteTrade(resource);
                Expect(cheap.buyPrice>cheap.sellPrice&&cheap.buyPrice<full.buyPrice&&cheap.sellPrice>full.sellPrice,"merchant arbitrage "+resource);
            }
            Expect(m.QuoteTrade("food").buyPrice==254,"fixed point buy discount");
            m.Trade("food",50);
            Expect(Manor.Import(m.Export()).QuoteTrade("food").buyRemaining==0,"persisted cap rolled back");
        });

        check("watch training and mobilization prices stack and extra budget is scoped to raiders",() => {
            var m=new Manor {coin=1000,timber=500,food=200,arms=100};
            m.Build(1,"barracks");m.Build(2,"drill_yard");m.Build(3,"stables");
            Expect(m.RecruitPrice("militia").StartsWith("4.8 ")&&m.ExpeditionLogistics(true).raidPercent==2,"watchman Lv.1");
            m.UpgradeSpecialist("watchman");
            Expect(m.RecruitPrice("militia").StartsWith("4.65 ")&&m.ExpeditionLogistics(true).raidPercent==4,"watchman Lv.2");
            m.UpgradeSpecialist("watchman");m.SetDecree("mobilize");
            Expect(m.RecruitPrice("militia").StartsWith("4.05 ")&&m.RecruitPrice("barricade").StartsWith("5 ")&&
                m.TrainPrice(2,"militia").StartsWith("14.4 "),"watchman discount stacking");
            int purse=Purse(m);m.Recruit(1,"militia");m.Train(2,"militia");
            Expect(Purse(m)==purse-1845,"fractional training or recruit was charged incorrectly");
            m.stock["giant"]=1;m.stock["crossbow"]=1;
            var force=new Dictionary<string,int>{{"giant",1},{"crossbow",1},{"archer",1},{"militia",1}};
            Reject(()=>m.Prepare(data,0,false,force));Reject(()=>m.PrepareFrontier(data,force));
            var raid=m.Prepare(data,0,true,force);
            Expect(raid.Logistics.Budget==657&&raid.config.budget==657&&m.Budget==620,"raider bonus leaked or failed to open room");
        });

        check("field herbalists and chapels cap rescued casualties at 65 percent before rounding",() => {
            var m=new Manor {approval=75,coin=500,timber=500,food=200};
            m.Build(16,"chapel");m.Upgrade(16);m.Upgrade(16);m.Assign(16,1);
            Expect(m.Logistics.recoveryRate==192,"Lv.1 herbs and staffed chapel");
            m.UpgradeSpecialist("herbalist");m.UpgradeSpecialist("herbalist");
            m.stock["militia"]=8;
            var sortie=m.Prepare(data,0,false,Party(8,0).Where(p=>p.Value>0).ToDictionary(p=>p.Key,p=>p.Value));
            var settled=m.Settle(sortie,Outcome(sortie,"blue",0));
            Expect(settled.logistics.recoveryRate==195&&settled.troops["militia"].wounded==6&&
                settled.troops["militia"].fallen==2&&m.Stock("militia")==6,"cap or rescue rounding");
        });

        check("legacy v1 through v4 retain their entire valid schema and append pristine plots",() => {
            var m=new Manor {coin=5000,food=400,timber=5000,arms=100,workers=8,day=42,cleared=3,raidReady=true,eventDay=42,approval=78};
            for(int i=1;i<=4;i++)m.Build(i,"cottage");
            m.Upgrade(1);m.Upgrade(2);
            m.Build(7,"farm");m.Assign(7,1);m.Build(13,"farm");m.Assign(13,1);
            m.SetTax("high");m.SetDecree("trade");m.remainder.food=37;
            m.UpgradeSpecialist("steward");
            m.Trade("food",5);
            for(int version=1;version<=4;version++)
            {
                if(version==4)m.Trade("food",25);
                var old=LegacySchema(m,version);
                var migrated=Manor.Import(Json.Write(old));
                var comparable=LegacySchema(migrated,version);
                foreach(string key in old.Keys)
                    Expect(Json.Write(old[key])==Json.Write(comparable[key]),"v"+version+" lost "+key);
                Expect(migrated.version==Manor.SaveVersion&&migrated.slots.Length==Manor.PlotCount&&migrated.slots.Skip(14).All(plot=>plot.kind==null&&plot.level==0&&plot.workers==0)&&
                    migrated.specialists.Count==0&&migrated.SpecialistLevel("steward")==1,"migration defaults v"+version);
                Expect((migrated.ActiveEvent?.title=="寒潮来袭")== (version==4),"v4 event was not retained");
            }
            var paddedV1=LegacySchema(m,1);
            var current=(Dictionary<string,object>)Json.Parse(m.Export());
            paddedV1["slots"]=LegacyPlots(current["slots"],14);
            var padded=Manor.Import(Json.Write(paddedV1));
            Expect(padded.slots[13].kind=="farm"&&padded.slots[13].workers==1,"padded v1 lost plot 13");
            var badV4=LegacySchema(m,4);
            Reject(()=>Manor.Import(Json.Write(badV4).Replace("\"bought\":30","\"bought\":36")));
        });

        check("v5 rejects malformed specialist and slot schema without discarding valid ranks",() => {
            var m=new Manor {coin=500,timber=500};
            m.Build(17,"chapel");m.UpgradeSpecialist("steward");
            var oldSave=LegacySchema(m,5);
            string save=Json.Write(oldSave);
            Expect(Json.Write(LegacySchema(Manor.Import(save),5))==save,"canonical v5 save");
            foreach(string invalid in new[]{ "\"steward\":4","\"steward\":1","\"steward\":2.5","\"steward\":\"2\"","\"cook\":2",
                "\"steward\":2,\"steward\":3" })
                Reject(()=>Manor.Import(save.Replace("\"steward\":2",invalid)));
            Reject(()=>Manor.Import(save.Replace(",\"specialists\":{\"steward\":2}","").Replace("\"specialists\":{\"steward\":2},","")));
            var old=LegacySchema(m,4);
            old["version"]=5;old["specialists"]=new Dictionary<string,int>();
            Reject(()=>Manor.Import(Json.Write(old)));
        });
    }
}
