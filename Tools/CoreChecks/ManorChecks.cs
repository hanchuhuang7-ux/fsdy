using System;
using System.Collections.Generic;
using System.Linq;
using Frontline.Core;

static partial class ManorChecks
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
        check("manor has one hall, twenty-three pickable sites and an independent new-game ledger",() => {
            var m = new Manor(); var other = new Manor();
            Expect(m.slots.Length == Manor.PlotCount && m.slots.Count(p => p.kind != null) == 1,"founding layout");
            for (int i = 0; i < m.slots.Length; i++)
                Expect(Manor.PickSlot(Manor.SlotX(i), Manor.SlotZ(i)) == i,"ambiguous plot");
            Expect(Manor.PickSlot(18,0) == -1,"off plateau");
            m.Build(1,"farm"); Expect(other.slots[1].kind == null,"shared state");
        });
        check("build and upgrade charge once and failed orders preserve the ledger",() => {
            var m = new Manor();
            m.Build(1,"farm"); Expect(m.coin == 126 && m.timber == 72,"building price");
            m.Upgrade(1); Expect(m.coin == 112 && m.timber == 64 && m.slots[1].level == 2,"upgrade price");
            var before = m.Export();
            Reject(() => m.Build(1,"farm")); Reject(() => m.Demolish(0)); Reject(() => m.Upgrade(0));
            Expect(before == m.Export(),"failed operation charged");
            m.coin = 0; Reject(() => m.Upgrade(1)); Expect(m.timber == 64,"partial payment");
        });
        check("workers cannot exceed households or staffed slots; demolition returns workers",() => {
            var m = new Manor(); m.Build(1,"farm"); m.Build(2,"timber_camp"); m.Build(3,"smithy");
            m.Assign(1,1); Reject(() => m.Assign(1,2)); m.Assign(2,1);
            Reject(() => m.Assign(3,1)); Reject(() => m.Assign(1,0)); Reject(() => m.Assign(1,int.MinValue));
            m.Assign(1,-1); Reject(() => m.Assign(1,-1)); m.Assign(3,1); m.Demolish(3);
            Expect(m.FreeWorkers == 1 && m.slots[3].workers == 0,"workers stuck at demolished site");
        });
        check("twelve manual days equal a season and shortages preserve households",() => {
            var m = new Manor { food = 0 }; m.Build(1,"cottage"); m.Advance(12);
            Expect(m.food == 0 && m.approval == 0 && m.workers == 2 && m.FieldLimit == 3 && m.raidReady,"shortage");
            m.Build(2,"farm"); m.Assign(2,1); m.SetTax("low"); var n = Manor.Import(m.Export());
            m.Advance(12); for (int i=0; i<12; i++) n.Advance(1);
            Expect(m.Export() == n.Export() && m.workers == 2 && m.approval == 33 && m.food > 0,"season vs day or premature migration");
            m.Advance(6);
            Expect(m.workers == 5 && m.approval == 51,"low-tax migration recovery");
            Reject(() => m.Demolish(1)); Expect(m.Housing == 5,"occupied houses were lost");
        });
        check("farms, timber and smithies settle in order, idle shops produce nothing",() => {
            var m = new Manor(); m.Build(1,"timber_camp"); m.Build(2,"smithy"); int a = m.arms;
            m.Advance(1); Expect(m.arms == a,"unworked forge");
            m.Assign(1,1); m.Assign(2,1); m.timber = 0; int oldCoin = m.coin;
            m.Advance(1); Expect(m.arms == a+2 && m.remainder.arms==20 && m.timber == 4 && m.coin > oldCoin,"daily ledger");
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
            Bad("coins",5); Bad("format","frontline-replay"); Bad("version",Manor.SaveVersion+1);
            Bad("coin",-1); Bad("workers",1); Bad("workers",22); Bad("food",2.5); Bad("day","2"); Bad("raidReady",1);
            Bad("slots",null); Bad("stock",new Dictionary<string,int>());
            Bad("training",new Dictionary<string,int>{{"spikes",2}});
            Bad("training",new Dictionary<string,int>{{"archer",9}});
            Bad("heroes",new Dictionary<string,object>{{"hero_vanguard",new { level = 13, restUntil = 0 }}});
            Bad("heroes",new Dictionary<string,object>{{"hero_vanguard",new { level = 1, restUntil = m.day + 3 }}});
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
            Expect(s.config.red.Count==6 && s.config.blue.Count>0 && s.Matches(data.Validate(s.config,true)),"roster");
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
            Expect(next.coin==m.coin+22 && next.timber==m.timber+10 && next.arms==m.arms+5 &&
                receipt.troops.Values.Sum(row => row.wounded)==2 && receipt.troops.Values.Sum(row => row.fallen)==2 &&
                receipt.spent["spikes"]==1 && receipt.clearedAfter==1,"settlement breakdown");
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
        check("version one saves keep plot positions, residents, stocks and default training when migrated",() => {
            var original = new Manor { workers = 4 };
            original.Build(1,"cottage"); original.Upgrade(1);
            original.Build(9,"farm"); original.Assign(9,1);
            var legacy = LegacySchema(original,1);
            var m = Manor.Import(Json.Write(legacy));
            Expect(m.version==Manor.SaveVersion && m.workers==4 && m.Stock("archer")==2 && m.slots[9].workers==1 &&
                m.slots.Skip(10).All(p=>p.kind==null) && m.UnitLevel("archer")==1 && m.heroes.Count==0,"migration");
            ExpectOperationsDefaults(m);
            Expect(Manor.Import(m.Export()).Export()==m.Export(),"migrated ledger");
        });
        check("forecast and settlement accrue exact ten percent agriculture and forge yields across days and saves",() => {
            var m = new Manor { coin=500, timber=500 };
            m.Build(1,"irrigated_field"); m.Build(2,"smithy");
            m.Assign(1,1); m.Assign(2,1);
            Expect(m.Forecast().food==700 && m.Forecast().arms==200,"base production");
            m.Upgrade(1); m.Upgrade(2);
            Expect(m.Forecast().food==780 && m.Forecast().arms==220 && m.Forecast().forgeTimber==100,"Lv.2 production");
            m.Upgrade(1); m.Upgrade(2);
            var next = m.Forecast(); int food = m.food, arms = m.arms;
            Expect(next.food==868 && next.arms==242 && next.timber==0,"Lv.3 production");
            m.Advance(1); m = Manor.Import(m.Export()); m.Advance(9);
            Expect(m.food-food==86 && m.remainder.food==80 && m.arms-arms==24 && m.remainder.arms==20,"lost fractional yield");
            m.Build(3,"pasture");
            Expect(m.Housing==4 && m.FieldLimit==13 && m.ArmyLimit==21,"farm and pasture capacities");
            m.Assign(2,-1); m.Assign(3,1);
            Expect(m.Forecast().newHousehold && m.Forecast().food==1145,"pasture food, crop synergy and immigration");
        });
        check("tavern and barracks unlock progression up to Lv.12 and Lv.8 with atomic resource costs",() => {
            var m = new Manor { coin=10000,timber=1000,food=1000,arms=1000 };
            var hero = data.Heroes["hero_vanguard"]; var ranger = data.Heroes["hero_ranger"];
            m.Build(1,"tavern"); m.Build(2,"drill_yard");
            Reject(()=>m.HireHero(1,hero)); m.Assign(1,1);
            Reject(()=>m.HireHero(1,ranger)); m.HireHero(1,hero);
            m.Upgrade(1); m.HireHero(1,ranger);
            m.Upgrade(1); m.HireHero(1,data.Heroes["hero_commander"]);
            for (int level=1; level<Manor.HeroMaxLevel; level++) m.UpgradeHero(1,hero);
            for (int level=1; level<Manor.UnitMaxLevel; level++) m.Train(2,"medic");
            string full = m.Export();
            Reject(()=>m.Train(2,"medic")); Reject(()=>m.Train(2,"spikes"));
            Reject(()=>m.UpgradeHero(1,hero)); Reject(()=>m.HireHero(1,hero));
            Expect(m.Export()==full && m.HeroLevel(hero.id)==12 && m.UnitLevel("medic")==8,"level cap or charging");
            m.arms=0; int coin=m.coin; Reject(()=>m.Train(2,"archer"));
            Expect(m.coin==coin && Manor.Import(m.Export()).UnitLevel("medic")==8,"partial payment or training save");
        });
        check("leveled red troops and three hero roles use sortie stats while blue and arena use base stats",() => {
            var m = new Manor { coin=1000,timber=1000,food=100,arms=100 };
            m.Build(1,"tavern"); m.Assign(1,1); m.Upgrade(1); m.Upgrade(1);
            foreach (var hero in data.Data.heroes) { m.HireHero(1,hero); m.UpgradeHero(1,hero); }
            m.Build(2,"drill_yard"); m.Train(2,"militia");
            var party = data.Heroes.Keys.ToDictionary(id=>id,id=>1); party.Add("militia",1);
            var s = m.Prepare(data,0,false,party);
            Reject(()=>data.Import(Json.Write(s.config)));
            Reject(()=>new Battle(data,s.config,new ReferenceFactory()));
            var wrong = Json.Clone(s.config); wrong.red[0].level=8;
            Expect(!s.Matches(wrong),"changed level accepted");
            wrong.red.Add(Json.Clone(wrong.red[1])); Reject(()=>data.Validate(wrong,true));
            var old = data.Default(); var json = Json.Write(old);
            Expect(!json.Contains("\"level\"") && !json.Contains("\"manorHall\""),"old format gained data");
            using (var b = new Battle(data,s.config,new ReferenceFactory { freezeUnits=true },true))
            {
                var troops = b.Units.Where(u=>u.entity.team=="red").ToArray();
                foreach (var unit in troops)
                {
                    var def = data.Unit(unit.entity.type);
                    Expect(Math.Abs(unit.entity.hp-LevelScaling.Scale(def.hp,2))<.001 &&
                        Math.Abs(unit.definition.damage-LevelScaling.Scale(def.damage,2))<.001 &&
                        Math.Abs(unit.definition.heal-LevelScaling.Scale(def.heal,2))<.001,"level did not scale");
                }
                Expect(b.Units.First(u=>u.entity.team=="blue").entity.hp==80 && data.Units["militia"].hp==80,"base stats mutated");
                var tank = troops.Single(u=>u.entity.type=="hero_vanguard");
                float hp=tank.entity.hp;
                b.Hit(tank,40,null,0); Expect(tank.entity.hp==hp-30,"vanguard reduction");
                Expect(!b.IssueOrder("red",new Order("focus",-5,0),out _) && b.IssueOrder("blue",new Order("focus",5,0),out _),"order lock");
                for (int tick=0; tick<6; tick++) b.Step();
                var healer = troops.Single(u=>u.entity.type=="hero_commander");
                var ranger = troops.Single(u=>u.entity.type=="hero_ranger");
                Expect(Math.Abs(healer.entity.stats.healing-2.64)<.001 && tank.entity.hp>hp-30,"leveled field healing");
                Expect(ranger.target?.entity.type=="archer","marksman did not prioritize an enemy backliner");
                for (int tick=0; tick<49 && b.Shots.All(shot=>shot.owner!=ranger.entity); tick++) b.Step();
                Expect(b.Shots.Any(shot=>shot.owner==ranger.entity && shot.type=="bolt" && shot.pierce==.9f),"armor-piercing shot");
            }
            var seed = Replay.Create(data,s.config,null,new Result(),true,Replay.ExpeditionEngine);
            Expect(Replay.Import(data,Json.Write(seed)).config.red.All(u=>u.level==2),"seed lost roster progression");
            seed.engineVersion=Replay.Engine; Reject(()=>Replay.Validate(data,seed));
            var returned = m.ReturnedState(s,Outcome(s,"blue",0),out var receipt);
            Expect(receipt.heroes.Count==3 && receipt.heroes.Values.All(days=>days==2) && returned.HeroLevel("hero_vanguard")==2 &&
                returned.Available("hero_vanguard")==0,"lost heroes did not return to rest");
            Reject(()=>returned.Prepare(data,0,false,party));
            returned.Advance(2);
            Expect(returned.Available("hero_vanguard")==1 && returned.Prepare(data,0,false,party).config.red.Count==4,"rest never finished");
        });
        check("enemy preview and selection limits match the sortie; carried kit can move only to legal positions",() => {
            var m = new Manor { cleared=3 };
            m.stock["spikes"]=12;
            m.stock["giant"]=2;
            var party=Party(1,1); party["spikes"]=1;
            var s=m.Prepare(data,3,false,party);
            Expect(m.EnemyCost(data,3,false)==650 && data.Cost(s.config,"blue")==650 && s.config.blue.Count==m.EnemyRoster(3,false).Count,"enemy preview");
            var moved=s.MoveProp(data,s.config,0,-12,7);
            Expect(s.Matches(data.Validate(moved,true)) && moved.props[0].x==-12 && s.config.props[0].x!=-12,"moving props changed the manifest");
            Reject(()=>s.MoveProp(data,s.config,0,5,0));
            Reject(()=>s.MoveProp(data,s.config,0,s.config.red[0].x,s.config.red[0].z));
            var units=s.MoveTroops(data,moved,new[]{0},-4,4);
            Expect(s.Matches(data.Validate(units,true)) && units.red[0].z==moved.red[0].z+4,"moving troops changed the manifest");
            m.food=0;
            Expect(m.ForceAddBlock(data,Party(2,1),"militia").Contains("3"),"supply reason");
            Expect(m.ForceAddBlock(data,new Dictionary<string,int>{{"giant",1}},"giant").Contains("军费"),"budget reason");
        });
        check("clearing a completed sortie's recording list leaves its frozen replay importable",() => {
            var s=new Manor().Prepare(data,0,false,Party(1,0));
            using (var battle=new Battle(data,s.config,new ReferenceFactory { freezeUnits=true },true))
            {
                var frames=new List<Frame> { battle.Snapshot() };
                foreach (var unit in battle.Units.Where(u=>u.entity.team=="blue")) battle.Hit(unit,1000,null,0);
                battle.Step(); frames.Add(battle.Snapshot());
                var replay=Replay.Create(data,s.config,frames,battle.Result,engineVersion:Replay.ExpeditionEngine);
                frames.Clear();
                var reopened=Replay.Import(data,Json.Write(replay));
                Expect(reopened.frames.Count==2 && reopened.result.winner=="red","the recording list was shared");
            }
        });
        check("small housing and farming capacities show a whole gain at every tier with gentler prices",() => {
            var m=new Manor { coin=1000,timber=1000 };
            m.Build(1,"cottage"); m.Build(2,"farm"); m.Build(3,"pasture");
            for(int level=2;level<=3;level++)
            {
                int housing=m.Housing,field=m.FieldLimit,coin=m.coin;
                m.Upgrade(1); m.Upgrade(2); m.Upgrade(3);
                Expect(m.Housing==housing+2 && m.FieldLimit==field+2,"no integer capacity gained");
                Expect(coin-m.coin==(level==2?46:50),"upgrades did not follow the new price curve");
            }
            Expect(Manor.BuildingOutput("cottage",3).Contains("5 住房") &&
                Manor.BuildingOutput("farm",3).Contains("3 行军"),"preview differs from awarded capacity");
        });
        check("the field commander advances, heals and lands melee strikes; older seeds retain their movement flag",() => {
            var c=new Deployment { manorHall=1 };
            c.red.Add(new UnitPlacement("hero_commander",-3,0) { level=1 });
            c.red.Add(new UnitPlacement("militia",-4.7f,.8f,"hold") { level=1 });
            c.blue.Add(new UnitPlacement("militia",4,0,"hold"));
            foreach(bool legacy in new[]{false,true})
                using(var battle=new Battle(data,c,new ReferenceFactory(),true,legacy))
                {
                    var leader=battle.Units[0];
                    battle.Units[1].entity.hp-=20;
                    for(int i=0;i<240 && battle.Result==null;i++)battle.Step();
                    Expect(leader.entity.stats.healing>0,"support healing stopped");
                    Expect(legacy ? leader.entity.stats.damageDealt==0 : leader.entity.stats.damageDealt>0,"wrong melee movement");
                }
        });
        check("frontier platforms connect only through the causeway and preserve the six regular maps",() => {
            Expect(data.Maps.Count==6 && !data.Maps.ContainsKey(FrontierMap.Id),"frontier leaked into arena");
            for(float x=FrontierMap.BridgeStart-.5f;x<=FrontierMap.Boundary+.5f;x+=.25f)
                Expect(data.Height(FrontierMap.Id,x,0)==0,"causeway has a physical gap");
            Expect(!Catalog.Finite(data.Height(FrontierMap.Id,-16,6)) && data.Height(FrontierMap.Id,52,20)==0,"coastline is not isolated");
            Expect(data.OrderGround(FrontierMap.Id,52,20) && !data.OrderGround("meadow",52,20) &&
                !data.OrderGround(FrontierMap.Id,-16,6),"order bounds");
            Expect(!FrontierMap.Fell(new V3(52,.8f,20)) && FrontierMap.Fell(new V3(52,-5,20)),"fall bounds");
            V3 outward=FrontierMap.Waypoint(new V3(-30,0,7),new V3(24,0,-15),0);
            V3 inward=FrontierMap.Waypoint(new V3(24,0,-15),new V3(-30,0,7),0);
            Expect(outward.x<FrontierMap.BridgeStart && inward.x>FrontierMap.Boundary && outward.z==0 && inward.z==0,"routes bypassed the causeway");
        });
        check("patrols cap the party at sixteen including heroes, and zero food only allows three",() => {
            var m=new Manor { cleared=3,coin=1000,timber=1000,food=100 };
            Expect(m.FrontierLimit==8,"founding patrol capacity");
            m.Upgrade(0);m.Upgrade(0);
            for(int i=1;i<=4;i++)m.Build(i,"irrigated_field");
            m.Build(5,"tavern");m.Assign(5,1);m.HireHero(5,data.Heroes["hero_vanguard"]);
            m.stock["militia"]=17; m.stock["spikes"]=1;
            var roster=new Dictionary<string,int>{{"militia",15},{"hero_vanguard",1}};
            Expect(m.FrontierLimit==16 && m.ForceAddBlock(data,new Dictionary<string,int>{{"militia",16}},"militia",true).Contains("人数"),"patrol cap");
            var sortie=m.PrepareFrontier(data,roster);
            Expect(sortie.config.red.Count==16 && sortie.config.blue.Count==15 && sortie.Matches(sortie.config),"patrol roster");
            Reject(()=>data.Import(Json.Write(sortie.config)));
            Reject(()=>m.PrepareFrontier(data,new Dictionary<string,int>{{"militia",17}}));
            Reject(()=>m.PrepareFrontier(data,new Dictionary<string,int>{{"militia",1},{"spikes",1}}));
            m.food=0;
            Expect(m.FrontierLimit==3,"shortage cap");
            Reject(()=>m.PrepareFrontier(data,Party(4,0)));
        });
        check("frontier guards engage only exposed units, stop at the gate, and intercept a launched projectile",() => {
            var m=new Manor();
            var s=m.PrepareFrontier(data,Party(1,0));
            using(var b=new Battle(data,s.config,new ReferenceFactory { freezeUnits=true },true))
            {
                var ally=b.Units[0];
                var guard=b.Units[1];
                var archer=b.Units[5];
                ReferenceWorld.Position(ally.entity.body,FrontierMap.Boundary-.2f,0);
                ReferenceWorld.Position(guard.entity.body,FrontierMap.Boundary-2,1.1f);
                b.Step();
                Expect(guard.entity.body.Position.x==FrontierMap.EnemyStop && guard.target==null,"safe gate");
                b.Hit(ally,60,guard.entity,10);
                Expect(ally.entity.hp==ally.entity.maxHp,"combat crossed safe gate");
                ReferenceWorld.Position(ally.entity.body,1,0);
                ReferenceWorld.Position(guard.entity.body,9,0);
                ReferenceWorld.Position(archer.entity.body,8,0);
                b.MarchFrontier(20,0);
                for(int i=0;i<60 && b.Shots.Count==0;i++)b.Step();
                Expect(guard.target==ally && b.Units[6].target==null && b.Units[11].target==null,"wrong patrol aggro");
                var shot=b.Shots.First(p=>p.owner==archer.entity);
                b.MarchFrontier(FrontierMap.Rally.x,0);
                ReferenceWorld.Position(ally.entity.body,FrontierMap.Boundary-.4f,0);
                b.Step();
                Expect(guard.target==null && archer.target==null && ally.target==null,"guard chased into safety");
                for(int i=0;i<150 && !shot.removed;i++)b.Step();
                Expect(shot.removed && FrontierMap.Safe(shot.body.Position.x),"arrow was not intercepted at the boundary");
                Expect(b.Units.All(u=>u.entity.alive) && b.IssueOrder("red",new Order("inspire",52,20),out _),"far units or orders used arena bounds");
            }
        });
        check("march routes pass both tactical fences and keep selected endpoints on walkable ground",() => {
            foreach(var wall in data.Frontier.walls)
            {
                bool horizontal=wall.hx>wall.hz;
                var offset=horizontal?new V3(0,0,6):new V3(6,0,0);
                var center=new V3(wall.x,0,wall.z);
                var p=center-offset; var goal=center+offset;
                for(int i=0;i<400 && V3.DistanceXZ(p,goal)>.12f;i++)
                {
                    var direction=FrontierMap.AroundFence(p,goal,wall)-p;
                    p+=direction.Normalized*Math.Min(.08f,direction.Planar);
                    Expect(Math.Abs(p.x-wall.x)>wall.hx || Math.Abs(p.z-wall.z)>wall.hz,"march crosses palisade");
                }
                Expect(V3.DistanceXZ(p,goal)<.13f,"march stalled at palisade");
            }
        });
        check("a downed frontier guard releases its pursuit target",() => {
            var s=new Manor().PrepareFrontier(data,Party(1,0));
            using(var b=new Battle(data,s.config,new ReferenceFactory { freezeUnits=true },true))
            {
                var ally=b.Units[0];var guard=b.Units[1];
                var camp=FrontierMap.CampCenter(0);
                ReferenceWorld.Position(ally.entity.body,camp.x-4,camp.z);
                ReferenceWorld.Position(guard.entity.body,camp.x,camp.z);
                b.MarchFrontier(camp.x,camp.z);b.Step();
                Expect(guard.target==ally,"guard never acquired the exposed party");
                b.Hit(guard,10000,ally.entity,0);
                b.MarchFrontier(FrontierMap.Rally.x,0);b.Step();
                Expect(!guard.entity.alive && guard.target==null,"dead guard retained a pursuit target");
            }
        });
        check("a sixteen-person moving patrol reuses grid storage, march slots and prepared snapshots",() => {
            var m=new Manor {coin=1000,timber=1000,cleared=3};m.Upgrade(0);m.Upgrade(0);
            for(int i=1;i<=3;i++)m.Build(i,"irrigated_field");
            m.stock["militia"]=16;
            var sortie=m.PrepareFrontier(data,new Dictionary<string,int>{{"militia",16}});
            Expect(sortie.config.red.Select(u=>u.z).Distinct().Count()==4,"rally did not form four files");
            using(var b=new Battle(data,sortie.config,new ReferenceFactory(),true))
            {
                var frame=b.PrepareSnapshot();
                b.MarchFrontier(-13,0);
                for(int i=0;i<12;i++){b.Step();b.Snapshot(frame);}
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<120;i++){b.Step();b.Snapshot(frame);}
                Expect(GC.GetAllocatedBytesForCurrentThread()==before,"patrol movement or snapshot allocated");
                Expect(b.Result==null&&b.Units.Take(16).All(u=>FrontierMap.Safe(u.entity.body.Position.x)),"safe march crossed the boundary");
            }
        });
        check("withdrawal saves frontier casualties and patrol loot once without advancing the campaign",() => {
            var m=new Manor { raidReady=true,cleared=1 };
            var s=m.PrepareFrontier(data,Party(2,0));
            using(var b=new Battle(data,s.config,new ReferenceFactory { freezeUnits=true },true))
            {
                for(int i=2;i<5;i++)b.Hit(b.Units[i],10000,null,0);
                b.EndFrontier();
                var next=m.ReturnedState(s,b.Result,out var receipt);
                Expect(receipt.frontier && receipt.withdrew && receipt.defeated==3 && receipt.coin==9 && receipt.timber==3 && receipt.arms==2 &&
                    next.cleared==1 && next.raidReady && next.ArmySize==m.ArmySize,"frontier withdrawal settlement");
                Reject(()=>m.Settle(s,b.Result));
                Expect(Manor.Import(next.Export()).coin==m.coin+9,"frontier loot did not persist");
            }
        });
    }
}

