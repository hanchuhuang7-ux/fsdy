"""Build Unity flipbook texture-array sources from the verified photographer's maps.

Run with Python, Pillow and numpy from the repository root. The four-by-four grid
is only a transport image. Unity imports each cell as an independently mipmapped
512-pixel texture layer so seams never bleed into a neighbouring material.
"""
from pathlib import Path
import json
import sys

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / "ThirdPartyAssets"
sys.path.insert(0, str(CACHE / "python"))
from PIL import Image
import numpy as np

OUT = ROOT / "Unity/Assets/Frontline/ThirdParty/Resources/Scans"
OUT.mkdir(parents=True, exist_ok=True)
D = 512
SURFACES = ["medieval_blocks_03", "medieval_wood", "worn_mossy_plasterwall", "clay_roof_tiles",
            "metal_plate_02", "rough_linen", "leafy_grass", "forest_floor",
            "muddy_tracks", "cobblestone_floor_01", "damp_beach_sand", "bark_brown_01",
            "thatch_roof_angled", "rusty_metal_02", "mossy_rock", "fabric_leather_01"]
NATURE = ["island_tree_01", "island_tree_01_branches", "island_tree_01_leaves",
          "pine_sapling_small_bark", "pine_sapling_small_twig", "shrub_01", "rock_07",
          "wooden_lantern_01", "wooden_lantern_01_glass", "wine_barrel_01", "wooden_crate_01", "grass_bermuda_01"]


def pixels(path, mode="RGB"):
    with Image.open(path) as im:
        return np.asarray(im.convert(mode).resize((D,D), Image.Resampling.LANCZOS)).copy()


def write_array(name, layers, nature):
    albedo = Image.new("RGBA", (4*D,((len(layers)+3)//4)*D), (218,218,218,255))
    packed = Image.new("RGBA", albedo.size, (128,128,200,255))
    for i, key in enumerate(layers):
        if nature:
            family=next(k for k in ["island_tree_01","pine_sapling_small","shrub_01","rock_07",
                                     "wooden_lantern_01","wine_barrel_01","wooden_crate_01","grass_bermuda_01"] if key.startswith(k))
            folder=CACHE / "polyhaven" / family / "textures"
            color_file=folder / (key+"_diff_1k.jpg")
            normal_file=folder / (key+"_nor_gl_1k.jpg")
            arm_file=folder / (key+"_arm_1k.jpg")
        else:
            folder=CACHE/"polyhaven"/key
            color_file,normal_file,arm_file=(folder/n for n in ("color.jpg","normal.jpg","arm.jpg"))
        source=pixels(color_file)
        normal=pixels(normal_file)
        alpha=np.full((D,D),255,np.uint8)
        if nature and key.endswith(("_leaves","_twig")):
            alpha=pixels(next(folder.glob(("*leaves_alpha*" if key.endswith("_leaves") else "*twig_alpha*"))),"L")
        elif nature and key=="grass_bermuda_01":alpha=pixels(folder/"alpha.jpg","L")
        elif nature and key=="shrub_01":
            # The publisher's shrub JPG carries RGBA silhouette as a separate mask if supplied.
            # Its original mesh already traces the blades.
            alpha=np.where(source.max(axis=2)<10,0,255).astype(np.uint8)
        if arm_file.exists():
            arm=pixels(arm_file); ao,rough=arm[:,:,0],arm[:,:,1]
        else:
            rough=pixels(folder/"rough.jpg","L")
            ao=pixels(folder/"ao.jpg","L") if (folder/"ao.jpg").exists() else np.full((D,D),255,np.uint8)
        xy=(i%4*D,i//4*D)
        albedo.paste(Image.fromarray(np.dstack((source,alpha))),xy)
        packed.paste(Image.fromarray(np.dstack((normal[:,:,:2],rough,ao))),xy)
    albedo.save(OUT/(name+"Color.png"),compress_level=8)
    packed.save(OUT/(name+"Detail.png"),compress_level=8)


def garments():
    directory=CACHE/"universal"
    color=Image.new("RGBA",(3*D,3*D));detail=Image.new("RGBA",color.size,(128,128,196,255))
    for i,key in enumerate(["Peasant","Ranger","Superhero_Male","Superhero_Female","Regular_Male","Regular_Female","Eye","Hair_1","Hair_2"]):
        base=directory/("T_"+key+"_BaseColor.png")
        if i==2:base=directory/"T_Superhero_Male_Ligh.png"
        if i==3:base=directory/"T_Superhero_Female_Light_BaseColor.png"
        if i in (4,5):base=directory/("T_"+key+"_Dark_BaseColor.png")
        if i==6:base=directory/"T_Eye_Brown.png"
        pigment=pixels(base);mask=np.zeros((D,D),np.uint8)
        if i in (0,1):
            alt=pixels(directory/("T_Peasant_2_BaseColor.png" if i==0 else "T_Ranger_3_BaseColor.png"))
            mask=np.clip(np.abs(pigment.astype(float)-alt).max(axis=2)*3.8,0,255).astype(np.uint8)
        npath=directory/("T_"+key+"_Normal.png")
        normal=pixels(npath) if npath.exists() else np.broadcast_to(np.array([128,128,255],np.uint8),(D,D,3))
        orm=directory/("T_"+key+"_ORM.png")
        if orm.exists():
            occlusion=pixels(orm);rough,ao=occlusion[:,:,1],occlusion[:,:,0]
        else:
            rp=directory/("T_"+key+"_Roughness.png")
            rough=pixels(rp,"L") if rp.exists() else np.full((D,D),174,np.uint8)
            ao=np.full((D,D),255,np.uint8)
        at=(i%3*D,i//3*D)
        color.paste(Image.fromarray(np.dstack((pigment,mask))),at)
        detail.paste(Image.fromarray(np.dstack((normal[:,:,:2],rough,ao))),at)
    color.save(OUT/"WardrobeColor.png");detail.save(OUT/"WardrobeDetail.png")

if __name__=="__main__":
    garments()
    write_array("Landscape",SURFACES,False)
    write_array("Specimen",NATURE,True)
    print("CC0_SCAN_ARRAYS_OK",len(SURFACES),len(NATURE),D)
