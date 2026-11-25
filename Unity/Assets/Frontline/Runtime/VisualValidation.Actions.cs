using System.Collections;
using System.Collections.Generic;
using System.Text;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class VisualValidation
    {
        enum MotionMoment { Draw,Strike,Guard,Gallop,Heal }

        sealed class MotionCapture
        {
            public readonly string type,name;
            public readonly MotionMoment moment;
            public readonly int hold;
            public int drawTicks;
            public bool captured;

            public MotionCapture(string type,string name,MotionMoment moment,int hold=4)
            {
                this.type=type;this.name=name;this.moment=moment;this.hold=hold;
            }

            public bool Matches(Frame frame,UnitFrame actor)
            {
                switch(moment)
                {
                    case MotionMoment.Draw:
                        drawTicks=actor.attack==.70f?drawTicks+1:0;
                        return drawTicks>=hold;
                    case MotionMoment.Strike:
                        return actor.attack>.88f&&actor.attack<.99f;
                    case MotionMoment.Guard:
                        return actor.guard>.75f&&actor.guard<.91f;
                    case MotionMoment.Gallop:
                        return actor.moveSpeed>4.1f&&actor.charge>.55f;
                    case MotionMoment.Heal:
                        if(actor.attack>0)return false;
                        foreach(var effect in frame.effects)
                        {
                            if(effect.type!="heal"||effect.life<.22f||effect.life>.33f)continue;
                            float x=actor.t[0]-effect.x,z=actor.t[2]-effect.z;
                            if(x*x+z*z<36)return true;
                        }
                        return false;
                    default:return false;
                }
            }
        }

        IEnumerator CheckActionMotion()
        {
            BattleView.CheckActionCurves();
            var view=app.View;
            string original=Json.Write(app.Config);
            app.SetMode("sandbox");
            view.SetQuality(2);
            Require(view.ParallelPoseWorkers==4,"Motion acceptance needs all four pose workers");
            view.CheckParallelPoses=true;
            var evidence=new List<object>();
            app.ImportText(Json.Write(ActionFormation()));
            app.RequestStart();
            Require(app.Battle?.World is UnityWorld,"The action fixture did not start a real PhysX battle");
            app.Paused=true;
            var arenaShots=new[] {
                new MotionCapture("spear","spear-thrust",MotionMoment.Strike),
                new MotionCapture("giant","hammer-windup",MotionMoment.Draw,9),
                new MotionCapture("giant","hammer-impact",MotionMoment.Strike),
                new MotionCapture("archer","archer-draw",MotionMoment.Draw),
                new MotionCapture("crossbow","crossbow-release",MotionMoment.Strike),
                new MotionCapture("cavalry","gallop-lance",MotionMoment.Gallop),
                new MotionCapture("cavalry","lance-strike",MotionMoment.Strike),
                new MotionCapture("shield","sword-slash",MotionMoment.Strike),
                new MotionCapture("shield","shield-block",MotionMoment.Guard),
                new MotionCapture("bomber","throw-windup",MotionMoment.Draw),
                new MotionCapture("bomber","throw-release",MotionMoment.Strike),
                new MotionCapture("medic","medic-blessing",MotionMoment.Heal),
                new MotionCapture("catapult","catapult-release",MotionMoment.Strike)
            };
            app.Battle.Hit(app.Battle.Units[2],35,null,0);
            yield return RecordActionMatch(arenaShots,evidence);
            int compared=view.CheckedParallelPoses;
            int draws=view.CheckedWindupStyles,releases=view.CheckedReleaseStyles;
            Require(compared>0&&(draws&255)==255&&(releases&255)==255,
                "Parallel verification missed action stages: samples="+compared+" draw="+draws+" release="+releases);
            view.CheckParallelPoses=false;
            app.ReturnToDeploy();

            yield return Press("Mode.Manor");
            app.SetManorSpeed(0);
            var estate=WideEstateFixture();
            estate.guideDismissed=true;
            estate.Upgrade(0);estate.Upgrade(0);
            for(int guard=0;guard<6;guard++)estate.RecruitGuard();
            foreach(var hero in app.Data.Data.heroes)
            {
                estate.HireHero(3,hero);
                for(int tier=0;tier<Manor.MaxHeroArmament;tier++)estate.ForgeHero(hero.id);
            }
            app.ImportText(estate.Export());
            var market=new Vector3(FrontierMap.ManorX+3,.98f,.5f);
            bool residentFramed=false;
            for(int attempt=0;attempt<7&&!residentFramed;attempt++)
            {
                view.Focus(market,23,43,-24);
                yield return new WaitForEndOfFrame();
                if(!view.WalkingResidentVisible)continue;
                var site=view.ResidentPoint;
                view.Focus(site+Vector3.up*.83f,4.8f,33,view.ResidentHeading+126+attempt*14);
                yield return new WaitForEndOfFrame();
                residentFramed=view.WalkingResidentVisible&&Vector3.Distance(view.ResidentPoint,site)<.55f;
            }
            Require(residentFramed,"A walking resident did not remain in the portrait framing");
            yield return ManorShot("220-resident-stride");
            view.Focus(market,64,43,-24);
            yield return new WaitForEndOfFrame();
            yield return WaitForState(()=>(view.VisibleSpecialistMask&32)!=0,"Minstrel was not on the manor lanes");
            view.Focus(view.SpecialistPoint(5)+Vector3.up*.87f,6.1f,19,view.SpecialistFacing(5)+146);
            yield return ManorShot("220-minstrel-stride");
            view.Focus(BattleView.RetinuePost(1)+Vector3.up*.90f,6.3f,17,-56);
            yield return WaitForState(()=>view.VisibleRetinue>=3,"The watch was missing from its assigned posts");
            yield return ManorShot("220-watch-breath");

            while(app.ForceCount("militia")>2)app.SelectForce("militia",-1);
            foreach(string hero in Manor.HeroIds)app.SelectForce(hero,1);
            app.PrepareExpedition(0,false);
            app.RequestStart();
            Require(app.InManor&&app.Battle?.World is UnityWorld&&app.Config.manorHall==3,
                "Hero animation test did not start an estate expedition");
            app.Paused=true;
            foreach(var actor in app.Battle.Units)
                if(actor.entity.type=="hero_commander"){app.Battle.Hit(actor,34,null,0);break;}
            yield return RecordActionMatch(new[]{
                new MotionCapture("hero_vanguard","vanguard-hammer",MotionMoment.Draw,8),
                new MotionCapture("hero_ranger","ranger-aim",MotionMoment.Draw),
                new MotionCapture("hero_commander","commander-blessing",MotionMoment.Heal)
            },evidence);
            app.ReturnManor();
            app.SetManorMode(false);
            app.ImportText(original);
            Require(app.Config.manorHall==0&&Json.Write(app.Config)==original,"The animation fixture changed the arena deployment");
            var report=new {version=Application.version,unity=Application.unityVersion,width=Screen.width,height=Screen.height,
                workers=view.ParallelPoseWorkers,checkedParallelPoses=compared,drawStyles=draws,releaseStyles=releases,
                realPhysX=true,manorCaptures=3,captures=evidence,screenshots=evidence.Count+3};
            yield return Save("action-motion-smoke.json",Encoding.UTF8.GetBytes(Json.Write(report)),"application/json");
            Debug.Log("FRONTLINE_ACTION_MOTION_OK "+Json.Write(report));
        }

        Deployment ActionFormation()
        {
            var config=new Deployment{budget=10000};
            Place("red","shield",-3.35f,-8.2f);
            Place("red","spear",-3.35f,-5.0f);
            Place("red","giant",-3.52f,-.15f);
            Place("red","archer",-6.35f,2.5f);
            Place("red","cavalry",-13.1f,6.8f);
            Place("red","catapult",-11,9.7f);
            Place("red","bomber",-7.5f,-8.5f);
            Place("red","crossbow",-8.4f,-.95f);
            Place("red","medic",-6.00f,-4.14f);
            Place("blue","militia",3.3f,-8.2f);
            Place("blue","shield",3.3f,-5.0f);
            Place("blue","shield",3.4f,-.35f);
            Place("blue","shield",3.15f,6.9f,"hold");
            Place("blue","giant",6.8f,8.1f,"hold");
            for(int i=0;i<8;i++)
            {
                Place("red","militia",-15,-9.2f+i*2.45f,"hold");
                Place("blue","militia",10.5f,-9.1f+i*2.58f,"hold");
            }
            return config;

            void Place(string team,string type,float x,float z,string stance="advance")
            {
                Require(app.Data.CanPlace(config,team,type,x,z),"Invalid motion fixture placement: "+type+" at "+x+","+z);
                config.Army(team).Add(new UnitPlacement(type,x,z,stance));
            }
        }

        IEnumerator RecordActionMatch(MotionCapture[] shots,List<object> records,string series="220")
        {
            var battle=app.Battle;
            var view=app.View;
            var snapshot=battle.PrepareSnapshot();
            int outstanding=shots.Length;
            view.Focus(new Vector3(0,.6f,0),43,55,-11);
            for(int tick=0;tick<1450&&battle.Result==null&&outstanding>0;tick++)
            {
                battle.Step();
                battle.Snapshot(snapshot);
                view.Current=snapshot;
                foreach(var capture in shots)
                {
                    if(capture.captured)continue;
                    foreach(var actor in snapshot.units)
                    {
                        if(!actor.alive||actor.t==null||actor.team!="red"||actor.type!=capture.type||!capture.Matches(snapshot,actor))continue;
                        var center=new Vector3(actor.t[0],actor.t[1],actor.t[2]);
                        float angle=actor.angle*Mathf.Rad2Deg;
                        bool horse=actor.type=="cavalry",siege=actor.type=="catapult";
                        var forward=Quaternion.Euler(0,angle,0)*Vector3.forward;
                        bool focusSword=actor.type=="shield";
                        bool aim=actor.type=="archer"||actor.type=="hero_ranger";
                        view.Focus(center+Vector3.up*(horse?.55f:siege?.45f:.27f)+forward*(horse?.35f:.42f),
                            horse?7.8f:siege?8.7f:actor.type=="giant"?7.4f:aim?5.6f:6.3f,
                            horse?20:siege?26:focusSword?24:17,angle+(horse?-80:siege?128:focusSword?-125:125));
                        if(series=="300")
                            view.Focus(center+Vector3.up*(horse?.45f:.22f)+forward*.24f,horse?4.85f:3.85f,
                                horse?14:17,angle+(horse?-76:focusSword?-119:103));
                        if(series=="310")
                            view.Focus(center+Vector3.up*(horse?.40f:siege?.37f:.24f)+forward*.19f,
                                horse?5.21f:siege?7.3f:actor.type=="giant"?5.35f:4.25f,
                                horse?13:siege?23:17,angle+(horse?132:siege?-68:focusSword?103:127));
                        yield return ManorShot(series+"-"+capture.name);
                        if((series=="300"||series=="310")&&capture.type=="shield")
                        {
                            view.Focus(new Vector3(0,.71f,-1.7f),24,30,-47);
                            yield return ManorShot(series+"-arena-engagement");
                        }
                        records.Add(new {name=capture.name,type=actor.type,tick=snapshot.tick,actor=actor.id,
                            moment=capture.moment.ToString(),attack=actor.attack,guard=actor.guard,charge=actor.charge,
                            speed=actor.moveSpeed,expedition=app.InManor});
                        capture.captured=true;
                        outstanding--;
                        view.Focus(new Vector3(0,.6f,0),43,55,-11);
                        break;
                    }
                }
                if(tick%2==0)yield return null;
            }
            foreach(var capture in shots)
                Require(capture.captured,"The real battle did not show "+capture.name+" by tick "+snapshot.tick);
        }
    }

    public sealed partial class BattleView
    {
        internal static void CheckActionCurves()
        {
            AttackClock clock=default;
            var start=ReadAction(ref clock,RaiseSignal,3,.24f);
            var ready=ReadAction(ref clock,DrawSignal,3.18f,.24f);
            var hit=ReadAction(ref clock,1,3.24f,.24f);
            var follow=ReadAction(ref clock,.70f,3.34f,.24f);
            Check(start.stage==ActionStage.Draw&&ready.progress>start.progress&&hit.stage==ActionStage.Release&&
                follow.stage==ActionStage.Release,"Snapshot attack signals did not progress from draw to release");
            clock=default;
            ReadAction(ref clock,RaiseSignal,4,.10f);
            var abort=ReadAction(ref clock,.65f,4.09f,.10f);
            Check(abort.stage==ActionStage.Interrupted,"An interrupted draw became a false impact");
            foreach(var style in new[]{AttackStyle.Slash,AttackStyle.Thrust,AttackStyle.Hammer,AttackStyle.Aim,
                AttackStyle.Throw,AttackStyle.Bless,AttackStyle.Lance})
            {
                var draw=AttackPose(style,new ActionSample(ActionStage.Draw,1));
                var strike=AttackPose(style,new ActionSample(ActionStage.Release,.105f));
                var rest=AttackPose(style,new ActionSample(ActionStage.Release,1));
                Check((draw.weapon-strike.weapon).sqrMagnitude>30&&rest.torso==Vector3.zero&&rest.weapon==Vector3.zero,
                    "Missing distinct attack keys or recovery for "+style);
            }
            Check(SiegePitch(new ActionSample(ActionStage.Draw,1))< -47&&
                SiegePitch(new ActionSample(ActionStage.Release,.13f))>60&&
                SiegePitch(new ActionSample(ActionStage.Release,1))==SiegePitch(default),"The siege arm did not cock and recoil");

            var forward=new BipedGait(.11f,2.3f,3,1,0,0);
            var backward=new BipedGait(.11f,2.3f,3,-1,0,0);
            Check(forward.lean>0&&backward.lean<0&&backward.direction<0,"Reverse drive did not lean away from the threat");
            var sole=new Vector3(0,-.274f,.054f);
            var planted=WalkingLeg(Matrix4x4.identity,forward,Vector3.zero,false).MultiplyPoint3x4(sole);
            var raised=WalkingLeg(Matrix4x4.identity,new BipedGait(.99f,2.3f,3,1,0,0),Vector3.zero,false).MultiplyPoint3x4(sole);
            Check(Mathf.Abs(sole.y-planted.y)<.0001f&&raised.y-sole.y>.045f,"The contact foot drifted or the swing foot did not lift");
            CheckHorse(new HorseStride(1,1,0,0,0,2),true);
            CheckHorse(new HorseStride(1,5.5f,1.3f,0,0,2),false);
            var trot=new HorseStride(1,3.19f,0,0,0,3);
            Check(Mathf.Abs(trot.Offset(false,false)-trot.Offset(true,true))<.05f&&
                Mathf.Abs(trot.Offset(false,true)-trot.Offset(true,false))<.05f,"The trot lost diagonal coordination");

            static void CheckHorse(HorseStride stride,bool slow)
            {
                float a=stride.Offset(false,false),b=stride.Offset(false,true);
                float c=stride.Offset(true,false),d=stride.Offset(true,true);
                Check(a!=b&&a!=c&&a!=d&&b!=c&&b!=d&&c!=d&&(slow?stride.gallop==0:stride.gallop>.95f),
                    "Horse feet are paired incorrectly");
            }
            static void Check(bool ok,string message)
            {
                if(!ok)throw new System.InvalidOperationException(message);
            }
        }
    }
}
