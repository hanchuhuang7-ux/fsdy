using UnityEngine;

namespace Frontline.UnityGame
{
    public sealed class FrontierTerrainAsset : ScriptableObject
    {
        public const string Resource = "World/FrontierTerrain";
        public Mesh manorFloor,frontierFloor,shoreBarrier;
        public Mesh[] closeChunks,wideChunks;
    }
}
