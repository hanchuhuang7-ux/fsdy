using UnityEngine;

namespace Frontline.UnityGame
{
    // Body support and solid prop envelopes. Cloth and weapon animation are outside these shapes.
    public static class ContactShape
    {
        public const float InfantryHeight=1.51f,InfantryCenterY=-.035f;
        public const float HorseCenterY=-.41f,CartCenterY=-.40f;
        public const float BannerRadius=.085f,BannerHeight=2.77f;
        public static readonly Vector3 BannerStand=new Vector3(.54f,.13f,.54f);
        public static readonly Vector3 BarricadeSize=new Vector3(1.04f,1.4f,2.52f);

        internal static float InfantryRadius(string type)
        {
            switch(type)
            {
                case "hero_vanguard": return .36f;
                case "giant": return .35f;
                case "shield": return .33f;
                case "hero_commander": return .31f;
                case "hero_ranger": return .29f;
                default: return .27f;
            }
        }

        internal static void SetSolver(Rigidbody body,float maxDepenetration)
        {
            body.solverIterations=8;
            body.solverVelocityIterations=2;
            body.maxDepenetrationVelocity=maxDepenetration;
        }
    }
}
