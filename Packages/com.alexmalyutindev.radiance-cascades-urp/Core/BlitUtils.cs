using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace AlexMalyutinDev.RadianceCascades
{
    public class BlitUtils
    {
        private static Mesh s_QuadMesh;
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");

        public static void Blit(CommandBuffer cmd, Material material, int pass)
        {
            Initialize();
            cmd.DrawMesh(s_QuadMesh, Matrix4x4.identity, material, pass);
        }
        
        public static void BlitTexture(CommandBuffer cmd, Texture texture, Material material, int pass)
        {
            Initialize();
            var props = new MaterialPropertyBlock();
            props.SetTexture(BlitTextureId, texture);
            cmd.DrawMesh(s_QuadMesh, Matrix4x4.identity, material, 0, pass, props);
        }
#if UNITY_6000_0_OR_NEWER
        public static void BlitTexture(RasterCommandBuffer cmd, TextureHandle texture, Material material, int pass)
        {
            Initialize();
            var props = new MaterialPropertyBlock();
            props.SetTexture(BlitTextureId, texture);
            cmd.DrawMesh(s_QuadMesh, Matrix4x4.identity, material, 0, pass, props);
        }
#endif

        public static void Initialize()
        {
            if (!s_QuadMesh)
            {
                /*UNITY_NEAR_CLIP_VALUE*/
                float nearClipZ = -1;
                if (SystemInfo.usesReversedZBuffer)
                {
                    nearClipZ = 1;
                }

                s_QuadMesh = new Mesh();
                s_QuadMesh.vertices = GetQuadVertexPosition(nearClipZ);
                s_QuadMesh.uv = GetQuadTexCoord();
                s_QuadMesh.triangles = new int[6] { 0, 1, 2, 0, 2, 3 };
            }
        }

        // Should match Common.hlsl
        public static Vector3[] GetQuadVertexPosition(float z /*= UNITY_NEAR_CLIP_VALUE*/)
        {
            var r = new Vector3[4];
            for (uint i = 0; i < 4; i++)
            {
                uint topBit = i >> 1;
                uint botBit = (i & 1);
                float x = topBit;
                float y = 1 - (topBit + botBit) & 1; // produces 1 for indices 0,3 and 0 for 1,2
                r[i] = new Vector3(x, y, z);
            }

            return r;
        }

        // Should match Common.hlsl
        public static Vector2[] GetQuadTexCoord()
        {
            var r = new Vector2[4];
            for (uint i = 0; i < 4; i++)
            {
                uint topBit = i >> 1;
                uint botBit = (i & 1);
                float u = topBit;
                float v = (topBit + botBit) & 1; // produces 0 for indices 0,3 and 1 for 1,2
                if (SystemInfo.graphicsUVStartsAtTop)
                    v = 1.0f - v;

                r[i] = new Vector2(u, v);
            }

            return r;
        }
    }
}
