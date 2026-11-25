using System;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        enum AttackStyle : byte { Slash,Thrust,Hammer,Aim,Throw,Bless,Lance,Siege }
        enum ActionStage : byte { Idle,Draw,Release,Interrupted }

        readonly struct ActionSample
        {
            public readonly ActionStage stage;
            public readonly float progress;
            public ActionSample(ActionStage stage,float progress)
            {
                this.stage=stage;
                this.progress=progress;
            }
        }

        struct AttackClock
        {
            public float at,signal,drawAt,held;
            public ActionSample action;
        }

        struct HealGesture
        {
            public int tick;
            public float age;
        }

        const int VisualUnitSlots=Catalog.MaxUnits*4+1;
        const float RaiseSignal=.85f,DrawSignal=.70f;
        readonly AttackClock[] attackClocks=new AttackClock[VisualUnitSlots];
        readonly HealGesture[] healGestures=new HealGesture[VisualUnitSlots];
        Deployment poseTimeline;
        int poseTick=-1;

        static AttackStyle StyleOf(string type) => type switch
        {
            "spear"=>AttackStyle.Thrust,
            "giant" or "hero_vanguard"=>AttackStyle.Hammer,
            "archer" or "crossbow" or "hero_ranger"=>AttackStyle.Aim,
            "bomber"=>AttackStyle.Throw,
            "medic" or "hero_commander"=>AttackStyle.Bless,
            "cavalry"=>AttackStyle.Lance,
            "catapult"=>AttackStyle.Siege,
            _=>AttackStyle.Slash
        };

        void PrepareSnapshotMotion()
        {
            if(poseTimeline!=Config||Current.tick<poseTick||Current.tick-poseTick>20)
            {
                Array.Clear(attackClocks,0,attackClocks.Length);
                Array.Clear(healGestures,0,healGestures.Length);
                Array.Clear(oldMotion,0,oldMotion.Length);
                Array.Clear(motionHistory,0,motionHistory.Length);
                Array.Clear(headingSamples,0,headingSamples.Length);
                poseTimeline=Config;
                poseTick=-1;
            }
            if(Current.tick==poseTick)return;
            poseTick=Current.tick;
            GatherHealGestures();
        }

        void GatherHealGestures()
        {
            // Recorded healing bursts name the recipient's position. Assign each
            // to the nearest visible friendly healer within the existing heal reach.
            var effects=Current.effects;
            var actors=Current.units;
            int remaining=12;
            for(int i=effects.Count-1;i>=0&&remaining>0;i--)
            {
                var effect=effects[i];
                if(effect.type!="heal")continue;
                remaining--;
                string team=null;
                float closest=1.32f*1.32f;
                foreach(var actor in actors)
                {
                    if(!actor.alive||actor.t==null)continue;
                    float distance=EffectDistance(effect,actor);
                    if(distance>=closest)continue;
                    closest=distance;
                    team=actor.team;
                }
                if(team==null)continue;
                int healer=0;
                closest=6.2f*6.2f;
                foreach(var actor in actors)
                {
                    if(!actor.alive||actor.t==null||actor.team!=team||
                        (actor.type!="medic"&&actor.type!="hero_commander"))continue;
                    float distance=EffectDistance(effect,actor);
                    if(distance>=closest||(uint)actor.id>=healGestures.Length)continue;
                    closest=distance;
                    healer=actor.id;
                }
                if(healer!=0&&healGestures[healer].tick!=poseTick)
                    healGestures[healer]=new HealGesture{tick=poseTick,age=effect.maxLife-effect.life};
            }
        }

        static float EffectDistance(EffectFrame effect,UnitFrame actor)
        {
            float x=actor.t[0]-effect.x,z=actor.t[2]-effect.z;
            return x*x+z*z;
        }

        ActionSample SnapshotAction(UnitFrame unit,UnitVisual model)
        {
            if((uint)unit.id>=attackClocks.Length)
            {
                AttackClock fallback=default;
                return ReadAction(ref fallback,unit.attack,Current.time,model.windup);
            }
            ref var clock=ref attackClocks[unit.id];
            var action=ReadAction(ref clock,unit.attack,Current.time,model.windup);
            if(model.attackStyle!=AttackStyle.Bless||action.stage!=ActionStage.Idle)return action;
            var heal=healGestures[unit.id];
            if(Current.tick==0||heal.tick!=Current.tick)return default;
            const float raise=.10f;
            return heal.age<raise?new ActionSample(ActionStage.Draw,.28f+.72f*heal.age/raise):
                new ActionSample(ActionStage.Release,Mathf.Clamp01((heal.age-raise)/.29f));
        }

        static ActionSample ReadAction(ref AttackClock clock,float signal,float at,float duration)
        {
            // The Core reports .85 once when winding up, then holds .70 until
            // contact. A release starts at 1 and decays at 3 units per second.
            if(!(signal>0))
            {
                clock=default;
                return default;
            }
            if(clock.signal==signal&&clock.at==at)return clock.action;
            var previous=clock.action;
            float dt=at-clock.at;
            bool draw=(signal==RaiseSignal||signal==DrawSignal)&&
                !(previous.stage==ActionStage.Release&&dt<.13f&&signal<clock.signal&&signal!=RaiseSignal);
            if(draw)
            {
                if(previous.stage!=ActionStage.Draw||dt<0||dt>.20f||signal==RaiseSignal)
                    clock.drawAt=at-(signal==DrawSignal?duration*.50f:0);
                clock.held=Mathf.Clamp01(.24f+(at-clock.drawAt)/duration);
                clock.action=new ActionSample(ActionStage.Draw,clock.held);
            }
            else if(signal<DrawSignal&&dt<=.13f&&
                (previous.stage==ActionStage.Draw||previous.stage==ActionStage.Interrupted))
                clock.action=new ActionSample(ActionStage.Interrupted,clock.held*Ease(signal/DrawSignal));
            else clock.action=new ActionSample(ActionStage.Release,Mathf.Clamp01(1-signal));
            clock.signal=signal;
            clock.at=at;
            return clock.action;
        }

        readonly struct AttackKey
        {
            public readonly Vector3 torso,weightShift,weapon,hand;
            public AttackKey(Vector3 torso,Vector3 weightShift,Vector3 weapon,Vector3 hand)
            {
                this.torso=torso;
                this.weightShift=weightShift;
                this.weapon=weapon;
                this.hand=hand;
            }

            public static AttackKey Blend(in AttackKey a,in AttackKey b,float t) => new AttackKey(
                Vector3.LerpUnclamped(a.torso,b.torso,t),Vector3.LerpUnclamped(a.weightShift,b.weightShift,t),
                Vector3.LerpUnclamped(a.weapon,b.weapon,t),Vector3.LerpUnclamped(a.hand,b.hand,t));
        }

        readonly struct AttackClip
        {
            public readonly AttackKey draw,strike,follow;
            public readonly float impactAt,settleAt;
            public AttackClip(AttackKey draw,AttackKey strike,AttackKey follow,float impactAt,float settleAt)
            {
                this.draw=draw;
                this.strike=strike;
                this.follow=follow;
                this.impactAt=impactAt;
                this.settleAt=settleAt;
            }
        }

        // Local-space keys for the shared baked rigs. Pitch is X, twist is Y,
        // and roll is Z. All offsets are in metres before the actor's visual scale.
        static readonly AttackClip[] attackClips={
            // Sword: shoulder cocks back, then the edge crosses the body diagonally.
            new AttackClip(
                new AttackKey(new Vector3(-7,29,-7),new Vector3(.026f,.024f,-.062f),new Vector3(-122,29,-37),new Vector3(.018f,.04f,-.15f)),
                new AttackKey(new Vector3(13,-24,7),new Vector3(-.028f,-.025f,.14f),new Vector3(31,-47,31),new Vector3(-.06f,.02f,.14f)),
                new AttackKey(new Vector3(8,-14,4),new Vector3(-.026f,-.034f,.085f),new Vector3(65,-60,23),new Vector3(-.12f,-.032f,.088f)),.08f,.40f),
            // Spear: tuck the elbow, then reach directly along the forward axis.
            new AttackClip(
                new AttackKey(new Vector3(-6,19,-3),new Vector3(.018f,-.025f,-.042f),new Vector3(-8,-14,-7),new Vector3(.015f,.012f,-.29f)),
                new AttackKey(new Vector3(15,-7,3),new Vector3(-.018f,-.041f,.15f),new Vector3(-10,4,-4),new Vector3(-.03f,.022f,.35f)),
                new AttackKey(new Vector3(6,5,0),new Vector3(0,-.028f,.06f),new Vector3(6,-6,-7),new Vector3(.01f,0,.045f)),.062f,.33f),
            // Hammer: keep the head above the helmet on the draw; drop the chest with the blow.
            new AttackClip(
                new AttackKey(new Vector3(-14,17,-6),new Vector3(.008f,.05f,-.061f),new Vector3(-67,4,-17),new Vector3(-.13f,.24f,-.15f)),
                new AttackKey(new Vector3(22,-9,5),new Vector3(-.005f,-.085f,.12f),new Vector3(82,-9,14),new Vector3(-.105f,.01f,.22f)),
                new AttackKey(new Vector3(15,-6,3),new Vector3(-.007f,-.06f,.085f),new Vector3(110,-14,9),new Vector3(-.08f,-.06f,.065f)),.11f,.52f),
            // Aim: turn the shoulders while keeping the sightline centred through the release.
            new AttackClip(
                new AttackKey(new Vector3(-3,41,-2),new Vector3(0,.011f,-.03f),new Vector3(-22,-35,2),new Vector3(-.13f,.15f,.08f)),
                new AttackKey(new Vector3(-7,34,-2),new Vector3(0,.007f,-.053f),new Vector3(-27,-32,-4),new Vector3(-.12f,.15f,.018f)),
                new AttackKey(new Vector3(2,19,0),new Vector3(0,0,-.017f),new Vector3(-12,-16,-1),new Vector3(-.066f,.06f,.026f)),.044f,.29f),
            // Throw: pass above the shoulder, then let the open hand follow the projectile.
            new AttackClip(
                new AttackKey(new Vector3(-14,28,-8),new Vector3(.021f,.026f,-.060f),new Vector3(-141,17,-23),new Vector3(.022f,.103f,-.174f)),
                new AttackKey(new Vector3(17,-16,8),new Vector3(-.017f,-.031f,.104f),new Vector3(22,-22,20),new Vector3(-.055f,.118f,.22f)),
                new AttackKey(new Vector3(12,-21,4),new Vector3(-.029f,-.040f,.079f),new Vector3(67,-34,18),new Vector3(-.088f,.01f,.10f)),.075f,.49f),
            // Blessing: an open hand answers the staff's slow lift and outward sweep.
            new AttackClip(
                new AttackKey(new Vector3(-7,9,-3),new Vector3(0,.035f,-.034f),new Vector3(-53,13,-19),new Vector3(-.026f,.12f,-.032f)),
                new AttackKey(new Vector3(5,-11,3),new Vector3(0,.023f,.049f),new Vector3(-32,-30,-7),new Vector3(-.013f,.11f,.177f)),
                new AttackKey(new Vector3(2,-7,1),new Vector3(0,0,.030f),new Vector3(-21,-22,6),new Vector3(-.034f,.048f,.09f)),.14f,.59f),
            // Lance: hold the shaft flat against the rider during the last stride.
            new AttackClip(
                new AttackKey(new Vector3(-3,7,-1),new Vector3(.01f,-.005f,-.026f),new Vector3(-15,-10,-9),new Vector3(.01f,.02f,-.123f)),
                new AttackKey(new Vector3(9,-5,3),new Vector3(-.005f,-.04f,.09f),new Vector3(-17,5,-6),new Vector3(-.021f,.001f,.24f)),
                new AttackKey(new Vector3(5,3,0),new Vector3(0,-.014f,.04f),new Vector3(-6,-5,-12),new Vector3(.024f,0,-.048f)),.054f,.40f)
        };

        static AttackKey AttackPose(AttackStyle style,in ActionSample sample)
        {
            if(sample.stage==ActionStage.Idle)return default;
            ref readonly var clip=ref attackClips[(int)style];
            float t=sample.progress;
            if(sample.stage==ActionStage.Draw||sample.stage==ActionStage.Interrupted)
                return AttackKey.Blend(default,clip.draw,Ease(t));
            if(t<clip.impactAt)
                return AttackKey.Blend(clip.draw,clip.strike,.72f+.28f*Ease(t/clip.impactAt));
            float arrest=style==AttackStyle.Hammer?.075f:style==AttackStyle.Slash?.048f:0;
            if(t<clip.impactAt+arrest)
            {
                float rebound=Mathf.Sin((t-clip.impactAt)/arrest*Mathf.PI)*.035f;
                return AttackKey.Blend(clip.strike,clip.draw,rebound);
            }
            if(t<clip.settleAt)
                return AttackKey.Blend(clip.strike,clip.follow,Ease((t-clip.impactAt-arrest)/(clip.settleAt-clip.impactAt-arrest)));
            return AttackKey.Blend(clip.follow,default,Ease((t-clip.settleAt)/(1-clip.settleAt)));
        }

        static float SiegePitch(in ActionSample sample)
        {
            const float rest=-30,loaded=-53,release=68,overshoot=76;
            float t=sample.progress;
            if(sample.stage==ActionStage.Idle)return rest;
            if(sample.stage==ActionStage.Draw||sample.stage==ActionStage.Interrupted)
                return Mathf.LerpUnclamped(rest,loaded,Ease(t));
            if(t<.078f)return Mathf.LerpUnclamped(loaded,release,.88f+.12f*Ease(t/.078f));
            if(t<.21f)return Mathf.LerpUnclamped(release,overshoot,Ease((t-.078f)/.132f));
            float settle=Ease((t-.21f)/.79f),damping=1-settle;
            return Mathf.LerpUnclamped(overshoot,rest,settle)+Mathf.Sin(settle*3*Mathf.PI)*damping*damping*7;
        }
    }
}
