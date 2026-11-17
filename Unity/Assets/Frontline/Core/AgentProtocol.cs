// Persistence uses strict Frontline.Core.Json; Unity inspector serialization is intentionally unused.
#pragma warning disable UAC1015, UAC1009
using System;
using System.Collections.Generic;
using System.Linq;
namespace Frontline.Core
{
    [Serializable] public class ObservedUnit {public int id;public string team,type;public float x,y,z,hp,maxHp;}
    [Serializable] public class ObservedProp {public int id;public string team,type;public float x,z,hp;public bool alive;}
    [Serializable] public class ObservedCommander {public bool enabled;public Dictionary<string,int> points,readyAt;}
    [Serializable] public class Observation {public string schema="frontline-agent@1",kind="observation",requestId,team,map;public int round,tick,interval=60;public List<ObservedUnit> units=new List<ObservedUnit>();public List<ObservedProp> props=new List<ObservedProp>();public ObservedCommander commander;public Dictionary<string,float> power;}
    [Serializable] public class AgentReply {public string schema,kind,requestId,team;public Order order;}
    [Serializable] public class CommandReceipt {public bool ok,skipped;public string reason;}
    public static class AgentProtocol
    {
        public const string Schema="frontline-agent@1";public const int Interval=60,MaxLine=16384;
        public static Observation Observe(Battle battle,string team,string requestId,int round=1)
        {
            if(!Catalog.Team(team))throw new ArgumentException("阵营无效");var o=new Observation{requestId=requestId,team=team,round=round,tick=battle.Tick,map=battle.Config.map,commander=new ObservedCommander{enabled=battle.Config.commander.enabled,points=Json.Clone(battle.Commander.points),readyAt=Json.Clone(battle.Commander.readyAt)},power=new Dictionary<string,float>{{"red",battle.Power("red")},{"blue",battle.Power("blue")}}};
            foreach(var u in battle.Units)if(u.entity.alive){var e=u.entity;var p=e.body.Position;o.units.Add(new ObservedUnit{id=e.id,team=e.team,type=e.type,x=p.x,y=p.y,z=p.z,hp=e.hp,maxHp=e.maxHp});}
            foreach(var p in battle.Props){var e=p.entity;o.props.Add(new ObservedProp{id=e.id,team=e.team,type=e.type,x=e.spawn.x,z=e.spawn.z,hp=Math.Max(0,e.hp),alive=e.alive});}return o;
        }
        public static CommandReceipt Accept(Battle battle,string team,string requestId,string json)
        {
            try{
                var root=Json.Parse(json,MaxLine) as Dictionary<string,object>;if(root==null||root.Keys.Any(k=>!new[]{"schema","kind","requestId","team","order"}.Contains(k))||!root.ContainsKey("order"))throw new FormatException("命令字段无效");
                var reply=Json.Read<AgentReply>(json,MaxLine);if(reply.schema!=Schema||reply.kind!="command"||reply.requestId!=requestId||reply.team!=team||!Catalog.Team(team))throw new FormatException("协议、请求编号或阵营不匹配");
                if(root["order"]==null)return new CommandReceipt{ok=true,skipped=true};var order=root["order"] as Dictionary<string,object>;if(order==null||order.Count!=3||!order.ContainsKey("type")||!order.ContainsKey("x")||!order.ContainsKey("z"))throw new FormatException("只接受军令 type/x/z");
                bool accepted=battle.IssueOrder(team,reply.order,out var reason);return new CommandReceipt{ok=accepted,reason=reason};
            }catch(Exception e){return new CommandReceipt{reason=e.Message};}
        }
        public static CommandReceipt Timeout(){return new CommandReceipt{ok=true,skipped=true,reason="timeout"};}
    }
}

