using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        struct LimbMeshes {public MeshSet thigh,shin,upper,forearm;}
        LimbMeshes woolLimbs,steelLimbs;
        MeshSet horseUpper,horseCannon,watchPortrait;
        Batch bladeGlints;
        readonly MeshSet[] artisanPortraits=new MeshSet[Manor.SpecialistIds.Length];
        struct HeadingSample {public float angle,time,turn;}
        readonly HeadingSample[] headingSamples=new HeadingSample[VisualUnitSlots];

        void InitializeJoints()
        {
            woolLimbs=BuildLimbs("");steelLimbs=BuildLimbs("_plate");
            horseUpper=Load("horse_upper");horseCannon=Load("horse_cannon");
            ReserveParts(horseUpper,960);ReserveParts(horseCannon,960);
            foreach(var pair in units)
                if(pair.Value.kind!=SoldierKind.Catapult)
                {
                    pair.Value.portrait=Load("head_"+pair.Key).WithoutShadows();
                    ReserveParts(pair.Value.portrait,240);
                }
            foreach(var model in units.Values)
            {
                ReserveDistant(model.body,240);ReserveDistant(model.right,240);
                if(model.forgedArms!=null)
                    for(int t=1;t<model.forgedArms.Length;t++)
                    {ReserveDistant(model.forgedArms[t],4);ReserveDistant(model.insignia[t],4);}
            }
            ReserveDistant(leg,480);ReserveDistant(horseLeg,960);ReserveDistant(horse,240);
            ReserveDistant(catapult,240);ReserveDistant(catapultArm,240);ReserveDistant(armLeft,240);
            watchPortrait=Load("head_watch").WithoutShadows();ReserveParts(watchPortrait,12);
            for(int i=0;i<artisanPortraits.Length;i++)
            {
                artisanPortraits[i]=Load("head_"+Manor.SpecialistIds[i]).WithoutShadows();
                ReserveParts(artisanPortraits[i],1);
            }
            bladeGlints=Buffer(Load("steel_arc",true),moving,0);
#if !UNITY_WEBGL
            if(ParallelPoseWorkers>1){BeginSoldierPoses(256);collectingPoses=false;}
#endif
        }

        void ReserveDistant(MeshSet model,int count)
        {
            var proxy=new MeshSet{high=model.low,low=model.low,highVertices=model.lowVertices,lowVertices=model.lowVertices,
                localBounds=model.low.bounds,castShadow=model.castShadow,receiveShadow=model.receiveShadow};
            ReserveParts(proxy,count);model.distant=proxy;
        }

        LimbMeshes BuildLimbs(string suffix)
        {
            var parts=new LimbMeshes{thigh=Load("thigh"+suffix),shin=Load("shin"+suffix),
                upper=Load("upper_arm"+suffix).WithoutShadows(),forearm=Load("forearm"+suffix).WithoutShadows()};
            ReserveParts(parts.thigh,480);ReserveParts(parts.shin,480);
            ReserveParts(parts.upper,480);ReserveParts(parts.forearm,480);
            return parts;
        }

        void ReserveParts(MeshSet mesh,int instances)
        {
            // Reserve only the number of transforms this piece can actually use. Unique
            // faces and commissioned regalia do not need full crowd-sized native upload blocks.
            var page=Buffer(mesh,moving,0,Mathf.Min(Capacity,instances));
            for(int left=instances-page.matrices.Length;left>0;left-=page.matrices.Length)
            {page.next??=new Batch(Mathf.Min(Capacity,left)){mesh=mesh};page=page.next;}
        }

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

        void PutPair(in BonePair pair,MeshSet upper,MeshSet lower,Color tint,bool boot=false)
        {
            Add(upper,pair.upper,tint);Add(lower,pair.lower,tint,boot?-8+pair.solePitch/180:0);
        }

        void DrawFourLimbs(in JointPose rig,in LimbMeshes meshes,Color tint)
        {
            PutPair(rig.leftLeg,meshes.thigh,meshes.shin,tint,true);
            PutPair(rig.rightLeg,meshes.thigh,meshes.shin,tint,true);
            PutPair(rig.leftArm,meshes.upper,meshes.forearm,tint);
            PutPair(rig.rightArm,meshes.upper,meshes.forearm,tint);
        }

        static Color ClothTint(Color tint,float hipTwist,float turn)
        {
            // Integer degree and continuous turn share the unused fourth tint component.
            tint.a=4+(Mathf.Round(hipTwist)+48)*4+(1+turn);
            return tint;
        }

        void SubmitJoints(in PoseInput input,in PoseOutput pose,in PoseDraw draw)
        {
            ref readonly var rig=ref pose.joints;
            Add(draw.model.portrait,rig.head,input.color);
            DrawFourLimbs(rig,draw.model.armoured?steelLimbs:woolLimbs,input.color);
            if(!pose.emptyThrowHand)
                Add(draw.armament==0?draw.model.right:draw.model.forgedArms[draw.armament],rig.item,input.color,
                    input.style==AttackStyle.Aim?-3-rig.draw:rig.wind);
            if(input.kind==SoldierKind.Shield)Add(shield,rig.shield,input.color);
            if(input.kind==SoldierKind.Cavalry)
            {
                Add(horse,pose.horse,input.color,rig.wind);
                PutPair(rig.hindL,horseUpper,horseCannon,White);PutPair(rig.hindR,horseUpper,horseCannon,White);
                PutPair(rig.foreL,horseUpper,horseCannon,White);PutPair(rig.foreR,horseUpper,horseCannon,White);
            }
            if(input.action.stage==ActionStage.Release&&input.action.progress<.20f&&
                (input.style==AttackStyle.Slash||input.style==AttackStyle.Hammer))
            {
                float opacity=(1-Ease(input.action.progress/.20f))*.36f;
                var sweep=pose.root*Pose(new Vector3(.07f,.21f,.035f),
                    Quaternion.Euler(14,-11,-47+input.action.progress*110),Vector3.one*(input.style==AttackStyle.Hammer?.82f:.72f));
                Accent(bladeGlints,sweep,new Color(.73f,.85f,.91f,opacity));
            }
        }

        void SubmitTown(in PoseInput input,in PoseOutput pose,in PoseDraw draw)
        {
            ref readonly var rig=ref pose.joints;
            bool watch=input.town==TownRole.Watch;
            Add(draw.coat,pose.root,ClothTint(input.color,rig.hipTwist,0),rig.wind);
            if(input.town==TownRole.Resident)
            {
                Add(head,rig.head*Matrix4x4.Translate(new Vector3(0,.549f,.026f)),White);
                Add(draw.cap,rig.head*Matrix4x4.Translate(new Vector3(0,.60f,0)),draw.capColor);
                if(draw.pack!=null)Add(draw.pack,pose.root*Matrix4x4.Translate(new Vector3(0,-.03f,-.27f)),White);
            }
            else Add(draw.portrait,rig.head,input.color);
            DrawFourLimbs(rig,watch?steelLimbs:woolLimbs,input.color);
            if(draw.tool!=null)Add(draw.tool,rig.item,watch?input.color:White,watch?rig.wind:0);
            if(watch)Add(shield,rig.shield,input.color);
        }

        void WatchAt(Vector3 point,float degrees,int id,Color dye)
        {
            var watch=Model("shield");
            QueuePose(new PoseInput {town=TownRole.Watch,kind=SoldierKind.Shield,style=AttackStyle.Thrust,
                position=point+Vector3.up*.80f,angle=degrees*Mathf.Deg2Rad,scale=1,hp=1,maxSpeed=2,
                survey=true,id=id,color=dye},new PoseDraw {coat=watch.body,portrait=watchPortrait,tool=Model("spear").right});
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
