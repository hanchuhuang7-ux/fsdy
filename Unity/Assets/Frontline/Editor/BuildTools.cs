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
            PlayerSettings.companyName="Frontline Lab";PlayerSettings.productName="Frontline Lab";PlayerSettings.bundleVersion="3.0.0";PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;PlayerSettings.colorSpace=ColorSpace.Gamma;
            PlayerSettings.MTRendering=true;
            // With D3D11 windowed on the target Intel GPU, flip presentation paced even uncapped
            // frames to the desktop refresh interval. Use the measured windowed presentation path.
            PlayerSettings.useFlipModelSwapchain=false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            PlayerSettings.WebGL.threadsSupport=false;
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input=settings.FindProperty("activeInputHandler");
            if(input!=null)input.intValue=0;
            // PlayerSettings.graphicsJobs affects only the active target, which may be WebGL in Both().
            PlatformOption(settings,"m_BuildTargetGraphicsJobs","WindowsStandaloneSupport").FindPropertyRelative("m_GraphicsJobs").boolValue=true;
            PlatformOption(settings,"m_BuildTargetGraphicsJobMode","WindowsStandaloneSupport").FindPropertyRelative("m_GraphicsJobMode").intValue=(int)GraphicsJobMode.Legacy;
            PlatformOption(settings,"m_BuildTargetGraphicsJobs","WebGLSupport").FindPropertyRelative("m_GraphicsJobs").boolValue=false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone,ManagedStrippingLevel.Low);PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL,ManagedStrippingLevel.Low);
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;PlayerSettings.WebGL.decompressionFallback=true;PlayerSettings.WebGL.dataCaching=true;PlayerSettings.WebGL.template="PROJECT:Frontline";
            QualitySettings.vSyncCount=0;BattleView.ApplyQualityPreset(2);QualitySettings.shadowDistance=72;
            // All scene/UI content is created at runtime, so retain its required shaders explicitly.
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);var shaders=graphics.FindProperty("m_AlwaysIncludedShaders");var names=new[]{"UI/Default"};shaders.arraySize=names.Length;for(int i=0;i<names.Length;i++){var shader=Shader.Find(names[i]);if(!shader)throw new Exception("Missing shader "+names[i]);shaders.GetArrayElementAtIndex(i).objectReferenceValue=shader;}graphics.FindProperty("m_InstancingStripping").intValue=2 /* KeepAll in GraphicsSettings */;graphics.ApplyModifiedPropertiesWithoutUndo();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--reuse-baked-models")<0)ModelBaker.Bake();
            var font=AssetImporter.GetAtPath("Assets/Frontline/Resources/NotoSansCJKsc-Regular.otf") as TrueTypeFontImporter;if(font!=null){font.fontTextureCase=FontTextureCase.Dynamic;font.includeFontData=true;font.SaveAndReimport();}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Directory.CreateDirectory("Assets/Scenes");EditorSceneManager.SaveScene(scene,Scene);EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Scene,true)};AssetDatabase.SaveAssets();
        }
        static SerializedProperty PlatformOption(SerializedObject settings,string field,string platform)
        {
            var entries=settings.FindProperty(field);
            for(int i=0;i<entries.arraySize;i++)
            {
                var entry=entries.GetArrayElementAtIndex(i);
                if(entry.FindPropertyRelative("m_BuildTarget").stringValue==platform)return entry;
            }
            int index=entries.arraySize;
            entries.arraySize++;
            var added=entries.GetArrayElementAtIndex(index);
            added.FindPropertyRelative("m_BuildTarget").stringValue=platform;
            return added;
        }

        static string Root {get{return Directory.GetParent(Application.dataPath).Parent.FullName;}}
        static void WindowsPlayer()=>Build(BuildTarget.StandaloneWindows64,Path.Combine(Root,"Builds/Windows/FrontlineLab.exe"));
        static void WebPlayer()=>Build(BuildTarget.WebGL,Path.Combine(Root,"dist"));
        [MenuItem("Frontline/Build Windows")] public static void Windows(){Prepare();WindowsPlayer();}
        [MenuItem("Frontline/Build WebGL")] public static void Web(){Prepare();WebPlayer();}
        public static void ProfileBoth(){Prepare();Build(BuildTarget.StandaloneWindows64,Path.Combine(Root,"Builds/WindowsProfile/FrontlineLab.exe"),BuildOptions.Development);Build(BuildTarget.WebGL,Path.Combine(Root,"Builds/WebProfile"),BuildOptions.Development);}
        public static void Both(){Prepare();WindowsPlayer();WebPlayer();}
        static void Build(BuildTarget target,string output,BuildOptions extra=BuildOptions.None)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            bool scriptsOnly=Array.IndexOf(Environment.GetCommandLineArgs(),"--scripts-only")>=0;
            var options=BuildOptions.StrictMode|extra|(scriptsOnly?BuildOptions.BuildScriptsOnly:
                BuildOptions.CleanBuildCache|BuildOptions.DetailedBuildReport);
            bool windows=target==BuildTarget.StandaloneWindows64;
            bool originalTiming=PlayerSettings.enableFrameTimingStats;
            BuildReport report;
            try
            {
                PlayerSettings.enableFrameTimingStats=windows;
                report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=new[]{Scene},target=target,locationPathName=output,options=options
                });
            }
            finally {PlayerSettings.enableFrameTimingStats=originalTiming;}
            var summary=report.summary;
            if(summary.result!=BuildResult.Succeeded)throw new Exception(target+" build "+summary.result);
            if(!scriptsOnly)
            {
                string folder=Path.Combine(Root,"Builds","Reports",PlayerSettings.bundleVersion);
                Directory.CreateDirectory(folder);
                string filename=Path.Combine(folder,target.ToString());
                // Unity replaces this native report on the next target in Both().
                File.Copy(Path.Combine(Root,"Unity","Library","LastBuild.buildreport"),filename+".buildreport",true);
                File.WriteAllText(filename+".json",Json.Write(new {
                    version=PlayerSettings.bundleVersion,unity=Application.unityVersion,target=target.ToString(),
                    colorSpace=PlayerSettings.colorSpace.ToString(),graphicsApis=Array.ConvertAll(PlayerSettings.GetGraphicsAPIs(target),api=>api.ToString()),
                    useFlipModelSwapchain=windows&&PlayerSettings.useFlipModelSwapchain,
                    multiThreadedRendering=windows&&PlayerSettings.MTRendering,graphicsJobs=windows,graphicsJobMode=windows?"Legacy":"Off",
                    webGLThreadsSupport=PlayerSettings.WebGL.threadsSupport,frameTimingStats=windows,
                    guid=summary.guid.ToString(),options=summary.options.ToString(),result=summary.result.ToString(),
                    bytes=(long)summary.totalSize,seconds=summary.totalTime.TotalSeconds,steps=report.steps.Length,
                    files=report.GetFiles().Length,packedArchives=report.packedAssets?.Length??0,warnings=summary.totalWarnings,errors=summary.totalErrors
                }));
            }
            Debug.Log("FRONTLINE_BUILD_OK "+target+" "+summary.totalSize+" bytes "+summary.totalTime);
        }
        public static void CheckPhysics(){EditorApplication.EnterPlaymode();}
    }
}




