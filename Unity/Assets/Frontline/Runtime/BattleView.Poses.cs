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
        internal int CheckedJointActors {get;private set;}
        internal int CheckedDistantActors {get;private set;}
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

        enum TownRole : byte { None,Resident,Specialist,Watch }

        struct PoseInput
        {
            public Vector3 position;
            public Color color;
            public SoldierKind kind;
            public AttackStyle style;
            public ActionSample action;
            public float angle,scale,maxSpeed,speed,direction,gait,hitTiltX,hitTiltZ,charge,guard,hp,terrainTilt;
            public int id;
            public bool useMotionSample,stockWeapon,survey,distant,artist,working;
            public float hit;
            public TownRole town;
            public Vector3 rightHand,leftHand;
            public float slope,crossSlope,ground,nod,toolPitch,turn;
            public MotionSample previousMotion;
        }

        struct PoseOutput
        {
            public bool visible,emptyThrowHand;
            public Vector3 position;
            public float speed,gait;
            public MotionSample motion;
            public JointPose joints;
            public ArtistMotion take,horseMotion;
            public float phase,horsePhase;
            public Matrix4x4 root,leftLeg,rightLeg,rightArm,guardOrCatapultArm;
            public Matrix4x4 horse,nearLeft,nearRight,farLeft,farRight;
            public Matrix4x4 hpBackground,hpFill;
        }

        struct PoseDraw
        {
            public UnitVisual model;
            public DressedActor costume;
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
            if(count>0&&(count>=ParallelPoseThreshold||!lowActors)&&ParallelPoseWorkers>1)
            {
                if(!poseInputs.IsCreated||poseInputs.Length<count)
                {
                    DisposeSoldierPoses();
                    int capacity=Mathf.NextPowerOfTwo(Mathf.Max(256,count));
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
            if(poseCount==0)return;
            int workers=ParallelPoseWorkers;
            int batch=Mathf.Max(1,(poseCount+workers-1)/workers);
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
                        if(input.distant||lowActors)CheckedDistantActors++;else if(input.kind!=SoldierKind.Catapult)CheckedJointActors++;
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
            poseDraws=null;poseCount=0;collectingPoses=false;
#endif
        }

        float PoseTime() => Current?.time??(manor==null?Time.time:manorTime);

        void Soldier(UnitVisual model,string team,Vector3 p,float angle,int id,float hp=1,
            bool inspired=false,bool fortified=false,string stance="advance",float speed=0,float direction=0,
            float gait=0,float hitFlash=0,float hitTiltX=0,float hitTiltZ=0,float charge=0,float guard=0,float floor=float.NaN,
            int armament=0,ActionSample action=default,bool recorded=false)
        {
            float scale=model.definition.scale;
            // Only a manor roster carries a forge tier. Ordinary arena fighters always select their stock arm.
            int tier=model.tiers==null?0:Mathf.Clamp(armament,0,Manor.MaxHeroArmament);
            var input=new PoseInput {
                position=p,color=Team(team),kind=model.kind,artist=true,hit=hitFlash,style=model.attackStyle,stockWeapon=model.stockWeapon&&!(model.attackStyle==AttackStyle.Aim&&tier>0&&tier<3),
                action=action,id=id,angle=angle,scale=scale,maxSpeed=model.definition.speed,
                speed=speed,direction=direction,gait=gait,hitTiltX=hitTiltX,hitTiltZ=hitTiltZ,charge=charge,guard=guard,hp=hp,survey=!recorded
            };
            if(!lowActors)
            {
                // About 40 projected pixels for a person at desktop FOV. Tall silhouettes stay detailed
                // longer. Hysteresis is per actor, so the front files keep their rig when the rear files recede.
                float reach=(QualityLevel==2?38f:29f)*scale;
                bool wasFar=(uint)id<distantSoldier.Length&&distantSoldier[id];
                reach+=wasFar?-1.3f:1.3f;
                input.distant=(cameraPosition-p).sqrMagnitude>reach*reach;
                if((uint)id<distantSoldier.Length)distantSoldier[id]=input.distant;
            }
            if(recorded&&speed==0&&gait==0&&(uint)id<oldMotion.Length)
            {
                input.useMotionSample=true;
                input.previousMotion=oldMotion[id];
            }
            bool frontierSurface=Config.map==FrontierMap.Id;
            if((!lowActors&&!input.distant||model.kind==SoldierKind.Cavalry)&&(frontierSurface||variedTerrain&&manor==null)&&InView(p,5*scale))
            {
                // Sample the contact plane before scheduling. Workers only receive numeric heights.
                float dx=Mathf.Sin(angle)*.6f*scale,dz=Mathf.Cos(angle)*.6f*scale;
                float front=PoseGround(frontierSurface,p.x+dx,p.z+dz),back=PoseGround(frontierSurface,p.x-dx,p.z-dz);
                RenderHeightSamples+=2;
                if(Catalog.Finite(front)&&Catalog.Finite(back))
                {
                    input.slope=Mathf.Clamp((front-back)/(1.2f*scale),-.65f,.65f);
                    input.terrainTilt=Mathf.Atan(input.slope)*Mathf.Rad2Deg;
                }
                if(!lowActors&&!input.distant)
                {
                    float right=PoseGround(frontierSurface,p.x+dz,p.z-dx),left=PoseGround(frontierSurface,p.x-dz,p.z+dx);
                    RenderHeightSamples+=2;
                    if(Catalog.Finite(right)&&Catalog.Finite(left))
                        input.crossSlope=Mathf.Clamp((right-left)/(1.2f*scale),-.65f,.65f);
                    if(Catalog.Finite(front)&&Catalog.Finite(back))
                        input.ground=Mathf.Clamp(((front+back)*.5f-p.y)/scale+.80f,-.11f,.13f);
                }
            }
            var draw=new PoseDraw{model=model,stance=stance,floor=floor,hitFlash=hitFlash,armament=tier,inspired=inspired,fortified=fortified};
            input.turn=recorded&&!lowActors&&!input.distant?TurnRate(id,angle):0;
            input.color.a=1+input.turn;
            QueuePose(input,draw);
        }

        float PoseGround(bool frontier,float x,float z) =>
            frontier?(FrontierMap.HasGround(x,z)?FrontierMap.SurfaceHeight(x,z):float.NaN):Data.Height(Config.map,x,z);

        void QueuePose(in PoseInput input,in PoseDraw draw)
        {
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
            lowDetail|=input.distant;
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
            else if(input.artist)
            {
                pose.position=p;
                pose.root=Pose(p-Vector3.up*(.79f*scale),facing,size);
                ChooseTake(input,speed,gait,time,deploying,ref pose);
            }
            else
            {
                var step=new BipedGait(gait,deploying&&!input.survey?0:speed,input.maxSpeed,direction,time,input.id);
                var attack=AttackPose(input.style,input.action);
                float actionWeight=ActionWeight(input.action);
                float couch=0;
                if(!lowDetail)
                {
                    bool riding=input.kind==SoldierKind.Cavalry;
                    var stride=riding?new HorseStride(gait,deploying&&!input.survey?0:speed,input.charge,input.terrainTilt,step.breath,input.id):default;
                    SolveJoints(input,step,attack,actionWeight,time,facing,p,stride,riding,ref pose);
                }
                else if(input.kind==SoldierKind.Cavalry)
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
                if(lowDetail)PoseDistantWeapon(input,step,attack,actionWeight,couch,ref pose);
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

        static Matrix4x4 ArmPivot(Vector3 shoulder,Vector3 angles,Vector3 shift)
        {
            var rotation=Quaternion.Euler(angles);
            return Pose(shoulder+shift-rotation*shoulder,rotation,Vector3.one);
        }

        static void PoseDistantWeapon(in PoseInput input,in BipedGait step,in AttackKey attack,float weight,float couch,
            ref PoseOutput pose)
        {
            float stride=step.sway*(13+step.run*11)*step.direction*(1-weight);
            var angles=attack.weapon+new Vector3(stride,-couch*6,-6-couch*6);
            var travel=Vector3.Scale(attack.hand,new Vector3(.82f,.74f,.64f))+new Vector3(-.026f,.01f,.105f)*couch;
            if(input.style==AttackStyle.Aim&&input.stockWeapon)
            {
                angles.y+=14*weight;angles.x+=8*weight;travel.z-=.055f*weight;
            }
            if(input.style==AttackStyle.Bless&&input.stockWeapon)angles.x-=17*weight;
            pose.rightArm=pose.root*ArmPivot(RightShoulder,angles,travel);
            pose.emptyThrowHand=input.style==AttackStyle.Throw&&input.action.stage==ActionStage.Release&&input.action.progress<.86f;
        }

        void SubmitPose(in PoseInput input,in PoseOutput pose,in PoseDraw draw)
        {
            if(!pose.visible)return;
            var p=pose.position;
            float scale=input.scale;
            if(input.town==TownRole.None&&manor!=null&&FrontierMap.HasGround(p.x,p.z))ContactAt(input.position,scale*1.5f);
            if(input.useMotionSample)oldMotion[input.id]=pose.motion;
            activeFlash=draw.hitFlash;
            if(input.kind==SoldierKind.Catapult)
            {
                Add(catapult,pose.root,White);Add(catapultArm,pose.guardOrCatapultArm,White);
            }
            else DrawArtistActor(input,pose,draw);
            if(input.town!=TownRole.None){activeFlash=0;return;}
            activeFlash=0;
            bool tracks=!Deploying&&(input.kind==SoldierKind.Cavalry&&input.charge>.55f&&pose.speed>3.8f||
                !lowActors&&draw.model.armoured&&pose.speed>input.maxSpeed*.83f);
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
                        Accent(accentMist,Pose(dp,Quaternion.Euler(0,input.id*37,0),Vector3.one*(.25f+age*.45f)),new Color(.66f,.57f,.39f,.24f*(1-age)));
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
            if(job.take!=inline.take||job.horseMotion!=inline.horseMotion||!EqualBits(job.phase,inline.phase)||!EqualBits(job.horsePhase,inline.horsePhase)||
                !EqualJoints(job.joints,inline.joints)||job.visible!=inline.visible||job.emptyThrowHand!=inline.emptyThrowHand||
                !EqualBits(job.position,inline.position)||!EqualBits(job.speed,inline.speed)||!EqualBits(job.gait,inline.gait)||
                job.motion.valid!=inline.motion.valid||!EqualBits(job.motion.p,inline.motion.p)||
                !EqualBits(job.motion.time,inline.motion.time)||!EqualBits(job.motion.speed,inline.motion.speed)||
                !EqualBits(job.motion.phase,inline.motion.phase)||!EqualBits(job.motion.direction,inline.motion.direction)||
                !EqualBits(job.root,inline.root)||!EqualBits(job.horse,inline.horse)||
                !EqualBits(job.leftLeg,inline.leftLeg)||!EqualBits(job.rightLeg,inline.rightLeg)||
                !EqualBits(job.rightArm,inline.rightArm)||
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
