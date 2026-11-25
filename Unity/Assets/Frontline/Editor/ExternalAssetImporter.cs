using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Frontline.Core;
using Frontline.UnityGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontline.Editor
{
    sealed class ExternalArtSettings : AssetPostprocessor
    {
        const string Root="Assets/Frontline/ThirdParty/";
        void OnPreprocessModel()
        {
            if(!assetPath.StartsWith(Root,StringComparison.Ordinal))return;
            var model=(ModelImporter)assetImporter;
            bool rig=assetPath.StartsWith(Root+"Actors/",StringComparison.Ordinal);
            model.importAnimation=rig;model.animationType=rig?ModelImporterAnimationType.Legacy:ModelImporterAnimationType.None;
            model.animationCompression=ModelImporterAnimationCompression.Off;
            model.isReadable=true;model.importCameras=false;model.importLights=false;
            model.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            model.materialLocation=ModelImporterMaterialLocation.InPrefab;
            model.meshCompression=ModelImporterMeshCompression.Off;
            model.importNormals=ModelImporterNormals.Import;model.importTangents=ModelImporterTangents.None;
        }
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(Root+"Resources/Scans/",StringComparison.Ordinal))return;
            var tex=(TextureImporter)assetImporter;
            bool crown=assetPath.EndsWith("FoliageViews.png",StringComparison.Ordinal);
            bool specimen=assetPath.Contains("Specimen");
            var settings=new TextureImporterSettings();tex.ReadTextureSettings(settings);
            settings.textureShape=TextureImporterShape.Texture2DArray;
            bool clothing=assetPath.Contains("Wardrobe");
            settings.flipbookColumns=crown?8:clothing?3:4;settings.flipbookRows=crown?6:clothing||specimen?3:4;
            tex.SetTextureSettings(settings);
            tex.sRGBTexture=!assetPath.EndsWith("Detail.png",StringComparison.Ordinal);
            tex.alphaSource=TextureImporterAlphaSource.FromInput;tex.alphaIsTransparency=crown||specimen&&tex.sRGBTexture;
            tex.mipmapEnabled=true;tex.mipMapsPreserveCoverage=crown;tex.alphaTestReferenceValue=.27f;tex.anisoLevel=crown?2:4;tex.filterMode=FilterMode.Trilinear;
            tex.wrapMode=crown?TextureWrapMode.Clamp:TextureWrapMode.Repeat;
            tex.npotScale=TextureImporterNPOTScale.None;tex.isReadable=false;
            foreach(var target in new[]{"Standalone","WebGL"})
            {
                var opt=tex.GetPlatformTextureSettings(target);opt.name=target;opt.overridden=true;
                opt.maxTextureSize=4096;opt.format=target=="Standalone"?TextureImporterFormat.DXT5:TextureImporterFormat.ETC2_RGBA8;
                tex.SetPlatformTextureSettings(opt);
            }
        }
    }

    public static partial class ExternalAssetImporter
    {
        internal const string Third="Assets/Frontline/ThirdParty";
        internal const string Output="Assets/Frontline/Models/Resources/Models";
        internal static GameObject Source(string name,bool actor=false)
        {
            var p=Third+(actor?"/Actors/":"/Art/")+name+".fbx";
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if(!asset)throw new InvalidDataException("Missing verified CC0 FBX "+p);
            return asset;
        }

        public static void BakeArt()
        {
            EstateModels();BakeSkins();AssetDatabase.SaveAssets();
            Debug.Log("FRONTLINE_CC0_ART_OK "+Exported.Count+" static mesh references and a weighted ensemble");
        }

        public static void Inspect()
        {
            foreach(string n in new[]{"Castellan","Forester","Falconer","Yeoman","Goodwife","Horse"})
            {
                var go=Source(n,true);var skins=go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var clips=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(go)).OfType<AnimationClip>().Where(a=>!a.name.StartsWith("__")).ToArray();
                Debug.Log("CC0_AVATAR "+n+" meshes="+string.Join(",",skins.Select(s=>s.name+"@"+s.sharedMesh.vertexCount))+" clips="+string.Join(",",clips.Select(c=>c.name+"="+c.length.ToString("F2"))));
                if(skins.Length==0||clips.Length==0)throw new InvalidDataException("Unbound artist rig "+n);
            }
            foreach(string n in new[]{"building_castle_red","House_1","Inn","Mill","siege-catapult","island_tree_01"})
            {
                var go=Source(n);var mesh=go.GetComponentsInChildren<MeshFilter>(true);
                var names=mesh.Where(m=>m.name.StartsWith("High")).Take(4).Select(m=>m.name+" "+m.GetComponent<MeshRenderer>().bounds+" "+string.Join(",",m.GetComponent<MeshRenderer>().sharedMaterials.Select(a=>a?a.name:"null")));
                Debug.Log("CC0_STATIC "+n+" "+string.Join("/",names));
            }
            foreach(string n in new[]{"LandscapeColor","LandscapeDetail","SpecimenColor","FoliageViews"})
            {
                var t=Resources.Load<Texture2DArray>("Scans/"+n);
                if(!t)throw new Exception("Missing texture array "+n);
                Debug.Log("CC0_ARRAY "+n+" "+t.width+"x"+t.height+"x"+t.depth+" "+t.format);
            }
        }
    }
}
