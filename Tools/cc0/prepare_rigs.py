"""Common skeletal import helpers and the Quaternius horse clip bank."""
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parent))
from prepare_meshes import bpy, CACHE, OUT, read, paint, select_export

def bind_action(obj,action):
    anim=obj.animation_data_create();anim.action=action
    if action and len(action.slots):anim.action_slot=action.slots[0]

def disconnect(obj):
    anim=obj.animation_data_create()
    for track in list(anim.nla_tracks):anim.nla_tracks.remove(track)
    bind_action(obj,None)

def rest(obj):
    for bone in obj.pose.bones:
        bone.location=(0,0,0);bone.rotation_mode='QUATERNION'
        bone.rotation_quaternion=(1,0,0,0);bone.scale=(1,1,1)

def bake(name,source):
    bpy.ops.wm.read_factory_settings(use_empty=True);read(source)
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    model=next(o for o in bpy.context.scene.objects if o.name==name and o.type=='MESH')
    actions={a.name.rsplit('|',1)[-1]:a for a in bpy.data.actions}
    disconnect(rig);rest(rig);paint(model,source)
    clips={"Idle":"Idle","Walk":"Walk","Run":"Gallop","Hit":"Idle_HitReact_Left","Death":"Death",
           "Work":"Eating","Heavy":"Attack_Kick","Ride":"Gallop_Jump"}
    for label,key in clips.items():
        track=rig.animation_data.nla_tracks.new();track.name=label
        strip=track.strips.new(label,1,actions[key]);strip.name=label
    bpy.context.scene.frame_set(1)
    select_export(OUT.parent/'Actors'/(name+'.fbx'),[rig,model],True)
    print('CC0_ANIMAL',name,len(rig.data.bones),list(clips),flush=True)
