using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;

static class ManorChecks
{
    static void Expect(bool condition, string detail)
    {
        if (!condition) throw new Exception(detail);
    }
    static void Reject(Action action)
    {
        try { action(); }
        catch (FormatException) { return; }
        catch (InvalidOperationException) { return; }
        throw new Exception("Operation should have failed");
    }
    static Dictionary<string,int> Party(int militia = 4,int archers = 2) =>
        new Dictionary<string,int> { { "militia",militia }, { "archer",archers } };
    static Result Outcome(Manor.Sortie sortie,string winner,int alive)
    {
        var report = new BattleReport();
        foreach (var u in sortie.config.red)
        {
            int index = report.units.Count;
            report.units.Add(new UnitReport { id = index + 1, team = "red", type = u.type, alive = index < alive });
        }
        return new Result { winner = winner, report = report, red = alive };
    }

    public static void Run(Catalog data, Action<string,Action> check)
    {
        check("manor has one hall, nine pickable sites and an independent new-game ledger",() => {
            var m = new Manor(); var other = new Manor();
            Expect(m.slots.Length == 10 && m.slots.Count(p => p.kind != null) == 1,"founding layout");
            for (int i = 0; i < 10; i++)
                Expect(Manor.PickSlot(Manor.SlotX(i), Manor.SlotZ(i)) == i,"ambiguous plot");
            Expect(Manor.PickSlot(15,0) == -1,"off plateau");
            m.Build(1,"farm"); Expect(other.slots[1].kind == null,"shared state");
        });
        check("build and upgrade charge once and failed orders preserve the ledger",() => {
            var m = new Manor();
            m.Build(1,"farm"); Expect(m.coin == 126 && m.timber == 72,"building price");
            m.Upgrade(1); Expect(m.coin == 98 && m.timber == 56 && m.slots[1].level == 2,"upgrade price");
            var before = m.Export();
            Reject(() => m.Build(1,"farm")); Reject(() => m.Demolish(0)); Reject(() => m.Upgrade(0));
            Expect(before == m.Export(),"failed operation charged");
            m.coin = 0; Reject(() => m.Upgrade(1)); Expect(m.timber == 56,"partial payment");
        });
        check("workers cannot exceed households or staffed slots; demolition returns workers",() => {
            var m = new Manor(); m.Build(1,"farm"); m.Build(2,"timber_camp"); m.Build(3,"smithy");
            m.Assign(1,1); Reject(() => m.Assign(1,1)); m.Assign(2,1);
            Reject(() => m.Assign(3,1)); Reject(() => m.Assign(1,0)); Reject(() => m.Assign(1,int.MinValue));
            m.Assign(1,-1); Reject(() => m.Assign(1,-1)); m.Assign(3,1); m.Demolish(3);
            Expect(m.FreeWorkers == 1 && m.slots[3].workers == 0,"workers stuck at demolished site");
        });
        check("twelve manual days equal a season and shortages preserve households",() => {
            var m = new Manor { food = 0 }; m.Build(1,"cottage"); m.Advance(12);
            Expect(m.food == 0 && m.workers == 2 && m.FieldLimit == 3 && m.raidReady,"shortage recovery");
            m.Build(2,"farm"); m.Assign(2,1); var n = Manor.Import(m.Export());
            m.Advance(12); for (int i=0; i<12; i++) n.Advance(1);
            Expect(m.Export() == n.Export() && m.workers == 3 && m.food > 0,"season vs day");
            Reject(() => m.Demolish(1)); Expect(m.Housing == 3,"occupied houses were lost");
        });
        check("farms, timber and smithies settle in order, idle shops produce nothing",() => {
            var m = new Manor(); m.Build(1,"timber_camp"); m.Build(2,"smithy"); int a = m.arms;
            m.Advance(1); Expect(m.arms == a,"unworked forge");
            m.Assign(1,1); m.Assign(2,1); m.timber = 0; int oldCoin = m.coin;
            m.Advance(1); Expect(m.arms == a+2 && m.timber == 4 && m.coin > oldCoin,"daily ledger");
        });
        check("recruitment requires the proper building and tier, both currencies and a free berth",() => {
            var m = new Manor { coin = 3000, timber = 3000, arms = 300 };
            m.Build(1,"barracks"); m.Build(2,"stables"); Reject(() => m.Recruit(2,"archer"));
            Reject(() => m.Recruit(1,"medic")); Reject(() => m.Recruit(2,"banner"));
            m.Recruit(1,"militia"); Expect(m.Stock("militia") == 5 && m.arms == 299,"militia cost");
            m.Upgrade(1); m.Recruit(1,"shield"); m.Upgrade(1); m.Recruit(1,"medic");
            m.Upgrade(2); m.Recruit(2,"spring"); m.Upgrade(2); m.Recruit(2,"banner"); m.Recruit(2,"catapult");
            while (m.ArmySize < m.ArmyLimit) m.Recruit(1,"militia");
            Reject(() => m.Recruit(1,"militia")); m.arms = 0; int c = m.coin;
            Reject(() => m.Recruit(2,"barrel")); Expect(m.coin == c,"partial recruitment charge");
            var offers = Manor.Recruits("barracks",3).Concat(Manor.Recruits("stables",3));
            Expect(offers.OrderBy(x=>x).SequenceEqual(data.Units.Keys.Concat(data.Props.Keys).OrderBy(x=>x)),"missing army or prop");
        });
        check("manor JSON roundtrips canonically and rejects extra, missing, duplicate or mistyped data",() => {
            var m = new Manor(); m.Build(1,"farm"); m.Assign(1,1); m.Advance(2);
            var valid = m.Export();
            Expect(Manor.Import(valid).Export() == valid,"canonical roundtrip");
            var map = (Dictionary<string,object>)Json.Parse(valid);
            void Bad(string key,object value) {
                var copy = (Dictionary<string,object>)Json.Parse(valid); copy[key]=value;
                Reject(() => Manor.Import(Json.Write(copy)));
            }
            Bad("coins",5); Bad("format","frontline-replay"); Bad("version",2);
            Bad("coin",-1); Bad("workers",1); Bad("workers",22); Bad("food",2.5); Bad("day","2"); Bad("raidReady",1);
            Bad("slots",null); Bad("stock",new Dictionary<string,int>());
            map.Remove("timber"); Reject(() => Manor.Import(Json.Write(map)));
            Reject(() => Manor.Import(valid.Replace("\"format\":","\"format\":\"x\",\"format\":")));
            Reject(() => Manor.Import(valid.Replace("\"kind\":null","\"kind\":\"manor_hall\"")));
            Reject(() => Manor.Import(valid.Replace("\"level\":0","\"level\":1")));
            Reject(() => Manor.Import(valid.Replace("\"archer\":2","\"archer\":61")));
            Reject(() => Manor.Import(valid.Replace("\"kind\":\"farm\"","\"extra\":true,\"kind\":\"farm\"")));
            Reject(() => Manor.Import(" " + new string(' ',32768) + valid));
        });
        check("a sortie validates stock, stage, supply and budget before reserving a standard formation",() => {
            var m = new Manor(); var before=m.Export();
            Reject(() => m.Prepare(data,1,false,Party())); Reject(() => m.Prepare(data,0,false,Party(5)));
            Reject(() => m.Prepare(data,0,false,Party(0,0)));
            var s=m.Prepare(data,0,false,Party()); Expect(s.config.version=="0.6.0" && s.config.map=="meadow","schema");
            Expect(s.config.red.Count==6 && s.config.blue.Count>0 && s.Matches(data.Validate(s.config)),"roster");
            s.config.red[0].type="giant"; Expect(!s.Matches(s.config),"substituted soldier");
            Expect(m.Export()==before,"cancelled draft charged");
            m.food=0; Reject(() => m.Prepare(data,0,false,Party())); m.Prepare(data,0,false,Party(2,1));
            m.food=20; m.stock["giant"]=2; Reject(() => m.Prepare(data,0,false,new Dictionary<string,int>{{"giant",2}}));
        });
        check("battle report returns survivors plus one third of casualties, expends kit and pays loot once",() => {
            var m=new Manor(); m.stock["spikes"]=2; var p=Party();p["spikes"]=1;
            var s=m.Prepare(data,0,false,p); var result=Outcome(s,"red",2);
            var old=m.Export(); var bad=Outcome(s,"red",6);bad.report.units[1].id=bad.report.units[0].id;
            Reject(() => m.Settle(s,bad)); Expect(m.Export()==old,"partial settlement");
            var next=m.ReturnedState(s,result,out var receipt);
            Expect(m.Export()==old && next.cleared==1 && next.ArmySize==4 && next.Stock("spikes")==1,"return count");
            Expect(next.coin==m.coin+22 && next.timber==m.timber+10 && next.arms==m.arms+5 && receipt.Contains("伤员 2"),"loot");
            Reject(() => m.Settle(s,result)); Expect(Manor.Import(next.Export()).Export()==next.Export(),"restart on result");
        });
        check("all six routes unlock in order with tiered command seals; raids and replays cannot skip gates",() => {
            var m = new Manor { coin=5000,timber=5000 }; var one=Party(1,0);
            var raid=m.Prepare(data,0,true,one); m.Settle(raid,Outcome(raid,"red",1));
            Expect(m.cleared==0 && !m.raidReady && !m.AllowsOrder("focus"),"raid advanced campaign");
            var loss=m.Prepare(data,0,false,one); m.Settle(loss,Outcome(loss,"blue",0));
            Expect(m.ArmySize==6 && m.cleared==0,"loss broke recovery");
            for(int i=0;i<Manor.Route.Length;i++) {
                if(i<5)Reject(()=>m.Prepare(data,i+1,false,one));
                var s=m.Prepare(data,i,false,one);Expect(s.config.map==Manor.Route[i],"wrong map");
                m.Settle(s,Outcome(s,"red",1)); Expect(m.cleared==i+1,"frontier");
                var replay=m.Prepare(data,0,false,one);m.Settle(replay,Outcome(replay,"red",1));Expect(m.cleared==i+1,"repeat advanced");
                if(i==0){m.Upgrade(0);Expect(m.AllowsOrder("focus")&&!m.AllowsOrder("fortify"),"second seal");}
                if(i==2){m.Upgrade(0);Expect(m.AllowsOrder("fortify"),"third seal");}
            }
            Reject(()=>m.Prepare(data,6,false,one));
        });
    }
}

