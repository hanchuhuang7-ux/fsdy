using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontline.Editor
{
    public static partial class ExternalAssetImporter
    {
        static readonly string[] PhotoMaterials={"island_tree_01","island_tree_01_branches","island_tree_01_leaves",
            "pine_sapling_small_bark","pine_sapling_small_twig","shrub_01","rock_07","wooden_lantern_01",
            "wooden_lantern_01_glass","wine_barrel_01","wooden_crate_01","grass_bermuda_01"};

        internal sealed class Part
        {
            public string name,filter;
            public Vector3 at,box,angles;
            public bool exact,center,detailOnly;
            public Part(string model,float w,float h,float d,float x=0,float y=0,float z=0,float yaw=0,string filter=null,
                bool exact=false,bool center=false)
            {name=model;box=new Vector3(w,h,d);at=new Vector3(x,y,z);angles=new Vector3(0,yaw,0);this.filter=filter;this.exact=exact;this.center=center;}
        }
        internal sealed class ArtData
        {
            public readonly List<Vector3> p=new List<Vector3>(),n=new List<Vector3>();
            public readonly List<Vector2> uv=new List<Vector2>();
            public List<Vector2> flex;
            public readonly List<Vector4> surface=new List<Vector4>(),joints=new List<Vector4>(),weights=new List<Vector4>();
            public readonly List<Color32> color=new List<Color32>();
            public readonly List<int> idx=new List<int>();
            public Bounds Bounds()
            {
                if(p.Count==0)throw new InvalidDataException("Empty artist mesh");
                var box=new Bounds(p[0],Vector3.zero);for(int i=1;i<p.Count;i++)box.Encapsulate(p[i]);return box;
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=p.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.SetVertices(p);mesh.SetNormals(n);mesh.SetColors(color);mesh.SetUVs(0,uv);mesh.SetUVs(1,surface);
                if(flex!=null)mesh.SetUVs(2,flex);
                if(joints.Count!=0){mesh.SetUVs(3,joints);mesh.SetUVs(4,weights);}
                mesh.SetTriangles(idx,0);mesh.RecalculateBounds();return mesh;
            }
            public void Add(ArtData src,Matrix4x4 m,int bone=-1,Color? tincture=null,bool project=false)
            {
                int offset=p.Count;var normal=m.inverse.transpose;
                for(int i=0;i<src.p.Count;i++)
                {
                    var point=m.MultiplyPoint3x4(src.p[i]);var dir=normal.MultiplyVector(src.n[i]).normalized;
                    var tag=src.surface[i];
                    p.Add(point);n.Add(dir);uv.Add(project&&tag.x<32?Project(point,dir):src.uv[i]);surface.Add(tag);
                    Color dye=src.color[i];if(tincture.HasValue){var t=tincture.Value;dye=new Color(dye.r*t.r,dye.g*t.g,dye.b*t.b,dye.a);}
                    color.Add(dye);
                    if(src.flex!=null&&flex==null){flex=new List<Vector2>(p.Count);while(flex.Count<p.Count-1)flex.Add(Vector2.zero);}
                    flex?.Add(src.flex==null?Vector2.zero:src.flex[i]);
                    if(bone>=0){joints.Add(new Vector4(bone,0,0,0));weights.Add(new Vector4(1,0,0,0));}
                    else if(src.joints.Count!=0){joints.Add(src.joints[i]);weights.Add(src.weights[i]);}
                }
                foreach(int index in src.idx)idx.Add(index+offset);
            }
        }

        static Vector2 Project(Vector3 p,Vector3 n) =>
            Mathf.Abs(n.y)>.67f?new Vector2(p.x,p.z)*.82f:
            Mathf.Abs(n.x)>Mathf.Abs(n.z)?new Vector2(p.z,p.y)*.82f:new Vector2(p.x,p.y)*.82f;

        static readonly string[] DressMaterials={"MI_Peasant","MI_Ranger","MI_Superhero_Male","MI_Superhero_Female","MI_Regular_Male","MI_Regular_Female","MI_Eyes","MI_Hair_1","MI_Hair_2"};
        static int Tag(string material,Color color,string model)
        {
            int wardrobe=Array.FindIndex(DressMaterials,m=>material.StartsWith(m,StringComparison.OrdinalIgnoreCase));
            if(wardrobe>=0)return 112+wardrobe;
            if(model=="Horse")return 10;
            if(model=="Mantle")return 6;
            if(model.StartsWith("siege-catapult"))return color.b>color.r*.99f?5:2;
            if(model.StartsWith("shield")&&color.r>color.b*1.12f)return 6;
            if(material.StartsWith("Court_"))return material.Contains("Leather")?10:5;
            if(material.StartsWith("Canopy_",StringComparison.Ordinal))return 64+int.Parse(material.Substring(7));
            if(material.StartsWith("Photo_",StringComparison.Ordinal))
            {
                int index=Array.IndexOf(PhotoMaterials,material.Substring(6).Split('.')[0]);
                if(index>=0)return 32+index;
            }
            string s=material.ToLowerInvariant();
            if(s.Contains("window")||s.Contains("glass"))return 14;
            if(s.Contains("fire")||s.Contains("flame")||s.Contains("ember"))return 15;
            if(s.Contains("roof"))return 4;
            if(s.Contains("plaster")||s.Contains("lime"))return 3;
            if(s.Contains("stone")||s.Contains("rock")||s.Contains("brick"))return 1;
            if(s.Contains("wood")||s.Contains("plank")||s.Contains("bark"))return 2;
            if(s.Contains("gold")||s.Contains("steel")||s.Contains("iron")||s.Contains("metal")||s.Contains("armor"))return 5;
            if(s.Contains("wheat")||s.Contains("straw")||s.Contains("hay"))return 11;
            if(s.Contains("grass")||s.Contains("green")||s.Contains("leaf"))return 8;
            if(s.Contains("skin")||s.Contains("hair")||s.Contains("eye")||s.Contains("boot"))return 10;
            if(s.Contains("dirt")||s.Contains("earth"))return 9;
            if(model.Contains("grain")||model=="Hay"||model.StartsWith("Farm_"))return 11;
            if(model.StartsWith("Grass")||model.StartsWith("Flower")||model.StartsWith("Bush"))return 8;
            if(model.Contains("dirt"))return 9;
            if(model.Contains("bridge")||model.Contains("fence_wood")||model.Contains("wheelbarrow")||model.Contains("bucket"))return 2;
            if(model.Contains("castle")||model.Contains("church")||model.Contains("tower")||model.Contains("wall")||model.Contains("fence_stone"))
            {
                if(color.r>color.g*1.37f&&color.g>color.b*1.05f)return color.r>.47f?4:2;
                return color.r>color.b*1.13f&&color.r<.51f?2:1;
            }
            if(color.maxColorComponent-Mathf.Min(color.r,Mathf.Min(color.g,color.b))<.14f&&color.grayscale>.32f)return 5;
            return 6;
        }

        static Color Pigment(Color c,int tag,bool actor,string part,string slot,string model)
        {
            if(tag>=112)return new Color(1,1,1,tag<=113?1:0);
            if(tag>=32)return Color.white;
            if(model=="Mantle")return new Color(.55f,.52f,.46f,.90f);
            if(model.StartsWith("shield")&&tag==6||model=="Banner"&&!slot.ToLowerInvariant().Contains("wood"))return new Color(.63f,.60f,.54f,1);
            if(model=="Horse")return slot switch {"Hair" or "Hooves"=>new Color(.20f,.15f,.12f,0),"Muzzle"=>new Color(.38f,.27f,.20f,0),"Eye_White"=>new Color(.71f,.66f,.54f,0),"Eye_Black"=>new Color(.073f,.062f,.046f,0),"Main_Dark"=>new Color(.39f,.245f,.156f,0),_=>new Color(.51f,.322f,.20f,0)};
            if(slot.StartsWith("Court_"))return slot.Contains("Gold")?new Color(.61f,.46f,.25f,0):slot.Contains("Leather")?new Color(.249f,.166f,.111f,0):new Color(.48f,.506f,.516f,0);
            float y=c.grayscale;
            if(actor)
            {
                string lower=part.ToLowerInvariant();
                bool cloth=tag==6||lower.Contains("body")&&tag!=5&&tag!=10||lower.Contains("cape")||lower.Contains("shirt");
                float dye=cloth&&y>.08f?.85f:0;
                var baseColor=Color.Lerp(new Color(y,y,y),c,.73f);
                if(dye>0)baseColor=Color.Lerp(baseColor,new Color(.82f,.79f,.72f),.65f);
                return new Color(baseColor.r,baseColor.g,baseColor.b,dye);
            }
            Color baseTone=tag switch {1=>new Color(.78f,.78f,.73f),2=>new Color(.66f,.50f,.36f),
                3=>new Color(.89f,.84f,.73f),4=>new Color(.70f,.46f,.38f),11=>new Color(.81f,.70f,.43f),
                14=>new Color(.94f,.64f,.29f),15=>new Color(1,.51f,.14f),_=>c};
            var result=Color.Lerp(baseTone,c,tag==4?.20f:tag==1||tag==2||tag==3?.24f:.37f);
            result.a=0;return result;
        }
        static ArtData ReadMesh(Mesh mesh,Material[] materials,Matrix4x4 transform,string model,string part,bool actor=false,
            int[] bones=null)
        {
            var result=new ArtData();
            var points=mesh.vertices;var normals=mesh.normals;var uvs=mesh.uv;var colors=mesh.colors32;
            var bw=bones!=null?mesh.boneWeights:null;
            var normal=transform.inverse.transpose;
            for(int s=0;s<mesh.subMeshCount;s++)
            {
                string slot=s<materials.Length&&materials[s]?materials[s].name:"";
                var lookup=new Dictionary<int,int>();
                foreach(int index in mesh.GetTriangles(s))
                {
                    if(!lookup.TryGetValue(index,out int dest))
                    {
                        dest=result.p.Count;lookup.Add(index,dest);
                        var p=transform.MultiplyPoint3x4(points[index]);var n=normal.MultiplyVector(normals[index]).normalized;
                        Color original=colors.Length==points.Length?(Color)colors[index]:(s<materials.Length&&materials[s]?materials[s].color:Color.white);
                        int tag=Tag(slot,original,model);
                        if(model.StartsWith("bridge-straight",StringComparison.Ordinal))tag=n.y>.70f?2:1;
                        result.p.Add(p);result.n.Add(n);result.uv.Add(uvs.Length==points.Length?uvs[index]:Project(p,n));
                        float ao=.96f;
                        result.surface.Add(new Vector4(tag,tag==5?.81f:0,tag==5?.46f:.82f,ao));
                        result.color.Add(Pigment(original,tag,actor,part,slot,model));
                        if(bones!=null)
                        {
                            var w=bw[index];
                            result.joints.Add(new Vector4(bones[w.boneIndex0],bones[w.boneIndex1],bones[w.boneIndex2],bones[w.boneIndex3]));
                            result.weights.Add(new Vector4(w.weight0,w.weight1,w.weight2,w.weight3));
                        }
                    }
                    result.idx.Add(dest);
                }
            }
            return result;
        }
        internal static readonly SortedSet<string> UsedSources=new SortedSet<string>(StringComparer.Ordinal);
        static readonly Dictionary<(string,bool,string),ArtData> parts=new Dictionary<(string,bool,string),ArtData>();
        internal static ArtData ReadPart(string name,bool low=false,string filter=null)
        {
            var key=(name,low,filter??"");
            UsedSources.Add(name);
            if(parts.TryGetValue(key,out var found))return found;
            var asset=Source(name);var result=new ArtData();
            foreach(var mf in asset.GetComponentsInChildren<MeshFilter>(true))
            {
                string prefix=low?"Low_":"High_";
                if(!mf.name.StartsWith(prefix,StringComparison.Ordinal))continue;
                string node=mf.name.Substring(prefix.Length);
                if(filter!=null&&(filter[0]=='!'?node==filter.Substring(1):node!=filter))continue;
                var piece=ReadMesh(mf.sharedMesh,mf.GetComponent<MeshRenderer>().sharedMaterials,mf.transform.localToWorldMatrix,name,node);
                result.Add(piece,Matrix4x4.identity);
            }
            if(result.p.Count==0)throw new InvalidDataException("No artist mesh for "+key);
            if(low)
            {
                // A quadric collapse may move a boundary vertex outwards. Register the proxy to the
                // author's detailed envelope (including its foot plane) before fitting any plot or verge.
                var high=ReadPart(name,false,filter).Bounds();var proxy=result.Bounds();
                float Ratio(float a,float b)=>b>.00001f&&a>.00001f?a/b:1;
                var k=new Vector3(Ratio(high.size.x,proxy.size.x),Ratio(high.size.y,proxy.size.y),Ratio(high.size.z,proxy.size.z));
                for(int i=0;i<result.p.Count;i++)
                {
                    result.p[i]=high.center+Vector3.Scale(result.p[i]-proxy.center,k);
                    var n=result.n[i];result.n[i]=new Vector3(n.x/k.x,n.y/k.y,n.z/k.z).normalized;
                }
            }
            parts.Add(key,result);return result;
        }

        internal static Matrix4x4 Fit(Bounds b,Part part)
        {
            var q=Quaternion.Euler(part.angles);
            float width=Mathf.Abs(q.eulerAngles.y%180-90)<.1f?part.box.z:part.box.x;
            float depth=Mathf.Abs(q.eulerAngles.y%180-90)<.1f?part.box.x:part.box.z;
            var ratio=new Vector3(width/Mathf.Max(.0001f,b.size.x),part.box.y/Mathf.Max(.0001f,b.size.y),
                depth/Mathf.Max(.0001f,b.size.z));
            float uniform=Mathf.Min(ratio.x,Mathf.Min(ratio.y,ratio.z));
            var size=part.exact?ratio:Vector3.one*uniform;
            var origin=b.center;if(!part.center)origin.y=b.min.y;
            return Matrix4x4.TRS(part.at,q,size)*Matrix4x4.Translate(-origin);
        }

        internal static Mesh WriteMesh(string name,Mesh mesh)
        {
            string path=Output+"/"+name+".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);mesh.name=name;
            if(!old){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;
        }

        static void ConformToHull(ArtData art,Mesh proxy)
        {
            var v=proxy.vertices.Distinct().ToArray();var bounds=proxy.bounds;
            var faces=new List<Plane>();
            for(int i=0;i<v.Length;i++)for(int j=i+1;j<v.Length;j++)for(int k=j+1;k<v.Length;k++)
            {
                if(Vector3.Cross(v[j]-v[i],v[k]-v[i]).sqrMagnitude<1e-9f)continue;
                var plane=new Plane(v[i],v[j],v[k]);if(plane.GetDistanceToPoint(bounds.center)>0)plane=plane.flipped;
                if(v.Any(p=>plane.GetDistanceToPoint(p)>.0001f)||faces.Any(f=>Vector3.Dot(f.normal,plane.normal)>.9999f&&Mathf.Abs(f.distance-plane.distance)<.0002f))continue;
                faces.Add(plane);
            }
            for(int i=0;i<art.p.Count;i++)
            {
                var dir=art.p[i]-bounds.center;float factor=1;
                foreach(var plane in faces){float dot=Vector3.Dot(plane.normal,dir);if(dot>.00001f)factor=Mathf.Min(factor,-plane.GetDistanceToPoint(bounds.center)/dot);}
                art.p[i]=bounds.center+dir*Mathf.Clamp01(factor);
            }
        }
    }
}
