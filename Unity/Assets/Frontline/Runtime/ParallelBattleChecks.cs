using System;
using System.Collections.Generic;
using System.IO;
using Frontline.Core;

namespace Frontline.UnityGame
{
    // Shared acceptance check for .NET reference physics and Unity PhysX, without Unity dependencies.
    static class ParallelBattleChecks
    {
        internal static object Verify(Catalog data,Deployment config,IWorldFactory factory)
        {
            using(var serial=new Battle(data,config,factory,targetScanWorkers:1))
            using(var parallel=new Battle(data,config,factory,targetScanWorkers:Battle.TargetWorkerCap))
            using(var left=new MemoryStream())
            using(var right=new MemoryStream())
            using(var a=new BinaryWriter(left))
            using(var b=new BinaryWriter(right))
            {
                if(config.commander.enabled)
                {
                    var focus=new Order("focus",0,0);
                    if(!serial.IssueOrder("red",focus,out var reason)||!parallel.IssueOrder("red",focus,out reason))
                        throw new InvalidOperationException("Test order rejected: "+reason);
                }

                var serialFrame=serial.PrepareSnapshot();
                var parallelFrame=parallel.PrepareSnapshot();
                while(true)
                {
                    left.Position=right.Position=0;
                    WriteFrame(a,serial.Snapshot(serialFrame),serial);
                    WriteFrame(b,parallel.Snapshot(parallelFrame),parallel);
                    if(!left.GetBuffer().AsSpan(0,(int)left.Position).SequenceEqual(right.GetBuffer().AsSpan(0,(int)right.Position)))
                        throw new InvalidOperationException("Single-thread and parallel snapshot bits diverged at tick "+serial.Tick);
                    if(serial.Result!=null||parallel.Result!=null)break;
                    serial.Step();
                    parallel.Step();
                }

                if(serial.Result==null||parallel.Result==null||Json.Write(serial.Result)!=Json.Write(parallel.Result))
                    throw new InvalidOperationException("Single-thread and parallel battle reports diverged");
                if(serial.ParallelTargetScanTicks!=0||parallel.TargetScanWorkers>1&&parallel.ParallelTargetScanTicks==0)
                    throw new InvalidOperationException("Determinism check did not exercise the requested scan modes");

                return new {
                    formation=config.red.Count+"v"+config.blue.Count,seed=config.seed,ticks=serial.Tick,snapshots=serial.Tick+1,
                    winner=serial.Result.winner,redSurvived=serial.Result.red,blueSurvived=serial.Result.blue,
                    workers=parallel.TargetScanWorkers,parallelTicks=parallel.ParallelTargetScanTicks,
                    fallbacks=parallel.ParallelTargetScanFallbacks,bitIdentical=true
                };
            }
        }

        // BinaryWriter preserves the exact IEEE 754 payload, including signed zeros omitted by replay JSON.
        static void WriteFrame(BinaryWriter writer,Frame frame,Battle battle)
        {
            writer.Write(frame.tick);writer.Write(frame.time);writer.Write(frame.result!=null);writer.Write(battle.Random.state);
            writer.Write(frame.units.Count);
            for(int i=0;i<frame.units.Count;i++)
            {
                var u=frame.units[i];
                writer.Write(u.id);writer.Write(u.type);writer.Write(u.team);writer.Write(u.stance);
                writer.Write(u.alive);writer.Write(u.inspired);writer.Write(u.fortified);
                writer.Write(u.hp);writer.Write(u.angle);writer.Write(u.attack);writer.Write(u.charge);
                writer.Write(u.hitFlash);writer.Write(u.hitTiltX);writer.Write(u.hitTiltZ);
                writer.Write(u.moveSpeed);writer.Write(u.moveForward);writer.Write(u.gait);writer.Write(u.guard);
                WriteTransform(writer,u.t);
                WriteStats(writer,battle.Units[i].entity.stats);
                writer.Write(battle.Units[i].target?.entity.id??0);
            }
            writer.Write(frame.props.Count);
            for(int i=0;i<frame.props.Count;i++)
            {
                var p=frame.props[i];
                writer.Write(p.id);writer.Write(p.type);writer.Write(p.team);
                writer.Write(p.x);writer.Write(p.z);writer.Write(p.floor);writer.Write(p.hp);
                writer.Write(p.alive);writer.Write(p.active);
                WriteStats(writer,battle.Props[i].entity.stats);
            }
            writer.Write(frame.projectiles.Count);
            foreach(var s in frame.projectiles)
            {
                writer.Write(s.id);writer.Write(s.type);WriteTransform(writer,s.t);
                writer.Write(s.v.x);writer.Write(s.v.y);writer.Write(s.v.z);
            }
            writer.Write(frame.ragdolls.Count);
            foreach(var corpse in frame.ragdolls)
            {
                writer.Write(corpse.id);writer.Write(corpse.type);writer.Write(corpse.team);writer.Write(corpse.age);
                WriteTransform(writer,corpse.wreck);
                writer.Write(corpse.parts.Count);
                foreach(var part in corpse.parts){writer.Write(part.name);WriteTransform(writer,part.t);}
            }
            writer.Write(frame.effects.Count);
            foreach(var effect in frame.effects)
            {
                writer.Write(effect.id);writer.Write(effect.type);writer.Write(effect.x);writer.Write(effect.y);writer.Write(effect.z);
                writer.Write(effect.life);writer.Write(effect.maxLife);writer.Write(effect.angle);
            }
            writer.Write(frame.cues.Count);
            foreach(var cue in frame.cues)
            {
                writer.Write(cue.id);writer.Write(cue.tick);writer.Write(cue.type);
                writer.Write(cue.x);writer.Write(cue.y);writer.Write(cue.z);writer.Write(cue.strength);
            }
            var orders=frame.commander;
            writer.Write(orders.points["red"]);writer.Write(orders.points["blue"]);
            writer.Write(orders.readyAt["red"]);writer.Write(orders.readyAt["blue"]);
            WriteOrders(writer,orders.active);
            WriteOrders(writer,orders.log);
        }

        static void WriteTransform(BinaryWriter writer,float[] values)
        {
            writer.Write(values?.Length??-1);
            if(values!=null)foreach(float value in values)writer.Write(value);
        }

        static void WriteOrders(BinaryWriter writer,List<OrderEntry> orders)
        {
            writer.Write(orders.Count);
            foreach(var order in orders)
            {
                writer.Write(order.id);writer.Write(order.tick);writer.Write(order.endTick);
                writer.Write(order.team);writer.Write(order.type);writer.Write(order.x);writer.Write(order.z);
            }
        }

        static void WriteStats(BinaryWriter writer,Contribution c)
        {
            writer.Write(c.damageDealt);writer.Write(c.damageTaken);writer.Write(c.healing);writer.Write(c.friendlyFire);
            writer.Write(c.kills);writer.Write(c.falls);writer.Write(c.fallKills);writer.Write(c.friendlyKills);
        }
    }
}
