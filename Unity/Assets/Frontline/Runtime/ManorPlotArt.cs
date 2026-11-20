using Frontline.Core;
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
        public const float BuiltPadHalfWidth=2.10f,BuiltPadHalfDepth=1.57f;
        public static float HalfWidth(bool hall)=>hall?FrontierMap.CastleHalfWidth:1.84f;
        public static float HalfDepth(bool hall)=>hall?FrontierMap.CastleHalfDepth:1.40f;
        public static Vector3 BuildingScale(string kind) => kind switch {
            "chapel" => new Vector3(1.11f,1.64f,1.10f),
            "granary" => new Vector3(1.14f,1.43f,1.11f),
            "tavern" or "barracks" => new Vector3(1.14f,1.37f,1.10f),
            Manor.Bakery => new Vector3(1.12f,1.39f,1.10f),
            Manor.Weaver => new Vector3(1.14f,1.30f,1.11f),
            "cottage" => new Vector3(1.10f,1.24f,1.08f),
            _ => Vector3.one
        };
        public const float BackyardHalfWidth=.41f, BackyardHalfDepth=.24f, BackyardHeight=.78f, BackyardDepthScale=.86f;
        public static readonly Vector3 BackyardOrigin=new Vector3(.97f,0,-.94f);
        public static readonly Vector3 BackyardScale=new Vector3(1,1,BackyardDepthScale);
        public static readonly Vector3 ForgeMouth=new Vector3(.88f,.35f,-.95f);
        public static readonly Vector3 ForgeChimney=new Vector3(.77f,2.49f,.31f);
        public static readonly Vector3 CottageChimney=new Vector3(-.03f,1.87f,.27f);
        public static readonly Vector3 MillHub=new Vector3(-.90f,1.39f,-.55f);
        public static readonly Vector3 BakeryHub=new Vector3(.82f,1.48f,-.70f);
        public static readonly Vector3 BakeryChimney=new Vector3(-1.18f,2.27f,.32f);
    }
}
