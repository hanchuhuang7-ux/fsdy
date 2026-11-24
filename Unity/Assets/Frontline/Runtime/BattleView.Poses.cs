using System;
using Frontline.Core;
using UnityEngine;
#if !UNITY_WEBGL
using Unity.Collections;
using Unity.Jobs;
#endif

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        internal const int ParallelPoseThreshold=24;
        internal int ParallelPoseFrames {get;private set;}
        internal int ParallelPoseWorkers=>Math.Min(CpuWork.TargetWorkers,CpuWork.UnityWorkers+1);
        internal bool CheckParallelPoses {get;set;}
        internal int CheckedParallelPoses {get;private set;}
        internal int CheckedWindupStyles {get;private set;}
        internal int CheckedReleaseStyles {get;private set;}

        enum SoldierKind : byte { Infantry,Shield,Cavalry,Catapult }

        readonly struct PoseFrustum
        {
            readonly Plane left,right,bottom,top,near,far;
            public PoseFrustum(Plane[] planes)
            {
                left=planes[0];right=planes[1];bottom=planes[2];top=planes[3];near=planes[4];far=planes[5];
            }

            public bool Contains(Vector3 point,float radius)
            {
                float limit=-radius;
                // Use the same rejection test as InView, including for old snapshots with invalid coordinates.
                return !(left.GetDistanceToPoint(point)<limit||right.GetDistanceToPoint(point)<limit||
                    bottom.GetDistanceToPoint(point)<limit||top.GetDistanceToPoint(point)<limit||
                    near.GetDistanceToPoint(point)<limit||far.GetDistanceToPoint(point)<limit);
            }
        }

        PoseFrustum poseFrustum;

        struct PoseInput
        {
            public Vector3 position;
            public Color color;
            public SoldierKind kind;
            public AttackStyle style;
            public ActionSample action;
            public float angle,scale,maxSpeed,speed,direction,gait,hitTiltX,hitTiltZ,charge,guard,hp,terrainTilt;
            public int id;
            public bool useMotionSample,stockWeapon;
            public MotionSample previousMotion;
        }

        struct PoseOutput
        {
            public bool visible,emptyThrowHand;
            public Vector3 position;
            public float speed,gait;
            public MotionSample motion;
            public Matrix4x4 root,leftLeg,rightLeg,leftArm,rightArm,guardOrCatapultArm;
            public Matrix4x4 horse,nearLeft,nearRight,farLeft,farRight;
            public Matrix4x4 hpBackground,hpFill;
        }

        struct PoseDraw
        {
            public UnitVisual model;
            public string stance;
            public float floor,hitFlash;
            public int armament;
            public bool inspired,fortified;
        }

#if !UNITY_WEBGL
        NativeArray<PoseInput> poseInputs;
        NativeArray<PoseOutput> poseOutputs;
        PoseDraw[] poseDraws;
        JobHandle poseHandle;
        bool collectingPoses;
        int poseCount;

        struct PoseJob:IJobParallelFor
        {
            [ReadOnly] public NativeArray<PoseInput> inputs;
            [WriteOnly] public NativeArray<PoseOutput> outputs;
            public PoseFrustum frustum;
            public float time;
            public bool deploying,lowDetail;

            public void Execute(int index)
            {
                var value=inputs[index];
                ComputePose(value,frustum,time,deploying,lowDetail,out var pose);
                outputs[index]=pose;
            }
        }
#endif

        bool BeginSoldierPoses(int count)
        {
#if !UNITY_WEBGL
            if(count>=ParallelPoseThreshold&&ParallelPoseWorkers>1)
            {
                if(!poseInputs.IsCreated||poseInputs.Length<count)
                {
                    DisposeSoldierPoses();
                    int capacity=Mathf.NextPowerOfTwo(count);
                    poseInputs=new NativeArray<PoseInput>(capacity,Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
                    poseOutputs=new NativeArray<PoseOutput>(capacity,Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
                    poseDraws=new PoseDraw[capacity];
                }
                poseCount=0;
                collectingPoses=true;
                return true;
            }
#endif
            return false;
        }

        void FinishSoldierPoses()
        {
#if !UNITY_WEBGL
            collectingPoses=false;
            int workers=ParallelPoseWorkers;
            int batch=Mathf.Max(8,(poseCount+workers-1)/workers);
            var poses=new PoseJob{inputs=poseInputs,outputs=poseOutputs,frustum=poseFrustum,
                time=PoseTime(),deploying=Deploying,lowDetail=lowActors};
            poseHandle=poses.Schedule(poseCount,batch);
            poseHandle.Complete();
            ParallelPoseFrames++;

            for(int i=0;i<poseCount;i++)
            {
                var input=poseInputs[i];
                var output=poseOutputs[i];
                if(CheckParallelPoses)
                {
                    ComputePose(input,poseFrustum,poses.time,Deploying,lowActors,out var expected);
                    VerifyPose(output,expected);
                    CheckedParallelPoses++;
                    if(output.visible)
                    {
                        if(input.action.stage==ActionStage.Draw)CheckedWindupStyles|=1<<(int)input.style;
                        if(input.action.stage==ActionStage.Release)CheckedReleaseStyles|=1<<(int)input.style;
                    }
                }
                SubmitPose(input,output,poseDraws[i]);
            }
#endif
        }

        void DisposeSoldierPoses()
        {
#if !UNITY_WEBGL
            poseHandle.Complete();
            if(poseInputs.IsCreated)poseInputs.Dispose();
            if(poseOutputs.IsCreated)poseOutputs.Dispose();
#endif
        }

        float PoseTime() => Current?.time??(manor==null?Time.time:manorTime);

        void Soldier(UnitVisual model,string team,Vector3 p,float angle,int id,float hp=1,
            bool inspired=false,bool fortified=false,string stance="advance",float speed=0,float direction=0,
            float gait=0,float hitFlash=0,float hitTiltX=0,float hitTiltZ=0,float charge=0,float guard=0,float floor=float.NaN,
            int armament=0,ActionSample action=default,bool recorded=false)
        {
            float scale=model.definition.scale;
            var input=new PoseInput {
                position=p,color=Team(team),kind=model.kind,style=model.attackStyle,stockWeapon=model.stockWeapon,
                action=action,id=id,angle=angle,scale=scale,maxSpeed=model.definition.speed,
                speed=speed,direction=direction,gait=gait,hitTiltX=hitTiltX,hitTiltZ=hitTiltZ,charge=charge,guard=guard,hp=hp
            };
            if(recorded&&speed==0&&gait==0&&(uint)id<oldMotion.Length)
            {
                input.useMotionSample=true;
                input.previousMotion=oldMotion[id];
            }
            if(model.kind==SoldierKind.Cavalry&&variedTerrain&&InView(p,5*scale))
            {
                // The terrain catalog belongs to the main thread. Only sample horses that can be drawn.
                float dx=Mathf.Sin(angle)*.6f*scale,dz=Mathf.Cos(angle)*.6f*scale;
                float front=Data.Height(Config.map,p.x+dx,p.z+dz),back=Data.Height(Config.map,p.x-dx,p.z-dz);
                RenderHeightSamples+=2;
                if(Catalog.Finite(front)&&Catalog.Finite(back))input.terrainTilt=Mathf.Atan2(front-back,1.2f*scale)*Mathf.Rad2Deg;
            }
            var draw=new PoseDraw{model=model,stance=stance,floor=floor,hitFlash=hitFlash,armament=armament,inspired=inspired,fortified=fortified};
#if !UNITY_WEBGL
            if(collectingPoses)
            {
                poseInputs[poseCount]=input;
                poseDraws[poseCount++]=draw;
                return;
            }
#endif
            ComputePose(input,poseFrustum,PoseTime(),Deploying,lowActors,out var pose);
            SubmitPose(input,pose,draw);
        }

        static void ComputePose(in PoseInput input,in PoseFrustum frustum,float time,bool deploying,bool lowDetail,out PoseOutput pose)
        {
            pose=default;
            var p=input.position;
            float scale=input.scale;
            if(!frustum.Contains(p,5*scale))return;
            pose.visible=true;
            float speed=input.speed,gait=input.gait,direction=input.direction;
            if(input.useMotionSample)
            {
                var sample=input.previousMotion;
                if(time<sample.time)sample=default;
                if(sample.valid&&time>sample.time)
                {
                    var delta=p-sample.p;
                    float travel=new Vector2(delta.x,delta.z).magnitude;
                    float elapsed=time-sample.time;
                    sample.speed=elapsed<=.35f?Mathf.Min(input.maxSpeed*1.9f,travel/elapsed):0;
                    if(sample.speed>0)sample.phase+=travel/scale;
                    sample.direction=(delta.x*Mathf.Sin(input.angle)+delta.z*Mathf.Cos(input.angle))/Mathf.Max(.001f,travel);
                }
                sample.p=p;sample.time=time;sample.valid=true;
                pose.motion=sample;
                speed=sample.speed;gait=sample.phase;direction=sample.direction;
            }
            pose.speed=speed;pose.gait=gait;
            var facing=Quaternion.Euler(input.hitTiltZ*Mathf.Rad2Deg,input.angle*Mathf.Rad2Deg,-input.hitTiltX*Mathf.Rad2Deg);
            var size=Vector3.one*scale;
            if(input.kind==SoldierKind.Catapult)
            {
                pose.position=p;
                float kick=input.action.stage==ActionStage.Release?1-Ease(input.action.progress/.47f):0;
                pose.root=Pose(p-facing*Vector3.forward*(kick*.035f*scale),facing*Quaternion.Euler(-kick*2.8f,0,0),size);
                pose.guardOrCatapultArm=pose.root*Pose(new Vector3(0,.59f,0),
                    Quaternion.Euler(SiegePitch(input.action),0,0),Vector3.one);
            }
            else
            {
                var step=new BipedGait(gait,deploying?0:speed,input.maxSpeed,direction,time,input.id);
                var attack=AttackPose(input.style,input.action);
                float actionWeight=ActionWeight(input.action);
                float couch=0;
                if(input.kind==SoldierKind.Cavalry)
                {
                    var horse=new HorseStride(gait,deploying?0:speed,input.charge,input.terrainTilt,step.breath,input.id);
                    p.y+=(.55f+horse.bob)*scale;
                    pose.horse=Pose(p,facing*Quaternion.Euler(horse.pitch,0,horse.roll),size);
                    pose.nearLeft=Hoof(pose.horse,horse,false,false);
                    pose.nearRight=Hoof(pose.horse,horse,false,true);
                    pose.farLeft=Hoof(pose.horse,horse,true,false);
                    pose.farRight=Hoof(pose.horse,horse,true,true);
                    couch=horse.gallop;
                    var seat=pose.horse*Matrix4x4.Translate(new Vector3(0,-.005f,-.034f));
                    // The seat follows the horse; the upper body counterbalances its rocking motion.
                    pose.root=seat*Pose(attack.weightShift+new Vector3(0,0,.075f*couch),
                        Quaternion.Euler(attack.torso+new Vector3(19*couch-horse.pitch*.55f,0,-horse.roll*.56f)),Vector3.one);
                    pose.leftLeg=seat*Pose(new Vector3(-.19f,-.455f,-.07f),Quaternion.Euler(-20-9*couch,0,-16),Vector3.one);
                    pose.rightLeg=seat*Pose(new Vector3(.19f,-.455f,-.07f),Quaternion.Euler(-20-9*couch,0,16),Vector3.one);
                }
                else
                {
                    var hips=Pose(p,facing,size);
                    float feet=1-.69f*actionWeight;
                    pose.leftLeg=WalkingLeg(hips,step,new Vector3(-.15f,-.53f,0),false,feet);
                    pose.rightLeg=WalkingLeg(hips,step,new Vector3(.15f,-.53f,0),true,feet);
                    var shift=attack.weightShift+new Vector3(step.sway*.019f,step.bob*feet,0);
                    var spine=attack.torso+new Vector3(step.lean*feet,step.twist*feet,step.roll*feet);
                    if(input.style==AttackStyle.Aim&&input.stockWeapon)spine.y-=14*actionWeight;
                    pose.root=hips*Pose(shift,Quaternion.Euler(spine),Vector3.one);
                }
                PoseArms(input,step,attack,actionWeight,couch,lowDetail,ref pose);
                p=pose.root.GetColumn(3);
                pose.position=p;
            }
            if(input.hp<.99f)
            {
                pose.hpBackground=Pose(p+Vector3.up*(1.1f*scale),Quaternion.identity,new Vector3(.7f*scale,.055f,.1f));
                pose.hpFill=Pose(p+new Vector3(-.35f*scale*(1-input.hp),1.12f*scale,0),Quaternion.identity,
                    new Vector3(.7f*scale*Mathf.Clamp01(input.hp),.06f,.11f));
            }
        }

        static float ActionWeight(in ActionSample sample) => sample.stage switch
        {
            ActionStage.Idle=>0,
            ActionStage.Release=>1-Ease((sample.progress-.22f)/.78f),
            _=>Ease(sample.progress)
        };

        static readonly Vector3 RightShoulder=new Vector3(.282f,.251f,.009f),LeftShoulder=new Vector3(-.282f,.251f,.009f);
        static readonly Vector3 LeftPalm=new Vector3(-.36f,-.14f,.078f);

        static Matrix4x4 ArmPivot(Vector3 shoulder,Vector3 angles,Vector3 shift)
        {
            var rotation=Quaternion.Euler(angles);
            return Pose(shoulder+shift-rotation*shoulder,rotation,Vector3.one);
        }

        static Matrix4x4 ReachLeftHand(Vector3 target)
        {
            var resting=LeftPalm-LeftShoulder;
            var reach=target-LeftShoulder;
            float reachRatio=Mathf.Clamp(reach.magnitude/resting.magnitude,.67f,1.29f);
            var rotation=Quaternion.FromToRotation(resting,reach);
            var stretch=new Vector3(1,reachRatio,1);
            return Pose(LeftShoulder-rotation*Vector3.Scale(LeftShoulder,stretch),rotation,stretch);
        }

        static void PoseArms(in PoseInput input,in BipedGait step,in AttackKey attack,float actionWeight,float couch,
            bool distant,ref PoseOutput pose)
        {
            float stride=step.sway*(13+step.run*11)*step.direction*(1-actionWeight);
            float block=Ease(input.guard);
            var angles=attack.weapon+new Vector3(stride,-couch*6,-6-couch*6);
            var travel=Vector3.Scale(attack.hand,new Vector3(.82f,.74f,.64f))+new Vector3(-.026f,.01f,.105f)*couch;
            if(input.style==AttackStyle.Aim&&input.stockWeapon)
            {
                angles.y+=14*actionWeight;
                angles.x+=8*actionWeight;
                travel.z-=.055f*actionWeight;
            }
            if(input.style==AttackStyle.Bless&&input.stockWeapon)angles.x-=17*actionWeight;
            var right=ArmPivot(RightShoulder,angles,travel);
            pose.rightArm=pose.root*right;
            pose.emptyThrowHand=input.style==AttackStyle.Throw&&input.action.stage==ActionStage.Release&&input.action.progress<.86f;
            if(distant)return;
            if(input.kind==SoldierKind.Shield)
            {
                float cover=Mathf.Max(block,actionWeight*.62f);
                var shift=new Vector3(.014f,.091f,.142f)*cover;
                var guard=new Vector3(-18*cover-stride*.12f,11*cover,-7*cover);
                pose.leftArm=pose.root*ArmPivot(LeftShoulder,guard,shift*.55f);
                pose.guardOrCatapultArm=pose.root*ArmPivot(new Vector3(-.30f,-.03f,.291f),guard,shift);
            }
            else if((input.style==AttackStyle.Aim||input.style==AttackStyle.Hammer)&&actionWeight>.001f)
            {
                Vector3 grip;
                if(input.style==AttackStyle.Hammer)grip=right.MultiplyPoint3x4(new Vector3(.36f,-.145f,.36f));
                else
                {
                    grip=right.MultiplyPoint3x4(new Vector3(.36f,.10f,input.stockWeapon?.37f:.441f));
                    if(!input.stockWeapon)
                    {
                        float pull=input.action.stage==ActionStage.Release?1-Ease(input.action.progress/.44f):Ease(input.action.progress);
                        grip+=new Vector3(-.20f,.10f,-.43f)*pull;
                    }
                }
                pose.leftArm=pose.root*ReachLeftHand(Vector3.LerpUnclamped(LeftPalm,grip,actionWeight));
            }
            else
            {
                float balance=input.style==AttackStyle.Bless?61:input.style==AttackStyle.Throw?34:18;
                pose.leftArm=pose.root*ArmPivot(LeftShoulder,new Vector3(-stride-balance*actionWeight-couch*18,
                    actionWeight*11,5+actionWeight*9),Vector3.zero);
            }
        }

        MeshSet shoulderGusset;
        void CoverShoulder(in Matrix4x4 torso,in Matrix4x4 arm,float scale,Color tint)
        {
            var a=torso.MultiplyPoint3x4(RightShoulder);
            var b=arm.MultiplyPoint3x4(RightShoulder);
            var axis=b-a;
            if(axis.sqrMagnitude<.0049f*scale*scale)return;
            Add(shoulderGusset,Pose((a+b)*.5f,Quaternion.FromToRotation(Vector3.up,axis),
                new Vector3(.158f*scale,axis.magnitude+.088f*scale,.185f*scale)),tint);
        }

        void SubmitPose(in PoseInput input,in PoseOutput pose,in PoseDraw draw)
        {
            if(!pose.visible)return;
            var p=pose.position;
            float scale=input.scale;
            if(manor!=null&&FrontierMap.HasGround(p.x,p.z))ContactAt(input.position,scale*1.5f);
            if(input.useMotionSample)oldMotion[input.id]=pose.motion;
            activeFlash=draw.hitFlash;
            Add(draw.model.body,pose.root,input.color);
            if(draw.armament!=0&&draw.model.crest!=null)
            {
                var dye=draw.armament==3?new Color(1,.91f,.45f):draw.armament==2?Gold:new Color(.73f,.47f,.24f);
                Add(draw.model.crest,pose.root,dye);
            }
            if(input.kind==SoldierKind.Catapult)
            {
                Add(catapult,pose.root,White);
                Add(catapultArm,pose.guardOrCatapultArm,White);
            }
            else
            {
                Add(leg,pose.leftLeg,White);Add(leg,pose.rightLeg,White);
                if(!lowActors&&input.action.stage!=ActionStage.Idle)CoverShoulder(pose.root,pose.rightArm,scale,input.color);
                if(!lowActors)Add(armLeft,pose.leftArm,input.color);
                if(pose.emptyThrowHand)Add(armLeft,pose.rightArm*Matrix4x4.Translate(new Vector3(.72f,0,0)),input.color);
                else Add(draw.model.right,pose.rightArm,input.color);
                if(input.kind==SoldierKind.Shield&&!lowActors)Add(shield,pose.guardOrCatapultArm,input.color);
                if(input.kind==SoldierKind.Cavalry)
                {
                    Add(horse,pose.horse,input.color);
                    Add(horseLeg,pose.nearLeft,White);Add(horseLeg,pose.nearRight,White);
                    Add(horseLeg,pose.farLeft,White);Add(horseLeg,pose.farRight,White);
                }
            }
            activeFlash=0;
            bool tracks=!Deploying&&input.kind==SoldierKind.Cavalry&&input.charge>.55f&&pose.speed>3.8f;
            bool selected=Selection.Contains(input.id),showStance=Deploying&&draw.stance!="advance";
            if(tracks||selected||showStance||draw.inspired||draw.fortified)
            {
                float floor=float.IsNaN(draw.floor)?UnitFloor(input.id,p.x,p.z):draw.floor;
                if(!Catalog.Finite(floor))floor=p.y-.8f*scale;
                var foot=new Vector3(p.x,floor+.045f,p.z);
                if(tracks)
                {
                    for(int i=0;i<2;i++)
                    {
                        float age=Mathf.Repeat(pose.gait*.65f+i*.5f,1);
                        var dp=foot-new Vector3(Mathf.Sin(input.angle),0,Mathf.Cos(input.angle))*(.5f+age*.9f);
                        dp.y+=.12f+age*.22f;
                        Add(dust,Pose(dp,Quaternion.Euler(0,input.id*37,0),Vector3.one*(.25f+age*.45f)),new Color(.66f,.57f,.39f,.24f*(1-age)));
                    }
                }
                if(selected)Ring(foot,.7f*scale,Gold);else if(showStance)Ring(foot,.48f*scale,draw.stance=="hold"?new Color(.64f,.72f,.75f):White);
                if(draw.inspired||draw.fortified)Ring(foot,.65f*scale,draw.fortified?new Color(.47f,.76f,1):Gold);
            }
            if(input.hp<.99f)
            {
                Add(dot,pose.hpBackground,new Color(.12f,.15f,.18f));
                Add(dot,pose.hpFill,input.color);
            }
        }

#if !UNITY_WEBGL
        static bool EqualBits(Matrix4x4 a,Matrix4x4 b)
        {
            for(int i=0;i<16;i++)if(BitConverter.SingleToInt32Bits(a[i])!=BitConverter.SingleToInt32Bits(b[i]))return false;
            return true;
        }

        static void VerifyPose(in PoseOutput job,in PoseOutput inline)
        {
            if(job.visible!=inline.visible||job.emptyThrowHand!=inline.emptyThrowHand||
                !EqualBits(job.position,inline.position)||!EqualBits(job.speed,inline.speed)||!EqualBits(job.gait,inline.gait)||
                job.motion.valid!=inline.motion.valid||!EqualBits(job.motion.p,inline.motion.p)||
                !EqualBits(job.motion.time,inline.motion.time)||!EqualBits(job.motion.speed,inline.motion.speed)||
                !EqualBits(job.motion.phase,inline.motion.phase)||!EqualBits(job.motion.direction,inline.motion.direction)||
                !EqualBits(job.root,inline.root)||!EqualBits(job.horse,inline.horse)||
                !EqualBits(job.leftLeg,inline.leftLeg)||!EqualBits(job.rightLeg,inline.rightLeg)||
                !EqualBits(job.leftArm,inline.leftArm)||!EqualBits(job.rightArm,inline.rightArm)||
                !EqualBits(job.guardOrCatapultArm,inline.guardOrCatapultArm)||
                !EqualBits(job.nearLeft,inline.nearLeft)||!EqualBits(job.nearRight,inline.nearRight)||
                !EqualBits(job.farLeft,inline.farLeft)||!EqualBits(job.farRight,inline.farRight)||
                !EqualBits(job.hpBackground,inline.hpBackground)||!EqualBits(job.hpFill,inline.hpFill))
                throw new InvalidOperationException("Parallel soldier pose differs from its inline result");
        }

        static bool EqualBits(float a,float b) => BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b);
        static bool EqualBits(Vector3 a,Vector3 b) => EqualBits(a.x,b.x)&&EqualBits(a.y,b.y)&&EqualBits(a.z,b.z);
#endif
    }
}
