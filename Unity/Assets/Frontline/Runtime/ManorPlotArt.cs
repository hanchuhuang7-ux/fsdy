using UnityEngine;

namespace Frontline.UnityGame
{
    public static class ManorPlotArt
    {
        public enum CropAppearance : byte { Regular, Fallow, Rich }
        public const float RetinuePostRadius = .62f;
        public static string CropModel(string kind,CropAppearance state) => state switch {
            CropAppearance.Fallow => kind+"_fallow", CropAppearance.Rich => kind+"_rich", _ => kind
        };
        public static float HalfWidth(bool hall)=>hall?2.14f:1.84f;
        public static float HalfDepth(bool hall)=>hall?1.66f:1.40f;
        public const float BackyardHalfWidth=.41f, BackyardHalfDepth=.24f, BackyardHeight=.78f, BackyardDepthScale=.86f;
        public static readonly Vector3 BackyardOrigin=new Vector3(.97f,0,-.94f);
        public static readonly Vector3 BackyardScale=new Vector3(1,1,BackyardDepthScale);
        public static readonly Vector3 ForgeMouth=new Vector3(.88f,.35f,-.95f);
        public static readonly Vector3 ForgeChimney=new Vector3(.77f,2.49f,.31f);
        public static readonly Vector3 CottageChimney=new Vector3(-.03f,1.87f,.27f);
        public static readonly Vector3 MillHub=new Vector3(-.90f,1.39f,-.55f);
    }
}
