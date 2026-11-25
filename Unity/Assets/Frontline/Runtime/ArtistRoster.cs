using System;
using UnityEngine;

namespace Frontline.UnityGame
{
    public enum ArtistMotion : byte { Idle,Walk,Run,Slash,Thrust,Heavy,Bow,Block,Hit,Death,Cast,Throw,Ride,Work,Trot,Lance }
    [Serializable] public struct ArtistTake
    {
        public int first,intervals;
        public float seconds;
        public string source;
        public float Row(float t)=>first+Mathf.Clamp01(t)*intervals;
    }
    [Serializable] public sealed class ArtistSkeleton
    {
        public string name;
        public Texture2D palette;
        public Mesh body,mid,proxy;
        public ArtistTake[] takes;
        public Vector3[] saddle,hips;
        public Quaternion[] saddleRotation;
        public int boneCount,skinnedVertices;
    }
    [Serializable] public sealed class ArtistCostume
    {
        public string name;
        public int skeleton;
        public Mesh high,mid,proxy;
    }
    [PreferBinarySerialization]
    public sealed class ArtistRoster : ScriptableObject
    {
        public ArtistSkeleton[] skeletons;
        public ArtistCostume[] costumes;
    }
}
