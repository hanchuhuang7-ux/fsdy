"""Assemble Quaternius adult outfits on their native humanoid skeleton and bind crownjoshua's armour.
Only editor-side conversions are performed. Mesh topology comes from the licensed source artists.
"""
from pathlib import Path
import math
import re
import sys
sys.path.insert(0,str(Path(__file__).resolve().parent))
import bpy,bmesh
from mathutils import Matrix,Vector,Quaternion
from prepare_meshes import CACHE,OUT as ART,read,select_export
from prepare_rigs import disconnect,bind_action,rest,bake as animal

DEST=ART.parent/"Actors"
UNIVERSAL=CACHE/"universal"
DIRECT={"Idle":"Idle_Loop","Walk":"Walk_Loop","Run":"Sprint_Loop","Slash":"Sword_Attack",
        "Hit":"Hit_Chest","Death":"Death01","Cast":"Spell_Simple_Shoot","Ride":"Sitting_Idle_Loop","Work":"Interact"}
RETARGET={"Thrust":"1H_Melee_Attack_Stab","Heavy":"2H_Melee_Attack_Chop","Bow":"2H_Ranged_Shooting",
          "Block":"Block_Hit","Throw":"Throw"}

def import_set(path):
    before=set(bpy.context.scene.objects);actions=set(bpy.data.actions)
    read(path)
    created=[o for o in bpy.context.scene.objects if o not in before]
    arm=next(o for o in created if o.type=='ARMATURE')
    disconnect(arm);rest(arm);arm.data.pose_position='REST';bpy.context.view_layer.update()
    return arm,[o for o in created if o.type=='MESH' and o.name!='Icosphere'],{a.name.rsplit('|',1)[-1]:a for a in bpy.data.actions if a not in actions}

def joint(arm,name):
    return arm.matrix_world@arm.data.bones[name].head_local

def attach(o,target,world,shift):
    data=o.data.copy();o.data=data
    # Expressions in the exported assets use skeletal weights. Morph keys on the
    # original head are omitted from this renderer's static bind mesh.
    if o.data.shape_keys:o.shape_key_clear()
    data.transform(shift@world)
    o.parent=target;o.parent_type='OBJECT';o.matrix_world=Matrix.Identity(4)
    o.modifiers.clear();mod=o.modifiers.new('Artist skeleton','ARMATURE');mod.object=target

def crop_head(o,threshold):
    bm=bmesh.new();bm.from_mesh(o.data)
    cut=bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.00001,
        plane_co=(0,0,threshold),plane_no=(0,0,1),clear_inner=True)
    bm.to_mesh(o.data);bm.free();o.data.update()

def add_head(target,sex,bearded):
    arm,meshes,_=import_set(UNIVERSAL/('Superhero_'+sex+'_FullBody.fbx'))
    shift=Matrix.Translation(joint(target,'Head')-joint(arm,'Head'));chosen=[]
    for o in meshes:
        if 'Eyebrow' in o.name:continue
        attach(o,target,o.matrix_world.copy(),shift)
        if 'FullBody' in o.name or 'SuperHero' in o.name or 'Superhero' in o.name:
            crop_head(o,joint(target,'neck_01').z+.006)
        chosen.append(o)
    hair=['Eyebrows_Regular','Hair_SimpleParted'] if sex=='Male' else ['Eyebrows_Female','Hair_BuzzedFemale']
    if bearded:hair+=['Hair_Beard']
    for part in hair:
        arm,items,_=import_set(UNIVERSAL/(part+'.fbx'))
        shift=Matrix.Translation(joint(target,'Head')-joint(arm,'Head'))
        for o in items:
            attach(o,target,o.matrix_world.copy(),shift);chosen.append(o)
    return chosen

def donor_name(name):
    torso={'pelvis':'hips','spine_01':'spine','spine_02':'spine','spine_03':'chest','neck_01':'chest','Head':'head'}
    if name in torso:return torso[name]
    if name.endswith(('_l','_r')):
        base,side=name[:-2],name[-1]
        root={'upperarm':'upperarm','lowerarm':'lowerarm','hand':'hand','thigh':'upperleg','calf':'lowerleg','foot':'foot','ball':'toes'}.get(base)
        if root:return root+'.'+side
    return None

def retarget_arms(target,driver,action,label,idle):
    # Source FBX looks along -Y; the Universal character faces +Y in Blender.
    # Measure the two bind shoulder lines, then transfer rotations in a common body frame.
    left0=joint(driver,'hand.l')-joint(driver,'hand.r');left1=joint(target,'hand_l')-joint(target,'hand_r')
    align=Quaternion((0,0,1),math.atan2(left1.y,left1.x)-math.atan2(left0.y,left0.x))
    old={b.name:(driver.matrix_world@b.matrix_local).to_quaternion().normalized() for b in driver.data.bones}
    base={b.name:(target.matrix_world@b.matrix_local).to_quaternion().normalized() for b in target.data.bones}
    driver.data.pose_position='POSE';target.data.pose_position='POSE'
    bind_action(driver,action);bind_action(target,idle);bpy.context.scene.frame_set(8)
    grip={b.name:b.rotation_quaternion.copy() for b in target.pose.bones if donor_name(b.name) is None}
    out=bpy.data.actions.new('Retarget_'+label);bind_action(target,out);rest(target)
    bones=sorted(target.pose.bones,key=lambda b:len(b.parent_recursive))
    a,z=action.frame_range;previous={}
    for i in range(25):
        frame=a+(z-a)*i/24;bpy.context.scene.frame_set(int(frame),subframe=frame%1)
        deltas={}
        for pb in bones:
            inherited=deltas.get(pb.parent.name,Quaternion()) if pb.parent else Quaternion()
            source=donor_name(pb.name)
            if source and source in old:
                world=(driver.matrix_world@driver.pose.bones[source].matrix).to_quaternion().normalized()
                delta=align@(world@old[source].inverted())@align.inverted()
                q=base[pb.name].inverted()@inherited.inverted()@delta@base[pb.name];q.normalize()
            else:
                q=grip.get(pb.name,Quaternion())
                delta=inherited@base[pb.name]@q@base[pb.name].inverted()
            if pb.name in previous:q.make_compatible(previous[pb.name])
            previous[pb.name]=q.copy();pb.rotation_mode='QUATERNION';pb.rotation_quaternion=q
            pb.keyframe_insert(data_path='rotation_quaternion',frame=1+(frame-a),group=pb.name)
            deltas[pb.name]=delta
    disconnect(driver);disconnect(target)
    return out

COURT={"Belt","BreastPlate","cuisse","Gauntlets","Helmet","Shoes","Shoulder-Plate"}
def armour(target):
    src=CACHE/'court/CourtKnight.blend'
    with bpy.data.libraries.load(str(src),link=False) as (library,items):
        items.objects=[name for name in library.objects if name in COURT or name=='rig']
    for o in items.objects:
        if o and o.name not in bpy.context.scene.collection.objects:bpy.context.scene.collection.objects.link(o)
    rig=next(o for o in items.objects if o and o.type=='ARMATURE')
    rig.data.pose_position='REST';bpy.context.view_layer.update()
    spine=['pelvis','spine_01','spine_02','spine_03','neck_01','neck_01','Head']
    def route(group):
        name=group.removeprefix('DEF-');side='_l' if '.L' in name else '_r'
        if name.startswith('spine'):
            num=int(name[-3:]) if name[-3:].isdigit() else 0
            return spine[min(num,6)],'DEF-'+name
        for left,right,child in [('upper_arm','upperarm','forearm'),('forearm','lowerarm','hand'),('hand','hand',None),('shoulder','clavicle','upper_arm'),
            ('thigh','thigh','shin'),('shin','calf','foot'),('foot','foot','toe'),('toe','ball',None)]:
            if name.startswith(left+'.'):
                return right+side,'DEF-'+left+('.L' if side=='_l' else '.R')
        if 'finger' in name or 'thumb' in name or name.startswith('f_') or 'palm' in name:return 'hand'+side,'DEF-hand'+('.L' if side=='_l' else '.R')
        return 'Head','DEF-spine.006'
    scale=(joint(target,'Head')-joint(target,'pelvis')).length/(joint(rig,'DEF-spine.006')-joint(rig,'DEF-spine')).length
    transform={}
    def fitting(name):
        if name in transform:return transform[name]
        dest,source=route(name)
        if source not in rig.data.bones:source='DEF-spine.006'
        a=joint(rig,source);b=joint(target,dest)
        # Long bone direction is defined by anatomical endpoints, not rig roll.
        original=rig.matrix_world.to_3x3()@(rig.data.bones[source].tail_local-rig.data.bones[source].head_local)
        desired=target.matrix_world.to_3x3()@(target.data.bones[dest].tail_local-target.data.bones[dest].head_local)
        q=original.normalized().rotation_difference(desired.normalized())
        if dest in spine:q=Quaternion()
        transform[name]=(dest,Matrix.Translation(b)@q.to_matrix().to_4x4()@Matrix.Scale(scale,4)@Matrix.Translation(-a))
        return transform[name]
    exports=[]
    for o in items.objects:
        if not o or o.name not in COURT:continue
        for mod in list(o.modifiers):
            if mod.type in ['BUILD','SUBSURF']:o.modifiers.remove(mod)
        bpy.context.view_layer.update()
        evaluated=o.evaluated_get(bpy.context.evaluated_depsgraph_get())
        data=bpy.data.meshes.new_from_object(evaluated,preserve_all_data_layers=True,depsgraph=bpy.context.evaluated_depsgraph_get())
        world=o.matrix_world.copy()
        groups=[g.name for g in o.vertex_groups]
        vertices=[]
        for v in data.vertices:
            pos=world@v.co;entries={}
            points=Vector((0,0,0));total=0
            for g in v.groups:
                if g.group>=len(groups) or not groups[g.group].startswith('DEF-') or g.weight<.0001:continue
                targetname,mat=fitting(groups[g.group]);points+=(mat@pos)*g.weight;total+=g.weight
                entries[targetname]=entries.get(targetname,0)+g.weight
            if not entries:
                # Rigid parts (e.g. the helmet) can be attached to a single original anatomical landmark.
                default='DEF-spine.006' if o.name=='Helmet' else 'DEF-spine.003'
                targetname,mat=fitting(default);points=mat@pos;entries={targetname:1};total=1
            v.co=points/total;vertices.append(entries)
        obj=bpy.data.objects.new('Court_'+o.name,data);bpy.context.collection.objects.link(obj)
        obj.parent=target;obj.matrix_world=Matrix.Identity(4)
        # new_from_object preserves deform weights by index; clear them before installing named groups.
        bm=bmesh.new();bm.from_mesh(data);layer=bm.verts.layers.deform.active
        if layer:
            for v in bm.verts:v[layer].clear()
        bm.to_mesh(data);bm.free()
        pool={}
        for i,entries in enumerate(vertices):
            for key,w in entries.items():
                if key not in pool:pool[key]=obj.vertex_groups.new(name=key)
                pool[key].add([i],w,'REPLACE')
        for i,mat in enumerate(data.materials):
            label='Court_Gold' if mat and mat.name.startswith('Gold') else 'Court_Leather' if mat and mat.name.startswith('Black') else 'Court_Steel'
            data.materials[i]=bpy.data.materials.get(label) or bpy.data.materials.new(label)
        mod=obj.modifiers.new('Transferred original armour weights','ARMATURE');mod.object=target;exports.append(obj)
    if len(exports)!=len(COURT):raise ValueError('Missing authored armour pieces')
    return exports

def company(name,sex,garment,plate=False):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    target,parts,_=import_set(UNIVERSAL/(sex+'_'+garment+'.fbx'))
    parts+=add_head(target,sex,name in ['Castellan','Yeoman'])
    if plate:
        parts=[o for o in parts if not o.name.startswith("Hair_SimpleParted")];parts+=armour(target)
    lib,_,bank=import_set(UNIVERSAL/'UAL1_Standard.fbx')
    driver,_,donor=import_set(CACHE/'kaykit/Knight.fbx')
    clips={label:bank[src] for label,src in DIRECT.items()}
    if plate:clips["Idle"]=bank["Sword_Idle"]
    for label,src in RETARGET.items():clips[label]=retarget_arms(target,driver,donor[src],label,bank['Sword_Idle'])
    disconnect(target);rest(target);target.data.pose_position='POSE'
    for key,clip in clips.items():
        track=target.animation_data.nla_tracks.new();track.name=key
        strip=track.strips.new(key,1,clip);strip.name=key
    bpy.context.scene.frame_set(1)
    select_export(DEST/(name+'.fbx'),[target]+parts,True)
    print('CC0_COMPANY',name,len(parts),len(target.data.bones),len(clips),flush=True)

if __name__=='__main__':
    for name,sex,role,heavy in [('Castellan','Male','Peasant',True),('Forester','Male','Ranger',False),
        ('Falconer','Female','Ranger',False),('Yeoman','Male','Peasant',False),('Goodwife','Female','Peasant',False)]:
        targets=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
        if not targets or name in targets:company(name,sex,role,heavy)
    if not targets or 'Horse' in targets:animal('Horse',CACHE/'quaternius/Horse.glb')
