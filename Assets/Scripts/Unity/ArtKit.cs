// Copper Camp — the materials the 3D world is built from. The scene builder fills this in, so every
// material (and every shader variant it needs) is part of the build.
using UnityEngine;

namespace CopperCamp
{
    public class ArtKit : MonoBehaviour
    {
        public Material rock, dirt, timber, brick, steel, rail, ore, plain, glow, window, redGlow, greenGlow, particle, photo, snow, cloth, headframe, headframeBlack, headframeRed, mmSign, mineRock, mineFace, mineFloor, mineTimber, lagging, water;
        public Texture2D hero;

        public static ArtKit Instance;
        void Awake() { Instance = this; }
    }
}
