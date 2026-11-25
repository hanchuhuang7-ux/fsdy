using System;
using System.Collections.Generic;
using Frontline.Core;
using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed partial class BattleView
    {
        sealed class DressedActor
        {
            public MeshSet close,mid,far;
            public ArtistSkeleton skeleton;
            public DressedActor shared;
        }
        readonly Dictionary<string,DressedActor> collection=new Dictionary<string,DressedActor>(StringComparer.Ordinal);
        readonly float[] lastFacing=new float[VisualUnitSlots];
        Material[] skinMaterials;
        Batch bladeGlints;
        internal int SkinnedInstances {get;private set;}
        internal int ProxyActors {get;private set;}

        void InitializeArtists()
        {
            var roster=Resources.Load<ArtistRoster>("CC0/ArtistRoster");
            if(!roster||roster.skeletons.Length<2)throw new InvalidOperationException("Bake the verified artist roster before building");
            skinMaterials=new Material[roster.skeletons.Length];
            for(int i=0;i<skinMaterials.Length;i++)
            {
                var skin=new Material(material){name=roster.skeletons[i].name+" animated skin",enableInstancing=true};
                skin.EnableKeyword("FRONTLINE_SKIN");skin.SetTexture("_BonePalette",roster.skeletons[i].palette);
                skinMaterials[i]=skin;
            }
            var bodies=new DressedActor[roster.skeletons.Length];
            for(int i=0;i<bodies.Length;i++)
            {
                var rig=roster.skeletons[i];var skin=skinMaterials[i];
                bodies[i]=new DressedActor{skeleton=rig,close=ArtistSet(rig.body,rig.proxy,skin,240),
                    mid=ArtistSet(rig.mid,rig.proxy,skin,240),far=ArtistSet(rig.proxy,rig.proxy,null,240)};
            }
            foreach(var costume in roster.costumes)
            {
                bool group=costume.name=="militia"||costume.name=="shield"||costume.name=="spear"||costume.name=="giant"||
                    costume.name=="archer"||costume.name=="crossbow"||costume.name=="bomber"||costume.name=="cavalry"||
                    costume.name=="charger"||costume.name=="medic";
                bool household=costume.name=="villager"||costume.name=="carrier"||costume.name=="farmer"||
                    costume.name=="woodcutter"||costume.name=="smith";
                int cap=group?240:household?OrdinaryResidentLimit:12;
                if(!costume.high){collection.Add(costume.name,bodies[costume.skeleton]);continue;}
                var art=new DressedActor{skeleton=roster.skeletons[costume.skeleton],shared=bodies[costume.skeleton]};
                art.close=ArtistSet(costume.high,costume.proxy,skinMaterials[costume.skeleton],cap);
                art.mid=ArtistSet(costume.mid,costume.proxy,skinMaterials[costume.skeleton],cap);
                art.far=ArtistSet(costume.proxy,costume.proxy,null,cap);
                collection.Add(costume.name,art);
            }
            foreach(string type in new[]{"militia","shield","spear","giant","archer","crossbow","bomber","cavalry","medic","catapult"})
                units.Add(type,new UnitVisual(type,type=="catapult"?null:collection[type]));
            foreach(var hero in Manor.HeroIds)
            {
                var tiers=new DressedActor[4];tiers[0]=collection[hero];
                for(int i=1;i<4;i++)tiers[i]=collection[hero+"_t"+i];
                units.Add(hero,new UnitVisual(hero,tiers[0],tiers));
            }
            bladeGlints=Buffer(Load("steel_arc",true),moving,0);
#if !UNITY_WEBGL
            if(ParallelPoseWorkers>1){BeginSoldierPoses(256);collectingPoses=false;}
#endif
        }
        MeshSet ArtistSet(Mesh mesh,Mesh proxy,Material skin,int maximum)
        {
            var set=new MeshSet{high=mesh,low=proxy,highVertices=mesh.vertexCount,lowVertices=proxy.vertexCount,
                localBounds=mesh.bounds,hasRig=mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord2),skin=skin,detailProxy=skin!=null,castShadow=true,receiveShadow=true};
            var page=Buffer(set,moving,0,Mathf.Min(maximum,Capacity));
            for(int left=maximum-page.matrices.Length;left>0;left-=page.matrices.Length)
            {page.next=new Batch(Mathf.Min(left,Capacity),skin!=null){mesh=set};page=page.next;}
            if(skin==null){set.ambient=new Batch(Mathf.Min(Capacity,maximum)){mesh=set};ambient.Add(set.ambient);}
            return set;
        }
        static void ChooseTake(in PoseInput input,float speed,float gait,float time,bool deploying,ref PoseOutput pose)
        {
            bool mounted=input.kind==SoldierKind.Cavalry;
            float pace=deploying&&!input.survey?0:speed;
            var step=new BipedGait(gait,pace,input.maxSpeed,input.direction,time,input.id);
            pose.horseMotion=pace<.18f?ArtistMotion.Idle:pace<1.72f?ArtistMotion.Walk:pace<3.65f?ArtistMotion.Trot:ArtistMotion.Run;
            pose.horsePhase=pace<.18f?time+input.id*.37f:new HorseStride(gait,pace,input.charge,input.terrainTilt,step.breath,input.id).phase;
            pose.take=mounted?ArtistMotion.Ride:pace<.14f?ArtistMotion.Idle:step.run>.20f?ArtistMotion.Run:ArtistMotion.Walk;
            pose.phase=mounted||pace<.14f?time+input.id*.383f:step.phase;
            if(input.working&&pace<.14f){pose.take=input.duty==ArtistMotion.Idle?ArtistMotion.Work:input.duty;pose.phase=time+input.id*.61f;}
            if(input.working&&input.duty==ArtistMotion.Heavy)
                pose.phase=Mathf.PingPong(time*.91f+input.id*.13f,1);
            if(input.duty==ArtistMotion.Haul&&pace>=.14f){pose.take=ArtistMotion.Haul;pose.phase=time+input.id*.23f;}
            if(input.guard>.17f){pose.take=ArtistMotion.Block;pose.phase=1-input.guard;}
            else if(input.action.stage!=ActionStage.Idle)
            {
                pose.take=input.style switch {AttackStyle.Hammer=>ArtistMotion.Heavy,AttackStyle.Thrust=>ArtistMotion.Thrust,
                    AttackStyle.Lance=>ArtistMotion.Lance,AttackStyle.Aim=>ArtistMotion.Bow,AttackStyle.Throw=>ArtistMotion.Throw,
                    AttackStyle.Bless=>ArtistMotion.Cast,_=>ArtistMotion.Slash};
                float progress=input.action.progress;
                if(input.style==AttackStyle.Aim&&input.action.stage==ActionStage.Draw)
                    progress=1-(1-progress)*(1-progress); // Draw to the cheek, then hold the sight line until the recorded release.
                pose.phase=input.action.stage==ActionStage.Release?.49f+.51f*progress:.49f*progress;
            }
            else if(!mounted&&input.hit>.46f){pose.take=ArtistMotion.Hit;pose.phase=1-input.hit;}
        }
        static float ArtistRow(ArtistSkeleton skeleton,ArtistMotion motion,float phase)
        {
            var take=skeleton.takes[(int)motion];
            if(motion==ArtistMotion.Idle||motion==ArtistMotion.Ride||motion==ArtistMotion.Work||motion>=ArtistMotion.Tend)phase/=Mathf.Max(.1f,take.seconds);
            bool loop=motion==ArtistMotion.Idle||motion==ArtistMotion.Walk||motion==ArtistMotion.Run||
                motion==ArtistMotion.Trot||motion==ArtistMotion.Ride||motion==ArtistMotion.Work||motion>=ArtistMotion.Tend;
            return take.Row(loop?Mathf.Repeat(phase,1):phase);
        }
        const float BlendSeconds=.15f;
        const int MotionSlots=VisualUnitSlots+512;
        readonly ClipHistory[] motionHistory=new ClipHistory[MotionSlots*2];

        struct ClipHistory
        {
            public ArtistSkeleton rig;
            public ArtistMotion current,previous;
            public float row,oldRow,start,last,yaw;
            public bool fading;

            public Vector4 Sample(ArtistSkeleton skeleton,ArtistMotion motion,float nextRow,float time,float degrees)
            {
                if(rig!=skeleton||time<last||time-last>.38f)
                {
                    rig=skeleton;current=motion;fading=false;yaw=degrees;
                }
                else
                {
                    if(yaw!=degrees&&time>last)yaw=Mathf.LerpAngle(yaw,degrees,1-Mathf.Exp(-18f*(time-last)));
                    if(current!=motion)
                    {
                        // On interruption continue from the dominant source, including its current frame.
                        if(!fading||time-start>=BlendSeconds*.5f){previous=current;oldRow=row;}
                        else oldRow=Outgoing(skeleton,time);
                        current=motion;start=time;fading=true;
                    }
                }
                last=time;row=nextRow;
                float progress=(time-start)/BlendSeconds;
                if(!fading||progress>=1){fading=false;return default;}
                return new Vector4(Outgoing(skeleton,time),1-Mathf.SmoothStep(0,1,progress),0,0);
            }
            float Outgoing(ArtistSkeleton skeleton,float time)
            {
                var clip=skeleton.takes[(int)previous];
                float offset=oldRow-clip.first+(time-start)*clip.intervals/Mathf.Max(.1f,clip.seconds);
                bool loop=previous==ArtistMotion.Idle||previous==ArtistMotion.Walk||previous==ArtistMotion.Run||
                    previous==ArtistMotion.Ride||previous==ArtistMotion.Trot||previous==ArtistMotion.Work||previous>=ArtistMotion.Tend;
                return clip.first+(loop?Mathf.Repeat(offset,clip.intervals):Mathf.Clamp(offset,0,clip.intervals));
            }
        }
        static int MotionKey(in PoseInput input)=>input.town==TownRole.None?input.id:VisualUnitSlots+input.id;

        Vector4 SampleBlend(int key,ArtistSkeleton rig,ArtistMotion motion,float row,float now,float radians,out float turn)
        {
            turn=0;if((uint)key>=motionHistory.Length)return default;
            float degrees=radians*Mathf.Rad2Deg;
            ref var state=ref motionHistory[key];
            var blend=state.Sample(rig,motion,row,now,degrees);
            turn=Mathf.DeltaAngle(degrees,state.yaw);
            return blend;
        }

        internal static object CheckArtistBlend()
        {
            var rig=Resources.Load<ArtistRoster>("CC0/ArtistRoster").skeletons[0];var history=new ClipHistory();
            var idle=rig.takes[(int)ArtistMotion.Idle];var run=rig.takes[(int)ArtistMotion.Run];
            history.Sample(rig,ArtistMotion.Idle,idle.Row(.25f),8,179);
            var first=history.Sample(rig,ArtistMotion.Run,run.first,8.01f,-179);
            var half=history.Sample(rig,ArtistMotion.Run,run.Row(.24f),8.085f,-179);
            var end=history.Sample(rig,ArtistMotion.Run,run.Row(.5f),8.17f,-178);
            var rewind=history.Sample(rig,ArtistMotion.Idle,idle.first,1,20);
            if(first.y!=1||Mathf.Abs(half.y-.5f)>.001f||half.x<idle.first||half.x>idle.first+idle.intervals||
                end.y!=0||rewind.y!=0||history.yaw!=20)throw new InvalidOperationException("GPU pose blend timeline discontinuity");
            return new {seconds=BlendSeconds,start=first.y,midpoint=half.y,end=end.y,replaySeek=rewind.y};
        }

        void DrawArtist(DressedActor actor,in Matrix4x4 root,Color tint,float row,bool mid,bool far,Vector4 blend=default)
        {
            var shared=actor.shared;
            if(far)
            {
                Add(actor.far,root,tint);if(shared!=null)Add(shared.far,root,tint);ProxyActors++;
            }
            else
            {
                Add(mid?actor.mid:actor.close,root,tint,row,blend);
                if(shared!=null)Add(mid?shared.mid:shared.close,root,tint,row,blend);
                SkinnedInstances++;
            }
        }
        void DrawArtistActor(in PoseInput input,in PoseOutput pose,in PoseDraw draw)
        {
            var actor=draw.costume??(draw.armament>0?draw.model.tiers[draw.armament]:draw.model.artist);
            bool far=lowActors,mid=input.distant||(cameraPosition-input.position).sqrMagnitude>18f*18f*input.scale*input.scale;
            float row=ArtistRow(actor.skeleton,pose.take,pose.phase);
            var root=pose.root;
            int key=MotionKey(input);
            float now=input.town==TownRole.None?PoseTime():manorTime;
            var blend=SampleBlend(key,actor.skeleton,pose.take,row,now,input.angle,out float yawOffset);
            if(yawOffset!=0)root*=Matrix4x4.Rotate(Quaternion.Euler(0,yawOffset,0));
            if(input.kind==SoldierKind.Cavalry)
            {
                var mount=collection["charger"];var skeleton=mount.skeleton;
                float hrow=ArtistRow(skeleton,pose.horseMotion,pose.horsePhase);
                var mountBlend=SampleBlend(key+MotionSlots,skeleton,pose.horseMotion,hrow,now,input.angle,out _);
                DrawArtist(mount,root,input.color,hrow,mid,far,mountBlend);
                int h=Mathf.FloorToInt(hrow),r=Mathf.FloorToInt(row);
                Vector3 seat=Vector3.Lerp(skeleton.saddle[h],skeleton.saddle[Mathf.Min(h+1,skeleton.saddle.Length-1)],hrow-h);
                var rock=Quaternion.Slerp(skeleton.saddleRotation[h],skeleton.saddleRotation[Mathf.Min(h+1,skeleton.saddle.Length-1)],hrow-h);
                var hips=actor.skeleton.hips;Vector3 rider=Vector3.Lerp(hips[r],hips[Mathf.Min(r+1,hips.Length-1)],row-r);
                if(mountBlend.y>0)
                {
                    int index=(int)mountBlend.x,next=Mathf.Min(index+1,skeleton.saddle.Length-1);
                    seat=Vector3.Lerp(seat,Vector3.Lerp(skeleton.saddle[index],skeleton.saddle[next],mountBlend.x-index),mountBlend.y);
                    rock=Quaternion.Slerp(rock,Quaternion.Slerp(skeleton.saddleRotation[index],skeleton.saddleRotation[next],mountBlend.x-index),mountBlend.y);
                }
                if(blend.y>0)
                {
                    int index=(int)blend.x,next=Mathf.Min(index+1,hips.Length-1);
                    rider=Vector3.Lerp(rider,Vector3.Lerp(hips[index],hips[next],blend.x-index),blend.y);
                }
                root=root*Pose(seat,rock,Vector3.one)*Matrix4x4.Translate(-rider);
            }
            var dye=input.color;dye.a=1+Mathf.Clamp01(pose.speed/Mathf.Max(.1f,input.maxSpeed));
            DrawArtist(actor,root,dye,row,mid,far,blend);
            if((uint)input.id<lastFacing.Length)lastFacing[input.id]=input.angle;
            if(!far&&input.action.stage==ActionStage.Release&&input.action.progress<.20f&&
                (input.style==AttackStyle.Slash||input.style==AttackStyle.Hammer))
            {
                var arc=root*Pose(new Vector3(.07f,1.02f,.035f),Quaternion.Euler(14,-11,-47+input.action.progress*110),Vector3.one*.78f);
                Accent(bladeGlints,arc,new Color(.73f,.85f,.91f,(1-Ease(input.action.progress/.20f))*.35f));
            }
        }
        void DrawArtistRemains(CorpseFrame corpse)
        {
            var model=Model(corpse.type);float size=model.definition.scale;var tint=Team(corpse.team);
            if(corpse.wreck!=null&&corpse.wreck.Length==4&&Catalog.Finite(corpse.wreck[1]))
            {
                var w=corpse.wreck;
                var at=Pose(new Vector3(w[0],w[1],w[2]),Quaternion.Euler(0,w[3]*Mathf.Rad2Deg,0),Vector3.one*size);
                if(corpse.type=="catapult"){Add(wreckCart,at,tint);return;}
                if(corpse.type=="cavalry")
                {
                    var horse=collection["charger"];
                    var death=horse.skeleton.takes[(int)ArtistMotion.Death];
                    float fallen=death.Row(corpse.age/death.seconds);
                    var transition=SampleBlend(corpse.id+MotionSlots,horse.skeleton,ArtistMotion.Death,fallen,PoseTime(),w[3],out _);
                    DrawArtist(horse,at,tint,fallen,true,false,transition);
                }
            }
            if(model.artist==null||corpse.parts.Count==0)return;
            var t=corpse.parts[0].t;
            var point=new Vector3(t[0],t[1],t[2]);if(!InView(point,2.5f*size))return;
            float floor=Data.Height(Config.map,point.x,point.z);
            point.y=Catalog.Finite(floor)&&point.y>floor-.3f?floor:point.y-.20f*size;
            float yaw=(uint)corpse.id<lastFacing.Length?lastFacing[corpse.id]:corpse.team=="red"?Mathf.PI*.5f:-Mathf.PI*.5f;
            var actor=model.artist;var take=actor.skeleton.takes[(int)ArtistMotion.Death];
            float row=take.Row(corpse.age/take.seconds);
            var blend=SampleBlend(corpse.id,actor.skeleton,ArtistMotion.Death,row,PoseTime(),yaw,out _);
            DrawArtist(actor,Pose(point,Quaternion.Euler(0,yaw*Mathf.Rad2Deg,0),Vector3.one*size),tint,row,true,false,blend);
        }
        void WatchAt(Vector3 point,float degrees,int id,Color tint,float pace=0,float gait=0)
        {
            QueuePose(new PoseInput {artist=true,town=TownRole.Watch,kind=SoldierKind.Shield,style=AttackStyle.Thrust,
                position=point+Vector3.up*.79f,angle=degrees*Mathf.Deg2Rad,speed=pace,gait=gait,direction=1,scale=1,hp=1,maxSpeed=2,
                survey=true,id=id,color=tint},new PoseDraw {costume=collection["watch"]});
        }
        void ReleaseArtists()
        {
            if(skinMaterials==null)return;
            foreach(var mat in skinMaterials)if(mat){mat.SetTexture("_BonePalette",null);Destroy(mat);}
            skinMaterials=null;
            foreach(var id in ScanGlobals)Shader.SetGlobalTexture(id,null);
        }
    }
}
