from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parent))
from prepare_meshes import bpy,CACHE,OUT,paint,reduce,select_export
from prepare_rigs import disconnect,rest,read
from mathutils import Matrix

def mantle():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    file=CACHE/'kaykit/Knight.fbx';read(file)
    arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    disconnect(arm);rest(arm);arm.data.pose_position='REST';bpy.context.view_layer.update()
    cloak=next(o for o in bpy.context.scene.objects if o.name=='Knight_Cape')
    world=cloak.matrix_world.copy();paint(cloak,file);cloak.parent=None;cloak.matrix_world=Matrix.Identity(4);cloak.data.transform(world)
    cloak.name='High_Mantle';low=cloak.copy();low.data=cloak.data.copy();bpy.context.collection.objects.link(low)
    reduce(low,32);low.name='Low_Mantle'
    select_export(OUT/'Mantle.fbx',[cloak,low])

if __name__=='__main__':mantle()
