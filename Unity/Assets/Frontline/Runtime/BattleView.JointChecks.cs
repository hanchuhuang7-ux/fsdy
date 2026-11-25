using System;
using UnityEngine;
#if !UNITY_WEBGL
using Unity.Collections;
using Unity.Jobs;
#endif

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        internal static float CaptureFold(Frontline.Core.UnitFrame actor,bool cavalry,float topSpeed)
        {
            float best=0;
            var gait=new BipedGait(actor.gait,actor.moveSpeed,topSpeed,actor.moveForward,0,actor.id);
            var stride=new HorseStride(actor.gait,actor.moveSpeed,actor.charge,0,0,actor.id);
            for(int side=0;side<2;side++)
            {
                var pair=cavalry?HorseStep(Matrix4x4.identity,stride,true,side==1,0,0):
                    HumanStep(Matrix4x4.identity,gait,side==1,1,0,0,0,0,0,false);
                best=Mathf.Max(best,Vector3.Angle(pair.upper.MultiplyVector(Vector3.down),pair.lower.MultiplyVector(Vector3.down)));
            }
            return best;
        }

        internal static void CheckJointRig()
        {
            var origin=new Vector3(.12f,.29f,-.02f);
            var directions=new[]{Vector3.zero,Vector3.up*.16f,Vector3.down*.39f,
                new Vector3(.145f,-.31f,.19f),Vector3.forward*3};
            foreach(var delta in directions)
            {
                var pair=TwoBone(Matrix4x4.identity,origin,origin+delta,Vector3.up,.27f,.25f,out var end);
                var joint=(Vector3)pair.lower.GetColumn(3);
                Demand((pair.upper.MultiplyPoint3x4(Vector3.down*.27f)-joint).magnitude<.0001f,"upper bind");
                Demand((pair.lower.MultiplyPoint3x4(Vector3.down*.25f)-end).magnitude<.0001f,"lower bind");
                Demand(Mathf.Abs((joint-origin).magnitude-.27f)<.0001f&&Mathf.Abs((end-joint).magnitude-.25f)<.0001f,"length");
                if(delta.magnitude>.021f&&delta.magnitude<.519f)Demand((origin+delta-end).sqrMagnitude<1e-8f,"reachable goal");
            }
            float largestKnee=0,largestHock=0,largestElbow=0,plantError=0;
            for(int i=0;i<96;i++)
            {
                var gait=new BipedGait(i/80f,3.4f,3.6f,1,1,0);
                var step=new Footfall(gait.phase,.63f-.14f*gait.run);
                var leg=HumanStep(Matrix4x4.identity,gait,false,1,0,0,.13f,-.06f,0,false);
                var end=leg.lower.MultiplyPoint3x4(Vector3.down*.263f);
                if(step.lift==0&&Mathf.Abs(step.travel)<.24f)
                    plantError=Mathf.Max(plantError,Mathf.Abs(end.y-(-.735f+.13f*end.z-.06f*end.x)));
                largestKnee=Mathf.Max(largestKnee,Angle(leg));
                var stride=new HorseStride(i/54f,5.8f,1,0,0,1);
                largestHock=Mathf.Max(largestHock,Angle(HorseStep(Matrix4x4.identity,stride,true,false,0,0)));
            }
            PoseFrustum frustum=default;
            var inputs=new PoseInput[128];
            for(int i=0;i<inputs.Length;i++)
            {
                var style=(AttackStyle)(i%8);
                inputs[i]=new PoseInput {id=i,kind=style==AttackStyle.Lance?SoldierKind.Cavalry:
                    style==AttackStyle.Siege?SoldierKind.Catapult:style==AttackStyle.Slash?SoldierKind.Shield:SoldierKind.Infantry,
                    scale=1.1f,hp=.8f,angle=i*.17f,speed=i%6,maxSpeed=4,style=style,gait=i*.074f,guard=i%3*.4f,
                    charge=.82f,distant=i%13==6,direction=1,slope=.13f,crossSlope=-.06f,turn=.4f,action=new ActionSample(i%2==0?ActionStage.Draw:ActionStage.Release,.91f)};
            }
            var bow=inputs[3];bow.kind=SoldierKind.Infantry;bow.style=AttackStyle.Aim;
            bow.action=new ActionSample(ActionStage.Draw,1);
            ComputePose(bow,frustum,1,false,false,out var aim);
            largestElbow=Angle(aim.joints.rightArm);
            var forward=aim.root.inverse.MultiplyVector(aim.joints.head.MultiplyVector(Vector3.forward)).normalized;
            Demand(Mathf.Abs(Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg)<=32.01f&&Mathf.Abs(Mathf.Asin(forward.y)*Mathf.Rad2Deg)<=15.01f,"neck limits");
            Demand(largestKnee>70&&largestHock>85&&largestElbow>55&&plantError<.001f,"swing and slope");
            var shield=inputs[0];shield.action=default;shield.kind=SoldierKind.Shield;shield.style=AttackStyle.Slash;
            shield.guard=0;ComputePose(shield,frustum,1,false,false,out var rest);
            shield.guard=1;ComputePose(shield,frustum,1,false,false,out var block);
            var hand=block.joints.leftArm.lower.MultiplyPoint3x4(Vector3.down*.25f);
            Demand((hand-block.joints.shield.MultiplyPoint3x4(new Vector3(-.30f,-.03f,.199f))).sqrMagnitude<1e-7f,"shield grip");
            Demand(rest.root.m13-block.root.m13>.07f&&block.joints.shield.m13>rest.joints.shield.m13,"guard crouch and lift");
            int compared=0;
#if !UNITY_WEBGL
            using(var jobInputs=new NativeArray<PoseInput>(inputs,Allocator.TempJob))
            using(var results=new NativeArray<PoseOutput>(inputs.Length,Allocator.TempJob))
            {
                for(int level=0;level<2;level++)
                {
                    bool low=level==1;
                    var job=new PoseJob{inputs=jobInputs,outputs=results,time=1.2f,frustum=frustum,lowDetail=low};
                    job.Schedule(inputs.Length,8).Complete();
                    for(int i=0;i<inputs.Length;i++)
                    {
                        ComputePose(inputs[i],frustum,job.time,false,low,out var reference);
                        VerifyPose(results[i],reference);
                        if(low||inputs[i].distant)Demand(reference.joints.head==Matrix4x4.zero,"low-detail rig was evaluated");
                        compared++;
                    }
                }
            }
#endif
            Debug.Log("FRONTLINE_JOINT_IK_OK compared="+compared+" knee="+largestKnee+" horse="+largestHock+
                " elbow="+largestElbow+" plantedAnkleError="+plantError);

            static float Angle(in BonePair limb)=>Vector3.Angle(limb.upper.MultiplyVector(Vector3.down),limb.lower.MultiplyVector(Vector3.down));
            static void Demand(bool ok,string check){if(!ok)throw new InvalidOperationException("Articulated rig failed "+check);}
        }
    }
}
