using System;
using Frontline.Core;
using UnityEngine;
using UnityEngine.Rendering;
#if !UNITY_WEBGL
using Unity.Jobs.LowLevel.Unsafe;
#endif

namespace Frontline.UnityGame
{
    static class CpuWork
    {
        internal static int TargetWorkers {get;private set;}=1;
        internal static bool SingleThreadQa {get;private set;}
        internal static int UnityWorkers
        {
            get
            {
#if !UNITY_WEBGL
                return JobsUtility.JobWorkerCount;
#else
                return 0;
#endif
            }
        }

        internal static bool GraphicsJobs
        {
            get
            {
                var mode=SystemInfo.renderingThreadingMode;
                return mode==RenderingThreadingMode.LegacyJobified||mode==RenderingThreadingMode.NativeGraphicsJobs||
                    mode==RenderingThreadingMode.NativeGraphicsJobsWithoutRenderThread||
                    mode==RenderingThreadingMode.NativeGraphicsJobsSplitThreading;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Configure()
        {
#if !UNITY_WEBGL && (UNITY_STANDALONE || UNITY_EDITOR)
            SingleThreadQa=Array.IndexOf(Environment.GetCommandLineArgs(),"--single-thread-qa")>=0;
            TargetWorkers=SingleThreadQa?1:Mathf.Clamp(SystemInfo.processorCount,1,Battle.TargetWorkerCap);
            // Include the calling thread in the budget. Respect lower engine/command-line limits.
            if(!Application.isEditor||SingleThreadQa)
                JobsUtility.JobWorkerCount=Mathf.Min(JobsUtility.JobWorkerCount,TargetWorkers-1);
#endif
        }
    }
}
