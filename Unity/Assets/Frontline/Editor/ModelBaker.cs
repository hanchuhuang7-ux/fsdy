using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Frontline.Core;
using Frontline.UnityGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using CropAppearance = Frontline.UnityGame.ManorPlotArt.CropAppearance;

namespace Frontline.Editor
{
    // Deterministic editor bake. The render mesh and its physics/shadow proxy have independent layouts.
    public static partial class ModelBaker
    {
        const string Folder="Assets/Frontline/Models/Resources/Models";
        static readonly Color Cloth=new Color(.93f,.94f,.9f,1),Skin=new Color(.86f,.64f,.43f,0),Steel=new Color(.49f,.59f,.64f,0),LightSteel=new Color(.73f,.8f,.8f,0),Dark=new Color(.10f,.14f,.17f,0),Wood=new Color(.37f,.22f,.12f,0),WoodLight=new Color(.57f,.36f,.18f,0),Gold=new Color(.91f,.64f,.20f,0),Rock=new Color(.47f,.51f,.49f,0),Leaf=new Color(.27f,.43f,.23f,0),Snow=new Color(.88f,.95f,.96f,0),Red=new Color(.8f,.16f,.1f,0);
        static readonly string[] Types={"militia","shield","spear","archer","crossbow","bomber","giant","cavalry","medic","catapult"};
        static readonly List<string> Written=new List<string>();
        [MenuItem("Frontline/Bake low polygon models")]
        public static void Bake()
        {
            Directory.CreateDirectory(Folder);Written.Clear();
            ProceduralTextureBaker.BakeLakeNormals();
            for(int lod=0;lod<2;lod++)
            {
                foreach(string key in new[]{"platform","ring","dot","cliff","base","flame","snow","island_root","mist","icicle"})
                    Save(key,lod,EnvironmentMesh(key,lod));
                var ground=new Geometry{surface=Surface.Terrain};ground.Box(Vector3.zero,Vector3.one,Cloth);Save("arena_ground",lod,ground);
                Save("arena_scree",lod,ArenaScree(lod));Save("arena_scree_corner",lod,ScreeCorner(lod));Save("steel_arc",lod,SteelArc());
                Save("road_path",lod,RoadRibbon(0,lod));Save("road_link",lod,RoadRibbon(.125f,lod));
                Save("road_setts",lod,RoadRibbon(.5f,lod));Save("road_grass",lod,RoadRibbon(1,lod));Save("plot_ropes",lod,SurveyRopes(lod));
            }
            BakeCoast();ExternalAssetImporter.BakeArt();
            Written.AddRange(ExternalAssetImporter.Exported);
            var roster=Resources.Load<ArtistRoster>("CC0/ArtistRoster");
            var manifest=new {generator="Frontline.Editor.ExternalAssetImporter",version=PlayerSettings.bundleVersion,
                credits="Assets/Frontline/ThirdParty/CREDITS-CC0.md",surfaceChannels="UV0 photographic projection or original specimen UV; UV1 material, metal, roughness, AO; UV3 and UV4 original skeleton indices and weights",
                textures=new[]{"Scans/LandscapeColor","Scans/LandscapeDetail","Scans/SpecimenColor","Scans/SpecimenDetail","Scans/FoliageViews","Scans/WardrobeColor","Scans/WardrobeDetail","Textures/ShoreDistance","Textures/LakeNormals"},
                sources=ExternalAssetImporter.UsedSources.ToArray(),resources=Written.ToArray(),
                skeletons=Array.ConvertAll(roster.skeletons,a=>new {a.name,a.boneCount,a.skinnedVertices,takes=Array.ConvertAll(a.takes,t=>t.source)}),
                costumes=Array.ConvertAll(roster.costumes,a=>new {a.name,rig=roster.skeletons[a.skeleton].name,main=roster.skeletons[a.skeleton].body.vertexCount+(a.high?a.high.vertexCount:0),mid=roster.skeletons[a.skeleton].mid.vertexCount+(a.mid?a.mid.vertexCount:0),proxy=roster.skeletons[a.skeleton].proxy.vertexCount+(a.proxy?a.proxy.vertexCount:0),attachments=a.high?a.high.vertexCount:0,attachmentHash=ExternalAssetImporter.Fingerprint(a.high)})};
            File.WriteAllText("Assets/Frontline/Models/model-manifest.json",Json.Write(manifest));
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("FRONTLINE_MODELS_OK "+Written.Count+" static mesh references, "+roster.costumes.Length+" artist costumes");
        }
        static void Save(string name,int lod,Geometry g)
        {
            string key=name+(lod==1?"_lod1":""),path=Folder+"/"+key+".asset";var mesh=lod==0?g.SurfaceMesh(key):g.ProxyMesh(key);if(mesh.vertexCount==0||mesh.normals.Length!=mesh.vertexCount||mesh.colors.Length!=mesh.vertexCount)throw new InvalidDataException("Incomplete baked mesh "+key);foreach(var v in mesh.vertices)if(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z))throw new InvalidDataException("Non-finite baked vertex "+key);foreach(var n in mesh.normals)if(n.sqrMagnitude<.98f)throw new InvalidDataException("Invalid baked normal "+key);var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old&&key=="rock_lod1")
            {
                if(!System.Linq.Enumerable.SequenceEqual(old.vertices,mesh.vertices)||
                    !System.Linq.Enumerable.SequenceEqual(old.triangles,mesh.triangles))
                    throw new InvalidDataException("Frozen rock proxy geometry differs");
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            else if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);}else AssetDatabase.CreateAsset(mesh,path);Written.Add("Models/"+key);
        }
        static Vector3 V(float x,float y,float z){return new Vector3(x,y,z);}
        static Geometry Leg(int lod)
        {
            var g=new Geometry();
            if(lod==1)
            {
                g.Frustum(V(0,-.18f,0),V(.16f,0,.17f),V(0,.2f,0),V(.23f,0,.24f),Dark,5);
                g.Box(V(0,-.22f,.045f),V(.20f,.12f,.29f),Wood);
                return g;
            }
            g.Ellipsoid(V(0,.175f,-.012f),V(.102f,.135f,.109f),Dark,9,4);
            g.Frustum(V(0,-.16f,-.013f),V(.108f,0,.127f),V(0,.06f,.016f),V(.145f,0,.162f),Walnut,7);
            g.Frustum(V(0,.058f,.016f),V(.145f,0,.162f),V(0,.218f,-.013f),V(.176f,0,.191f),Dark,7);
            g.Bevel(V(0,-.216f,.052f),V(.143f,.122f,.246f),.037f,Walnut);
            g.Box(V(0,-.267f,.057f),V(.142f,.025f,.238f),Dark);
            for(int i=0;i<3;i++)
            {
                float y=-.119f+i*.046f,z=.057f+i*.011f;
                g.Quad(V(-.046f,y,z),V(.051f,y+.020f,z),V(.051f,y+.034f,z),V(-.046f,y+.014f,z),Pigment(WoodLight,.75f),true);
            }
            return g;
        }
        static Geometry Arm(string type,bool left,int lod)
        {
            var g=new Geometry();
            float x=left?-.36f:.36f;
            if(lod==0)
            {
                if(left||type=="catapult")ArticulatedSleeve(g,type,left);
            }
            else
            {
                g.Frustum(V(x,-.07f,.03f),V(.16f,0,.17f),V(x,.24f,.015f),V(.24f,0,.25f),Cloth,5);
                g.Bevel(V(x,-.14f,.08f),V(.15f,.22f,.17f),.035f,Skin);
                g.Bevel(V(x,-.05f,.1f),V(.18f,.11f,.2f),.025f,Wood);
            }
            if(left)return g;
            if(lod==0&&(type=="archer"||type=="crossbow"||type=="bomber"||type=="giant"||type=="medic"))
            {
                FieldImplement(g,type);
                return g;
            }
            WeaponTrim(g,type,lod);
            if(lod==1)WeaponGrip(g,type,lod);
            if(lod==0)WeaponFinish(g,type,x);
            if(type=="spear"||type=="cavalry"){g.Cylinder(V(x,.04f,-.35f),V(x,.04f,1.55f),.025f,WoodLight,6);g.Spear(V(x,.04f,1.48f),V(x,.04f,1.9f),.078f,LightSteel);if(lod==0)g.Cylinder(V(x,.04f,1.4f),V(x,.04f,1.52f),.04f,Dark,6);}
            else if(type=="archer"){for(int i=0;i<6;i++){float a=-1.2f+i*.4f,b=a+.4f;g.Cylinder(V(x,Mathf.Sin(a)*.48f+.1f,.36f+Mathf.Cos(a)*.23f),V(x,Mathf.Sin(b)*.48f+.1f,.36f+Mathf.Cos(b)*.23f),.027f,WoodLight,5);}g.Cylinder(V(x,-.35f,.44f),V(x,.55f,.44f),.006f,Snow,3);g.Cylinder(V(x,.1f,.05f),V(x,.1f,.9f),.012f,Wood,4);}
            else if(type=="crossbow"){g.Bevel(V(x,.04f,.41f),V(.13f,.16f,.7f),.025f,WoodLight);g.Cylinder(V(x-.4f,.1f,.61f),V(x,.1f,.73f),.035f,Steel,5);g.Cylinder(V(x,.1f,.73f),V(x+.4f,.1f,.61f),.035f,Steel,5);g.Cylinder(V(x-.4f,.1f,.61f),V(x,.1f,.22f),.006f,Snow,3);g.Cylinder(V(x,.1f,.22f),V(x+.4f,.1f,.61f),.006f,Snow,3);g.Bevel(V(x,-.08f,.12f),V(.1f,.25f,.14f),.02f,Wood);}
            else if(type=="bomber"){g.Ellipsoid(V(x,.03f,.38f),V(.17f,.18f,.17f),Dark,8,3);g.Cylinder(V(x,.2f,.37f),V(x+.055f,.33f,.38f),.021f,WoodLight,5);g.Ellipsoid(V(x+.06f,.33f,.38f),V(.035f,.05f,.035f),Gold,5,2);}
            else if(type=="giant"){g.Cylinder(V(x,-.44f,.36f),V(x,.68f,.36f),.06f,WoodLight,7);g.Bevel(V(x,.62f,.36f),V(.63f,.35f,.38f),.07f,Rock);g.Bevel(V(x-.19f,.62f,.36f),V(.065f,.4f,.42f),.018f,Steel);g.Bevel(V(x+.19f,.62f,.36f),V(.065f,.4f,.42f),.018f,Steel);}
            else if(type=="medic"){g.Cylinder(V(x,-.45f,.28f),V(x,.65f,.28f),.04f,WoodLight,6);g.Torus(V(x,.66f,.28f),.13f,.035f,new Color(.4f,.8f,.47f,0),10);}
            else{Blade(g,x);g.Bevel(V(x,.1f,.04f),V(.26f,.055f,.055f),.012f,Gold);g.Cylinder(V(x,.1f,-.16f),V(x,.1f,.05f),.035f,Wood,6);}
            return g;
        }
        static Geometry Shield(int lod)
        {
            if(lod==0)return CurvedShield(-.30f,.294f,WroughtIron,DyedWool);
            var g=new Geometry();
            g.Spear(V(-.30f,-.23f,.29f),V(-.30f,-.51f,.29f),.18f,Steel);
            g.Bevel(V(-.30f,.006f,.29f),V(.47f,.72f,.12f),.084f,Steel);
            g.Bevel(V(-.30f,.006f,.356f),V(.39f,.61f,.026f),.061f,Cloth);
            g.Ellipsoid(V(-.30f,.02f,.389f),V(.102f,.10f,.055f),LightSteel,6,2);
            g.Box(V(-.34f,-.061f,.202f),V(.27f,.09f,.108f),Wood);
            return g;
        }
        static Geometry Horse(int lod)
        {
            if(lod==1)return DistantHorse();
            return Charger();
        }
        static Geometry HorseLeg(int lod)
        {
            if(lod==0)return ChargerLeg();
            var g=new Geometry();
            g.Cylinder(V(0,.12f,0),V(0,-.35f,.025f),.085f,Wood,lod==0?6:4);
            g.Cylinder(V(0,-.35f,.025f),V(0,-.63f,.065f),.055f,WoodLight,lod==0?5:4);
            if(lod==0)g.Bevel(V(0,-.64f,.09f),V(.16f,.13f,.22f),.035f,Dark);
            else g.Box(V(0,-.64f,.09f),V(.16f,.13f,.22f),Dark);
            return g;
        }
        static Geometry Catapult(int lod)
        {
            if(lod==1)return DistantCatapult();
            var g=new Geometry();foreach(float x in new[]{-.43f,.43f})g.Bevel(V(x,-.48f,0),V(.18f,.23f,1.65f),.035f,Wood);foreach(float z in new[]{-.59f,.59f})g.Bevel(V(0,-.47f,z),V(1.25f,.18f,.17f),.03f,WoodLight);foreach(float x in new[]{-.56f,.56f})foreach(float z in new[]{-.55f,.55f}){var wheel=new Geometry();wheel.Torus(Vector3.zero,.29f,.06f,Wood,lod==0?12:8);for(int i=0;i<(lod==0?8:4);i++){float a=i*Mathf.PI/(lod==0?4:2);wheel.Cylinder(Vector3.zero,V(Mathf.Sin(a)*.27f,0,Mathf.Cos(a)*.27f),.025f,WoodLight,4);}wheel.Cylinder(V(0,-.06f,0),V(0,.06f,0),.07f,Steel,6);g.Append(wheel,Matrix4x4.TRS(V(x,-.43f,z),Quaternion.Euler(0,0,90),Vector3.one));}foreach(float x in new[]{-.38f,.38f}){g.Cylinder(V(x,-.4f,-.43f),V(x,.61f,0),.065f,WoodLight,6);g.Cylinder(V(x,-.4f,.53f),V(x,.61f,0),.065f,WoodLight,6);}g.Cylinder(V(-.49f,.59f,0),V(.49f,.59f,0),.08f,Steel,7);g.Bevel(V(0,-.29f,0),V(.54f,.12f,1.03f),.03f,Wood);
            foreach(float z in new[]{-.64f,.64f})
            {
                g.Box(V(0,-.36f,z),V(1.08f,.052f,.11f),Steel);
                if(lod==0)foreach(float x in new[]{-.42f,.42f})
                    g.Cylinder(V(x,-.35f,z),V(x,-.317f,z),.055f,LightSteel,6);
            }
            EngineIronwork(g);
            SiegeJoinery(g);
            SiegeTackle(g);
            return g;
        }
        static Geometry CatapultArm(int lod)
        {
            var g=new Geometry();
            if(lod==1)
            {
                g.Box(V(0,0,-.35f),V(.14f,.17f,1.83f),WoodLight);
                g.Box(V(0,0,.55f),V(.55f,.47f,.44f),Wood);
                g.Box(V(0,.08f,-1.2f),V(.40f,.16f,.46f),Dark);
                return g;
            }
            g.Bevel(V(0,0,-.35f),V(.14f,.17f,1.83f),.035f,WoodLight);
            g.Bevel(V(0,0,.55f),V(.55f,.42f,.43f),.045f,Wood);
            foreach(float x in new[]{-.185f,.185f})g.Box(V(x,-.026f,.55f),V(.051f,.432f,.445f),Steel);
            ThrowingPouch(g);
            return g;
        }
        static Geometry WreckHorse(int lod){var g=new Geometry();g.Append(Horse(lod),Matrix4x4.TRS(V(0,.12f,0),Quaternion.Euler(0,0,82),Vector3.one));for(int i=0;i<4;i++)g.Append(HorseLeg(lod),Matrix4x4.TRS(V(i%2==0?-.35f:.35f,.12f,i<2?-.45f:.4f),Quaternion.Euler(0,0,i%2==0?65:110),Vector3.one));g.SetAtGrade();return g;}
        static Geometry WreckCatapult(int lod){var g=new Geometry();g.Append(Catapult(lod),Matrix4x4.TRS(V(0,.42f,0),Quaternion.Euler(13,17,54),Vector3.one));g.Append(CatapultArm(lod),Matrix4x4.TRS(V(.6f,.08f,.3f),Quaternion.Euler(0,65,0),Vector3.one));g.Bevel(V(-.6f,.08f,.5f),V(.14f,.14f,.85f),.025f,WoodLight,Quaternion.Euler(0,-42,0));g.SetAtGrade();return g;}
        static Geometry Prop(string name,int lod)
        {
            var g=new Geometry();int n=lod==0?10:6;
            if(name=="barrel")BlastBarrel(g,lod);
            else if(name=="barricade")Stockade(g,lod);
            else if(name=="banner")BattleStandard(g,lod);
            else if(name=="spikes"){g.Bevel(V(0,.07f,0),V(1.7f,.14f,1.7f),.06f,Wood);for(int i=-1;i<=1;i++)for(int j=-1;j<=1;j++){g.Cylinder(V(i*.5f,.1f,j*.5f),V(i*.5f,.34f,j*.5f),.04f,WoodLight,5);g.Spear(V(i*.5f,.34f,j*.5f),V(i*.5f+.035f,.63f,j*.5f),.065f,Steel);}}
            else{g.Bevel(V(0,.1f,0),V(1.7f,.2f,1.7f),.09f,Wood);for(int j=-1;j<=1;j+=2)for(int k=-1;k<=1;k+=2){Vector3 last=V(j*.5f,.2f,k*.5f);int segments=lod==0?24:12;for(int i=1;i<=segments;i++){float a=i*Mathf.PI*6/segments;Vector3 next=V(j*.5f+Mathf.Sin(a)*.12f,.2f+i*.38f/segments,k*.5f+Mathf.Cos(a)*.12f);g.Cylinder(last,next,.02f,Steel,4);last=next;}}g.Bevel(V(0,.64f,0),V(1.65f,.15f,1.65f),.07f,Cloth);g.Bevel(V(0,.72f,0),V(.75f,.015f,.13f),.015f,Gold);}
            if(lod==0)PropFinish(g,name);
            return g;
        }
        static Geometry EnvironmentMesh(string name,int lod)
        {
            var g=new Geometry();int n=lod==0?10:6;
            if(name=="ring")g.Torus(Vector3.zero,1,.022f,Cloth,lod==0?40:24);
            else if(name=="dot")g.Cylinder(V(0,-.01f,0),V(0,.01f,0),.5f,Cloth,6);
            else if(name=="mist"){for(int i=0;i<n*2;i++){float a=i*Mathf.PI/n,b=(i+1)*Mathf.PI/n;g.Quad(Vector3.zero,V(Mathf.Sin(a)*.5f,0,Mathf.Cos(a)*.5f),V(Mathf.Sin(b)*.5f,0,Mathf.Cos(b)*.5f),Vector3.zero,Snow,true);}}
            else if(name=="island_root"){g.Frustum(V(0,-.5f,0),V(.18f,0,.18f),V(0,.5f,0),V(1,0,1),Rock,n);g.Frustum(V(0,.2f,0),V(.72f,0,.72f),V(0,.27f,0),V(.78f,0,.78f),new Color(.3f,.34f,.32f,0),n);}
            else if(name=="platform"){g.Box(Vector3.zero,Vector3.one,Cloth);}
            else if(name=="base"||name=="cliff"){g.Frustum(V(0,-.5f,0),V(1,0,1),V(0,.5f,0),V(.88f,0,.92f),Rock,8);for(int j=0;j<3;j++)g.Frustum(V(0,-.48f+j*.3f,0),V(.96f-j*.035f,0,.97f-j*.035f),V(0,-.39f+j*.3f,0),V(.97f-j*.035f,0,.98f-j*.035f),new Color(.32f+j*.035f,.36f+j*.035f,.34f+j*.035f,0),8);}
            else if(name=="rock")
            {
                // Frozen PhysX adapters cook this exact six-sided LOD1 hull.
                g.Rock(Vector3.zero,.5f,Rock,6,4);
                if(lod==0)return g.StoneStrata();
            }
            else if(name=="stone"){g.surface=Surface.Outcrop;g.Rock(Vector3.zero,.5f,Rock,n,4);}
            else if(name=="wall")
            {
                WallMasonry(g,lod);
                if(lod==0)for(int i=0;i<6;i++)
                    g.Bevel(V((i-2.5f)*.156f,.456f,0),V(.142f,.065f,.90f),.016f,Stonework);
            }
            else if(lod==0&&(name=="grass"||name=="reeds"))MeadowTuft(g,name=="reeds");
            else if(lod==0&&name=="shrub"){g.surface=Surface.Leaf;for(int i=0;i<4;i++){float a=i*2.4f;LeafCrown(g,V(Mathf.Sin(a)*.24f,.35f+.084f*(i%2),Mathf.Cos(a)*.24f),V(.39f,.32f,.37f),i,Moss);}}
            else if(name=="grass"||name=="reeds"){int blades=lod==0?7:3;for(int i=0;i<blades;i++){float a=i*2.4f,h=name=="reeds"?.85f+i*.1f:.26f+i*.04f,x=Mathf.Sin(a)*.21f,z=Mathf.Cos(a)*.21f;g.Quad(V(x-.025f,0,z),V(x+.025f,0,z),V(x+.09f,h*.58f,z+.025f),V(x+.06f,h,z),Leaf,true);if(name=="reeds"){g.Cylinder(V(x,h*.7f,z),V(x+.06f,h+.12f,z),.014f,Leaf,4);g.Cylinder(V(x+.06f,h,z),V(x+.06f,h+.22f,z),.035f,Wood,5);}}}
            else if(name=="flowers"){int flowers=lod==0?5:2;for(int i=0;i<flowers;i++){float a=i*2.4f,x=Mathf.Sin(a)*.3f,z=Mathf.Cos(a)*.3f,h=.28f+i*.035f;g.Cylinder(V(x,0,z),V(x,h,z),.012f,Leaf,3);g.Cylinder(V(x,h-.015f,z),V(x,h+.015f,z),.095f,i%2==0?new Color(.96f,.8f,.34f,0):new Color(.84f,.62f,.76f,0),5);g.Cylinder(V(x,h+.016f,z),V(x,h+.025f,z),.03f,Gold,5);}}
            else if(name=="shrub"){g.Cylinder(Vector3.zero,V(0,.4f,0),.08f,Wood,5);for(int i=0;i<(lod==0?5:3);i++){float a=i*2.4f;g.Rock(V(Mathf.Sin(a)*.3f,.43f+(i%2)*.15f,Mathf.Cos(a)*.3f),.4f,Leaf,n,3);}}
            else if(name=="tree"||name=="pine"||name=="dead_tree")CanopyLayers(g,name,lod);
            else if(name=="column"){g.Bevel(V(0,.12f,0),V(1,.24f,1),.07f,Rock);g.Cylinder(V(0,.24f,0),V(.03f,1.95f,0),.31f,Rock,n);g.Bevel(V(0,1.96f,0),V(.83f,.22f,.83f),.04f,Rock);g.Bevel(V(0,.3f,0),V(.78f,.13f,.78f),.025f,LightSteel);}
            else if(name=="arch"){foreach(float x in new[]{-.85f,.85f}){g.Bevel(V(x,.7f,0),V(.45f,1.4f,.55f),.06f,Rock);g.Bevel(V(x,.12f,0),V(.68f,.24f,.74f),.05f,Rock);}int parts=lod==0?7:5;for(int i=0;i<parts;i++){float a=i*Mathf.PI/parts,b=(i+1)*Mathf.PI/parts;g.Cylinder(V(Mathf.Cos(a)*.85f,1.35f+Mathf.Sin(a)*.65f,0),V(Mathf.Cos(b)*.85f,1.35f+Mathf.Sin(b)*.65f,0),.26f,Rock,5);}}
            else if(name=="brazier"){for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;g.Cylinder(V(Mathf.Sin(a)*.26f,0,Mathf.Cos(a)*.26f),V(Mathf.Sin(a)*.18f,.65f,Mathf.Cos(a)*.18f),.035f,Dark,5);}g.Frustum(V(0,.49f,0),V(.3f,0,.3f),V(0,.68f,0),V(.7f,0,.7f),Dark,n);if(lod==0)g.surface=Surface.Ember;g.Cone(V(0,.66f,0),.23f,.5f,new Color(1,.44f,.1f,0),6);g.Cone(V(.04f,.68f,0),.12f,.7f,Gold,5);}
            else if(name=="icicle")g.Cone(Vector3.zero,.18f,1.1f,new Color(.55f,.79f,.88f,0),5);
            else if(name=="flame"){if(lod==0)g.surface=Surface.Ember;g.Cone(Vector3.zero,.4f,1,new Color(1,.47f,.12f,0),6);g.Cone(V(.05f,0,0),.19f,1.3f,Gold,5);}
            else if(name=="snow"){g.Quad(V(-.05f,0,0),V(.05f,0,0),V(.05f,.1f,0),V(-.05f,.1f,0),Snow,true);g.Quad(V(0,0,-.05f),V(0,0,.05f),V(0,.1f,.05f),V(0,.1f,-.05f),Snow,true);}
            else if(name=="arrow"||name=="bolt"){g.Cylinder(V(0,0,-.5f),V(0,0,.35f),name=="arrow"?.014f:.022f,name=="arrow"?WoodLight:Steel,5);g.Spear(V(0,0,.33f),V(0,0,.5f),.05f,LightSteel);g.Quad(V(-.07f,0,-.48f),V(.07f,0,-.48f),V(.035f,0,-.3f),V(-.035f,0,-.3f),Snow,true);}
            else if(name=="bomb"){g.Ellipsoid(Vector3.zero,Vector3.one*.5f,Dark,n,4);g.Cylinder(V(0,.45f,0),V(.07f,.68f,0),.035f,WoodLight,5);g.Ellipsoid(V(.07f,.69f,0),Vector3.one*.05f,Gold,5,2);}
            else if(name=="head")
            {
                if(lod==0)FaceBust(g,Vector3.zero,V(.116f,.137f,.119f),1,false,false);
                else g.Ellipsoid(Vector3.zero,V(.2f,.22f,.19f),Skin,8,3);
            }
            else if(name=="torso")g.Bevel(Vector3.zero,V(.46f,.64f,.32f),.06f,Cloth);
            else if(name=="limb")g.Bevel(Vector3.zero,V(.21f,.6f,.23f),.04f,Cloth);
            return g;
        }
        // Manor pieces are rooted at grade. The public-facing side of each plot is local -Z.
        static readonly Color Plaster=new Color(.89f,.81f,.63f,0), Straw=new Color(.85f,.67f,.34f,0),
            StrawLight=new Color(.97f,.83f,.48f,0), Tile=new Color(.67f,.28f,.18f,0),
            Slate=new Color(.27f,.39f,.44f,0), Green=new Color(.23f,.43f,.34f,0),
            Ochre=new Color(.80f,.49f,.18f,0), Glass=new Color(.24f,.43f,.44f,0),
            Stonework=new Color(.60f,.60f,.53f,0), Earth=new Color(.42f,.31f,.20f,0);

        static Geometry FrontierGate(int lod)
        {
            var g=new Geometry();
            foreach(int side in new[]{-1,1})
            {
                float z=side*3.73f;
                g.Bevel(V(0,.24f,z),V(1.28f,.48f,1.07f),.105f,Slate);
                g.Bevel(V(0,1.60f,z),V(.92f,2.65f,.8f),.12f,Stonework);
                g.Bevel(V(0,2.92f,z),V(1.13f,.34f,.98f),.07f,Stonework);
                g.Box(V(0,3.20f,z),V(1.11f,.18f,.91f),Ochre);
                if(lod==0)
                    for(int course=0;course<4;course++)
                        g.Box(V(-.462f,.58f+course*.52f,z),V(.02f,.052f,.72f),Slate);
            }
            g.Bevel(V(0,3.17f,0),V(.58f,.51f,7.99f),.11f,Stonework);
            g.Box(V(0,3.39f,0),V(.66f,.12f,7.94f),Slate);
            g.Box(V(0,3.08f,-.35f),V(.23f,.59f,.72f),Slate);
            g.Box(V(-.132f,3.08f,-.35f),V(.037f,.42f,.085f),Gold);
            g.Box(V(-.135f,3.12f,-.35f),V(.041f,.085f,.41f),Gold);
            if(lod==0){GateStoneFinish(g);WatchTurret(g,V(0,2.90f,3.73f),.92f);WatchTurret(g,V(0,2.90f,-3.73f),.92f);}
            return g;
        }

        static Geometry ManorBuilding(string kind,int lod,CropAppearance state=CropAppearance.Regular)
        {
            if(kind=="manor_hall")return Castle(1,lod);
            var g=new Geometry();
            switch(kind)
            {
                case "cottage": Cottages(g,lod); break;
                case "farm": Farm(g,lod,state); break;
                case "irrigated_field": IrrigatedField(g,lod,state); break;
                case "pasture": Pasture(g,lod); break;
                case "timber_camp": TimberCamp(g,lod); break;
                case "smithy": Smithy(g,lod); break;
                case "barracks": Barracks(g,lod); break;
                case "stables": Stables(g,lod); break;
                case "tavern": Tavern(g,lod); break;
                case "drill_yard": DrillYard(g,lod); break;
                case "granary": Granary(g,lod); break;
                case "chapel": Chapel(g,lod); break;
                case Manor.Bakery: Bakery(g,lod); break;
                case Manor.Weaver: Weaver(g,lod); break;
                default: throw new ArgumentException("Unknown manor building "+kind);
            }
            YardDetail(g,kind,lod);
            if(lod==0)TradeFront(g,kind);
            g.FitFootprint(ManorPlotArt.HalfWidth(false),ManorPlotArt.HalfDepth(false));
            return g;
        }

        // A five-sided roof prism with its ridge along X. Each slope and gable has its own normal.
        static void Gable(Geometry g,Vector3 eave,float width,float depth,float rise,Color roof,Color end,int lod)
        {
            float half=width*.5f,side=depth*.5f;
            Vector3 a=eave+V(-half,0,-side),b=eave+V(half,0,-side),
                c=eave+V(half,0,side),d=eave+V(-half,0,side);
            Vector3 left=eave+V(-half,rise,0),right=eave+V(half,rise,0);
            g.Quad(a,left,right,b,roof);g.Quad(left,d,c,right,roof);
            g.Triangle(a,d,left,end);g.Triangle(b,right,c,end);g.Quad(a,b,c,d,Wood);
            if(lod!=0)return;
            RoofCourses(g,eave,width,depth,rise,roof);
            g.Box(eave+V(0,.015f,-side),V(width,.07f,.07f),WoodLight);
            g.Box(eave+V(0,.015f,side),V(width,.07f,.07f),WoodLight);
            g.Box(eave+V(0,rise,0),V(width+.04f,.07f,.09f),roof*1.13f);
        }

        static void House(Geometry g,float x,float z,float width,float depth,float eave,float rise,Color roof,int lod)
        {
            if(lod==0)
            {
                TimberHouse(g,x,z,width,depth,eave,rise,roof);
                return;
            }
            g.Bevel(V(x,.10f,z),V(width+.14f,.2f,depth+.14f),.055f,Stonework);
            g.Box(V(x,.044f,z),V(width+.17f,.066f,depth+.15f),Slate);
            g.Box(V(x,eave*.5f,z),V(width,eave,depth),Plaster);
            Gable(g,V(x,eave,z),width+.24f,depth+.28f,rise,roof,Plaster,lod);
            float face=z-depth*.5f-.025f;
            g.Box(V(x,eave-.06f,face-.036f),V(width+.18f,.14f,.15f),Wood);
            g.Box(V(x,eave+rise-.026f,z),V(width+.28f,.075f,.12f),roof);
            foreach(int side in new[]{-1,1})
            {
                float corner=x+side*(width*.5f-.05f);
                g.Box(V(corner,eave*.5f,face),V(.13f,eave,.09f),Wood);
            }
        }

        static void Door(Geometry g,float x,float face,float height,float width)
        {
            g.Box(V(x,height*.5f,face-.03f),V(width+.13f,height+.13f,.11f),Wood);
            g.Box(V(x,height*.5f,face-.095f),V(width,height,.06f),Green);
            g.Box(V(x,.06f,face-.20f),V(width+.26f,.12f,.37f),Stonework);
        }

        static void Window(Geometry g,float x,float y,float face,int lod)
        {
            g.Box(V(x,y,face-.024f),V(.36f,.39f,.055f),Dark);
            g.Box(V(x,y-.23f,face-.105f),V(.48f,.065f,.19f),Stonework);
            foreach(int side in new[]{-1,1})
            {
                g.Box(V(x+side*.182f,y,face-.061f),V(.065f,.41f,.105f),Wood);
                g.Box(V(x,y+side*.17f,face-.059f),V(.34f,.065f,.10f),WoodLight);
                g.Box(V(x+side*.243f,y,face-.063f),V(.09f,.33f,.065f),Green);
            }
            if(lod==0)g.surface=Surface.WindowLight;
            g.Box(V(x,y,face-.055f),V(.25f,.246f,.025f),lod==0?new Color(.76f,.52f,.30f,0):Glass);
            g.surface=Surface.Auto;
            if(lod==0)
            {
                g.Box(V(x,y,face-.076f),V(.027f,.243f,.025f),Linen);
                g.Box(V(x,y,face-.077f),V(.248f,.027f,.025f),Linen);
            }
        }

        static void Chimney(Geometry g,float x,float z,float top,float width,int lod)
        {
            g.Box(V(x,top*.5f,z),V(width,top,width),Stonework);
            g.Box(V(x,top-.07f,z),V(width+.14f,.16f,width+.14f),Rock);
            g.Box(V(x,top+.02f,z),V(width*.71f,.025f,width*.71f),Dark);
            if(lod==0)
                for(int face=0;face<4;face++)
                {
                    var turn=Quaternion.Euler(0,face*90,0);
                    MasonryFace(g,V(x,top-.80f,z)+turn*V(0,0,-width*.5f),turn,width,.67f,.132f);
                }
        }

        static void Pennant(Geometry g,Vector3 foot,float height,float span,Color field,int lod)
        {
            var top=foot+Vector3.up*height;
            g.Cylinder(foot,top,.045f,WoodLight,lod==0?7:5);
            int cloth=g.Mark;
            Vector3 upper=top-Vector3.up*.09f,lower=upper-Vector3.up*(span*.55f);
            g.Quad(upper,upper+V(span*.70f,-.045f,.10f),lower+V(span*.70f,.05f,.10f),lower,field,true);
            g.Triangle(upper+V(span*.70f,-.045f,.10f),upper+V(span,-.11f,.04f),lower+V(span*.70f,.05f,.10f),field);
            g.Triangle(lower+V(span*.70f,.05f,.10f),upper+V(span,-.11f,.04f),upper+V(span*.70f,-.045f,.10f),field);
            if(lod==0){g.WeightSince(cloth,upper,upper+Vector3.right*span);g.Cone(top,.07f,.14f,Gold,6);}
        }

        static void Cottages(Geometry g,int lod)
        {
            House(g,-.55f,.05f,1.70f,1.60f,1.00f,.71f,lod==0?FiredClay:Straw,lod);
            Door(g,-.81f,-.75f,.77f,.37f);
            Window(g,-.08f,.59f,-.75f,lod);
            House(g,.91f,.29f,1.17f,1.18f,.83f,.50f,StrawLight,lod);
            Door(g,.92f,-.30f,.62f,.32f);
            Chimney(g,-.03f,.27f,1.83f,.24f,lod);
            // The short rail marks a reserved work yard beside the cottage entrance.
            const float fenceZ=-1.225f;
            foreach(float x in new[]{.27f,1.53f})
                g.Box(V(x,.19f,fenceZ),V(.11f,.38f,.09f),Wood);
            g.Box(V(.90f,.215f,fenceZ),V(1.35f,.075f,.065f),WoodLight);
            if(lod==0)g.Box(V(.90f,.10f,fenceZ),V(1.35f,.06f,.065f),WoodLight);
        }

        static readonly Color FallowSoil=new Color(.34f,.21f,.13f,0), FallowRidge=new Color(.46f,.30f,.19f,0);
        static readonly Color RichStalk=new Color(.96f,.76f,.30f,0), RichGrain=new Color(1,.89f,.46f,0);

        static void Farm(Geometry g,int lod,CropAppearance state)
        {
            // A tapered mill, broad linen sails and planted rows remain legible from the overview.
            var tower=V(-.90f,0,.24f);
            g.Frustum(tower,V(1.20f,0,1.20f),tower+Vector3.up*1.58f,V(.78f,0,.78f),Plaster,lod==0?8:6);
            g.Cone(tower+Vector3.up*1.56f,.63f,.61f,Tile,lod==0?8:6);
            var hub=V(-.90f,1.39f,-.40f);
            g.Bevel(V(-.90f,.22f,-.36f),V(.43f,.46f,.14f),.052f,Wood);
            g.Bevel(V(-.90f,.06f,-.47f),V(.57f,.12f,.27f),.036f,Stonework);
            g.Cylinder(hub-Vector3.forward*.08f,hub+Vector3.forward*.12f,.11f,Dark,6);
            if(lod==0){FieldWork(g,false,state);return;}
            bool fallow=state==CropAppearance.Fallow, rich=state==CropAppearance.Rich;
            g.Box(V(.89f,.055f,0),V(1.74f,.11f,2.42f),fallow?FallowSoil:Earth);
            for(int row=0;row<4;row++)
            {
                float x=.24f+row*.43f;
                var ridge=new Geometry();
                Color crop=fallow?FallowRidge:rich?RichStalk:Straw;
                Color tips=fallow?FallowRidge:rich?RichGrain:StrawLight;
                Gable(ridge,Vector3.zero,2.22f,.31f,fallow?.066f:.21f,row%2==0?tips:crop,crop,1);
                g.Append(ridge,Matrix4x4.TRS(V(x,.13f,0),Quaternion.Euler(0,90,0),Vector3.one));
                if(lod==0&&!fallow)for(int j=0;j<5;j++)
                {
                    float z=-.91f+j*.43f+(row%2)*.08f;
                    g.Cylinder(V(x,.18f,z),V(x+.07f,.43f,z+.05f),.027f,tips,4);
                    g.Cone(V(x+.07f,.37f,z+.05f),.052f,.14f,rich?RichGrain:Ochre,5);
                }
            }
        }

        static void IrrigatedField(Geometry g,int lod,CropAppearance state)
        {
            if(lod==0){FieldWork(g,true,state);return;}
            bool fallow=state==CropAppearance.Fallow, rich=state==CropAppearance.Rich;
            g.Box(V(0,.07f,0),V(3.45f,.14f,2.59f),fallow?FallowSoil:Earth);
            g.Box(V(0,.15f,0),V(.47f,.025f,2.51f),new Color(.20f,.57f,.67f,0));
            foreach(float bank in new[]{-.28f,.28f})
                g.Box(V(bank,.18f,0),V(.14f,.12f,2.48f),Stonework);
            for(int i=0;i<6;i++)
            {
                float x=(i<3?-1:1)*(.48f+i%3*.39f);
                Color crop=fallow?FallowRidge:rich?(i%2==0?RichStalk:RichGrain):i%2==0?Straw:StrawLight;
                g.Box(V(x,fallow?.152f:.19f,0),V(.22f,fallow?.078f:.17f,2.21f),crop);
                if(lod==0&&!fallow)for(int j=0;j<5;j++)
                    g.Cone(V(x,.26f,j*.42f-.88f),.083f,.18f+(j%2)*.10f,rich?RichGrain:StrawLight,4);
            }
            for(int i=0;i<(lod==0?5:3);i++)
                g.Box(V(0,.29f,-1.06f+i*(lod==0?.097f:.151f)),V(.77f,.085f,.088f),WoodLight);
            g.Bevel(V(0,.42f,1.12f),V(.73f,.62f,.27f),.08f,Wood);
            g.Cylinder(V(0,.57f,1.11f),V(0,.96f,1.11f),.035f,Steel,5);
            g.Cylinder(V(-.21f,.96f,1.11f),V(.21f,.96f,1.11f),.033f,WoodLight,5);
        }

        static void Pasture(Geometry g,int lod)
        {
            g.Box(V(0,.056f,0),V(3.47f,.11f,2.59f),Green);
            House(g,-.94f,.49f,1.25f,1.26f,.78f,.45f,Straw,lod);
            Door(g,-.94f,-.14f,.56f,.35f);
            for(int i=0;i<=5;i++)
            {
                float x=-1.67f+i*.67f;
                g.Box(V(x,.29f,-1.17f),V(.105f,.58f,.107f),Wood);
                if(i<5&&i!=2)g.Box(V(x+.335f,.33f,-1.17f),V(.68f,.09f,.10f),WoodLight);
            }
            foreach(var p in new[]{V(.65f,.34f,.42f),V(1.14f,.32f,-.49f)})
            {
                g.Ellipsoid(p,V(.27f,.24f,.38f),Plaster,lod==0?8:5,2);
                g.Ellipsoid(p+V(0,-.01f,-.33f),V(.135f,.12f,.165f),Dark,6,2);
                foreach(float x in new[]{-.13f,.13f})foreach(float z in new[]{-.19f,.19f})
                    g.Box(p+V(x,-.23f,z),V(.071f,.23f,.071f),Wood);
            }
        }

        static void Tavern(Geometry g,int lod)
        {
            House(g,0,.24f,2.96f,1.83f,1.66f,.69f,new Color(.51f,.21f,.13f,0),lod);
            Door(g,-.14f,-.68f,1.03f,.57f);
            foreach(float x in new[]{-1.02f,.91f})
            {
                Window(g,x,1.12f,-.68f,lod);
                g.Box(V(x,1.12f,-.83f),V(.22f,.22f,.042f),StrawLight);
            }
            Chimney(g,1.03f,.36f,2.68f,.35f,lod);
            Gable(g,V(.06f,1.48f,-1.03f),2.91f,.77f,.19f,Ochre,Plaster,lod);
            // Hanging tankard above the porch.
            g.Box(V(.68f,1.38f,-1.21f),V(.09f,.11f,.44f),Wood);
            g.Box(V(.68f,1.12f,-1.43f),V(.53f,.44f,.075f),Wood);
            g.Bevel(V(.63f,1.13f,-1.48f),V(.24f,.25f,.055f),.03f,Gold);
            g.Bevel(V(.63f,1.245f,-1.508f),V(.25f,.044f,.042f),.015f,Linen);
            g.Box(V(.79f,1.13f,-1.48f),V(.075f,.13f,.045f),Gold);
            g.Barrel(V(-1.18f,.27f,-1.05f),.22f,.51f,WoodLight,lod==0?9:6);
        }

        static void DrillYard(Geometry g,int lod)
        {
            g.Bevel(V(0,.08f,0),V(3.48f,.16f,2.62f),.10f,Earth);
            House(g,-.72f,.54f,1.92f,1.16f,1.21f,.43f,Slate,lod);
            Door(g,-.77f,-.04f,.75f,.37f);
            Pennant(g,V(-1.56f,0,-.93f),2.24f,.53f,Slate,lod);
            foreach(float x in new[]{.53f,1.25f})
            {
                g.Bevel(V(x,.14f,.45f),V(.52f,.28f,.59f),.06f,Stonework);
                g.Cylinder(V(x,.19f,.45f),V(x,1.04f,.45f),.08f,WoodLight,5);
                g.Bevel(V(x,.72f,.45f),V(.32f,.47f,.28f),.07f,Straw);
                g.Cylinder(V(x-.26f,.79f,.45f),V(x+.28f,.79f,.45f),.07f,Wood,lod==0?6:4);
                g.Cylinder(V(x,.75f,.29f),V(x,.75f,.32f),.154f,Tile,lod==0?8:5);
                g.Cylinder(V(x,.75f,.27f),V(x,.75f,.29f),.065f,Linen,lod==0?7:5);
            }
            g.Box(V(.79f,.17f,-.93f),V(1.63f,.27f,.26f),Wood);
            if(lod==0)for(int i=0;i<4;i++)
                g.Cylinder(V(.19f+i*.33f,.30f,-.91f),V(.22f+i*.33f,.48f,-.56f),.036f,LightSteel,4);
        }

        static void TimberCamp(Geometry g,int lod)
        {
            g.Bevel(V(-.75f,.10f,-.05f),V(1.70f,.20f,2.26f),.08f,Earth);
            int sides=lod==0?9:6;
            for(int layer=0;layer<3;layer++)for(int log=0;log<3-layer;log++)
            {
                float x=-1.25f+layer*.22f+log*.44f,y=.37f+layer*.36f,z=-1.09f+log*.06f;
                g.Cylinder(V(x,y,z),V(x,y,.95f),.225f,Wood,sides);
                g.Cylinder(V(x,y,z-.025f),V(x,y,z),.19f,Straw,sides);
            }
            foreach(float z in new[]{-.77f,.81f})g.Box(V(.78f,.09f,z),V(1.54f,.18f,.25f),Wood);
            g.Box(V(.62f,1.06f,.54f),V(.20f,2.12f,.20f),Wood);
            g.Box(V(.86f,1.98f,.54f),V(1.68f,.17f,.17f),WoodLight);
            g.Cylinder(V(.63f,1.19f,.54f),V(1.45f,1.94f,.54f),.085f,Wood,5);
            g.Cylinder(V(1.46f,.95f,.54f),V(1.46f,1.99f,.54f),.035f,Straw,5);
            g.Bevel(V(1.45f,.82f,.54f),V(.51f,.28f,.47f),.11f,Stonework);
            g.Rock(V(.62f,.32f,-.48f),.46f,Stonework,sides,3);
            if(lod==0)
            {
                g.Rock(V(1.18f,.20f,-.65f),.29f,Rock,7,2);
                g.Cylinder(V(.63f,.13f,-.29f),V(.63f,1.09f,.54f),.075f,WoodLight,5);
            }
        }

        static void Smithy(Geometry g,int lod)
        {
            House(g,-.45f,.22f,1.98f,1.54f,1.12f,.52f,Slate,lod);
            Door(g,-.56f,-.55f,.82f,.45f);
            Chimney(g,.77f,.31f,2.45f,.55f,lod);
            g.Bevel(V(.88f,.43f,-.30f),V(1.14f,.86f,.96f),.12f,Stonework);
            g.Box(V(.88f,.50f,-.80f),V(.70f,.48f,.08f),Dark);
            if(lod==0)g.surface=Surface.Ember;
            g.Box(V(.88f,.33f,-.86f),V(.56f,.16f,.11f),new Color(.95f,.40f,.10f,0));
            g.surface=Surface.Auto;
            g.Box(V(.86f,.32f,-.89f),V(.40f,.07f,.06f),Copper);
            if(lod==0)
            {
                // Incandescent coals sit behind the grate. Keep stone, iron and unlit fuel dark.
                g.surface=Surface.Ember;
                for(int i=0;i<7;i++)
                {
                    float x=.65f+i*.073f;
                    g.Ellipsoid(V(x,.343f+.027f*Mathf.Sin(i*2.4f),-.926f),V(.046f,.031f+(i%3)*.009f,.025f),
                        i%2==0?Amber:new Color(.72f,.24f,.06f,0),7,3);
                }
                g.surface=Surface.Metal;
                for(int i=0;i<5;i++)g.Cylinder(V(.625f+i*.125f,.238f,-.935f),V(.641f+i*.125f,.479f,-.891f),.011f,Dark,5);
                g.surface=Surface.Auto;
            }
            g.Bevel(V(.88f,.76f,-.77f),V(.92f,.14f,.16f),.037f,Rock);
            for(int i=0;i<(lod==0?4:2);i++)
                g.Box(V(.60f+i*(lod==0?.18f:.41f),.27f,-.862f),V(.034f,.14f,.024f),Dark);
            // Anvil on a stump in front of the workshop.
            var stump=V(-.03f,0,-1.08f);
            g.Cylinder(stump,stump+Vector3.up*.34f,.24f,Wood,lod==0?8:5);
            g.Box(stump+Vector3.up*.44f,V(.40f,.25f,.25f),Dark);
            g.Bevel(stump+Vector3.up*.60f,V(.57f,.13f,.28f),.05f,Steel);
            g.Spear(stump+V(.22f,.60f,0),stump+V(.52f,.62f,0),.13f,Steel);
            if(lod==0)Window(g,-1.02f,.67f,-.55f,lod);
        }

        static void Barracks(Geometry g,int lod)
        {
            // A fabric drill tent and a two-colour straw roundel share one baked mesh.
            Gable(g,V(-.57f,.16f,.18f),2.16f,1.90f,1.39f,Tile,Plaster,lod);
            var entrance=V(-1.665f,.18f,.18f);
            g.Quad(entrance+V(0,0,-.38f),entrance+V(0,.75f,-.10f),entrance+V(0,.75f,.10f),entrance+V(0,0,.38f),Wood,true);
            Pennant(g,V(1.26f,0,.68f),1.97f,.49f,Tile,lod);
            foreach(float x in new[]{.78f,1.30f})
                g.Cylinder(V(x,.03f,-.81f),V(1.04f,1.01f,-.52f),.065f,WoodLight,5);
            const float targetY=.94f;
            g.Cylinder(V(1.04f,targetY,-.63f),V(1.04f,targetY,-.48f),.45f,Straw,lod==0?12:8);
            g.Cylinder(V(1.04f,targetY,-.65f),V(1.04f,targetY,-.63f),.31f,Tile,lod==0?12:8);
            g.Cylinder(V(1.04f,targetY,-.67f),V(1.04f,targetY,-.65f),.15f,Plaster,lod==0?10:6);
            if(lod==0)
            {
                for(int i=0;i<2;i++)
                {
                    float x=-1.25f+i*1.32f;
                    g.Quad(V(x,.168f,-.78f),V(x,1.563f,.18f),V(x+.16f,1.563f,.18f),V(x+.16f,.168f,-.78f),Plaster,true);
                    g.Cylinder(V(x,1.54f,.18f),V(x,.035f,-1.22f),.021f,Straw,4);
                }
            }
        }

        static void Stables(Geometry g,int lod)
        {
            g.Box(V(0,.07f,.08f),V(3.05f,.14f,1.98f),Earth);
            foreach(float x in new[]{-1.35f,0,1.35f})foreach(float z in new[]{-.72f,.84f})
                g.Box(V(x,.69f,z),V(.15f,1.38f,.15f),Wood);
            Gable(g,V(0,1.40f,.06f),3.26f,1.95f,.52f,Green,Straw,lod);
            g.Box(V(0,.63f,.85f),V(2.84f,1.15f,.10f),Wood);
            foreach(float x in new[]{-.69f,.69f})
            {
                g.Box(V(x,.46f,-.73f),V(1.21f,.11f,.10f),WoodLight);
                g.Box(V(x,.19f,-.73f),V(1.21f,.12f,.10f),WoodLight);
            }
            Vector3 nose=V(-.64f,.96f,-.83f);
            g.Cylinder(nose+V(0,-.49f,.39f),nose+V(0,.07f,.11f),.145f,WoodLight,lod==0?7:5);
            g.Ellipsoid(nose,V(.175f,.206f,.30f),WoodLight,lod==0?8:6,3);
            g.Bevel(nose+V(0,-.06f,-.21f),V(.24f,.19f,.23f),.05f,Wood);
            foreach(float x in new[]{-.095f,.095f})g.Cone(nose+V(x,.12f,.12f),.06f,.17f,Wood,5);
            g.Bevel(V(.72f,.24f,-.99f),V(.73f,.45f,.51f),.08f,Straw);
            if(lod==0)
            {
                g.Box(V(.72f,.34f,-1.245f),V(.12f,.21f,.035f),WoodLight);
                g.Cylinder(V(-1.28f,.18f,.81f),V(-1.28f,1.31f,-.68f),.05f,WoodLight,5);
            }
        }

        static Geometry CivilianHat(int lod)
        {
            var g=new Geometry();
            if(lod==0)
            {
                g.Frustum(V(0,-.009f,.018f),V(.469f,0,.390f),V(0,.061f,0),V(.271f,0,.263f),Straw,10);
                g.Frustum(V(0,.058f,0),V(.271f,0,.263f),V(.012f,.154f,-.016f),V(.164f,0,.173f),StrawLight,10);
                g.Cylinder(V(0,.057f,0),V(0,.077f,0),.135f,Walnut,9);
                g.surface=Surface.Thatch;
                for(int j=0;j<3;j++)g.Torus(V(0,.001f+j*.010f,.013f),.181f-j*.014f,.005f,StrawLight,14);
                return g;
            }
            const int n=7;
            g.Frustum(V(0,-.02f,0),V(.66f,0,.61f),V(0,.045f,0),V(.38f,0,.36f),Straw,n);
            g.Frustum(V(0,.036f,0),V(.37f,0,.36f),V(0,.177f,0),V(.24f,0,.23f),StrawLight,n);
            return g;
        }

        static Geometry CivilianPack(int lod)
        {
            var g=new Geometry();
            g.Bevel(Vector3.zero,V(.35f,.41f,.22f),.065f,Straw);
            g.Box(V(0,.055f,0),V(.356f,.048f,.23f),Wood);
            if(lod==0)
            {
                SagLine(g,V(-.166f,.166f,-.099f),V(.16f,.147f,-.103f),.082f,.018f,Walnut);
                g.Box(V(0,0,-.117f),V(.048f,.34f,.03f),Wood);
                g.Cylinder(V(-.18f,.22f,0),V(.18f,.22f,0),.076f,Green,7);
            }
            return g;
        }

        sealed partial class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();readonly List<Color> colors=new List<Color>();readonly List<int> indices=new List<int>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color){Vector3 cross=Vector3.Cross(b-a,c-a);if(cross.sqrMagnitude<1e-14f)return;Vector3 normal=cross.normalized;
                // Unity normalizes normals of sub-millimetre end caps to zero. Keep their winding and unit length.
                if(normal==Vector3.zero)normal=cross/Mathf.Sqrt(cross.sqrMagnitude);
                int i=vertices.Count;faceSurfaces.Add(surface);vertices.Add(a);vertices.Add(b);vertices.Add(c);normals.Add(normal);normals.Add(normal);normals.Add(normal);colors.Add(color);colors.Add(color);colors.Add(color);indices.Add(i);indices.Add(i+1);indices.Add(i+2);}
            public void Gradient(Vector3 a,Vector3 b,Vector3 c,Color first,Color second,Color third)
            {
                int end=colors.Count;
                Triangle(a,b,c,first);
                if(colors.Count!=end){colors[end+1]=second;colors[end+2]=third;}
            }
            public void SmoothFace(Vector3 a,Vector3 b,Vector3 c,Color first,Color second,Color third,
                Vector3 normalA,Vector3 normalB,Vector3 normalC)
            {
                int start=vertices.Count;
                Gradient(a,b,c,first,second,third);
                if(vertices.Count==start)return;
                normals[start]=normalA;
                normals[start+1]=normalB;
                normals[start+2]=normalC;
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,bool doubleSided=false){Triangle(a,b,c,color);Triangle(a,c,d,color);if(doubleSided){Triangle(c,b,a,color);Triangle(d,c,a,color);}}
            public void Box(Vector3 center,Vector3 size,Color color)
            {
                float x=size.x*.5f,y=size.y*.5f,z=size.z*.5f;
                var a=center+V(-x,-y,-z);var b=center+V(x,-y,-z);var c=center+V(x,-y,z);var d=center+V(-x,-y,z);
                var up=V(0,size.y,0);
                Quad(a,b,c,d,color);Quad(d+up,c+up,b+up,a+up,color);
                Quad(a,a+up,b+up,b,color);Quad(b,b+up,c+up,c,color);
                Quad(c,c+up,d+up,d,color);Quad(d,d+up,a+up,a,color);
            }
            void ConvexTriangle(Vector3 a,Vector3 b,Vector3 c,Color color){if(Vector3.Dot(Vector3.Cross(b-a,c-a),a+b+c)<0)Triangle(c,b,a,color);else Triangle(a,b,c,color);}
            void ConvexQuad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color){ConvexTriangle(a,b,c,color);ConvexTriangle(a,c,d,color);}
            public void Append(Geometry other,Matrix4x4 matrix){int offset=vertices.Count;foreach(var w in other.flexibility)flexibility[offset+w.Key]=w.Value;foreach(var tag in other.faceSurfaces)faceSurfaces.Add(surface==Surface.Auto?tag:surface);foreach(var uv in other.loftUV)loftUV.Add(offset+uv.Key,uv.Value);for(int i=0;i<other.vertices.Count;i++){vertices.Add(matrix.MultiplyPoint3x4(other.vertices[i]));normals.Add(matrix.MultiplyVector(other.normals[i]).normalized);colors.Add(other.colors[i]);}foreach(int i in other.indices)indices.Add(offset+i);}
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
            public void FitFootprint(float width,float depth)
            {
                float factor=1;
                foreach(var v in vertices)
                    factor=Mathf.Min(factor,width/Mathf.Max(width,Mathf.Abs(v.x)),depth/Mathf.Max(depth,Mathf.Abs(v.z)));
                if(factor>=1)return;
                for(int i=0;i<vertices.Count;i++)
                {
                    var p=vertices[i];vertices[i]=V(p.x*factor,p.y,p.z*factor);
                    var normal=normals[i];normals[i]=V(normal.x,normal.y*factor,normal.z).normalized;
                }
            }

            public void SetAtGrade()
            {
                float bottom=float.PositiveInfinity;
                foreach(var p in vertices)bottom=Mathf.Min(bottom,p.y);
                for(int i=0;i<vertices.Count;i++)vertices[i]-=Vector3.up*bottom;
            }

            public Geometry StoneStrata()
            {
                var cut=new Geometry {surface=Surface.Outcrop};
                for(int i=0;i<vertices.Count;i+=3)
                {
                    var a=vertices[i];var b=vertices[i+1];var c=vertices[i+2];
                    // Split each exposed face within the original silhouette and keep all cooked-hull vertices.
                    float stratum=.31f+.09f*Mathf.Sin(i*.63f);
                    var seam=Vector3.Lerp(a,b,stratum);
                    var tint=colors[i];
                    Color mineral=i%4==0?new Color(.67f,.68f,.60f,0):new Color(.43f,.49f,.46f,0);
                    cut.Triangle(a,seam,c,Color.Lerp(tint,mineral,.54f));
                    cut.Triangle(seam,b,c,tint*(i%2==0?1.09f:.88f));
                }
                return cut;
            }

            public Mesh ProxyMesh(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetTriangles(indices,0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
