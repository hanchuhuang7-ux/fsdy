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
            public float angle,attack,scale,maxSpeed,speed,direction,gait,hitTiltX,hitTiltZ,charge,guard,hp,terrainTilt;
            public int id;
            public bool useMotionSample;
            public MotionSample previousMotion;
        }

        struct PoseOutput
        {
            public bool visible;
            public Vector3 position;
            public float speed,gait;
            public MotionSample motion;
            public Matrix4x4 root,leftLeg,rightLeg,leftArm,rightArm,guardOrCatapultArm;
            public Matrix4x4 nearLeft,nearRight,farLeft,farRight;
            public Matrix4x4 hpBackground,hpFill;
        }

        struct PoseDraw
        {
            public UnitVisual model;
            public string stance;
            public float floor,hitFlash;
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
                time=Current?.time??Time.time,deploying=Deploying,lowDetail=lowActors};
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

        void Soldier(UnitVisual model,string team,Vector3 p,float angle,float attack,int id,float hp=1,
            bool inspired=false,bool fortified=false,string stance="advance",float speed=0,float direction=0,
            float gait=0,float hitFlash=0,float hitTiltX=0,float hitTiltZ=0,float charge=0,float guard=0,float floor=float.NaN)
        {
            float scale=model.definition.scale;
            var input=new PoseInput {
                position=p,color=Team(team),kind=model.kind,id=id,angle=angle,attack=attack,scale=scale,maxSpeed=model.definition.speed,
                speed=speed,direction=direction,gait=gait,hitTiltX=hitTiltX,hitTiltZ=hitTiltZ,charge=charge,guard=guard,hp=hp
            };
            if(!Deploying&&gait==0&&(uint)id<oldMotion.Length)
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
            var draw=new PoseDraw{model=model,stance=stance,floor=floor,hitFlash=hitFlash,inspired=inspired,fortified=fortified};
#if !UNITY_WEBGL
            if(collectingPoses)
            {
                poseInputs[poseCount]=input;
                poseDraws[poseCount++]=draw;
                return;
            }
#endif
            ComputePose(input,poseFrustum,Current?.time??Time.time,Deploying,lowActors,out var pose);
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
                    sample.speed=travel/(time-sample.time);
                    sample.phase+=travel/scale;
                    sample.direction=(delta.x*Mathf.Sin(input.angle)+delta.z*Mathf.Cos(input.angle))/Mathf.Max(.001f,travel);
                }
                sample.p=p;sample.time=time;sample.valid=true;
                pose.motion=sample;
                speed=sample.speed;gait=sample.phase;direction=sample.direction;
            }
            p.y+=Mathf.Sin(time*2.2f+input.id)*.008f*scale;
            bool mounted=input.kind==SoldierKind.Cavalry;
            float tilt=0;
            if(mounted){p.y+=.55f*scale;tilt=Mathf.Clamp01(input.charge/1.1f)*7-input.terrainTilt;}
            pose.position=p;
            pose.speed=speed;pose.gait=gait;

            var root=Pose(p,Quaternion.Euler(input.hitTiltZ*Mathf.Rad2Deg,0,-input.hitTiltX*Mathf.Rad2Deg)*
                Quaternion.Euler(0,input.angle*Mathf.Rad2Deg,0)*Quaternion.Euler(tilt,0,0),Vector3.one*scale);
            pose.root=root;
            float swing=Mathf.Sin(Mathf.Clamp01(input.attack)*Mathf.PI)*65;
            if(input.kind==SoldierKind.Catapult)
                pose.guardOrCatapultArm=root*Pose(new Vector3(0,.59f,0),Quaternion.Euler(-35-swing*.8f,0,0),Vector3.one);
            else
            {
                bool reverse=direction<-.15f;
                float amplitude=speed<.12f?0:Mathf.Clamp01(speed/input.maxSpeed);
                float walk=deploying?0:Mathf.Sin(gait*4.4f*(reverse?-1:1)+input.id)*28*amplitude*(reverse?.65f:1);
                pose.leftLeg=root*Pose(new Vector3(-.15f,-.53f,0),Quaternion.Euler(walk,0,0),Vector3.one);
                pose.rightLeg=root*Pose(new Vector3(.15f,-.53f,0),Quaternion.Euler(-walk,0,0),Vector3.one);
                if(!lowDetail)pose.leftArm=root*Pose(new Vector3(-.36f,.27f,0),Quaternion.Euler(-walk*.5f,0,0),Vector3.one)*
                    Matrix4x4.Translate(new Vector3(.36f,-.27f,0));
                pose.rightArm=root*Pose(new Vector3(.36f,.27f,0),Quaternion.Euler(-swing+walk*.4f,0,-4),Vector3.one)*
                    Matrix4x4.Translate(new Vector3(-.36f,-.27f,0));
                if(input.kind==SoldierKind.Shield&&!lowDetail)
                    pose.guardOrCatapultArm=root*Pose(new Vector3(0,input.guard*.1f,input.guard*.15f),
                        Quaternion.Euler(-input.guard*14,0,0),Vector3.one);
                if(mounted)
                {
                    var left=Quaternion.Euler(walk,0,0);
                    var right=Quaternion.Euler(-walk,0,0);
                    pose.nearLeft=root*Pose(new Vector3(-.24f,-.65f,-.4f),left,Vector3.one);
                    pose.nearRight=root*Pose(new Vector3(.24f,-.65f,-.4f),right,Vector3.one);
                    pose.farLeft=root*Pose(new Vector3(-.24f,-.65f,.4f),left,Vector3.one);
                    pose.farRight=root*Pose(new Vector3(.24f,-.65f,.4f),right,Vector3.one);
                }
            }
            if(input.hp<.99f)
            {
                pose.hpBackground=Pose(p+Vector3.up*(1.1f*scale),Quaternion.identity,new Vector3(.7f*scale,.055f,.1f));
                pose.hpFill=Pose(p+new Vector3(-.35f*scale*(1-input.hp),1.12f*scale,0),Quaternion.identity,
                    new Vector3(.7f*scale*Mathf.Clamp01(input.hp),.06f,.11f));
            }
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
            if(input.kind==SoldierKind.Catapult)
            {
                Add(catapult,pose.root,White);
                Add(catapultArm,pose.guardOrCatapultArm,White);
            }
            else
            {
                Add(leg,pose.leftLeg,White);Add(leg,pose.rightLeg,White);
                if(!lowActors)Add(armLeft,pose.leftArm,input.color);
                Add(draw.model.right,pose.rightArm,input.color);
                if(input.kind==SoldierKind.Shield&&!lowActors)Add(shield,pose.guardOrCatapultArm,input.color);
                if(input.kind==SoldierKind.Cavalry)
                {
                    Add(horse,pose.root,White);
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
            if(job.visible!=inline.visible||!EqualBits(job.root,inline.root)||
                !EqualBits(job.leftLeg,inline.leftLeg)||!EqualBits(job.rightLeg,inline.rightLeg)||
                !EqualBits(job.leftArm,inline.leftArm)||!EqualBits(job.rightArm,inline.rightArm)||
                !EqualBits(job.guardOrCatapultArm,inline.guardOrCatapultArm)||
                !EqualBits(job.nearLeft,inline.nearLeft)||!EqualBits(job.nearRight,inline.nearRight)||
                !EqualBits(job.farLeft,inline.farLeft)||!EqualBits(job.farRight,inline.farRight)||
                !EqualBits(job.hpBackground,inline.hpBackground)||!EqualBits(job.hpFill,inline.hpFill))
                throw new InvalidOperationException("Parallel soldier pose differs from its inline result");
        }
#endif
    }
}
