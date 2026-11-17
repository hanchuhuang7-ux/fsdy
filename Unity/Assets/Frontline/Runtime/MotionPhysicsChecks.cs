using System;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public static class MotionPhysicsChecks
    {
        static void Require(bool b,string message){if(!b)throw new InvalidOperationException(message);}
        public static List<object> Run(Catalog original)
        {
            // Controlled test copy removes explosive props so they cannot confound navigation.
            // The shipped catalog, all platform heights and all walls are left untouched.
            var data=new Catalog(Json.Write(original.Data));foreach(var map in data.Maps.Values)map.props.Clear();var rows=new List<object>();
            var c=new Deployment{budget=10000,red=new List<UnitPlacement>{new UnitPlacement("cavalry",-13,0)},blue=new List<UnitPlacement>{new UnitPlacement("shield",5,0,"hold")}};
            using(var b=new Battle(data,c,new UnityWorldFactory()))
            {
                b.Units[0].cooldown=999;float maximum=0;bool charge=false;int contactTick=0;
                while(b.Tick<600&&b.Result==null&&!charge){b.Step();maximum=Math.Max(maximum,b.Units[0].moveSpeed);foreach(var fx in b.Effects)if(fx.type=="charge"){charge=true;contactTick=b.Tick;}}
                Require(maximum>4&&charge&&b.Units[1].entity.hp<220,"Contact-driven cavalry charge failed: speed="+maximum+" tick="+b.Tick+" charge="+b.Units[0].charge);
                rows.Add(new{scenario="contact-charge",maxSpeed=maximum,contactTick=contactTick,targetHp=b.Units[1].entity.hp,attackCooldownDisabled=true});
                Debug.Log("FRONTLINE_CHARGE_OK contactTick="+contactTick+" maxSpeed="+maximum+" targetHp="+b.Units[1].entity.hp);
            }
            foreach(string map in new[]{"bridge","ruins","highland"})
            {
                c=new Deployment{map=map,budget=10000};for(int i=0;i<8;i++)c.red.Add(new UnitPlacement("militia",-6-(i/2)*2,i%2==0?-.9f:.9f));c.blue.Add(new UnitPlacement("giant",16,0,"hold"));
                using(var b=new Battle(data,c,new UnityWorldFactory()))
                {
                    var crossed=new HashSet<int>();int downhillSamples=0;float maxDownhillClearance=0;
                    while(b.Tick<720&&b.Result==null){b.Step();foreach(var u in b.Units)if(u.entity.team=="red"&&u.entity.alive){var p=u.entity.body.Position;if(p.x>3)crossed.Add(u.entity.id);if(map=="highland"&&p.x>3.2f&&p.x<7.8f){downhillSamples++;maxDownhillClearance=Math.Max(maxDownhillClearance,p.y-data.Height(map,p.x,p.z)-.62f*u.definition.scale);}}}
                    if(crossed.Count<6){foreach(var u in b.Units)Debug.Log("MOTION_TRACE "+map+" id="+u.entity.id+" p="+Json.Write(u.entity.body.Position)+" v="+Json.Write(u.entity.body.Velocity)+" floor="+u.floor+" slope="+u.slope+" blocked="+u.blocked+" route="+u.routeStage);}
                    Require(crossed.Count>=6,"Column stalled in "+map+": only "+crossed.Count+" / 8 crossed");
                    if(map=="highland")Require(downhillSamples>100&&maxDownhillClearance<.45f,"Downhill control lost ground: clearance="+maxDownhillClearance+" samples="+downhillSamples);
                    rows.Add(new{scenario=map,steps=b.Tick,crossed=crossed.Count,downhillSamples=downhillSamples,maxDownhillClearance=maxDownhillClearance});
                    Debug.Log("FRONTLINE_MOTION_OK map="+map+" crossed="+crossed.Count+"/8 downhillClearance="+maxDownhillClearance);
                }
            }
            return rows;
        }
    }
}
