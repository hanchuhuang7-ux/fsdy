using System;
using System.IO;
using Frontline.Core;
using Frontline.UnityGame;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace Frontline.Editor
{
    public static class BuildTools
    {
        public const string Scene="Assets/Scenes/Frontline.unity";
        [MenuItem("Frontline/Prepare project")] public static void Prepare()
        {
            PlayerSettings.companyName="Frontline Lab";PlayerSettings.productName="Frontline Lab";PlayerSettings.bundleVersion="0.7.0";PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;PlayerSettings.colorSpace=ColorSpace.Gamma;
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);var input=settings.FindProperty("activeInputHandler");if(input!=null){input.intValue=0;settings.ApplyModifiedPropertiesWithoutUndo();}
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone,ManagedStrippingLevel.Low);PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL,ManagedStrippingLevel.Low);
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;PlayerSettings.WebGL.decompressionFallback=true;PlayerSettings.WebGL.dataCaching=true;PlayerSettings.WebGL.template="PROJECT:Frontline";
            QualitySettings.vSyncCount=0;QualitySettings.antiAliasing=2;QualitySettings.shadowDistance=65;QualitySettings.shadows=ShadowQuality.All;
            // All scene/UI content is created at runtime, so retain its required shaders explicitly.
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);var shaders=graphics.FindProperty("m_AlwaysIncludedShaders");var names=new[]{"UI/Default"};shaders.arraySize=names.Length;for(int i=0;i<names.Length;i++){var shader=Shader.Find(names[i]);if(!shader)throw new Exception("Missing shader "+names[i]);shaders.GetArrayElementAtIndex(i).objectReferenceValue=shader;}graphics.FindProperty("m_InstancingStripping").intValue=2 /* KeepAll in GraphicsSettings */;graphics.ApplyModifiedPropertiesWithoutUndo();
            ModelBaker.Bake();
            var font=AssetImporter.GetAtPath("Assets/Frontline/Resources/NotoSansCJKsc-Regular.otf") as TrueTypeFontImporter;if(font!=null){font.fontTextureCase=FontTextureCase.Dynamic;font.includeFontData=true;font.SaveAndReimport();}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,Scene);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true)};AssetDatabase.SaveAssets();
        }
        static string Root {get{return Directory.GetParent(Application.dataPath).Parent.FullName;}}
        [MenuItem("Frontline/Build Windows")] public static void Windows(){Prepare();Build(BuildTarget.StandaloneWindows64,Path.Combine(Root,"Builds/Windows/FrontlineLab.exe"));}
        [MenuItem("Frontline/Build WebGL")] public static void Web(){Prepare();Build(BuildTarget.WebGL,Path.Combine(Root,"dist"));}
        public static void ProfileBoth(){Prepare();Build(BuildTarget.StandaloneWindows64,Path.Combine(Root,"Builds/WindowsProfile/FrontlineLab.exe"),BuildOptions.Development);Build(BuildTarget.WebGL,Path.Combine(Root,"Builds/WebProfile"),BuildOptions.Development);}
        public static void Both(){Windows();Web();}
        static void Build(BuildTarget target,string output,BuildOptions extra=BuildOptions.None){Directory.CreateDirectory(Path.GetDirectoryName(output));var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},target=target,locationPathName=output,options=BuildOptions.StrictMode|BuildOptions.CleanBuildCache|extra});if(report.summary.result!=BuildResult.Succeeded)throw new Exception(target+" build "+report.summary.result);Debug.Log("FRONTLINE_BUILD_OK "+target+" "+report.summary.totalSize+" bytes "+report.summary.totalTime);}
        public static void CheckPhysics(){EditorApplication.EnterPlaymode();}
    }
}




