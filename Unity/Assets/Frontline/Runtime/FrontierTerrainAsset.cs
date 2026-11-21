using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed class FrontierTerrainAsset : ScriptableObject
    {
        public const string Resource = "World/FrontierTerrain";
        public Mesh manorFloor,frontierFloor,shoreBarrier,distantRelief,lakeSurface;
        public Mesh[] closeChunks,wideChunks;
    }
}
