using System;
using System.Collections;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    // Opt-in acceptance scenarios. No validation code runs in an ordinary game.
    public sealed partial class VisualValidation
    {
        readonly List<object> combatRows=new List<object>();
        void Scenario(string map)
        {
            app.ReturnToDeploy();app.SetMap(map);app.Config.seed=42;app.Config.budget=10000;app.PlayerLimit=10000;
            app.Config.red.Clear();app.Config.blue.Clear();app.Config.props.Clear();app.Config.commander.enabled=false;
        }
        void Unit(string team,string type,float x,float z,string stance="advance"){app.Config.Army(team).Add(new UnitPlacement(type,x,z,stance));}
        void Begin(Vector3 focus,float zoom=20)
        {
            app.Refresh();app.RequestStart();app.View.Focus(focus,Viewport.Compact?zoom*1.25f:zoom,42,-14);
        }
        IEnumerator UntilTick(int tick)
        {while(app.Battle!=null&&app.Battle.Tick<tick&&app.Battle.Result==null)yield return null;}
        IEnumerator Freeze(string name,string phase,bool controlled=false)
        {
            app.Paused=true;app.Refresh();Record(name+"-"+phase,controlled);yield return Capture(name,phase);app.Paused=false;
        }
        IEnumerator UntilEffect(string type,int deadline)
        {
            while(app.Battle!=null&&app.Battle.Tick<deadline&&app.Battle.Result==null){foreach(var effect in app.Battle.Effects)if(effect.type==type)yield break;yield return null;}
            throw new InvalidOperationException("Combat QA did not produce real "+type+" by tick "+deadline);
        }
        void Record(string scenario,bool controlledImpulse=false)
        {
            var b=app.Battle;float maxSpeed=0,maxFlash=0;int crossed=0;
            foreach(var u in b.Units){maxSpeed=Math.Max(maxSpeed,u.moveSpeed);maxFlash=Math.Max(maxFlash,u.hitFlash);if(u.entity.team=="red"&&u.entity.position.x>3)crossed++;}
            combatRows.Add(new{scenario=scenario,map=b.Config.map,tick=b.Tick,controlledImpulse=controlledImpulse,maxSpeed=maxSpeed,maxHitFlash=maxFlash,redCrossedX3=crossed,frame=b.Snapshot()});
        }
        IEnumerator CombatScenes()
        {
            Scenario("meadow");for(int i=0;i<3;i++){Unit("red","cavalry",-12,-2+i*2);Unit("blue","shield",5,-2+i*2,"hold");Unit("blue","militia",8,-2+i*2,"hold");}Begin(new Vector3(0,.8f,0),22);
            yield return UntilTick(100);yield return Freeze("charge","approach");yield return UntilEffect("charge",600);Record("charge-contact");
            int impact=app.Battle.Tick;for(int i=0;i<6;i++){yield return UntilTick(impact+i*3);yield return Freeze("charge","contact-"+i);}

            Scenario("meadow");Unit("red","militia",-3,0);Unit("red","spear",-5,2);Unit("blue","shield",3,0,"hold");Unit("blue","shield",5,2,"hold");Begin(new Vector3(1,.8f,0),13);
            yield return UntilEffect("block",600);Record("shield-block");impact=app.Battle.Tick;
            for(int i=0;i<5;i++){yield return UntilTick(impact+i*2);yield return Freeze("block","sparks-"+i);}

            Scenario("ruins");Unit("red","catapult",-3.8f,5.5f);Unit("blue","shield",9,5.5f,"hold");Begin(new Vector3(-.7f,.9f,5.5f),15);
            yield return UntilTick(2);yield return Freeze("cart","before-wall");
            // Explicit controlled collision test: one initial velocity impulse, real PhysX wall.
            var cart=app.Battle.Units[0];cart.entity.body.Impulse(new V3(cart.definition.mass*24,0,0));
            yield return UntilTick(70);Record("cart-wall",true);yield return Freeze("cart","wall-contact",true);

            Scenario("bridge");for(int i=0;i<8;i++)Unit("red",i%3==0?"shield":"militia",-6-(i/2)*2,i%2==0?-.9f:.9f);Unit("blue","giant",14,0,"hold");Unit("blue","medic",16,2,"hold");Begin(new Vector3(0,0,0),23);
            yield return UntilTick(210);Record("bridge-column");yield return Freeze("bridge","column");yield return UntilTick(420);Record("bridge-exit");yield return Freeze("bridge","exit");

            Scenario("highland");Unit("red","cavalry",-12,-1);Unit("red","militia",-10,1);Unit("red","shield",-12,2.5f);Unit("blue","giant",10,0,"hold");Unit("blue","crossbow",13,-2,"hold");Begin(new Vector3(-2,1,0),22);
            yield return UntilTick(110);Record("slope-ascent");yield return Freeze("highland-motion","ascent");yield return UntilTick(210);Record("slope-plateau");yield return Freeze("highland-motion","plateau");
            yield return Save("combat-"+Application.platform+"-"+Screen.width+".json",System.Text.Encoding.UTF8.GetBytes(Json.Write(combatRows)),"application/json");
            app.ReturnToDeploy();app.View.Focus(Vector3.zero,Viewport.Compact?70:40,51,0);
        }
    }
}
