using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed class FrontierTerrainAsset : ScriptableObject
    {
        public const string Resource = "World/FrontierTerrain";
        public Vector4 shoreRect;
        public Mesh manorFloor,frontierFloor,shoreBarrier,distantRelief,lakeSurface;
        public Mesh[] closeChunks,wideChunks;
    }
}
