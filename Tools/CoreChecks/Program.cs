using System;using System.IO;using System.Collections.Generic;using System.Linq;using Frontline.Core;
class Program
{
 static int passed,failed;static Catalog db;
 static void Check(string name,Action body){try{body();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
 static void Assert(bool condition,string message="assertion failed"){if(!condition)throw new Exception(message);}static void Reject(Action body){try{body();}catch{return;}throw new Exception("invalid input accepted");}
 static Deployment Duel(){return new Deployment{red=new List<UnitPlacement>{new UnitPlacement("militia",-6,0)},blue=new List<UnitPlacement>{new UnitPlacement("militia",6,0)},commander=new CommanderConfig{enabled=true}};}
 static int Main(string[] args)
 {
  db=new Catalog(File.ReadAllText("Unity/Assets/Frontline/Resources/catalog.json"));var factory=new ReferenceFactory();
  Check("catalog preserves 10 units, 5 props, 6 maps, 6 missions",()=>Assert(db.Units.Count==10&&db.Props.Count==5&&db.Maps.Count==6&&db.Data.missions.Count==6));
  Check("JSON rejects prototype pollution",()=>Reject(()=>Json.Parse("{\"x\":{\"__proto__\":1}}")));
  Check("JSON rejects duplicate keys",()=>Reject(()=>Json.Parse("{\"x\":1,\"x\":2}")));
  Check("JSON rejects NaN/infinity/trailing data",()=>{Reject(()=>Json.Parse("NaN"));Reject(()=>Json.Parse("1e999"));Reject(()=>Json.Parse("{}{}"));});
  Check("JSON requires integer seed",()=>Reject(()=>db.Import(Json.Write(Duel()).Replace("\"seed\":42","\"seed\":42.5"))));
  Check("JSON requires complete formation schema",()=>Reject(()=>db.Import("{}")));
  Check("JSON serializes diagnostic anonymous values",()=>Assert(Json.Write(new {rows=new[]{new {steps=60}}}).Contains("\"steps\":60")));
  foreach(string version in new[]{"0.1.0","0.2.0","0.3.0","0.4.0","0.5.0","0.6.0"})Check("formation import "+version,()=>{var c=Duel();c.version=version;Assert(db.Import(Json.Write(c)).version=="0.6.0");});
  foreach(string map in db.Maps.Keys)Check("default army on "+map,()=>{var c=db.Default(map);Assert(c.red.Count>0&&c.blue.Count>0);db.Validate(c);Assert(c.red.All(u=>Catalog.Finite(db.Height(map,u.x,u.z))));});
  Check("invalid coordinate/team/budget rejected",()=>{var c=Duel();c.red[0].x=6;Reject(()=>db.Validate(c));c=Duel();c.budget=10;Reject(()=>db.Validate(c));c=Duel();c.props.Add(new PropPlacement{type="barrel",team="evil",x=-5});Reject(()=>db.Validate(c));});
  Check("placement atomicity and overlap",()=>{var c=Duel();string before=Json.Write(c);Reject(()=>db.Place(c,"red",new[]{new UnitPlacement("shield",-6,0)}));Assert(before==Json.Write(c));});
  Check("hotseat privacy",()=>{var c=Duel();c.props.Add(new PropPlacement{type="barrel",team="blue",x=8});var v=Catalog.Visible(c,"red");Assert(v.blue.Count==0&&v.props.Count==0&&c.blue.Count==1);Assert(Catalog.Visible(c,"handoff").red.Count==0);});
  Check("diagnosis reads public data only",()=>{var c=Duel();string first=db.Diagnose(Catalog.Visible(c,"red"),"red");c.blue.Add(new UnitPlacement("cavalry",9,3));c.blue.Add(new UnitPlacement("cavalry",9,-3));Assert(first==db.Diagnose(Catalog.Visible(c,"red"),"red"));});
  Check("favorite side mirror",()=>{var c=Duel();var f=db.SaveFavorite(c,"red","test");var blue=db.LoadFavorite(c,"blue",f);Assert(blue.blue[0].x==6&&blue.red[0].x==-6);});
  foreach(string style in Catalog.Styles.Take(3))foreach(string map in db.Maps.Keys)Check(style+" legality/determinism / "+map,()=>{var c=db.Default(map,600);var a=Tactics.Plan(db,c,"red",style,c.blue);var b=Tactics.Plan(db,c,"red",style,c.blue);Assert(Json.Write(a)==Json.Write(b));var applied=Tactics.Apply(db,c,"red",a);Assert(applied.red.Count>0&&db.Cost(applied,"red")<=c.budget);});
  Check("oracle legality/determinism/disposal",()=>{var c=db.Default("meadow",100);var a=Tactics.SearchSync(db,c,"red",c.blue,factory);var b=Tactics.SearchSync(db,c,"red",c.blue,factory);Assert(a.style=="oracle"&&Json.Write(a)==Json.Write(b));db.Validate(Tactics.Apply(db,c,"red",a));Assert(factory.live==0&&factory.created>=12);});
  Check("oracle cancellation releases world",()=>{var c=db.Default("meadow",100);var e=Tactics.Search(db,c,"red",c.blue,factory).GetEnumerator();Assert(e.MoveNext());e.Dispose();Assert(factory.live==0);});
  Check("observation exposes public fields only",()=>{using(var b=new Battle(db,Duel(),factory)){var o=Json.Write(AgentProtocol.Observe(b,"red","r1"));Assert(o.Contains("\"hp\"")&&!o.Contains("Random")&&!o.Contains("target")&&!o.Contains("velocity"));}});
  Check("agent rejects overreach and extra fields",()=>{using(var b=new Battle(db,Duel(),factory)){string Cmd(string team,string order)=>"{\"schema\":\"frontline-agent@1\",\"kind\":\"command\",\"requestId\":\"r1\",\"team\":\""+team+"\",\"order\":"+order+"}";Assert(!AgentProtocol.Accept(b,"red","r1",Cmd("blue","null")).ok);Assert(!AgentProtocol.Accept(b,"red","r1",Cmd("red","{\"type\":\"inspire\",\"x\":99,\"z\":0}")).ok);Assert(!AgentProtocol.Accept(b,"red","r1",Cmd("red","{\"type\":\"inspire\",\"x\":-6,\"z\":0,\"damage\":999}")).ok);Assert(b.Commander.points["red"]==3);}});
  Check("agent cooldown and timeout fallback",()=>{using(var b=new Battle(db,Duel(),factory)){var reply=new AgentReply{schema=AgentProtocol.Schema,kind="command",requestId="a",team="red",order=new Order("inspire",-6,0)};Assert(AgentProtocol.Accept(b,"red","a",Json.Write(reply)).ok);Assert(!AgentProtocol.Accept(b,"red","a",Json.Write(reply)).ok);Assert(b.Commander.points["red"]==2);Assert(AgentProtocol.Timeout().skipped);reply.order=null;Assert(AgentProtocol.Accept(b,"red","a",Json.Write(reply)).skipped);}});
  Check("null coordinates reject without breaking legacy null report positions",()=>{using(var b=new Battle(db,Duel(),factory)){var reply=new AgentReply{schema=AgentProtocol.Schema,kind="command",requestId="a",team="red",order=new Order("inspire",-6,0)};var json=Json.Write(reply).Replace("\"x\":-6","\"x\":null");Assert(!AgentProtocol.Accept(b,"red","a",json).ok&&b.Commander.points["red"]==3);Reject(()=>Json.Read<V3>("{\"x\":null,\"y\":0,\"z\":0}"));Assert(Json.Read<UnitReport>("{\"type\":\"barrel\",\"spawn\":null}").spawn.Length==0);}});
  ReplayPackage replay=null;
  Check("snapshot reuse cannot mutate recorded commander/cues/transforms",()=>{using(var b=new Battle(db,Duel(),factory)){var recorded=b.Snapshot();var live=b.Snapshot();string before=Json.Write(recorded);b.IssueOrder("red",new Order("inspire",-6,0),out _);b.Step();b.Snapshot(live);Assert(Json.Write(recorded)==before&&live.commander.log.Count==1);}});
  Check("highlights align with native replay",()=>{using(var b=new Battle(db,Duel(),factory)){var frames=new List<Frame>{b.Snapshot()};Assert(b.IssueOrder("red",new Order("inspire",-6,0),out _));b.Hit(b.Units[1],10000,b.Units[0].entity,0);b.Step();frames.Add(b.Snapshot());replay=Replay.Create(db,b.Config,frames,b.Result);var imported=Replay.Import(db,Json.Write(replay));Assert(imported.highlights.Count==2&&imported.highlights[0].tick==0);Assert(Replay.AfterTick(imported.frames,0)==1&&Replay.AtTime(imported.frames,0)==0);}});
  Check("replay rejects forged points",()=>{var p=Json.Clone(replay);p.frames[1].commander.points["red"]=3;Reject(()=>Replay.Validate(db,p));});
  Check("replay rejects forged quaternion",()=>{var p=Json.Clone(replay);p.frames[0].units[0].t[6]=0;Reject(()=>Replay.Validate(db,p));});
  Check("native seed preserves orders",()=>{var p=Replay.Create(db,replay.config,replay.frames,replay.result,true);Assert(p.kind=="seed"&&p.frames.Count==0&&p.orderLog.Count==1);Replay.Import(db,Json.Write(p));});
  Check("legacy seed requires converter",()=>{var p=Json.Clone(replay);p.version=2;p.engineVersion="0.5.0";p.kind="seed";p.frames.Clear();Reject(()=>Replay.Validate(db,p));});
  Check("Bo3 scoring/seed",()=>{var c=Duel();var s=new BattleSeries(db,c);s.Record(c,replay.result);Assert(s.Archive.wins["red"]==1&&!s.Complete);var n=s.Next();Assert(n.seed==43);s.Record(n,replay.result);Assert(s.Complete&&s.Winner=="red");Reject(()=>s.Next());});
  Check("tournament draw credit and averages",()=>{var t=new Tournament(db,factory,new List<Entrant>{new Entrant{name="A",ai="guardian"},new Entrant{name="B",ai="counter"}},new List<string>{"meadow"},new List<uint>{42},100);var r=Json.Clone(replay.result);r.winner="draw";r.remainingValue["red"]=20;r.remainingValue["blue"]=30;r.time=10;t.Report.matches.Add(new MatchRecord{red=0,blue=1,result=r});t.Summarize();Assert(t.Report.ranking.All(x=>x.draws==1&&x.scoreRate==.5f&&x.winRate==0));Assert(t.Report.matrix[0][1].averageRemaining==20&&t.Report.matrix[1][0].averageRemaining==30);Assert(t.Report.ranking[0].name=="B");});
  foreach(string path in args)Check("legacy trajectory "+Path.GetFileName(path),()=>{var p=Replay.Import(db,File.ReadAllText(path));Assert(p.frames.Count>1&&p.result!=null);});
  Check("prepared snapshots retain independent slot ownership",()=>{using(var b=new Battle(db,Duel(),factory)){var a=b.PrepareSnapshot();var live=b.PrepareSnapshot();b.Snapshot(a);string before=Json.Write(a);for(int i=0;i<12;i++){b.Step();b.Snapshot(live);}Assert(before==Json.Write(a));Assert(!ReferenceEquals(a.units[0].t,live.units[0].t));Assert(!Json.Write(live).Contains("storage"));}});
  Check("prepared snapshot reuses commander, effects and cue objects",()=>{using(var b=new Battle(db,Duel(),factory)){var f=b.PrepareSnapshot();b.IssueOrder("red",new Order("inspire",-6,0),out _);b.Snapshot(f);var order=f.commander.log[0];var cue=f.cues[0];b.Snapshot(f);Assert(ReferenceEquals(order,f.commander.log[0])&&ReferenceEquals(cue,f.cues[0]));}});
  Check("prepared snapshot allocates zero bytes in a stable state",()=>{using(var b=new Battle(db,Duel(),factory)){var f=b.PrepareSnapshot();for(int i=0;i<8;i++)b.Snapshot(f);long start=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<100;i++)b.Snapshot(f);Assert(GC.GetAllocatedBytesForCurrentThread()==start,"steady snapshots allocated managed bytes");}});
  Check("projectile snapshot slots survive count shrink and regrowth",()=>{using(var b=new Battle(db,Duel(),factory)){var f=b.PrepareSnapshot();var shot=new Shot{id=100,type="arrow",body=b.World.Projectile(100,"arrow",new V3(0,2,0),new V3(),1,1)};b.Shots.Add(shot);b.Snapshot(f);var row=f.projectiles[0];var t=row.t;b.Shots.Clear();b.Snapshot(f);Assert(f.projectiles.Count==0);b.Shots.Add(shot);b.Snapshot(f);Assert(ReferenceEquals(row,f.projectiles[0])&&ReferenceEquals(t,row.t));}});
  Check("cached ground samples preserve catalog height on all six maps",()=>{foreach(string map in db.Maps.Keys){using(var b=new Battle(db,db.Default(map),factory)){for(int i=0;i<30;i++){b.Step();foreach(var u in b.Units)if(u.entity.alive){float h=db.Height(map,u.entity.position.x,u.entity.position.z);Assert(h==u.floor||Math.Abs(h-u.floor)<.000001f,"Cached ground changed gameplay height");}}}}});
  Check("dead unit snapshots preserve the legacy null transform schema",()=>{using(var b=new Battle(db,Duel(),factory)){var f=b.PrepareSnapshot();b.Snapshot(f);b.Hit(b.Units[1],10000,b.Units[0].entity,0);b.Snapshot(f);Assert(!f.units[1].alive&&f.units[1].t==null);}});
  Check("0.9.5 cached terrain preserves first match and strict boundaries",()=>{
   var data=new Catalog(File.ReadAllText("Unity/Assets/Frontline/Resources/catalog.json"));
   var zones=data.Maps["meadow"].zones;
   var mud=new Zone{type="mud",x=-6,z=0,hx=1,hz=1};
   var ice=new Zone{type="ice",x=-6,z=0,hx=1,hz=1};
   var ground=new Zone{type="ground",x=-6,z=0,hx=1,hz=1};
   float Impulse(params Zone[] ordered)
   {
    zones.Clear();zones.AddRange(ordered);
    var c=Duel();c.blue[0].stance="hold";
    var probe=new ReferenceFactory{freezeUnits=true};
    using(var b=new Battle(data,c,probe)){b.Step();return probe.lastUnitImpulse.x;}
   }
   var unit=data.Units["militia"];
   Assert(Impulse()==unit.speed*unit.mass*.16f,"no-zone movement changed");
   Assert(Impulse(mud,ice)==unit.speed*.45f*unit.mass*.16f,"first mud zone lost precedence");
   Assert(Impulse(ice,mud)==unit.speed*unit.mass*.035f,"first ice zone lost precedence");
   Assert(Impulse(ground,mud)==Impulse(),"a ground zone must still stop the search");
   mud.x=-5;Assert(Impulse(mud)==Impulse(),"zone edges must stay outside");
  });
  Check("0.9.5 cached melee structures preserve allegiance, order and removal",()=>{
   var data=new Catalog(File.ReadAllText("Unity/Assets/Frontline/Resources/catalog.json"));
   foreach(string team in new[]{"red","blue"})
   {
    string enemy=Catalog.Other(team);float x=team=="red"?-5.2f:5.2f;
    var map=data.Maps["meadow"];
    map.props=new List<PropPlacement>{
     new PropPlacement{type="banner",team="neutral",x=x},
     new PropPlacement{type="barricade",team=team,x=x},
     new PropPlacement{type="barricade",team="neutral",x=x},
     new PropPlacement{type="banner",team=enemy,x=x},
     new PropPlacement{type="barricade",team=enemy,x=x},
    };
    var c=Duel();c.Army(enemy)[0].stance="hold";
    using(var b=new Battle(data,c,new ReferenceFactory{freezeUnits=true}))
    {
     var attacker=b.Units[team=="red"?0:1];
     for(int i=2;i<=4;i++)b.Props[i].entity.hp=1;
     for(int i=2;i<=4;i++)
     {
      attacker.cooldown=0;b.Step();
      Assert(!b.Props[i].entity.alive,"first living hostile structure must take the hit");
      for(int later=i+1;later<=4;later++)Assert(b.Props[later].entity.alive,"later structures must wait");
     }
     attacker.cooldown=0;b.Step();
     Assert(b.Props.Take(2).All(p=>p.entity.hp==p.entity.maxHp),"friendly structure or neutral banner was attacked");
    }
   }
  });
  Check("0.9.5 fortify eligibility matches the four original melee types",()=>{
   var c=Duel();c.budget=10000;c.red.Clear();int i=0;
   foreach(var unit in db.Units.Keys)c.red.Add(new UnitPlacement(unit,-6+i%2*2,-4+i++/2*2));
   using(var b=new Battle(db,c,factory))
   {
    Assert(b.IssueOrder("red",new Order("fortify",-5,0),out _));
    var units=b.Snapshot().units;
    Assert(units.Where(u=>u.fortified).Select(u=>u.type).OrderBy(type=>type).SequenceEqual(new[]{"giant","militia","shield","spear"}));
   }
  });
  CombatChecks.Run(db,Check);
  Check("all test worlds disposed",()=>Assert(factory.live==0));Console.WriteLine($"Core rule checks: {passed} passed, {failed} failed. Reference physics is a test double; run Unity smoke for real PhysX.");return failed==0?0:1;
 }
}
