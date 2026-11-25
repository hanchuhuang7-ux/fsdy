using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Frontline.Core;
using Frontline.UnityGame;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Frontline.Editor
{
    public static partial class ExternalAssetImporter
    {
        const string LibraryPath=Third+"/Resources/CC0/ArtistRoster.asset";
        const int Intervals=24;
        static Dictionary<string,Object> stored;

        static T Store<T>(T obj,string name) where T:Object
        {
            obj.name=name;
            if(stored.TryGetValue(name,out var old))
            {
                EditorUtility.CopySerialized(obj,old);Object.DestroyImmediate(obj);EditorUtility.SetDirty(old);return (T)old;
            }
            AssetDatabase.AddObjectToAsset(obj,LibraryPath);stored.Add(name,obj);return obj;
        }

        sealed class RigBake : IDisposable
        {
            public readonly GameObject root;
            public readonly ArtData body=new ArtData();
            public readonly List<Transform> bones=new List<Transform>();
            readonly List<Matrix4x4> rest=new List<Matrix4x4>();
            readonly Dictionary<SkinnedMeshRenderer,Matrix4x4> rendererBinds=new Dictionary<SkinnedMeshRenderer,Matrix4x4>();
            public float maxSkinError,maxSocketError;public int comparedVertices,weightedVertices;
            public readonly ArtistSkeleton cooked;
            public Matrix4x4[][] samples;
            public Matrix4x4 norm;
            readonly bool horse;

            int Include(Transform bone,Matrix4x4 bind)
            {
                int i=bones.IndexOf(bone);if(i>=0)return i;
                bones.Add(bone);rest.Add(bind);return bones.Count-1;
            }
            public int Bone(params string[] names)
            {
                foreach(var n in names)for(int i=0;i<bones.Count;i++)
                    if(string.Equals(bones[i].name,n,StringComparison.OrdinalIgnoreCase))return i;
                throw new InvalidDataException(root.name+" lacks a socket "+string.Join("/",names));
            }
            public RigBake(string id)
            {
                horse=id=="Horse";
                root=Object.Instantiate(Source(id,true));root.name=id;root.hideFlags=HideFlags.HideAndDontSave;
                var anim=root.GetComponentInChildren<Animation>();if(anim)anim.enabled=false;
                var renderers=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var raw=new ArtData();
                foreach(var renderer in renderers)
                {
                    rendererBinds[renderer]=renderer.localToWorldMatrix;
                    var mesh=renderer.sharedMesh;
                    var map=new int[renderer.bones.Length];
                    var bp=mesh.bindposes;var rb=renderer.bones;
                    for(int i=0;i<map.Length;i++)map[i]=Include(rb[i],renderer.localToWorldMatrix*bp[i].inverse);
                    var chunk=ReadMesh(mesh,renderer.sharedMaterials,renderer.localToWorldMatrix,id,renderer.name,true,map);
                    raw.Add(chunk,Matrix4x4.identity);
                }
                foreach(var bone in root.GetComponentsInChildren<Transform>(true))
                    if(bone.name.StartsWith("handslot",StringComparison.OrdinalIgnoreCase)||bone.name=="Back"||
                        bone.name=="chest"||bone.name=="Chest")Include(bone,bone.localToWorldMatrix);
                var bounds=raw.Bounds();
                Vector3 forward=horse?BindPos(Bone("Head"))-BindPos(Bone("Back","Body")):
                    Vector3.Cross(BindPos(Bone("hand_r","handslot.r","Palm.R","Hand.R"))-BindPos(Bone("hand_l","handslot.l","Palm.L","Hand.L")),Vector3.up);
                forward.y=0;var facing=Quaternion.Euler(0,-Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg,0);
                float height=horse?1.91f:1.72f;
                norm=Matrix4x4.Scale(Vector3.one*(height/bounds.size.y))*Matrix4x4.Rotate(facing)*
                    Matrix4x4.Translate(-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z));
                body.Add(raw,norm,project:true);
                if(horse)
                    for(int v=0;v<body.p.Count;v++)
                    {
                        var p=body.p[v];Color pigment=body.color[v];
                        float dapples=.5f+.5f*Mathf.Sin(p.z*18+p.y*7)*Mathf.Sin(p.x*23-p.y*14);
                        float light=.79f+.17f*Mathf.Clamp01(p.y)+.10f*dapples;
                        pigment.r*=light; pigment.g*=light; pigment.b*=light*.96f;
                        if(p.y>.64f&&p.y<1.43f&&Mathf.Abs(p.z+.085f)<.049f&&Mathf.Abs(p.x)>.15f)
                        {
                            pigment=new Color(.235f,.154f,.101f,0);
                            if(p.y<.82f&&Mathf.Abs(p.z+.085f)>.031f)
                            {pigment=new Color(.44f,.435f,.392f,0);body.surface[v]=new Vector4(5,.76f,.46f,.85f);}
                        }
                        body.color[v]=pigment;
                    }
                cooked=new ArtistSkeleton{name=id,boneCount=bones.Count,skinnedVertices=body.p.Count,
                    takes=new ArtistTake[Enum.GetValues(typeof(ArtistMotion)).Length]};
                Sample(id);
                VerifyNativeSkin(id);
            }
            Vector3 BindPos(int b)=>rest[b].GetColumn(3);
            string ResolveTake(ArtistMotion motion,Dictionary<string,AnimationClip> clips)
            {
                if(motion>=ArtistMotion.Tend)
                    return clips.ContainsKey(motion.ToString())?motion.ToString():horse?"Idle":"Work";
                if(!horse)return motion==ArtistMotion.Trot?"Walk":motion==ArtistMotion.Lance?"Thrust":motion.ToString();
                return motion switch {ArtistMotion.Slash or ArtistMotion.Thrust or ArtistMotion.Heavy or ArtistMotion.Throw or ArtistMotion.Lance=>"Heavy",
                    ArtistMotion.Bow or ArtistMotion.Cast or ArtistMotion.Block=>"Idle",ArtistMotion.Trot=>"Walk",_=>motion.ToString()};
            }
            void Sample(string id)
            {
                var clips=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(Source(id,true))).OfType<AnimationClip>()
                    .Where(c=>!c.name.StartsWith("__",StringComparison.Ordinal)).ToDictionary(c=>c.name);
                int rows=cooked.takes.Length*(Intervals+1),width=bones.Count*3;
                var tex=new Texture2D(width,rows,TextureFormat.RGBAHalf,false,true){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color[width*rows];samples=new Matrix4x4[rows][];
                cooked.saddle=horse?new Vector3[rows]:Array.Empty<Vector3>();
                cooked.saddleRotation=horse?new Quaternion[rows]:Array.Empty<Quaternion>();
                var inverseRest=rest.Select(m=>m.inverse*norm.inverse).ToArray();
                int back=horse?Bone("Back","Body"):0,pelvis=horse?0:Bone("pelvis","hips","Hips");
                cooked.hips=horse?Array.Empty<Vector3>():new Vector3[rows];
                float deck=horse?body.p.Where(p=>Mathf.Abs(p.x)<.30f&&p.z>-.33f&&p.z<.10f).Max(p=>p.y):0;
                var lower=new bool[bones.Count];
                for(int i=0;i<lower.Length;i++){string key=bones[i].name.ToLowerInvariant();lower[i]=key.Contains("leg")||key.Contains("foot")||key.Contains("toe")||key=="hips"||key=="pelvis"||key.Contains("calf")||key.Contains("thigh")||key.Contains("ball_")||key=="body"||key=="root";}
                foreach(ArtistMotion motion in Enum.GetValues(typeof(ArtistMotion)))
                {
                    string take=ResolveTake(motion,clips);var clip=clips[take];int start=(int)motion*(Intervals+1);
                    cooked.takes[(int)motion]=new ArtistTake{first=start,intervals=Intervals,seconds=clip.length,source=horse&&motion==ArtistMotion.Trot?"Walk + Gallop (derived trot)":!horse&&motion==ArtistMotion.Lance?"Thrust + Ride (mounted)":take};
                    for(int i=0;i<=Intervals;i++)
                    {
                        int row=start+i;float t=i/(float)Intervals;
                        clip.SampleAnimation(root,t*clip.length);
                        var pose=samples[row]=new Matrix4x4[bones.Count];
                        for(int b=0;b<bones.Count;b++)pose[b]=norm*bones[b].localToWorldMatrix*inverseRest[b];
                        if(horse&&motion==ArtistMotion.Trot)
                        {
                            clips["Run"].SampleAnimation(root,t*clips["Run"].length);
                            for(int b=0;b<bones.Count;b++)
                            {
                                var gallop=norm*bones[b].localToWorldMatrix*inverseRest[b];
                                pose[b]=Matrix4x4.TRS(Vector3.Lerp(pose[b].GetColumn(3),gallop.GetColumn(3),.42f),
                                    Quaternion.Slerp(pose[b].rotation,gallop.rotation,.42f),Vector3.one);
                            }
                        }
                        if(!horse&&motion==ArtistMotion.Lance)
                        {
                            clips["Ride"].SampleAnimation(root,t*clips["Ride"].length);
                            var seated=norm*bones[pelvis].localToWorldMatrix*inverseRest[pelvis];
                            Vector3 bindHip=norm.MultiplyPoint3x4(BindPos(pelvis));
                            var offset=Matrix4x4.Translate(seated.MultiplyPoint3x4(bindHip)-pose[pelvis].MultiplyPoint3x4(bindHip));
                            for(int b=0;b<bones.Count;b++)pose[b]=lower[b]?norm*bones[b].localToWorldMatrix*inverseRest[b]:offset*pose[b];
                        }
                        if(!horse)cooked.hips[row]=pose[pelvis].MultiplyPoint3x4(norm.MultiplyPoint3x4(BindPos(pelvis)));
                        for(int b=0;b<bones.Count;b++)
                            for(int axis=0;axis<3;axis++)pixels[row*width+b*3+axis]=pose[b].GetRow(axis);
                        if(horse)
                        {
                            // Locate the saddle over the withers in normalized bind space and follow the actual spine.
                            cooked.saddle[row]=pose[back].MultiplyPoint3x4(new Vector3(0,deck+.018f,-.10f));
                            cooked.saddleRotation[row]=Quaternion.Slerp(Quaternion.identity,pose[back].rotation,.64f);
                        }
                    }
                }
                tex.SetPixels(pixels);tex.Apply(false,false);cooked.palette=Store(tex,id+"_Bones");
                float movement=0;var first=samples[(int)ArtistMotion.Run*(Intervals+1)];
                for(int i=1;i<=Intervals;i++)for(int b=0;b<bones.Count;b++)
                {
                    var reference=norm.MultiplyPoint3x4(BindPos(b));
                    movement=Mathf.Max(movement,(first[b].MultiplyPoint3x4(reference)-samples[(int)ArtistMotion.Run*(Intervals+1)+i][b].MultiplyPoint3x4(reference)).magnitude);
                }
                if(movement<.04f)throw new InvalidDataException("Artist run clip did not deform "+id+": "+movement);
                Debug.Log("CC0_SKIN_PALETTE "+id+" bones="+bones.Count+" frames="+rows+" runTrail="+movement);
            }
            void VerifyNativeSkin(string id)
            {
                var clips=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(Source(id,true))).OfType<AnimationClip>()
                    .Where(c=>!c.name.StartsWith("__")).ToDictionary(c=>c.name);
                foreach(var renderer in rendererBinds.Keys)
                    weightedVertices+=renderer.sharedMesh.boneWeights.Count(w=>w.weight1>.015f);
                foreach(var motion in new[]{ArtistMotion.Walk,ArtistMotion.Run,ArtistMotion.Hit,ArtistMotion.Death})
                {
                    var clip=clips[ResolveTake(motion,clips)];
                    foreach(int step in new[]{2,9,18,23})
                    {
                        clip.SampleAnimation(root,step/(float)Intervals*clip.length);
                        int row=(int)motion*(Intervals+1)+step;
                        foreach(var entry in rendererBinds)
                        {
                            var renderer=entry.Key;var mesh=renderer.sharedMesh;var verts=mesh.vertices;
                            var skin=mesh.boneWeights;var binds=mesh.bindposes;var links=renderer.bones;
                            for(int i=0;i<verts.Length;i+=Mathf.Max(1,verts.Length/23))
                            {
                                var weights=skin[i];var want=Vector3.zero;var got=Vector3.zero;
                                var p=norm.MultiplyPoint3x4(entry.Value.MultiplyPoint3x4(verts[i]));
                                Add(weights.boneIndex0,weights.weight0);Add(weights.boneIndex1,weights.weight1);
                                Add(weights.boneIndex2,weights.weight2);Add(weights.boneIndex3,weights.weight3);
                                maxSkinError=Mathf.Max(maxSkinError,(want-got).magnitude);comparedVertices++;
                                void Add(int local,float w)
                                {
                                    if(w<=0)return;int global=bones.IndexOf(links[local]);
                                    want+=w*(norm*links[local].localToWorldMatrix*binds[local]).MultiplyPoint3x4(verts[i]);
                                    int x=global*3;var t=cooked.palette;
                                    var q=new Vector4(p.x,p.y,p.z,1);
                                    got+=w*new Vector3(Vector4.Dot(t.GetPixel(x,row),q),Vector4.Dot(t.GetPixel(x+1,row),q),Vector4.Dot(t.GetPixel(x+2,row),q));
                                }
                            }
                        }
                    }
                }
                if(maxSkinError>.005f||weightedVertices==0)throw new InvalidDataException(id+" GPU palette disagrees with native FBX skin: "+maxSkinError);
            }

            public void Accessory(ArtData into,string model,string port,float length,string alignment,ArtistMotion reference,
                Vector3 offset=default,float tint=1)
            {
                int b=port switch {"right"=>Bone("hand_r","handslot.r","Palm.R","Hand.R"),"left"=>Bone("hand_l","handslot.l","Palm.L","Hand.L"),
                    "back"=>Bone("spine_03","chest","Chest","Torso"),_=>Bone("head","Head")};
                int row=(int)reference*(Intervals+1)+(reference==ArtistMotion.Idle?0:Intervals/2);
                var skin=samples[row][b];var hand=skin.MultiplyPoint3x4(norm.MultiplyPoint3x4(BindPos(b)));
                if(port=="right"||port=="left")
                {
                    int finger=Bone(port=="right"?"middle_01_r":"middle_01_l");
                    var knuckle=samples[row][finger].MultiplyPoint3x4(norm.MultiplyPoint3x4(BindPos(finger)));
                    hand=Vector3.Lerp(hand,knuckle,.76f);
                }
                var item=ReadPart(model);var box=item.Bounds();
                int axis=box.size.x>box.size.y&&box.size.x>box.size.z?0:box.size.y>box.size.z?1:2;
                var along=axis==0?Vector3.right:axis==1?Vector3.up:Vector3.forward;
                Vector3 aim=alignment switch {"forward"=>Vector3.forward,"sword"=>new Vector3(.13f,.84f,.53f),
                    "shield"=>Vector3.up,"hammer"=>Vector3.up,_=>Vector3.up};
                float scale=length/Mathf.Max(.001f,box.size[axis]);
                var grip=box.center;
                if(alignment=="sword")grip-=along*box.size[axis]*.34f;
                if(alignment=="hammer")grip-=along*box.size[axis]*.25f;
                if(alignment=="forward"&&model=="Spear")grip-=along*box.size[axis]*.17f;
                Quaternion q=Quaternion.FromToRotation(along,aim);
                if(alignment=="shield")
                {
                    var thin=box.size.z<box.size.x?Vector3.forward:Vector3.right;
                    q=Quaternion.FromToRotation(thin,Vector3.forward);
                }
                if(port=="head"){q=Quaternion.identity;grip=new Vector3(box.center.x,box.min.y,box.center.z);}
                if(port=="back")q=Quaternion.identity;
                var fit=skin.inverse*Matrix4x4.TRS(hand+offset,q,Vector3.one*scale)*Matrix4x4.Translate(-grip);
                maxSocketError=Mathf.Max(maxSocketError,(skin.MultiplyPoint3x4(fit.MultiplyPoint3x4(grip))-hand-offset).magnitude);
                int start=into.p.Count;
                into.Add(item,fit,b,new Color(tint,tint,tint,1),project:true);
                if(model=="Mantle")
                {
                    into.flex??=new List<Vector2>();while(into.flex.Count<into.p.Count)into.flex.Add(Vector2.zero);
                    int hip=Bone("pelvis"),spine=Bone("spine_01");
                    for(int i=0;i<item.p.Count;i++)
                    {
                        float drop=1-Mathf.InverseLerp(box.min.y,box.max.y,item.p[i].y);
                        into.joints[start+i]=new Vector4(b,spine,hip,0);
                        into.weights[start+i]=new Vector4(1-.66f*drop,.42f*drop,.24f*drop,0);
                        into.flex[start+i]=new Vector2(drop*drop,0);
                    }
                }
            }
            public void SaddleBags(ArtData gear)
            {
                int bone=Bone("Back","Body");
                var neutral=samples[(int)ArtistMotion.Idle*(Intervals+1)][bone];
                var seat=cooked.saddle[0];
                var pannier=ReadPart("Pouch");
                var box=pannier.Bounds();
                for(int side=-1;side<=1;side+=2)
                {
                    var fit=Matrix4x4.TRS(seat+new Vector3(side*.26f,-.145f,-.29f),
                        Quaternion.Euler(-7,side*85,7*side),new Vector3(.27f/box.size.x,.31f/box.size.y,.19f/box.size.z));
                    gear.Add(pannier,neutral.inverse*fit*Matrix4x4.Translate(-box.center),bone,new Color(.54f,.40f,.29f),true);
                }
            }
            public ArtData RestPose(ArtData src,ArtistMotion take)
            {
                var result=new ArtData();result.Add(src,Matrix4x4.identity);
                var pose=samples[(int)take*(Intervals+1)];
                for(int i=0;i<result.p.Count;i++)
                {
                    var p=Vector3.zero;var n=Vector3.zero;
                    for(int j=0;j<4;j++)
                    {
                        float w=src.weights[i][j];if(w==0)continue;
                        var m=pose[(int)src.joints[i][j]];p+=w*m.MultiplyPoint3x4(src.p[i]);n+=w*m.MultiplyVector(src.n[i]);
                    }
                    result.p[i]=p;result.n[i]=n.normalized;
                }
                result.joints.Clear();result.weights.Clear();return result;
            }
            public void Dispose()=>Object.DestroyImmediate(root);
        }

        static ArtData Cluster(ArtData src,float cell,bool skin)
        {
            var g=new ArtData();var bins=new Dictionary<(Vector3Int,int,int),int>();var counts=new List<int>();
            int[] replace=new int[src.p.Count];
            for(int i=0;i<replace.Length;i++)
            {
                var key=(Vector3Int.RoundToInt(src.p[i]/cell),(int)src.surface[i].x,skin?(int)src.joints[i].x:0);
                if(!bins.TryGetValue(key,out var dst))
                {
                    dst=g.p.Count;bins.Add(key,dst);g.p.Add(Vector3.zero);g.n.Add(Vector3.zero);counts.Add(0);
                    g.uv.Add(src.uv[i]);g.color.Add(src.color[i]);g.surface.Add(src.surface[i]);
                    if(skin)
                    {
                        var joints=src.joints[i];var w=src.weights[i];float sum=Mathf.Max(.00001f,w.x+w.y);
                        g.joints.Add(new Vector4(joints.x,joints.y,0,0));g.weights.Add(new Vector4(w.x/sum,w.y/sum,0,0));
                    }
                }
                if(src.flex!=null){g.flex??=new List<Vector2>();if(g.flex.Count==dst)g.flex.Add(src.flex[i]);}
                replace[i]=dst;g.p[dst]+=src.p[i];g.n[dst]+=src.n[i];counts[dst]++;
            }
            for(int i=0;i<g.p.Count;i++){g.p[i]/=counts[i];g.n[i]=g.n[i].sqrMagnitude>1e-9f?g.n[i].normalized:Vector3.up;}
            var faces=new HashSet<(int,int,int)>();
            for(int i=0;i<src.idx.Count;i+=3)
            {
                int a=replace[src.idx[i]],b=replace[src.idx[i+1]],c=replace[src.idx[i+2]];
                if(a==b||b==c||c==a||!faces.Add((a,b,c)))continue;
                g.idx.Add(a);g.idx.Add(b);g.idx.Add(c);
            }
            return g;
        }
        internal static string Fingerprint(Mesh piece)
        {
            if(!piece)return "";
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream))
            using(var digest=System.Security.Cryptography.SHA256.Create())
            {
                foreach(var p in piece.vertices){writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}
                foreach(var i in piece.triangles)writer.Write(i);
                writer.Flush();stream.Position=0;
                return BitConverter.ToString(digest.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            }
        }

        internal static void BakeSkins()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));AssetDatabase.Refresh();
            var roster=AssetDatabase.LoadAssetAtPath<ArtistRoster>(LibraryPath);
            if(!roster){roster=ScriptableObject.CreateInstance<ArtistRoster>();AssetDatabase.CreateAsset(roster,LibraryPath);}
            stored=AssetDatabase.LoadAllAssetsAtPath(LibraryPath).Where(a=>a!=roster&&a).ToDictionary(a=>a.name);
            var drafts=new List<RigBake>();var costumes=new List<ArtistCostume>();
            var sources=new[]{"Castellan","Forester","Falconer","Yeoman","Goodwife","Horse"};
            try
            {
                foreach(string name in sources)
                {
                    var rig=new RigBake(name);drafts.Add(rig);
                    var main=rig.body.Mesh(name);var mid=Cluster(rig.body,.089f,true).Mesh(name);
                    main.bounds=mid.bounds=new Bounds(new Vector3(0,.90f,0),new Vector3(4,4,5));
                    rig.cooked.body=Store(main,name+"_source");rig.cooked.mid=Store(mid,name+"_source_mid");
                    rig.cooked.proxy=Store(Cluster(rig.RestPose(rig.body,ArtistMotion.Idle),.125f,false).Mesh(name),name+"_source_proxy");
                }
                void Dress(string key,string rig,string tool=null,string left=null,int tier=0)
                {
                    int idx=Array.IndexOf(sources,rig);var bake=drafts[idx];var art=new ArtData();
                    bool hero=key.StartsWith("hero_",StringComparison.Ordinal);
                    if(key=="charger")bake.SaddleBags(art);
                    if(hero||key=="watch"||key=="cavalry")bake.Accessory(art,"Mantle","back",hero?.94f:.78f,"clasp",ArtistMotion.Idle,new Vector3(0,-.12f,-.16f));
                    if(hero&&tier>=2)bake.Accessory(art,"Crown","head",.33f,"hat",ArtistMotion.Idle,new Vector3(0,.112f,0));
                    if(tool!=null)
                    {
                        bool bow=tool.StartsWith("Bow_",StringComparison.Ordinal),spear=tool=="Spear",cross=tool.StartsWith("crossbow"),hammer=tool.Contains("Hammer"),sword=tool.Contains("sword");
                        string alignment=bow?"vertical":spear||cross?"forward":sword?"sword":hammer?"hammer":"vertical";
                        float length=bow?1.20f:spear?2.15f:cross?.84f:hammer?(tool=="Hammer_Small"?.51f:1.10f):sword?.94f:tool=="staff"?1.46f:tool=="Lute"?.79f:tool=="smokebomb"?.25f:tool=="Shovel"?.87f:tool=="axe_2handed"?.86f:.40f;
                        var take=bow||cross?ArtistMotion.Bow:spear&&key=="cavalry"?ArtistMotion.Ride:spear&&key!="watch"?ArtistMotion.Thrust:ArtistMotion.Idle;
                        if(spear&&key=="watch")alignment="vertical";
                        bake.Accessory(art,tool,bow?"left":"right",length,alignment,take);
                    }
                    if(left!=null)bake.Accessory(art,left,"left",left.StartsWith("shield")?.69f:.34f,
                        left.StartsWith("shield")?"shield":"vertical",ArtistMotion.Idle,new Vector3(0,0,.035f));
                    if(key.Contains("archer")||key.Contains("ranger")||key=="crossbow")bake.Accessory(art,"quiver","back",.65f,"clasp",ArtistMotion.Idle,new Vector3(0,-.11f,-.20f));
                    if(hero&&tier==3)bake.Accessory(art,key=="hero_commander"?"Banner":"Pouch","back",key=="hero_commander"?.65f:.23f,"clasp",ArtistMotion.Idle,new Vector3(-.12f,key=="hero_commander"?.36f:-.24f,-.20f));
                    if(hero&&tier==1)bake.Accessory(art,"Pouch","back",.24f,"clasp",ArtistMotion.Idle,new Vector3(.16f,-.25f,0));
                    if(art.p.Count==0)
                    {
                        costumes.Add(new ArtistCostume{name=key,skeleton=idx});return;
                    }
                    var mid=Cluster(art,.058f,true);var rest=bake.RestPose(art,key=="cavalry"?ArtistMotion.Ride:ArtistMotion.Idle);
                    var proxy=Cluster(rest,rig=="Horse"?.19f:.125f,false);
                    var hi=art.Mesh(key);var med=mid.Mesh(key);var lo=proxy.Mesh(key);
                    hi.bounds=med.bounds=new Bounds(new Vector3(0,.87f,0),new Vector3(4.5f,3.9f,5));
                    costumes.Add(new ArtistCostume{name=key,skeleton=idx,high=Store(hi,key+"_skin"),
                        mid=Store(med,key+"_mid"),proxy=Store(lo,key+"_proxy")});
                    if(key=="watch")
                    {
                        foreach(bool low in new[]{false,true})
                        {
                            var combined=new ArtData();combined.Add(bake.body,Matrix4x4.identity);combined.Add(art,Matrix4x4.identity);
                            var still=bake.RestPose(combined,ArtistMotion.Idle);if(low)still=Cluster(still,.11f,false);
                            var bb=still.Bounds();float radius=still.p.Max(p=>Mathf.Sqrt(p.x*p.x+p.z*p.z));
                            float k=Mathf.Min(1,(ManorPlotArt.RetinuePostRadius-.018f)/radius);
                            for(int i=0;i<still.p.Count;i++){var p=still.p[i];still.p[i]=new Vector3(p.x*k,p.y-bb.min.y,p.z*k);}
                            string n="retinue_guard"+(low?"_lod1":"");WriteMesh(n,still.Mesh(n));Exported.Add("Models/"+n);
                        }
                    }
                }
                Dress("militia","Yeoman","sword_1handed","shield_badge");Dress("shield","Castellan","sword_1handed","shield_square_color");
                Dress("spear","Castellan","Spear");Dress("archer","Forester","Bow_Wooden");Dress("crossbow","Falconer","crossbow_2handed");
                Dress("giant","Castellan","Hammer_Double");Dress("bomber","Forester","smokebomb");Dress("medic","Goodwife","staff","spellbook_open");
                Dress("cavalry","Castellan","Spear","shield_badge");Dress("charger","Horse");Dress("watch","Castellan","Spear","shield_badge");
                for(int t=0;t<=3;t++)
                {
                    string suffix=t==0?"":"_t"+t;
                    Dress("hero_vanguard"+suffix,"Castellan",t>=2?"Hammer_Double_Golden":"Hammer_Double",null,t);
                    Dress("hero_ranger"+suffix,"Falconer",t==3?"crossbow_2handed":t>0?"Bow_Golden":"Bow_Wooden",null,t);
                    Dress("hero_commander"+suffix,"Forester","staff","spellbook_open",t);
                }
                Dress("villager","Goodwife");Dress("farmer","Yeoman","Shovel");Dress("carrier","Yeoman","bucket_water");
                Dress("woodcutter","Yeoman","axe_2handed");Dress("smith","Yeoman","Hammer_Small");Dress("steward","Forester","Parchment");Dress("merchant","Yeoman","Pouch");
                Dress("herbalist","Goodwife","spellbook_open");Dress("watchman","Castellan","Spear");
                Dress("mason","Yeoman","Hammer_Small","Parchment");Dress("minstrel","Forester","Lute");
                roster.skeletons=drafts.Select(x=>x.cooked).ToArray();roster.costumes=costumes.ToArray();
                var live=new HashSet<Object>();
                foreach(var rig in roster.skeletons){live.Add(rig.body);live.Add(rig.mid);live.Add(rig.proxy);live.Add(rig.palette);}
                foreach(var piece in roster.costumes){live.Add(piece.high);live.Add(piece.mid);live.Add(piece.proxy);}
                foreach(var member in stored.Values)if(member&&!live.Contains(member))Object.DestroyImmediate(member,true);
                EditorUtility.SetDirty(roster);AssetDatabase.SaveAssets();
                string reports=Path.Combine(Path.GetDirectoryName(Application.dataPath),"../Builds/Reports",PlayerSettings.bundleVersion);
                Directory.CreateDirectory(reports);
                File.WriteAllText(Path.Combine(reports,"artist-bake.json"),Json.Write(new {version=PlayerSettings.bundleVersion,
                    rigs=drafts.Select(a=>new {a.cooked.name,a.cooked.boneCount,a.weightedVertices,a.comparedVertices,a.maxSkinError,a.maxSocketError,
                        sampledRows=a.cooked.palette.height,halfFloat=true,takes=Array.ConvertAll(a.cooked.takes,c=>c.source)}).ToArray(),
                    wardrobe=costumes.Count,sources=UsedSources.ToArray()}));
                if(drafts.Any(a=>a.maxSocketError>.0002f))throw new InvalidDataException("Hand socket alignment drifted at authoring time");
                Debug.Log("CC0_ROSTER_OK skeletons="+drafts.Count+" costumes="+costumes.Count);
            }
            finally{foreach(var r in drafts)r.Dispose();stored=null;}
        }
    }
}
