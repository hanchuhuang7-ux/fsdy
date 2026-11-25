using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        struct HeadingSample {public float angle,time,turn;}
        readonly HeadingSample[] headingSamples=new HeadingSample[VisualUnitSlots];
        float TurnRate(int id,float angle)
        {
            if((uint)id>=headingSamples.Length)return 0;
            ref var sample=ref headingSamples[id];
            float time=PoseTime(),dt=time-sample.time;
            if(dt!=0)
            {
                sample.turn=dt>0&&dt<.15f?Mathf.Clamp(Mathf.DeltaAngle(sample.angle,angle*Mathf.Rad2Deg)/(dt*310),-1,1):0;
                sample.time=time;sample.angle=angle*Mathf.Rad2Deg;
            }
            return sample.turn;
        }

#if !UNITY_WEBGL
        static bool EqualPair(in BonePair a,in BonePair b)=>EqualBits(a.upper,b.upper)&&EqualBits(a.lower,b.lower)&&EqualBits(a.solePitch,b.solePitch);
        static bool EqualJoints(in JointPose a,in JointPose b)=>
            EqualPair(a.leftLeg,b.leftLeg)&&EqualPair(a.rightLeg,b.rightLeg)&&EqualPair(a.leftArm,b.leftArm)&&EqualPair(a.rightArm,b.rightArm)&&
            EqualPair(a.hindL,b.hindL)&&EqualPair(a.hindR,b.hindR)&&EqualPair(a.foreL,b.foreL)&&EqualPair(a.foreR,b.foreR)&&
            EqualBits(a.head,b.head)&&EqualBits(a.item,b.item)&&EqualBits(a.shield,b.shield)&&EqualBits(a.wind,b.wind)&&EqualBits(a.draw,b.draw)&&EqualBits(a.hipTwist,b.hipTwist);
#endif
    }
}
