using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        struct BonePair
        {
            public Matrix4x4 upper,lower;
            public float solePitch;
        }

        struct JointPose
        {
            public BonePair leftLeg,rightLeg,leftArm,rightArm,hindL,hindR,foreL,foreR;
            public Matrix4x4 head,item,shield;
            public float wind,draw,hipTwist;
        }

        // Everything here works in unscaled rig space. The parent transform applies the actor's
        // uniform presentation scale once. A projected pole fixes the bend plane at either reach limit.
        static BonePair TwoBone(in Matrix4x4 parent,Vector3 origin,Vector3 goal,Vector3 pole,
            float first,float second,out Vector3 wrist)
        {
            Vector3 delta=goal-origin;
            float length=delta.magnitude;
            Vector3 along=length>.00001f?delta/length:Vector3.down;
            float span=Mathf.Clamp(length,Mathf.Abs(first-second)+.0001f,first+second-.0001f);
            Vector3 side=pole-along*Vector3.Dot(pole,along);
            float sq=side.sqrMagnitude;
            if(sq<.00001f)
            {
                side=Vector3.Cross(along,Mathf.Abs(along.x)<.8f?Vector3.right:Vector3.up);
                sq=side.sqrMagnitude;
            }
            float adjacent=(first*first-second*second+span*span)/(2f*span);
            Vector3 knee=origin+along*adjacent+side*Mathf.Sqrt(Mathf.Max(0,first*first-adjacent*adjacent)/sq);
            wrist=origin+along*span;
            return new BonePair {
                upper=BoneFrame(parent,origin,(origin-knee)/first),
                lower=BoneFrame(parent,knee,(knee-wrist)/second)
            };
        }

        // Shortest arc from local +Y to up. Assemble its affine columns directly, avoiding
        // two quaternion interop calls and a general matrix multiply for each limb segment.
        static Matrix4x4 BoneFrame(in Matrix4x4 parent,Vector3 start,Vector3 up)
        {
            Vector3 right,forward;
            if(up.y<-.9999f)
            {
                right=Vector3.Cross(up,Vector3.back).normalized;
                forward=Vector3.Cross(right,up);
            }
            else
            {
                float factor=1/(1+up.y),cross=-up.x*up.z*factor;
                right=new Vector3(1-up.x*up.x*factor,-up.x,cross);
                forward=new Vector3(cross,-up.z,1-up.z*up.z*factor);
            }
            var result=new Matrix4x4();
            result.SetColumn(0,parent.MultiplyVector(right));result.SetColumn(1,parent.MultiplyVector(up));
            result.SetColumn(2,parent.MultiplyVector(forward));result.SetColumn(3,parent.MultiplyPoint3x4(start));
            result.m33=1;
            return result;
        }

        static BonePair HumanStep(in Matrix4x4 hips,in BipedGait gait,bool right,float drive,float drop,float pelvisYaw,
            float forwardSlope,float crossSlope,float ground,bool seated)
        {
            float side=right?1:-1;
            var origin=new Vector3(side*.134f,-.204f-.039f*gait.weight-drop+gait.bob*.62f,0);
            var step=new Footfall(gait.phase+(right?.5f:0),.63f-.14f*gait.run);
            float weight=gait.weight*drive;
            float stride=step.travel*(.40f+.24f*gait.run)*gait.direction*weight;
            float lift=step.lift*weight*(.125f+.16f*gait.run);
            var ankle=new Vector3(side*(.144f+.056f*drop),-.735f+lift+ground,stride+.012f);
            ankle.y+=ankle.z*forwardSlope+ankle.x*crossSlope;
            if(seated)
            {
                origin=new Vector3(side*.15f,-.20f,-.062f);
                ankle=new Vector3(side*.341f,-.647f,.031f);
            }
            origin=Quaternion.Euler(0,pelvisYaw,0)*origin;
            var result=TwoBone(hips,origin,ankle,new Vector3(side*.09f,0,1),.29f,.263f,out _);
            var surface=hips.MultiplyVector(new Vector3(-crossSlope,1,-forwardSlope));
            var up=(Vector3)result.lower.GetColumn(1);var forward=(Vector3)result.lower.GetColumn(2);
            float contact=Mathf.Atan2(Vector3.Dot(surface,forward),Vector3.Dot(surface,up))*Mathf.Rad2Deg;
            result.solePitch=Mathf.Clamp(contact+(seated?9f:step.lift*weight*21f),-78,78);
            return result;
        }

        static BonePair HorseStep(in Matrix4x4 mount,in HorseStride stride,bool front,bool right,float idle,float forwardSlope)
        {
            float x=right?.221f:-.221f,z=front?.37f:-.44f;
            var step=new Footfall(stride.phase+stride.Offset(front,right),
                Mathf.Lerp(Mathf.Lerp(.70f,.55f,stride.trot),.31f,stride.gallop));
            float drive=stride.weight;
            float reach=step.travel*drive*(.48f+.35f*stride.gallop);
            float fold=step.lift*drive*Mathf.Lerp(.17f,front?.50f:.32f,stride.gallop);
            float paw=right&&front?Mathf.Max(0,idle)*(1-drive):0;
            var origin=new Vector3(x,front?-.455f:-.462f,z);
            float toe=z+reach-(front?.145f:.062f)*fold+.043f*paw;
            float floor=-1.345f-stride.bob+fold+.09f*paw+toe*(forwardSlope+Mathf.Sin(stride.pitch*Mathf.Deg2Rad));
            return TwoBone(mount,origin,new Vector3(x*1.035f,floor,toe),
                front?Vector3.forward:Vector3.back,.49f,.48f,out _);
        }

        static Matrix4x4 AtGrip(Vector3 hand,Quaternion rotation,Vector3 bakedGrip) =>
            Pose(hand-rotation*bakedGrip,rotation,Vector3.one);

        static Vector3 Grip(AttackStyle style,bool stock)
        {
            switch(style)
            {
                case AttackStyle.Aim:return stock?new Vector3(.36f,.035f,.22f):new Vector3(.36f,.10f,.51f);
                case AttackStyle.Hammer:return new Vector3(.36f,-.11f,.36f);
                case AttackStyle.Bless:return new Vector3(.36f,-.095f,.28f);
                case AttackStyle.Throw:return new Vector3(.36f,-.035f,.28f);
                case AttackStyle.Thrust:case AttackStyle.Lance:return new Vector3(.36f,.04f,.085f);
                default:return new Vector3(.36f,.10f,-.071f);
            }
        }

        static Matrix4x4 Look(in Matrix4x4 chest,float yaw,float pitch)
        {
            var neck=new Vector3(0,.391f,.009f);
            var rot=Quaternion.Euler(Mathf.Clamp(pitch,-15,15),Mathf.Clamp(yaw,-32,32),0);
            return chest*AtGrip(neck,rot,neck);
        }

        static void SolveJoints(in PoseInput input,in BipedGait step,in AttackKey attack,float weight,float time,
            Quaternion facing,Vector3 basePoint,in HorseStride horse,bool mounted,ref PoseOutput pose)
        {
            ref var rig=ref pose.joints;
            float phase=time*.83f+input.id*2.39996f;
            float idle=(1-step.weight)*(1-weight);
            float glance=Mathf.Sin(phase*.47f)*Mathf.Max(0,Mathf.Sin(phase*.23f))*idle;
            float gripChange=Mathf.Max(0,Mathf.Sin(phase*1.32f)-.62f)*idle;
            float brace=Mathf.Max(input.guard,input.style==AttackStyle.Hammer&&input.action.stage==ActionStage.Release?weight*.74f:0);
            float blow=Mathf.Min(.32f,Mathf.Abs(input.hitTiltX)+Mathf.Abs(input.hitTiltZ));
            float drop=.087f*brace+.075f*blow;
            float lead=attack.torso.y*.49f+step.sway*(3.1f+step.run*2.7f);
            var spine=attack.torso+new Vector3(step.lean*(1-.69f*weight),step.twist*(1-weight),step.roll);
            spine.z+=input.guard*Mathf.Sin(phase*7)*1.9f;
            var shift=attack.weightShift+new Vector3(step.sway*.019f,step.bob*(1-.70f*weight)-drop,0);
            if(blow>.001f)
            {
                var recoil=Quaternion.Inverse(facing)*new Vector3(input.hitTiltX,0,input.hitTiltZ);
                shift+=new Vector3(recoil.x*.29f,-blow*.044f,recoil.z*.29f);
            }
            var hips=Pose(basePoint,facing,Vector3.one*input.scale);
            if(mounted)
            {
                pose.horse=Pose(basePoint+Vector3.up*((.55f+horse.bob)*input.scale),
                    facing*Quaternion.Euler(horse.pitch,0,horse.roll),Vector3.one*input.scale);
                float paw=Mathf.Sin(phase*1.42f)*Ease((Mathf.Sin(phase*.41f)-.79f)/.20f);
                rig.hindL=HorseStep(pose.horse,horse,false,false,paw,input.slope);
                rig.hindR=HorseStep(pose.horse,horse,false,true,paw,input.slope);
                rig.foreL=HorseStep(pose.horse,horse,true,false,paw,input.slope);
                rig.foreR=HorseStep(pose.horse,horse,true,true,paw,input.slope);
                hips=pose.horse*Matrix4x4.Translate(new Vector3(0,-.005f,-.034f));
                spine=attack.torso+new Vector3(19*horse.gallop-horse.pitch*.55f,0,-horse.roll*.56f);
                shift=attack.weightShift+new Vector3(0,0,.075f*horse.gallop);
            }
            if(input.town!=TownRole.None)spine+=new Vector3(input.nod,0,0);
            pose.root=hips*Pose(shift,Quaternion.Euler(spine),Vector3.one);
            rig.hipTwist=Mathf.Clamp(lead-spine.y,-48,48);
            rig.wind=-1-Mathf.Clamp01(step.weight*(.25f+.63f*step.run)+horse.gallop*.28f+weight*Mathf.Abs(attack.torso.y)*.009f+Mathf.Abs(input.turn)*.23f);
            rig.leftLeg=HumanStep(hips,step,false,1-.66f*weight,drop,lead,input.slope,input.crossSlope,input.ground,mounted);
            rig.rightLeg=HumanStep(hips,step,true,1-.66f*weight,drop,lead,input.slope,input.crossSlope,input.ground,mounted);
            // Snapshot yaw already follows the combat target. Subtract chest yaw to stabilize the eyes.
            float scan=input.town!=TownRole.None||input.survey?24:6;
            rig.head=Look(pose.root,glance*scan-spine.y,-spine.x*.85f+idle*Mathf.Sin(phase*.66f)*2.2f);
            SolveHands(input,step,attack,weight,gripChange,horse.gallop,ref pose);
        }

        static void SolveHands(in PoseInput input,in BipedGait gait,in AttackKey attack,float weight,
            float idle,float couch,ref PoseOutput pose)
        {
            ref var rig=ref pose.joints;
            float walk=gait.sway*(1-weight)*gait.direction;
            Vector3 right=new Vector3(.321f,-.165f,.067f-walk*.163f);
            Vector3 left=new Vector3(-.326f,-.166f,.075f+walk*.15f);
            Vector3 elbowR=new Vector3(.88f,-.29f,-.75f),elbowL=new Vector3(-.85f,-.37f,-.65f);
            Quaternion weapon=Quaternion.Euler(attack.weapon+new Vector3(walk*12,-5*couch,-5));
            Vector3 grip=Grip(input.style,input.stockWeapon);
            bool leftWeapon=false;
            float block=Ease(input.guard);
            if(input.town==TownRole.Resident||input.town==TownRole.Specialist)
            {
                right=input.rightHand;left=input.leftHand;
                weapon=Quaternion.Euler(input.toolPitch,0,0);
                grip=Vector3.zero;
            }
            else if(input.style==AttackStyle.Aim)
            {
                leftWeapon=true;
                float draw=input.action.stage==ActionStage.Release?1-Ease(input.action.progress/.40f):weight;
                rig.draw=draw;
                var bowHand=new Vector3(-.245f,.33f,.477f);
                left=Vector3.Lerp(left,bowHand,weight);
                Vector3 cheek=new Vector3(.125f,.482f,.102f);
                var sight=(bowHand-cheek).normalized;
                weapon=Quaternion.Slerp(Quaternion.Euler(12,0,-17),Quaternion.LookRotation(sight,Vector3.up),weight);
                var stringPoint=left+weapon*(Vector3.back*(input.stockWeapon?.30f:.182f+.34f*draw));
                right=Vector3.Lerp(right,stringPoint+Vector3.up*(input.stockWeapon?.014f:0),weight);
                elbowR=Vector3.Lerp(elbowR,new Vector3(1,.25f,-1),weight);
                left.y+=idle*.021f;
            }
            else
            {
                Vector3 cock,strike;
                switch(input.style)
                {
                    case AttackStyle.Hammer:
                        cock=new Vector3(.10f,.62f,-.12f);strike=new Vector3(.16f,-.015f,.45f);break;
                    case AttackStyle.Slash:
                        cock=new Vector3(.40f,.52f,-.165f);strike=new Vector3(-.01f,.048f,.34f);break;
                    case AttackStyle.Lance:case AttackStyle.Thrust:
                        cock=new Vector3(.32f,.04f,-.19f);strike=new Vector3(.26f,.19f,.488f);
                        right+=new Vector3(-.018f,.148f,.062f)*couch;break;
                    case AttackStyle.Throw:
                        cock=new Vector3(.37f,.55f,-.22f);strike=new Vector3(.20f,.36f,.44f);break;
                    default:
                        cock=new Vector3(.31f,.44f,.15f);strike=new Vector3(.19f,.33f,.44f);break;
                }
                if(input.action.stage==ActionStage.Release)
                    right=Vector3.Lerp(right,Vector3.Lerp(cock,strike,Ease(.42f+input.action.progress*6)),weight);
                else right=Vector3.Lerp(right,cock,weight);
                right+=Vector3.up*(idle*(input.style==AttackStyle.Thrust?.071f:.042f));
                if(input.style==AttackStyle.Hammer)left=Vector3.Lerp(left,right+weapon*Vector3.up*.14f,weight);
                if(input.kind==SoldierKind.Shield||input.town==TownRole.Watch)
                {
                    float cover=Mathf.Max(block,weight*.55f);
                    left=Vector3.Lerp(new Vector3(-.295f,-.052f,.154f),new Vector3(-.202f,.155f,.348f),cover);
                }
            }
            if(input.town==TownRole.Watch)weapon=Quaternion.Euler(-79+idle*3,0,0);
            rig.rightArm=TwoBone(pose.root,RightShoulder,right,elbowR,.27f,.25f,out right);
            rig.leftArm=TwoBone(pose.root,LeftShoulder,left,elbowL,.27f,.25f,out left);
            rig.item=pose.root*AtGrip(leftWeapon?left:right,weapon,grip);
            var shieldTurn=Quaternion.Euler(-block*25,-block*14+walk*2,6+block*7);
            rig.shield=pose.root*AtGrip(left,shieldTurn,new Vector3(-.30f,-.03f,.199f));
            pose.emptyThrowHand=input.style==AttackStyle.Throw&&input.action.stage==ActionStage.Release&&input.action.progress<.86f;
        }
    }
}
