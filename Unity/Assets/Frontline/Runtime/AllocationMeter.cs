using System;
namespace Frontline.UnityGame
{
    // Some Unity runtimes expose this API but always return zero. Calibrate first.
    public static class AllocationMeter
    {
        public static readonly bool Available=Probe();
        static bool Probe()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            long before=GC.GetAllocatedBytesForCurrentThread();var calibration=new byte[8192];calibration[0]=1;GC.KeepAlive(calibration);return GC.GetAllocatedBytesForCurrentThread()-before>=8192;
#endif
        }
        public static long Read()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return -1;
#else
            return Available?GC.GetAllocatedBytesForCurrentThread():-1;
#endif
        }
    }
}
