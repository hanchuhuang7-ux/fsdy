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
                int cap=group?240:costume.name=="villager"||costume.name=="carrier"||costume.name=="farmer"?72:12;
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
            {page.next=new Batch(Mathf.Min(left,Capacity)){mesh=set};page=page.next;}
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
            if(input.working&&pace<.14f){pose.take=ArtistMotion.Work;pose.phase=time+input.id*.61f;}
            if(input.guard>.17f){pose.take=ArtistMotion.Block;pose.phase=1-input.guard;}
            else if(input.action.stage!=ActionStage.Idle)
            {
                pose.take=input.style switch {AttackStyle.Hammer=>ArtistMotion.Heavy,AttackStyle.Thrust=>ArtistMotion.Thrust,
                    AttackStyle.Lance=>ArtistMotion.Lance,AttackStyle.Aim=>ArtistMotion.Bow,AttackStyle.Throw=>ArtistMotion.Throw,
                    AttackStyle.Bless=>ArtistMotion.Cast,_=>ArtistMotion.Slash};
                pose.phase=input.action.stage==ActionStage.Release?.49f+.51f*input.action.progress:.49f*input.action.progress;
            }
            else if(!mounted&&input.hit>.46f){pose.take=ArtistMotion.Hit;pose.phase=1-input.hit;}
        }
        static float ArtistRow(ArtistSkeleton skeleton,ArtistMotion motion,float phase)
        {
            var take=skeleton.takes[(int)motion];
            if(motion==ArtistMotion.Idle||motion==ArtistMotion.Ride||motion==ArtistMotion.Work)phase/=Mathf.Max(.1f,take.seconds);
            bool loop=motion==ArtistMotion.Idle||motion==ArtistMotion.Walk||motion==ArtistMotion.Run||
                motion==ArtistMotion.Trot||motion==ArtistMotion.Ride||motion==ArtistMotion.Work;
            return take.Row(loop?Mathf.Repeat(phase,1):phase);
        }
        void DrawArtist(DressedActor actor,in Matrix4x4 root,Color tint,float row,bool mid,bool far)
        {
            var shared=actor.shared;
            if(far)
            {
                Add(actor.far,root,tint);if(shared!=null)Add(shared.far,root,tint);ProxyActors++;
            }
            else
            {
                Add(mid?actor.mid:actor.close,root,tint,row);
                if(shared!=null)Add(mid?shared.mid:shared.close,root,tint,row);
                SkinnedInstances++;
            }
        }
        void DrawArtistActor(in PoseInput input,in PoseOutput pose,in PoseDraw draw)
        {
            var actor=draw.costume??(draw.armament>0?draw.model.tiers[draw.armament]:draw.model.artist);
            bool far=lowActors,mid=input.distant||(cameraPosition-input.position).sqrMagnitude>18f*18f*input.scale*input.scale;
            float row=ArtistRow(actor.skeleton,pose.take,pose.phase);
            var root=pose.root;
            if(input.kind==SoldierKind.Cavalry)
            {
                var mount=collection["charger"];var skeleton=mount.skeleton;
                float hrow=ArtistRow(skeleton,pose.horseMotion,pose.horsePhase);
                DrawArtist(mount,root,input.color,hrow,mid,far);
                int h=Mathf.FloorToInt(hrow),r=Mathf.FloorToInt(row);
                Vector3 seat=Vector3.Lerp(skeleton.saddle[h],skeleton.saddle[Mathf.Min(h+1,skeleton.saddle.Length-1)],hrow-h);
                var rock=Quaternion.Slerp(skeleton.saddleRotation[h],skeleton.saddleRotation[Mathf.Min(h+1,skeleton.saddle.Length-1)],hrow-h);
                var hips=actor.skeleton.hips;Vector3 rider=Vector3.Lerp(hips[r],hips[Mathf.Min(r+1,hips.Length-1)],row-r);
                root=root*Pose(seat,rock,Vector3.one)*Matrix4x4.Translate(-rider);
            }
            var dye=input.color;dye.a=1+Mathf.Clamp01(pose.speed/Mathf.Max(.1f,input.maxSpeed));
            DrawArtist(actor,root,dye,row,mid,far);
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
                    DrawArtist(horse,at,tint,ArtistRow(horse.skeleton,ArtistMotion.Death,Mathf.Clamp01(corpse.age)),true,false);
                }
            }
            if(model.artist==null||corpse.parts.Count==0)return;
            var t=corpse.parts[0].t;
            var point=new Vector3(t[0],t[1],t[2]);if(!InView(point,2.5f*size))return;
            float floor=Data.Height(Config.map,point.x,point.z);
            point.y=Catalog.Finite(floor)&&point.y>floor-.3f?floor:point.y-.20f*size;
            float yaw=(uint)corpse.id<lastFacing.Length?lastFacing[corpse.id]:corpse.team=="red"?Mathf.PI*.5f:-Mathf.PI*.5f;
            var actor=model.artist;var take=actor.skeleton.takes[(int)ArtistMotion.Death];
            DrawArtist(actor,Pose(point,Quaternion.Euler(0,yaw*Mathf.Rad2Deg,0),Vector3.one*size),tint,take.Row(corpse.age/take.seconds),true,false);
        }
        void WatchAt(Vector3 point,float degrees,int id,Color tint)
        {
            QueuePose(new PoseInput {artist=true,town=TownRole.Watch,kind=SoldierKind.Shield,style=AttackStyle.Thrust,
                position=point+Vector3.up*.79f,angle=degrees*Mathf.Deg2Rad,scale=1,hp=1,maxSpeed=2,
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
