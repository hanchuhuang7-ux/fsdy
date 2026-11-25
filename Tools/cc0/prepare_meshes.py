"""Curate the publisher meshes into FBX high/detail and shadow children. Run in Blender background mode."""
from pathlib import Path
import bpy
import bmesh
import json
import math
import re
import sys
import numpy as np
from mathutils import Matrix, Vector

ROOT=Path(__file__).resolve().parents[2]
CACHE=ROOT/"ThirdPartyAssets"
OUT=ROOT/"Unity/Assets/Frontline/ThirdParty/Art"
OUT.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(CACHE/"python"))
from PIL import Image

ACTORS={"Knight","Rogue","Mage","Barbarian","Horse","Farmer","Adventurer","KnightCharacter"}
PLANTS=["island_tree_01","pine_sapling_small","shrub_01"]
imcache={}
def clr(linear):
    return np.where(linear<=.0031308,12.92*linear,1.055*np.power(np.maximum(0,linear),1/2.4)-.055)
def read(path):
    if path.suffix.lower()==".fbx":
        bpy.ops.import_scene.fbx(filepath=str(path),automatic_bone_orientation=False)
    else:
        bpy.ops.import_scene.gltf(filepath=str(path))

def paint(o,path):
    mesh=o.data
    attr=mesh.color_attributes.get("Pigment") or mesh.color_attributes.new("Pigment","BYTE_COLOR","CORNER")
    mesh.color_attributes.active_color=attr
    for slot,material in enumerate(mesh.materials):
        if not material:continue
        bs=next((n for n in material.node_tree.nodes if n.type=="BSDF_PRINCIPLED"),None) if material.node_tree else None
        tint=np.array(bs.inputs['Base Color'].default_value[:3] if bs else material.diffuse_color[:3])
        image=None
        if path.parent.name in ("kaykit","kenney"):
            if path.parent.name=="kenney":pn="colormap.png"
            else:
                referenced=re.search(rb"([A-Za-z]+_texture\.png|hexagons_medieval\.png)",path.read_bytes(),re.I)
                pn=referenced.group(1).decode() if referenced else "hexagons_medieval.png"
            candidate=path.parent/pn
            if candidate.exists():
                if candidate not in imcache:imcache[candidate]=np.asarray(Image.open(candidate).convert("RGB"))/255.
                image=imcache[candidate]
        value=list(clr(tint))+[1.]
        uv=mesh.uv_layers.active
        for poly in mesh.polygons:
            if poly.material_index!=slot:continue
            for idx in poly.loop_indices:
                if image is not None and uv:
                    u,v=uv.data[idx].uv
                    a=int((u%1)*image.shape[1])%image.shape[1]
                    b=int(((1-v)%1)*image.shape[0])%image.shape[0]
                    attr.data[idx].color_srgb=(*image[b,a],1.)
                else:attr.data[idx].color_srgb=value

def reduce(mesh_obj,target):
    mesh=mesh_obj.data
    # Merge coincident corners after glTF material splitting before quadric decimation.
    bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.000015)
    bm.to_mesh(mesh);bm.free();mesh.update()
    mesh.calc_loop_triangles()
    if len(mesh.loop_triangles)<=target:return
    mod=mesh_obj.modifiers.new("Artist mesh LOD","DECIMATE")
    mod.ratio=max(.0001,target/len(mesh.loop_triangles));mod.use_collapse_triangulate=True
    bpy.context.view_layer.objects.active=mesh_obj
    bpy.ops.object.modifier_apply(modifier=mod.name)

def select_export(path,objs,anim=False):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:o.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,axis_forward='-Z',axis_up='Y',
        add_leaf_bones=False,bake_anim=anim,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=True,
        bake_anim_simplify_factor=.6,bake_anim_step=1.,use_mesh_modifiers=True,mesh_smooth_type='OFF',
        colors_type='SRGB',prioritize_active_color=True,path_mode='STRIP',use_custom_props=False)

def specimen(key,index):
    cache=CACHE/"reduced";source=np.load(cache/(key+".npz")); objs=[]
    # glTF Y-up to Blender Z-up. These are the original source positions, UVs and indices.
    def coords(a):return np.stack((a[:,0],-a[:,2],a[:,1]),axis=1)
    m=0
    while "p"+str(m) in source:
        tag=str(source["mat"+str(m)])
        pos=coords(source["p"+str(m)]);tri=source["t"+str(m)]
        me=bpy.data.meshes.new(tag);me.from_pydata(pos.tolist(),[],tri.tolist());me.update()
        ob=bpy.data.objects.new(tag,me);bpy.context.collection.objects.link(ob)
        me.materials.append(bpy.data.materials.new("Photo_"+tag))
        tex=me.uv_layers.new(name="UVMap")
        uv=source["uv"+str(m)]
        for loop in me.loops:
            u,v=uv[loop.vertex_index];tex.data[loop.index].uv=(u,1-v)
        for f in me.polygons:f.use_smooth=True
        me.normals_split_custom_set_from_vertices(coords(source["n"+str(m)]).tolist())
        # Distinct budget for the silhouette-bearing trunk and the real near leaves.
        reduce(ob,1000 if m==0 else 850)
        ob.name="High_"+tag;objs.append(ob)
        if m==0 and key!="shrub_01":
            low=ob.copy();low.data=ob.data.copy();bpy.context.collection.objects.link(low)
            reduce(low,56);low.name="Low_"+tag;objs.append(low)
        m+=1
    for row in json.loads((cache/(key+".json")).read_text()):
        vertices=coords(np.array(row['corners']))
        for prefix in ['High','Low']:
            mesh=bpy.data.meshes.new("LeafSlice")
            mesh.from_pydata(vertices.tolist(),[],[(0,1,2),(0,2,3),(2,1,0),(3,2,0)])
            mesh.update(); name="Canopy_"+str(row['index'])
            mesh.materials.append(bpy.data.materials.get(name) or bpy.data.materials.new(name))
            uv=mesh.uv_layers.new(name="UVMap")
            for lp in mesh.loops:uv.data[lp.index].uv=((0,0),(1,0),(1,1),(0,1))[lp.vertex_index]
            obj=bpy.data.objects.new(prefix+"_"+name,mesh);bpy.context.collection.objects.link(obj);objs.append(obj)
    select_export(OUT/(key+".fbx"),objs)

def static(path):
    read(path)
    objs=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name!='Icosphere']
    if path.stem=="grass_bermuda_01":
        objs=[o for o in objs if o.name=="grass_bermuda_01_medium_a"]
    results=[]
    for o in objs:
        if not len(o.data.polygons):continue
        world=o.matrix_world.copy();o.parent=None;o.matrix_world=Matrix.Identity(4)
        o.data=o.data.copy();o.data.transform(world)
        if path.suffix=='.gltf':
            for mat in o.data.materials:
                if mat and not mat.name.startswith("Photo_"):mat.name="Photo_"+mat.name
        else:paint(o,path)
        name=o.name.replace(' ','_')
        reduce(o,2600 if len(objs)<4 else 1200)
        o.name="High_"+name
        low=o.copy();low.data=o.data.copy();bpy.context.collection.objects.link(low)
        reduce(low,max(24,148//len(objs)));low.name="Low_"+name
        results.extend((o,low))
    if not results:raise ValueError("No renderable meshes in "+str(path))
    select_export(OUT/(path.stem+".fbx"),results)

if __name__=="__main__":
    lock=json.loads((ROOT/"Tools/cc0-sources.json").read_text())
    files=[CACHE/f["path"] for f in lock["files"] if Path(f["path"]).suffix.lower() in (".fbx",".glb",".gltf")]
    for path in files:
        selected=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
        if path.parent.name=="universal" or path.stem in ACTORS or selected and path.stem not in selected:continue
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if path.stem in PLANTS:specimen(path.stem,PLANTS.index(path.stem))
        else:static(path)
        print("CURATED",path.stem,flush=True)
