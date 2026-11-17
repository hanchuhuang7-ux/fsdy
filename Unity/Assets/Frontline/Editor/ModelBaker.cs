using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontline.Editor
{
    // Editor-only, deterministic vertex-colour models. No textures or runtime mesh construction.
    public static class ModelBaker
    {
        const string Folder="Assets/Frontline/Models/Resources/Models";
        static readonly Color Cloth=new Color(.93f,.94f,.9f,1),Skin=new Color(.86f,.64f,.43f,0),Steel=new Color(.49f,.59f,.64f,0),LightSteel=new Color(.73f,.8f,.8f,0),Dark=new Color(.10f,.14f,.17f,0),Wood=new Color(.37f,.22f,.12f,0),WoodLight=new Color(.57f,.36f,.18f,0),Gold=new Color(.91f,.64f,.20f,0),Rock=new Color(.47f,.51f,.49f,0),Leaf=new Color(.27f,.43f,.23f,0),Snow=new Color(.88f,.95f,.96f,0),Red=new Color(.8f,.16f,.1f,0);
        static readonly string[] Types={"militia","shield","spear","archer","crossbow","bomber","giant","cavalry","medic","catapult"};
        static readonly List<string> Written=new List<string>();
        [MenuItem("Frontline/Bake low polygon models")]
        public static void Bake()
        {
            Directory.CreateDirectory(Folder);Written.Clear();
            for(int lod=0;lod<2;lod++)
            {
                foreach(string type in Types){Save("body_"+type,lod,Body(type,lod));Save("arm_right_"+type,lod,Arm(type,false,lod));}
                Save("arm_left",lod,Arm("militia",true,lod));Save("leg",lod,Leg(lod));Save("horse_leg",lod,HorseLeg(lod));Save("horse",lod,Horse(lod));
                Save("catapult",lod,Catapult(lod));Save("catapult_arm",lod,CatapultArm(lod));Save("wreck_horse",lod,WreckHorse(lod));Save("wreck_catapult",lod,WreckCatapult(lod));Save("shield",lod,Shield(lod));
                foreach(string name in new[]{"barrel","barricade","banner","spikes","spring"})Save("prop_"+name,lod,Prop(name,lod));
                foreach(string name in new[]{"rock","wall","platform","grass","flowers","shrub","tree","pine","dead_tree","reeds","column","arch","brazier","icicle","water_ripple","ring","dot","arrow","bolt","bomb","stone","torso","head","limb","cliff","base","flame","snow","island_root","mist"})Save(name,lod,EnvironmentMesh(name,lod));
            }
            AssetDatabase.SaveAssets();File.WriteAllText("Assets/Frontline/Models/model-manifest.json",JsonUtility.ToJson(new Manifest{generator="Frontline.Editor.ModelBaker",version="0.7.0",vertexColor="rgb=albedo; alpha=faction multiply weight",resources=Written.ToArray()},true));AssetDatabase.Refresh();Debug.Log("FRONTLINE_MODELS_OK "+Written.Count+" prebaked meshes, two LODs, vertex colours and flat normals");
        }
        [Serializable] sealed class Manifest{public string generator,version,vertexColor;public string[] resources;}
        static void Save(string name,int lod,Geometry g)
        {
            string key=name+(lod==1?"_lod1":""),path=Folder+"/"+key+".asset";var mesh=g.Build(key);if(mesh.vertexCount==0||mesh.normals.Length!=mesh.vertexCount||mesh.colors.Length!=mesh.vertexCount)throw new InvalidDataException("Incomplete baked mesh "+key);foreach(var v in mesh.vertices)if(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z))throw new InvalidDataException("Non-finite baked vertex "+key);foreach(var n in mesh.normals)if(n.sqrMagnitude<.98f)throw new InvalidDataException("Invalid baked normal "+key);var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(mesh,path);Written.Add("Models/"+key);
        }
        static Vector3 V(float x,float y,float z){return new Vector3(x,y,z);}
        static Geometry Body(string type,int lod)
        {
            var g=new Geometry();g.Bevel(V(0,0,0),V(.48f,.64f,.34f),.075f,Cloth);g.Cylinder(V(0,.34f,0),V(0,.42f,0),.09f,Skin,6);g.Ellipsoid(V(0,.53f,.015f),V(.185f,.21f,.175f),Skin,lod==0?8:6,3);
            // A projecting nose and shadowed visor make faces readable without a texture.
            g.Bevel(V(0,.53f,.19f),V(.065f,.065f,.075f),.015f,Skin);g.Bevel(V(0,.585f,.16f),V(.27f,.045f,.02f),.01f,Dark);
            if(type=="militia"){g.Cone(V(0,.7f,0),.25f,.15f,WoodLight,8);g.Cylinder(V(0,.64f,0),V(0,.67f,0),.25f,Wood,8);}
            else if(type=="archer"||type=="medic"){g.Ellipsoid(V(0,.66f,-.015f),V(.215f,.16f,.2f),Cloth,8,2);g.Bevel(V(0,.49f,-.14f),V(.42f,.31f,.12f),.04f,Cloth);}
            else{g.Ellipsoid(V(0,.7f,0),V(.23f,.19f,.22f),Steel,lod==0?10:6,2);g.Bevel(V(0,.64f,.21f),V(.48f,.065f,.05f),.025f,LightSteel);g.Bevel(V(-.19f,.5f,-.015f),V(.09f,.21f,.23f),.03f,Steel);g.Bevel(V(.19f,.5f,-.015f),V(.09f,.21f,.23f),.03f,Steel);}
            if(type=="shield"||type=="spear"||type=="giant"||type=="cavalry"){g.Bevel(V(0,.045f,.18f),V(.39f,.41f,.065f),.055f,Steel);g.Bevel(V(-.29f,.25f,0),V(.24f,.19f,.4f),.065f,LightSteel);g.Bevel(V(.29f,.25f,0),V(.24f,.19f,.4f),.065f,LightSteel);g.Bevel(V(0,-.27f,.04f),V(.53f,.12f,.39f),.04f,Dark);}
            if(type=="spear"||type=="cavalry"||type=="giant"){g.Bevel(V(0,.91f,-.025f),V(.08f,.23f,.23f),.03f,Cloth);g.Quad(V(-.23f,.28f,-.21f),V(.23f,.28f,-.21f),V(.34f,-.49f,-.34f),V(-.34f,-.49f,-.34f),Cloth,true);}
            if(type=="archer"||type=="crossbow"){g.Cylinder(V(-.16f,-.23f,-.26f),V(-.08f,.34f,-.22f),.105f,Wood,7);if(lod==0)for(int i=0;i<4;i++)g.Cylinder(V(-.16f+i*.045f,.27f,-.27f),V(-.13f+i*.045f,.5f,-.29f),.012f,WoodLight,4);}
            if(type=="bomber"){g.Bevel(V(0,.04f,-.28f),V(.39f,.42f,.19f),.045f,Wood);for(int i=-1;i<=1;i++)g.Cylinder(V(i*.105f,-.1f,-.4f),V(i*.105f,.17f,-.4f),.043f,Red,6);g.Cylinder(V(-.18f,-.22f,0),V(.18f,.26f,.19f),.035f,Dark,4);}
            if(type=="medic"){g.Bevel(V(-.32f,-.12f,-.04f),V(.25f,.35f,.28f),.035f,new Color(.85f,.84f,.72f,0));g.Bevel(V(-.32f,-.12f,.11f),V(.055f,.22f,.018f),.005f,Red);g.Bevel(V(-.32f,-.12f,.12f),V(.17f,.055f,.018f),.005f,Red);g.Quad(V(-.19f,.25f,.19f),V(.19f,.25f,.19f),V(.19f,-.32f,.21f),V(-.19f,-.32f,.21f),new Color(.85f,.87f,.77f,0),true);}
            g.Bevel(V(0,-.19f,.18f),V(.5f,.07f,.06f),.012f,Wood);g.Bevel(V(0,-.19f,.22f),V(.09f,.08f,.03f),.012f,Gold);return g;
        }
        static Geometry Leg(int lod){var g=new Geometry();g.Bevel(V(0,0,0),V(.2f,.42f,.22f),.04f,Dark);g.Bevel(V(0,-.2f,.065f),V(.23f,.16f,.36f),.035f,Wood);g.Bevel(V(0,.1f,.115f),V(.21f,.13f,.04f),.025f,Steel);return g;}
        static Geometry Arm(string type,bool left,int lod)
        {
            var g=new Geometry();float x=left?-.36f:.36f;g.Bevel(V(x,.06f,.025f),V(.17f,.39f,.19f),.045f,Cloth);g.Bevel(V(x,-.14f,.08f),V(.15f,.22f,.17f),.035f,Skin);g.Bevel(V(x,-.05f,.1f),V(.18f,.11f,.2f),.025f,Wood);if(left)return g;
            if(type=="spear"||type=="cavalry"){g.Cylinder(V(x,.04f,-.35f),V(x,.04f,1.55f),.025f,WoodLight,6);g.Spear(V(x,.04f,1.48f),V(x,.04f,1.9f),.11f,LightSteel);if(lod==0)g.Cylinder(V(x,.04f,1.4f),V(x,.04f,1.52f),.04f,Dark,6);}
            else if(type=="archer"){for(int i=0;i<6;i++){float a=-1.2f+i*.4f,b=a+.4f;g.Cylinder(V(x,Mathf.Sin(a)*.48f+.1f,.36f+Mathf.Cos(a)*.23f),V(x,Mathf.Sin(b)*.48f+.1f,.36f+Mathf.Cos(b)*.23f),.027f,WoodLight,5);}g.Cylinder(V(x,-.35f,.44f),V(x,.55f,.44f),.006f,Snow,3);g.Cylinder(V(x,.1f,.05f),V(x,.1f,.9f),.012f,Wood,4);}
            else if(type=="crossbow"){g.Bevel(V(x,.04f,.41f),V(.13f,.16f,.7f),.025f,WoodLight);g.Cylinder(V(x-.4f,.1f,.61f),V(x,.1f,.73f),.035f,Steel,5);g.Cylinder(V(x,.1f,.73f),V(x+.4f,.1f,.61f),.035f,Steel,5);g.Cylinder(V(x-.4f,.1f,.61f),V(x,.1f,.22f),.006f,Snow,3);g.Cylinder(V(x,.1f,.22f),V(x+.4f,.1f,.61f),.006f,Snow,3);g.Bevel(V(x,-.08f,.12f),V(.1f,.25f,.14f),.02f,Wood);}
            else if(type=="bomber"){g.Ellipsoid(V(x,.03f,.38f),V(.17f,.18f,.17f),Dark,8,3);g.Cylinder(V(x,.2f,.37f),V(x+.055f,.33f,.38f),.021f,WoodLight,5);g.Ellipsoid(V(x+.06f,.33f,.38f),V(.035f,.05f,.035f),Gold,5,2);}
            else if(type=="giant"){g.Cylinder(V(x,-.44f,.36f),V(x,.68f,.36f),.06f,WoodLight,7);g.Bevel(V(x,.62f,.36f),V(.63f,.35f,.38f),.07f,Rock);g.Bevel(V(x-.19f,.62f,.36f),V(.065f,.4f,.42f),.018f,Steel);g.Bevel(V(x+.19f,.62f,.36f),V(.065f,.4f,.42f),.018f,Steel);}
            else if(type=="medic"){g.Cylinder(V(x,-.45f,.28f),V(x,.65f,.28f),.04f,WoodLight,6);g.Torus(V(x,.66f,.28f),.13f,.035f,new Color(.4f,.8f,.47f,0),10);}
            else{g.Bevel(V(x,.1f,.38f),V(.045f,.07f,.75f),.012f,LightSteel);g.Bevel(V(x,.1f,.04f),V(.26f,.055f,.055f),.012f,Gold);g.Cylinder(V(x,.1f,-.16f),V(x,.1f,.05f),.035f,Wood,6);}
            return g;
        }
        static Geometry Shield(int lod){var g=new Geometry();g.Bevel(V(-.39f,.015f,.36f),V(.55f,.79f,.13f),.11f,Steel);g.Bevel(V(-.39f,.015f,.435f),V(.44f,.66f,.025f),.07f,Cloth);g.Ellipsoid(V(-.39f,.03f,.47f),V(.11f,.11f,.075f),LightSteel,6,2);if(lod==0)foreach(float x in new[]{-.58f,-.2f})foreach(float y in new[]{-.24f,.28f})g.Ellipsoid(V(x,y,.46f),V(.025f,.025f,.02f),Gold,5,2);return g;}
        static Geometry Horse(int lod)
        {
            var g=new Geometry();g.Ellipsoid(V(0,-.54f,-.05f),V(.36f,.39f,.73f),Wood,lod==0?10:6,3);g.Cylinder(V(0,-.49f,.4f),V(0,.03f,.68f),.22f,Wood,7);g.Ellipsoid(V(0,.11f,.81f),V(.2f,.26f,.34f),WoodLight,8,3);g.Bevel(V(0,-.01f,1.03f),V(.26f,.2f,.3f),.07f,Wood);g.Cone(V(-.11f,.43f,.7f),.075f,.22f,Wood,5);g.Cone(V(.11f,.43f,.7f),.075f,.22f,Wood,5);g.Bevel(V(-.18f,.16f,.88f),V(.025f,.06f,.065f),.01f,Dark);g.Bevel(V(.18f,.16f,.88f),V(.025f,.06f,.065f),.01f,Dark);g.Cylinder(V(0,-.33f,-.7f),V(0,-.9f,-.99f),.085f,Dark,6);g.Bevel(V(0,-.24f,-.06f),V(.62f,.12f,.61f),.06f,Cloth);g.Cylinder(V(-.18f,-.02f,.85f),V(-.2f,-.14f,.1f),.012f,Dark,3);g.Cylinder(V(.18f,-.02f,.85f),V(.2f,-.14f,.1f),.012f,Dark,3);for(int i=0;i<5;i++)g.Bevel(V(0,-.2f+i*.08f,.47f+i*.055f),V(.11f,.18f,.16f),.035f,Dark);return g;
        }
        static Geometry HorseLeg(int lod){var g=new Geometry();g.Cylinder(V(0,.12f,0),V(0,-.35f,.025f),.085f,Wood,6);g.Cylinder(V(0,-.35f,.025f),V(0,-.63f,.065f),.055f,WoodLight,5);g.Bevel(V(0,-.64f,.09f),V(.16f,.13f,.22f),.035f,Dark);return g;}
        static Geometry Catapult(int lod)
        {
            var g=new Geometry();foreach(float x in new[]{-.43f,.43f})g.Bevel(V(x,-.48f,0),V(.18f,.23f,1.65f),.035f,Wood);foreach(float z in new[]{-.59f,.59f})g.Bevel(V(0,-.47f,z),V(1.25f,.18f,.17f),.03f,WoodLight);foreach(float x in new[]{-.56f,.56f})foreach(float z in new[]{-.55f,.55f}){var wheel=new Geometry();wheel.Torus(Vector3.zero,.29f,.06f,Wood,lod==0?12:8);for(int i=0;i<(lod==0?8:4);i++){float a=i*Mathf.PI/(lod==0?4:2);wheel.Cylinder(Vector3.zero,V(Mathf.Sin(a)*.27f,0,Mathf.Cos(a)*.27f),.025f,WoodLight,4);}wheel.Cylinder(V(0,-.06f,0),V(0,.06f,0),.07f,Steel,6);g.Append(wheel,Matrix4x4.TRS(V(x,-.57f,z),Quaternion.Euler(0,0,90),Vector3.one));}foreach(float x in new[]{-.38f,.38f}){g.Cylinder(V(x,-.4f,-.43f),V(x,.61f,0),.065f,WoodLight,6);g.Cylinder(V(x,-.4f,.53f),V(x,.61f,0),.065f,WoodLight,6);}g.Cylinder(V(-.49f,.59f,0),V(.49f,.59f,0),.08f,Steel,7);g.Bevel(V(0,-.29f,0),V(.54f,.12f,1.03f),.03f,Wood);return g;
        }
        static Geometry CatapultArm(int lod){var g=new Geometry();g.Bevel(V(0,.0f,-.35f),V(.14f,.17f,1.83f),.035f,WoodLight);g.Bevel(V(0,0,.55f),V(.55f,.42f,.43f),.045f,Wood);g.Bevel(V(0,-.23f,.55f),V(.57f,.07f,.45f),.01f,Steel);g.Ellipsoid(V(0,.08f,-1.2f),V(.23f,.12f,.28f),Wood,8,2);g.Cylinder(V(0,0,-.9f),V(0,.08f,-1.3f),.016f,Dark,3);return g;}
        static Geometry WreckHorse(int lod){var g=new Geometry();g.Append(Horse(lod),Matrix4x4.TRS(V(0,.12f,0),Quaternion.Euler(0,0,82),Vector3.one));for(int i=0;i<4;i++)g.Append(HorseLeg(lod),Matrix4x4.TRS(V(i%2==0?-.35f:.35f,.12f,i<2?-.45f:.4f),Quaternion.Euler(0,0,i%2==0?65:110),Vector3.one));return g;}
        static Geometry WreckCatapult(int lod){var g=new Geometry();g.Append(Catapult(lod),Matrix4x4.TRS(V(0,.42f,0),Quaternion.Euler(13,17,54),Vector3.one));g.Append(CatapultArm(lod),Matrix4x4.TRS(V(.6f,.08f,.3f),Quaternion.Euler(0,65,0),Vector3.one));g.Bevel(V(-.6f,.08f,.5f),V(.14f,.14f,.85f),.025f,WoodLight,Quaternion.Euler(0,-42,0));return g;}
        static Geometry Prop(string name,int lod)
        {
            var g=new Geometry();int n=lod==0?10:6;
            if(name=="barrel"){g.Barrel(V(0,.55f,0),.43f,1.1f,WoodLight,n);for(int j=0;j<3;j++)g.Cylinder(V(0,.16f+j*.38f,0),V(0,.23f+j*.38f,0),j==1?.455f:.405f,Steel,n);g.Cylinder(V(0,1.08f,0),V(0,1.12f,0),.37f,Wood,n);g.Cylinder(V(.12f,1.1f,.05f),V(.12f,1.15f,.05f),.065f,Dark,6);}
            else if(name=="barricade"){for(int i=-1;i<=1;i++){float z=i*.78f;g.Cylinder(V(-.63f,.05f,z),V(.54f,1.24f,z),.095f,WoodLight,6);g.Cylinder(V(.63f,.05f,z),V(-.54f,1.24f,z),.095f,WoodLight,6);g.Spear(V(-.5f,1.18f,z),V(-.73f,1.46f,z),.095f,WoodLight);g.Spear(V(.5f,1.18f,z),V(.73f,1.46f,z),.095f,WoodLight);}g.Cylinder(V(0,.7f,-1.25f),V(0,.7f,1.25f),.095f,Wood,6);}
            else if(name=="banner"){g.Cylinder(Vector3.zero,V(0,2.65f,0),.04f,WoodLight,7);g.Spear(V(0,2.63f,0),V(0,2.88f,0),.07f,Gold);int segments=lod==0?6:3;for(int i=0;i<segments;i++){float a=i/(float)segments,b=(i+1)/(float)segments;g.Quad(V(a,2.5f,Mathf.Sin(a*5)*.05f),V(b,2.5f,Mathf.Sin(b*5)*.05f),V(b,1.75f+(i%2)*.08f,Mathf.Sin(b*5)*.05f),V(a,1.75f,Mathf.Sin(a*5)*.05f),Cloth,true);}g.Bevel(V(0,.06f,0),V(.55f,.12f,.55f),.08f,Rock);}
            else if(name=="spikes"){g.Bevel(V(0,.07f,0),V(1.7f,.14f,1.7f),.06f,Wood);for(int i=-1;i<=1;i++)for(int j=-1;j<=1;j++){g.Cylinder(V(i*.5f,.1f,j*.5f),V(i*.5f,.34f,j*.5f),.04f,WoodLight,5);g.Spear(V(i*.5f,.34f,j*.5f),V(i*.5f+.035f,.63f,j*.5f),.065f,Steel);}}
            else{g.Bevel(V(0,.1f,0),V(1.7f,.2f,1.7f),.09f,Wood);for(int j=-1;j<=1;j+=2)for(int k=-1;k<=1;k+=2){Vector3 last=V(j*.5f,.2f,k*.5f);int segments=lod==0?24:12;for(int i=1;i<=segments;i++){float a=i*Mathf.PI*6/segments;Vector3 next=V(j*.5f+Mathf.Sin(a)*.12f,.2f+i*.38f/segments,k*.5f+Mathf.Cos(a)*.12f);g.Cylinder(last,next,.02f,Steel,4);last=next;}}g.Bevel(V(0,.64f,0),V(1.65f,.15f,1.65f),.07f,Cloth);g.Bevel(V(0,.72f,0),V(.75f,.015f,.13f),.015f,Gold);}
            return g;
        }
        static Geometry EnvironmentMesh(string name,int lod)
        {
            var g=new Geometry();int n=lod==0?10:6;
            if(name=="ring"||name=="water_ripple")g.Torus(Vector3.zero,1,name=="ring"?.022f:.014f,Cloth,lod==0?40:24);
            else if(name=="dot")g.Cylinder(V(0,-.01f,0),V(0,.01f,0),.5f,Cloth,6);
            else if(name=="mist"){for(int i=0;i<n*2;i++){float a=i*Mathf.PI/n,b=(i+1)*Mathf.PI/n;g.Quad(Vector3.zero,V(Mathf.Sin(a)*.5f,0,Mathf.Cos(a)*.5f),V(Mathf.Sin(b)*.5f,0,Mathf.Cos(b)*.5f),Vector3.zero,Snow,true);}}
            else if(name=="island_root"){g.Frustum(V(0,-.5f,0),V(.18f,0,.18f),V(0,.5f,0),V(1,0,1),Rock,n);g.Frustum(V(0,.2f,0),V(.72f,0,.72f),V(0,.27f,0),V(.78f,0,.78f),new Color(.3f,.34f,.32f,0),n);}
            else if(name=="platform"){g.Bevel(Vector3.zero,Vector3.one,.035f,Cloth);}
            else if(name=="base"||name=="cliff"){g.Frustum(V(0,-.5f,0),V(1,0,1),V(0,.5f,0),V(.88f,0,.92f),Rock,8);for(int j=0;j<3;j++)g.Frustum(V(0,-.48f+j*.3f,0),V(.96f-j*.035f,0,.97f-j*.035f),V(0,-.39f+j*.3f,0),V(.97f-j*.035f,0,.98f-j*.035f),new Color(.32f+j*.035f,.36f+j*.035f,.34f+j*.035f,0),8);}
            else if(name=="rock"||name=="stone")g.Rock(Vector3.zero,.5f,Rock,n,4);
            else if(name=="wall"){g.Bevel(V(0,-.06f,0),V(1,.88f,1),.07f,Rock);for(int i=0;i<3;i++)g.Bevel(V(-.36f+i*.36f,.39f,0),V(.24f,.22f,1),.025f,new Color(.55f,.57f,.52f,0));if(lod==0){g.Cylinder(V(-.22f,.18f,.505f),V(-.03f,-.06f,.505f),.012f,Dark,3);g.Cylinder(V(-.03f,-.06f,.505f),V(.04f,-.33f,.505f),.012f,Dark,3);g.Cylinder(V(-.03f,-.06f,.505f),V(.21f,-.15f,.505f),.009f,Dark,3);}}
            else if(name=="grass"||name=="reeds"){int blades=lod==0?7:3;for(int i=0;i<blades;i++){float a=i*2.4f,h=name=="reeds"?.85f+i*.1f:.26f+i*.04f,x=Mathf.Sin(a)*.21f,z=Mathf.Cos(a)*.21f;g.Quad(V(x-.025f,0,z),V(x+.025f,0,z),V(x+.09f,h*.58f,z+.025f),V(x+.06f,h,z),Leaf,true);if(name=="reeds"){g.Cylinder(V(x,h*.7f,z),V(x+.06f,h+.12f,z),.014f,Leaf,4);g.Cylinder(V(x+.06f,h,z),V(x+.06f,h+.22f,z),.035f,Wood,5);}}}
            else if(name=="flowers"){int flowers=lod==0?5:2;for(int i=0;i<flowers;i++){float a=i*2.4f,x=Mathf.Sin(a)*.3f,z=Mathf.Cos(a)*.3f,h=.28f+i*.035f;g.Cylinder(V(x,0,z),V(x,h,z),.012f,Leaf,3);g.Cylinder(V(x,h-.015f,z),V(x,h+.015f,z),.095f,i%2==0?new Color(.96f,.8f,.34f,0):new Color(.84f,.62f,.76f,0),5);g.Cylinder(V(x,h+.016f,z),V(x,h+.025f,z),.03f,Gold,5);}}
            else if(name=="shrub"){g.Cylinder(Vector3.zero,V(0,.4f,0),.08f,Wood,5);for(int i=0;i<(lod==0?5:3);i++){float a=i*2.4f;g.Rock(V(Mathf.Sin(a)*.3f,.43f+(i%2)*.15f,Mathf.Cos(a)*.3f),.4f,Leaf,n,3);}}
            else if(name=="tree"||name=="pine"||name=="dead_tree"){g.Cylinder(Vector3.zero,V(.06f,2.65f,.01f),.12f,Wood,7);for(int j=0;j<3;j++){float a=j*2.1f;Vector3 branch=V(Mathf.Sin(a)*.8f,1.65f+j*.32f,Mathf.Cos(a)*.8f);g.Cylinder(V(0,1.15f+j*.35f,0),branch,.055f,Wood,5);if(name=="tree")g.Rock(branch+Vector3.up*.35f,.8f,Leaf,n,3);else if(name=="dead_tree")g.Cylinder(branch,branch+V(Mathf.Cos(a)*.3f,.4f,Mathf.Sin(a)*.3f),.025f,WoodLight,4);}if(name=="tree")g.Rock(V(0,2.75f,0),1.05f,Leaf,n,3);if(name=="pine"){for(int j=0;j<4;j++){float r=.96f-j*.2f,y=.95f+j*.6f;g.Cone(V(0,y,0),r,1.25f,Leaf,n);g.Cone(V(0,y+.35f,0),r*.8f,1.04f,Snow,n);}}}
            else if(name=="column"){g.Bevel(V(0,.12f,0),V(1,.24f,1),.07f,Rock);g.Cylinder(V(0,.24f,0),V(.03f,1.95f,0),.31f,Rock,n);g.Bevel(V(0,1.96f,0),V(.83f,.22f,.83f),.04f,Rock);g.Bevel(V(0,.3f,0),V(.78f,.13f,.78f),.025f,LightSteel);}
            else if(name=="arch"){foreach(float x in new[]{-.85f,.85f}){g.Bevel(V(x,.7f,0),V(.45f,1.4f,.55f),.06f,Rock);g.Bevel(V(x,.12f,0),V(.68f,.24f,.74f),.05f,Rock);}int parts=lod==0?7:5;for(int i=0;i<parts;i++){float a=i*Mathf.PI/parts,b=(i+1)*Mathf.PI/parts;g.Cylinder(V(Mathf.Cos(a)*.85f,1.35f+Mathf.Sin(a)*.65f,0),V(Mathf.Cos(b)*.85f,1.35f+Mathf.Sin(b)*.65f,0),.26f,Rock,5);}}
            else if(name=="brazier"){for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;g.Cylinder(V(Mathf.Sin(a)*.26f,0,Mathf.Cos(a)*.26f),V(Mathf.Sin(a)*.18f,.65f,Mathf.Cos(a)*.18f),.035f,Dark,5);}g.Frustum(V(0,.49f,0),V(.3f,0,.3f),V(0,.68f,0),V(.7f,0,.7f),Dark,n);g.Cone(V(0,.66f,0),.23f,.5f,new Color(1,.44f,.1f,0),6);g.Cone(V(.04f,.68f,0),.12f,.7f,Gold,5);}
            else if(name=="icicle")g.Cone(Vector3.zero,.18f,1.1f,new Color(.55f,.79f,.88f,0),5);
            else if(name=="flame"){g.Cone(Vector3.zero,.4f,1,new Color(1,.47f,.12f,0),6);g.Cone(V(.05f,0,0),.19f,1.3f,Gold,5);}
            else if(name=="snow"){g.Quad(V(-.05f,0,0),V(.05f,0,0),V(.05f,.1f,0),V(-.05f,.1f,0),Snow,true);g.Quad(V(0,0,-.05f),V(0,0,.05f),V(0,.1f,.05f),V(0,.1f,-.05f),Snow,true);}
            else if(name=="arrow"||name=="bolt"){g.Cylinder(V(0,0,-.5f),V(0,0,.35f),name=="arrow"?.014f:.022f,name=="arrow"?WoodLight:Steel,5);g.Spear(V(0,0,.33f),V(0,0,.5f),.05f,LightSteel);g.Quad(V(-.07f,0,-.48f),V(.07f,0,-.48f),V(.035f,0,-.3f),V(-.035f,0,-.3f),Snow,true);}
            else if(name=="bomb"){g.Ellipsoid(Vector3.zero,Vector3.one*.5f,Dark,n,4);g.Cylinder(V(0,.45f,0),V(.07f,.68f,0),.035f,WoodLight,5);g.Ellipsoid(V(.07f,.69f,0),Vector3.one*.05f,Gold,5,2);}
            else if(name=="head")g.Ellipsoid(Vector3.zero,V(.2f,.22f,.19f),Skin,8,3);
            else if(name=="torso")g.Bevel(Vector3.zero,V(.46f,.64f,.32f),.06f,Cloth);
            else if(name=="limb")g.Bevel(Vector3.zero,V(.21f,.6f,.23f),.04f,Cloth);
            return g;
        }
        sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();readonly List<Color> colors=new List<Color>();readonly List<int> indices=new List<int>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color){Vector3 cross=Vector3.Cross(b-a,c-a);if(cross.sqrMagnitude<1e-14f)return;Vector3 normal=cross.normalized;int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);normals.Add(normal);normals.Add(normal);normals.Add(normal);colors.Add(color);colors.Add(color);colors.Add(color);indices.Add(i);indices.Add(i+1);indices.Add(i+2);}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,bool doubleSided=false){Triangle(a,b,c,color);Triangle(a,c,d,color);if(doubleSided){Triangle(c,b,a,color);Triangle(d,c,a,color);}}
            void ConvexTriangle(Vector3 a,Vector3 b,Vector3 c,Color color){if(Vector3.Dot(Vector3.Cross(b-a,c-a),a+b+c)<0)Triangle(c,b,a,color);else Triangle(a,b,c,color);}
            void ConvexQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color){ConvexTriangle(a,b,c,color);ConvexTriangle(a,c,d,color);}
            public void Append(Geometry other,Matrix4x4 matrix){int offset=vertices.Count;for(int i=0;i<other.vertices.Count;i++){vertices.Add(matrix.MultiplyPoint3x4(other.vertices[i]));normals.Add(matrix.MultiplyVector(other.normals[i]).normalized);colors.Add(other.colors[i]);}foreach(int i in other.indices)indices.Add(offset+i);}
            public void Bevel(Vector3 p,Vector3 size,float bevel,Color color,Quaternion rotation=default)
            {
                if(rotation.x*rotation.x+rotation.y*rotation.y+rotation.z*rotation.z+rotation.w*rotation.w<.5f)rotation=Quaternion.identity;float x=size.x*.5f,y=size.y*.5f,z=size.z*.5f,b=Mathf.Min(bevel,Mathf.Min(x,Mathf.Min(y,z))*.8f);var local=new Geometry();Vector3[] low=new Vector3[8],high=new Vector3[8];float[] xx={-x+b,x-b,x,x,x-b,-x+b,-x,-x},zz={-z,-z,-z+b,z-b,z,z,z-b,-z+b};for(int i=0;i<8;i++){low[i]=V(xx[i],-y+b,zz[i]);high[i]=V(xx[i],y-b,zz[i]);}for(int i=0;i<8;i++)local.ConvexQuad(low[i],high[i],high[(i+1)%8],low[(i+1)%8],color);Vector3[] bottom={V(-x+b,-y,-z+b),V(x-b,-y,-z+b),V(x-b,-y,z-b),V(-x+b,-y,z-b)},top={V(-x+b,y,-z+b),V(x-b,y,-z+b),V(x-b,y,z-b),V(-x+b,y,z-b)};local.ConvexQuad(bottom[3],bottom[2],bottom[1],bottom[0],color);local.ConvexQuad(top[0],top[1],top[2],top[3],color);for(int i=0;i<4;i++){int a=i*2,bb=(i*2+1)%8;local.ConvexQuad(bottom[i],bottom[(i+1)%4],low[bb],low[a],color);local.ConvexQuad(top[(i+1)%4],top[i],high[a],high[bb],color);local.ConvexTriangle(bottom[(i+1)%4],low[(bb+1)%8],low[bb],color);local.ConvexTriangle(top[(i+1)%4],high[bb],high[(bb+1)%8],color);}Append(local,Matrix4x4.TRS(p,rotation,Vector3.one));
            }
            public void Cylinder(Vector3 a,Vector3 b,float radius,Color color,int sides){float h=(b-a).magnitude;if(h<.00001f)return;var local=new Geometry();local.Frustum(Vector3.zero,V(radius*2,0,radius*2),Vector3.up*h,V(radius*2,0,radius*2),color,sides);Append(local,Matrix4x4.TRS(a,Quaternion.FromToRotation(Vector3.up,b-a),Vector3.one));}
            public void Frustum(Vector3 a,Vector3 sizeA,Vector3 b,Vector3 sizeB,Color color,int sides){for(int i=0;i<sides;i++){float t=i*Mathf.PI*2/sides,u=(i+1)*Mathf.PI*2/sides;Vector3 aa=a+V(Mathf.Sin(t)*sizeA.x*.5f,0,Mathf.Cos(t)*sizeA.z*.5f),ab=a+V(Mathf.Sin(u)*sizeA.x*.5f,0,Mathf.Cos(u)*sizeA.z*.5f),ba=b+V(Mathf.Sin(t)*sizeB.x*.5f,0,Mathf.Cos(t)*sizeB.z*.5f),bb=b+V(Mathf.Sin(u)*sizeB.x*.5f,0,Mathf.Cos(u)*sizeB.z*.5f);Quad(aa,ab,bb,ba,color);Triangle(a,ab,aa,color);Triangle(b,ba,bb,color);}}
            public void Cone(Vector3 p,float radius,float h,Color color,int sides){Frustum(p,V(radius*2,0,radius*2),p+Vector3.up*h,Vector3.zero,color,sides);}
            public void Spear(Vector3 a,Vector3 b,float radius,Color color){var local=new Geometry();local.Cone(Vector3.zero,radius,(b-a).magnitude,color,4);Append(local,Matrix4x4.TRS(a,Quaternion.FromToRotation(Vector3.up,b-a),Vector3.one));}
            public void Ellipsoid(Vector3 p,Vector3 radius,Color color,int sides,int rings){for(int j=0;j<rings;j++)for(int i=0;i<sides;i++){float a=-Mathf.PI*.5f+j*Mathf.PI/rings,b=a+Mathf.PI/rings,t=i*Mathf.PI*2/sides,u=t+Mathf.PI*2/sides;Vector3 aa=p+Vector3.Scale(V(Mathf.Cos(a)*Mathf.Sin(t),Mathf.Sin(a),Mathf.Cos(a)*Mathf.Cos(t)),radius),ab=p+Vector3.Scale(V(Mathf.Cos(a)*Mathf.Sin(u),Mathf.Sin(a),Mathf.Cos(a)*Mathf.Cos(u)),radius),ba=p+Vector3.Scale(V(Mathf.Cos(b)*Mathf.Sin(t),Mathf.Sin(b),Mathf.Cos(b)*Mathf.Cos(t)),radius),bb=p+Vector3.Scale(V(Mathf.Cos(b)*Mathf.Sin(u),Mathf.Sin(b),Mathf.Cos(b)*Mathf.Cos(u)),radius);Quad(aa,ab,bb,ba,color);}}
            public void Rock(Vector3 p,float radius,Color color,int sides,int rings){for(int j=0;j<rings;j++)for(int i=0;i<sides;i++){Vector3 a=RockPoint(i,j,sides,rings),b=RockPoint(i+1,j,sides,rings),c=RockPoint(i+1,j+1,sides,rings),d=RockPoint(i,j+1,sides,rings);Color tint=color;float shade=.91f+.12f*Mathf.Sin(i*2.31f+j*1.33f);tint.r*=shade;tint.g*=shade;tint.b*=shade;Quad(p+a*radius,p+b*radius,p+c*radius,p+d*radius,tint);}}
            static Vector3 RockPoint(int i,int j,int sides,int rings){if(j==0)return Vector3.down*.86f;if(j==rings)return Vector3.up*.86f;i%=sides;float a=-Mathf.PI*.5f+j*Mathf.PI/rings,t=i*Mathf.PI*2/sides,r=.88f+.12f*Mathf.Sin(i*3.3f+j*1.7f);return V(Mathf.Cos(a)*Mathf.Sin(t)*r,Mathf.Sin(a)*(.86f+.08f*Mathf.Sin(i*1.7f)),Mathf.Cos(a)*Mathf.Cos(t)*r);}
            public void Torus(Vector3 p,float radius,float thickness,Color color,int sides){int cross=4;for(int i=0;i<sides;i++)for(int j=0;j<cross;j++){float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides,c=j*Mathf.PI*2/cross,d=(j+1)*Mathf.PI*2/cross;Quad(p+TorusPoint(a,c,radius,thickness),p+TorusPoint(b,c,radius,thickness),p+TorusPoint(b,d,radius,thickness),p+TorusPoint(a,d,radius,thickness),color);}}
            static Vector3 TorusPoint(float a,float b,float radius,float thickness){float r=radius+Mathf.Cos(b)*thickness;return V(Mathf.Sin(a)*r,Mathf.Sin(b)*thickness,Mathf.Cos(a)*r);}
            public void Barrel(Vector3 p,float radius,float height,Color color,int sides){Frustum(p-Vector3.up*(height*.5f),V(radius*1.7f,0,radius*1.7f),p,V(radius*2.1f,0,radius*2.1f),color,sides);Frustum(p,V(radius*2.1f,0,radius*2.1f),p+Vector3.up*(height*.5f),V(radius*1.7f,0,radius*1.7f),color,sides);}
            public Mesh Build(string name){var mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;}
        }
    }
}
