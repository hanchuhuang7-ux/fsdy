"""Project real scan triangles into depth-sliced foliage impostors, preserving their alpha.

The nearest crown also keeps a distributed sample of the author's textured leaf
and branch triangles. All colours and all sample positions come from the source.
The FBX converter reads the resulting slices and trunk meshes from this cache.
"""
from pathlib import Path
import json
import sys
import numpy as np

ROOT=Path(__file__).resolve().parents[2]
CACHE=ROOT/"ThirdPartyAssets"
sys.path.insert(0,str(CACHE/"python"))
from PIL import Image, ImageFilter
from prepare_scans import OUT
SIZE=256
PLANTS=["island_tree_01","pine_sapling_small","shrub_01"]
AXES=[(0,0,1),(.8660254,0,.5),(.8660254,0,-.5),(0,1,0)]
TYPES={5126:"<f4",5125:"<u4",5123:"<u2",5121:"u1"}

def read_primitive(doc,buf,prim,prop):
    acc=doc["accessors"][prim["indices"] if prop=="indices" else prim["attributes"][prop]]
    dim={"SCALAR":1,"VEC2":2,"VEC3":3,"VEC4":4}[acc["type"]]
    view=doc["bufferViews"][acc["bufferView"]]
    dt=np.dtype(TYPES[acc["componentType"]])
    return np.ndarray((acc["count"],dim),dt,buf,offset=view.get("byteOffset",0)+acc.get("byteOffset",0),
                      strides=(view.get("byteStride",dt.itemsize*dim),dt.itemsize))

def generate(key,order):
    folder=CACHE/"polyhaven"/key
    doc=json.loads((folder/(key+".gltf")).read_text())
    buf=(folder/doc["buffers"][0]["uri"]).read_bytes()
    # Only one specimen of the three saplings is used.
    prims=doc["meshes"][0]["primitives"]
    points=[];colors=[]
    geometry={}
    for i,prim in enumerate(prims):
        mat=doc["materials"][prim["material"]]["name"]
        p=read_primitive(doc,buf,prim,"POSITION")
        uv=read_primitive(doc,buf,prim,"TEXCOORD_0")
        normal=read_primitive(doc,buf,prim,"NORMAL")
        tri=read_primitive(doc,buf,prim,"indices").reshape(-1,3)
        is_leaf=key=="shrub_01" or mat.endswith(("_leaves","_twig"))
        woody=mat.endswith("_branches")
        if not is_leaf and not woody:
            selected=tri
        else:
            # Preserve patches of adjacent original faces (including their original UV islands).
            count=min(len(tri),720 if woody else 1600)
            starts=np.linspace(0,len(tri)-5,max(1,count//4),dtype=np.int64)
            selected=tri[(starts[:,None]+np.arange(4)).reshape(-1)]
        unique,indices=np.unique(selected.reshape(-1),return_inverse=True)
        geometry["p"+str(i)]=p[unique];geometry["n"+str(i)]=normal[unique]
        geometry["uv"+str(i)]=uv[unique];geometry["t"+str(i)]=indices.reshape(-1,3)
        geometry["mat"+str(i)]=np.array(mat)
        if not is_leaf and not woody:continue
        tex=np.asarray(Image.open(folder/"textures"/(mat+"_diff_1k.jpg")).convert("RGB"))
        alpha_file=list((folder/"textures").glob("*"+("leaves_alpha" if mat.endswith("_leaves") else "twig_alpha")+".jpg"))
        alpha=np.asarray(Image.open(alpha_file[0]).convert("L")) if is_leaf and alpha_file else None
        for j in range(0,len(tri),100000):
            batch=tri[j:j+100000]
            for bary in ((.34,.33,.33),(.68,.16,.16),(.16,.68,.16),(.16,.16,.68)):
                coord=uv[batch[:,0]]*bary[0]+uv[batch[:,1]]*bary[1]+uv[batch[:,2]]*bary[2]
                y=(coord[:,1]*tex.shape[0]).astype(int)%tex.shape[0]
                x=(coord[:,0]*tex.shape[1]).astype(int)%tex.shape[1]
                rgb=tex[y,x]
                keep=alpha[y%alpha.shape[0],x%alpha.shape[1]]>95 if alpha is not None else rgb.max(axis=1)>8
                pos=p[batch[:,0]]*bary[0]+p[batch[:,1]]*bary[1]+p[batch[:,2]]*bary[2]
                points.append(pos[keep]);colors.append(rgb[keep])
    cache=CACHE/"reduced";cache.mkdir(exist_ok=True)
    np.savez(cache/(key+".npz"),**geometry)
    pts=np.concatenate(points);rgb=np.concatenate(colors)
    sections=[]
    for orient,axis in enumerate(AXES):
        n=np.array(axis,np.float32)
        u=np.cross(np.array([0,1,0],np.float32) if orient<3 else np.array([0,0,1],np.float32),n);u/=np.linalg.norm(u)
        v=np.cross(n,u)
        a=pts@u;b=pts@v;z=pts@n
        # A small gutter surrounds every sparse layer; Unity clamps each slice independently.
        amin,amax=float(a.min()),float(a.max());bmin,bmax=float(b.min()),float(b.max())
        pad=(amax-amin)*.024+.001;amin-=pad;amax+=pad;bmin-=pad;bmax+=pad
        px=np.clip(((a-amin)/(amax-amin)*SIZE).astype(int),0,SIZE-1)
        py=np.clip(((bmax-b)/(bmax-bmin)*SIZE).astype(int),0,SIZE-1)
        depth_min,depth_max=z.min(),z.max()
        band=np.minimum(3,((z-depth_min)/max(.0001,depth_max-depth_min)*4).astype(int))
        for k in range(4):
            mask=band==k
            ix=py[mask]*SIZE+px[mask];cs=rgb[mask]
            count=np.bincount(ix,minlength=SIZE*SIZE).reshape(SIZE,SIZE)
            dye=np.stack([np.bincount(ix,weights=cs[:,c],minlength=SIZE*SIZE).reshape(SIZE,SIZE)/np.maximum(1,count) for c in range(3)],axis=-1)
            alpha=np.clip(count*130,0,255).astype(np.uint8)
            layer=Image.fromarray(np.dstack((dye.astype(np.uint8),alpha)))
            # Extend boundary colours under transparent texels before mip filtering.
            edge=Image.fromarray(dye.astype(np.uint8)).filter(ImageFilter.MaxFilter(3))
            edge.putalpha(layer.getchannel("A"));layer=Image.composite(layer,edge,Image.fromarray((alpha>0).astype(np.uint8)*255))
            index=order*16+orient*4+k
            atlas.paste(layer,(index%8*SIZE,index//8*SIZE))
            den=float(np.mean(z[mask])) if ix.size else float(depth_min+(k+.5)*(depth_max-depth_min)/4)
            corners=[(u*x+v*y+n*den).tolist() for x,y in [(amin,bmin),(amax,bmin),(amax,bmax),(amin,bmax)]]
            sections.append(dict(index=index,corners=corners,axis=n.tolist()))
    (cache/(key+".json")).write_text(json.dumps(sections))
    print(key,len(pts),"photographic samples",flush=True)

if __name__=="__main__":
    atlas=Image.new("RGBA",(8*SIZE,6*SIZE))
    for i,key in enumerate(PLANTS):generate(key,i)
    atlas.save(OUT/"FoliageViews.png")
