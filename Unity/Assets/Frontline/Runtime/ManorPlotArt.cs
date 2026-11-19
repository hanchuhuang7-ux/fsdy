using UnityEngine;

namespace Frontline.UnityGame
{
    public static class ManorPlotArt
    {
        public static float HalfWidth(bool hall)=>hall?2.14f:1.84f;
        public static float HalfDepth(bool hall)=>hall?1.66f:1.40f;
        public static readonly Vector3 ForgeMouth=new Vector3(.88f,.35f,-.95f);
        public static readonly Vector3 ForgeChimney=new Vector3(.77f,2.49f,.31f);
        public static readonly Vector3 CottageChimney=new Vector3(-.03f,1.87f,.27f);
        public static readonly Vector3 MillHub=new Vector3(-.90f,1.39f,-.55f);
    }
}
