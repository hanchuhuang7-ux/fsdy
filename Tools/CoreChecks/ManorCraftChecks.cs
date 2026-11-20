using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;

static partial class ManorChecks
{
    public static void RunCrafts(Catalog data,Action<string,Action> check)
    {
        check("bakery processes primary grain once, adds an independent food and gets its field bonus",()=>{
            var m=new Manor {coin=600,timber=300,food=40};
            m.Build(1,Manor.Bakery);
            Expect(m.Forecast().bakeryGrain==0&&m.Forecast().food==-100,"idle oven produced");
            m.Assign(1,1);
            var bread=m.Forecast();
            Expect(bread.bakeryGrain==100&&bread.food==100&&!bread.marketNeeds.variedDiet,"bread alone");
            m.Build(2,"farm");m.Assign(2,1);
            bread=m.Forecast();
            Expect(bread.food==636&&bread.synergy.breadPercent==12&&bread.marketNeeds.variedDiet,"wheat and bread diet");
            m.Assign(2,-1);m.Build(3,Manor.Bakery);m.Assign(3,1);
            m.food=2;
            Expect(m.Forecast().bakeryGrain==100,"today's bread was reprocessed as grain");
            m.food=0;
            Expect(m.Forecast().bakeryGrain==0&&m.Forecast().food==-100,"unfunded grain processing");
        });

        check("weaver earns fixed-point coins, warms winter households and gains a pasture bonus",()=>{
            var m=new Manor {coin=600,timber=300,food=40,day=37};
            m.Build(1,"cottage");m.workers=5;
            m.Build(2,Manor.Weaver);m.Build(3,"pasture");
            var before=m.Forecast();
            m.Assign(2,1);
            var active=m.Forecast();
            Expect(active.coin-before.coin==200&&before.winterTimberRequired==200&&active.winterTimberRequired==100,"loom production and warmth");
            m.Assign(3,1);
            Expect(m.Forecast().coin-before.coin==220&&m.Synergies().weavePercent==10,"pasture cloth");
            m.day=1;
            Expect(m.Forecast().winterTimberRequired==0,"spring heating");
        });

        check("mason changes actual and displayed building prices and keeps training if temporarily locked",()=>{
            var m=new Manor {coin=600,timber=300,food=40};
            Expect(m.SpecialistLevel(Manor.Mason)==0&&m.BuildPrice("farm")=="14 金币 · 8 建材","founding cost");
            m.Build(1,"timber_camp");m.Assign(1,1);
            Expect(m.SpecialistLevel(Manor.Mason)==1&&m.Forecast().timber==512&&m.BuildPrice("farm")=="13 金币 · 7 建材","mason I");
            m.UpgradeSpecialist(Manor.Mason);m.UpgradeSpecialist(Manor.Mason);
            Expect(m.SpecialistLevel(Manor.Mason)==3&&m.Forecast().timber==536,"mason III");
            int coins=m.coin,wood=m.timber;
            m.Build(2,"farm");
            Expect(m.coin==coins-12&&m.timber==wood-7&&m.UpgradePrice(2)=="12 金币 · 7 建材","discounted quote");
            m.Upgrade(2);
            Expect(m.coin==coins-24&&m.timber==wood-14,"upgrade did not charge the quoted price");
            m.Demolish(1);m=Manor.Import(m.Export());
            Expect(m.SpecialistRank(Manor.Mason)==3&&m.SpecialistLevel(Manor.Mason)==0,"latent trained rank lost");
            m.Build(1,"smithy");
            Expect(m.SpecialistLevel(Manor.Mason)==3&&m.chronicle.Count(entry=>entry.kind=="specialist")==2,"reopened workshop or chronicle");
        });

        check("minstrel raises morale and qualifies only existing tithe income for bonus prestige",()=>{
            var m=new Manor {coin=600,timber=300,food=90,influence=50};
            var config=Json.Write(data.Default());
            m.Build(1,Manor.Weaver);m.Build(2,"farm");m.Assign(2,1);
            m.UpgradeSpecialist(Manor.Minstrel);m.UpgradeSpecialist(Manor.Minstrel);
            var day=m.Forecast();
            Expect(day.approvalFactors.minstrel==3&&day.influence==0,"free influence or missing morale");
            m.SetTithe(Manor.TitheRate);day=m.Forecast();
            Expect(day.influence==4&&day.titheFood==1&&Json.Write(data.Default())==config,"tithe or duel scope");
            m.influence=Manor.InfluenceMax-1;
            Expect(m.Forecast().influence==1,"influence cap");
        });

        check("v11 earned grants preserve all old plot indices and acquire one new empty plot per rank",()=>{
            var old=PromotionEstate();
            for(int rank=1;rank<=4;rank++)old.PromoteNobility(rank);
            for(int i=24;i<32;i++)old.Build(i,i%2==0?"farm":"granary");
            var fixture=LegacySchema(old,11);
            foreach(Dictionary<string,object> entry in (List<object>)fixture["chronicle"])
                if((string)entry["kind"]=="nobility")entry["choice"]=0;
            var m=Manor.Import(Json.Write(fixture));
            Expect(m.version==Manor.SaveVersion&&m.GrantedPlots==36&&m.slots.Skip(32).All(plot=>plot.kind==null),"duke migration");
            var projected=LegacySchema(m,11);
            foreach(var field in fixture)Expect(Json.Write(field.Value)==Json.Write(projected[field.Key]),"v11 lost "+field.Key);

            var viscount=PromotionEstate();viscount.PromoteNobility(1);
            var oldViscount=LegacySchema(viscount,11);
            foreach(Dictionary<string,object> entry in (List<object>)oldViscount["chronicle"])
                if((string)entry["kind"]=="nobility")entry["choice"]=0;
            m=Manor.Import(Json.Write(oldViscount));
            m.Build(32,Manor.Bakery);
            Reject(()=>m.Build(33,Manor.Weaver));Reject(()=>m.Build(26,"farm"));
            Expect(Manor.Import(m.Export()).slots[32].kind==Manor.Bakery&&Manor.NobleGrantPlots(1)=="24–25、32","sparse grant or new building roundtrip");
        });

        check("craft saves reject new fields and buildings in old schema, and illegal v12 specialist ranks",()=>{
            var manor=PromotionEstate();manor.Build(1,Manor.Weaver);
            var old=LegacySchema(manor,11);
            Reject(()=>Manor.Import(Json.Write(old)));
            manor.Demolish(1);
            manor.chronicle.Clear();
            old=LegacySchema(manor,11);old["specialists"]=new Dictionary<string,int>{{Manor.Mason,2}};
            Reject(()=>Manor.Import(Json.Write(old)));
            manor.specialists[Manor.Minstrel]=4;
            Reject(()=>manor.Export());
            manor.specialists.Clear();
            manor.slots[35]=new ManorPlot {kind=Manor.Weaver,level=1};
            Reject(()=>manor.Export());
        });
    }
}
