using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;

static partial class ManorChecks
{
    static Manor PromotionEstate()
    {
        var manor=new Manor {coin=3000,timber=1000,food=120,arms=50,influence=999,prosperityPeak=3,
            cleared=5,clearedSectors=Manor.SectorMask};
        for(int i=0;i<Manor.Sectors.Count;i++)manor.ClaimSector(i);
        return manor;
    }

    public static void RunNobility(Catalog data,Action<string,Action> check)
    {
        check("baron keeps the original 24 grants and locks all eight crown survey plots",()=>{
            var manor=new Manor();
            Expect(manor.NobleTitle=="男爵"&&manor.GrantedPlots==24&&manor.slots.Length==32&&manor.ArmyLimit==13&&
                manor.FieldLimit==8&&manor.Budget==620,"baron baseline");
            string before=manor.Export();
            for(int index=Manor.FirstNoblePlot;index<Manor.PlotCount;index++)
            {
                Expect(!manor.IsPlotGranted(index)&&!manor.IsPlotCleared(index)&&manor.ReclaimBlock(index)!=null,"unsealed plot");
                Reject(()=>manor.ReclaimPlot(index));
                Reject(()=>manor.Build(index,"farm"));
            }
            Expect((manor.Todos&ManorTodo.Nobility)==0&&manor.Export()==before,"blocked grant changed estate");
        });

        check("promotion is sequential and atomic, grants two surveyed plots and writes the chronicle",()=>{
            var manor=PromotionEstate();
            int influence=manor.influence,coins=manor.coin;
            Reject(()=>manor.PromoteNobility(2));
            Reject(()=>manor.PromoteNobility(-1));
            manor.influence=Manor.NobleRanks[1].influence-1;
            string before=manor.Export();
            Expect((manor.Todos&ManorTodo.Nobility)==0,"unaffordable promotion in todos");
            Reject(()=>manor.PromoteNobility(1));
            Expect(before==manor.Export(),"failed promotion charged coins");
            manor.influence=influence;
            manor.coin=Manor.NobleRanks[1].coin-1;
            before=manor.Export();
            Reject(()=>manor.PromoteNobility(1));
            Expect(before==manor.Export(),"failed promotion charged influence");
            manor.coin=coins;
            for(int rank=1;rank<Manor.NobleRanks.Count;rank++)
            {
                int start=manor.GrantedPlots;
                var row=Manor.NobleRanks[rank];
                int coin=manor.coin,influenceBefore=manor.influence;
                Expect((manor.Todos&ManorTodo.Nobility)!=0,"eligible promotion missing todo");
                manor.PromoteNobility(rank);
                Expect(manor.GrantedPlots==start+2&&manor.influence==influenceBefore-row.influence&&
                    manor.coin==coin-row.coin&&manor.nobility==rank&&manor.chronicle.Last().kind=="nobility","promotion settlement");
                for(int index=start;index<start+2;index++)
                {
                    Expect(Manor.PlotRank(index)==rank&&manor.IsPlotCleared(index),"survey not unlocked");
                    manor.Build(index,index%2==0?"farm":"granary");
                }
                Reject(()=>manor.PromoteNobility(rank));
            }
            Expect((manor.Todos&ManorTodo.Nobility)==0&&manor.slots.Skip(24).All(plot=>plot.kind!=null)&&
                manor.chronicle.Count(entry=>entry.kind=="nobility")==4,"duchy did not complete");
            var exported=manor.Export();
            Expect(Manor.Import(exported).Export()==exported,"v11 round trip with all eight grants");
        });

        check("rank progression enforces lasting achievements and applies exact estate-only bonuses",()=>{
            var manor=PromotionEstate();
            manor.Build(1,"farm");manor.Assign(1,1);
            manor.Build(2,"timber_camp");manor.Assign(2,1);
            manor.Build(3,"cottage");manor.SetBackyard(3,"poultry");
            manor.Build(4,"smithy");manor.workers=3;manor.Assign(4,1);
            int army=manor.ArmyLimit,field=manor.FieldLimit;
            var arena=data.Default("meadow");
            var duel=Json.Write(data.Validate(arena));
            manor.PromoteNobility(1);
            Expect(manor.ArmyLimit==army+2&&manor.FieldLimit==field,"viscount recruit cap");
            manor.PromoteNobility(2);
            Expect(manor.FieldLimit==field+2&&manor.FrontierLimit<=FrontierMap.MaxParty,"count march cap");
            int claims=manor.claimedSectors;
            manor.claimedSectors=0;
            Expect(manor.NoblePromotionBlock(3)!=null,"marquis did not require a claim");
            manor.claimedSectors=claims;
            int budget=manor.Budget;
            manor.PromoteNobility(3);
            Expect(manor.Logistics.nobilityPercent==8&&manor.Budget==budget+50&&
                manor.PrepareFrontier(data,Party(1,0)).Logistics.Budget==manor.Budget,"marquis expedition and patrol");
            var baseline=manor.Forecast();
            manor.PromoteNobility(4);
            var after=manor.Forecast();
            Expect(after.coin-baseline.coin==46&&after.food-baseline.food==29&&
                after.timber-baseline.timber==20&&after.arms-baseline.arms==11&&after.forgeTimber==baseline.forgeTimber,"duke production");
            manor.approval=32;
            Expect(Manor.Import(manor.Export()).nobility==4&&Json.Write(data.Validate(arena))==duel,"rank permanence or duel isolation");
        });

        check("v1–v10 project losslessly into v11, with baron and eight empty unsealed plots",()=>{
            var estate=new Manor {coin=400,timber=250,food=80,day=42,cleared=3,influence=200};
            estate.Build(1,"farm");estate.Assign(1,1);
            estate.Build(2,"cottage");estate.SetBackyard(2,"poultry");
            estate.ReclaimPlot(18);estate.ReclaimPlot(19);estate.ClaimMilestone(0);
            for(int version=1;version<=10;version++)
            {
                if(version==9){estate.SetRotation(1,"intensive");estate.slots[1].fertility=83;estate.RecruitGuard();}
                if(version==10){estate.clearedSectors=3;estate.ClaimSector(1);}
                var old=LegacySchema(estate,version);
                var imported=Manor.Import(Json.Write(old));
                var projection=LegacySchema(imported,version);
                foreach(var field in old)
                    Expect(Json.Write(field.Value)==Json.Write(projection[field.Key]),"v"+version+" lost "+field.Key);
                Expect(imported.version==11&&imported.nobility==0&&imported.GrantedPlots==24&&
                    imported.slots.Skip(24).All(plot=>plot.kind==null&&plot.level==0&&plot.workers==0&&
                    plot.backyard==null&&plot.rotation==null&&plot.fertility==0),"v11 defaults");
            }
        });

        check("strict v11 rejects forged nobility, past-schema extras and buildings on unsealed land",()=>{
            var estate=PromotionEstate();
            string valid=estate.Export();
            Dictionary<string,object> Raw() => (Dictionary<string,object>)Json.Parse(valid);
            foreach(object rank in new object[]{-1,5,2.5,"3",null})
            {
                var bad=Raw();bad["nobility"]=rank;
                Reject(()=>Manor.Import(Json.Write(bad)));
            }
            var missing=Raw();missing.Remove("nobility");
            Reject(()=>Manor.Import(Json.Write(missing)));
            var legacy=LegacySchema(estate,10);legacy["nobility"]=0;
            Reject(()=>Manor.Import(Json.Write(legacy)));
            var tooShort=Raw();tooShort["slots"]=LegacyPlots(tooShort["slots"],24,true,true);
            Reject(()=>Manor.Import(Json.Write(tooShort)));
            var forged=Raw();forged["nobility"]=4;forged["cleared"]=3;
            Reject(()=>Manor.Import(Json.Write(forged)));
            var locked=Raw();
            var plot=(Dictionary<string,object>)((List<object>)locked["slots"])[24];
            plot["kind"]="cottage";plot["level"]=1;
            Reject(()=>Manor.Import(Json.Write(locked)));
            estate.nobility=1;estate.Build(24,"chapel");estate.nobility=0;
            Reject(()=>estate.Export());
            var invented=Raw();
            ((List<object>)invented["chronicle"]).Add(new {day=estate.day,kind="nobility",subject=2,choice=0});
            Reject(()=>Manor.Import(Json.Write(invented)));
        });
    }
}
