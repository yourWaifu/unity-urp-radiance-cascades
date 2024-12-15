using AlexMalyutinDev.RadianceCascades.Voxelization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades
{
    public class Voxelizator
    {
        private const RenderTextureMemoryless RenderTextureMemorylessAll =
            RenderTextureMemoryless.Color |
            RenderTextureMemoryless.Depth |
            RenderTextureMemoryless.MSAA;

        public static readonly int DummyID = Shader.PropertyToID(nameof(DummyID));
        public static readonly int Resolution = Shader.PropertyToID(nameof(Resolution));

        private readonly Shader _shader;
        private readonly ComputeShader _compute;
        private readonly ShaderTagId _shaderTagId;

        private readonly RenderTextureDescriptor _dummyDesc;

        // Buffer to store all appeared voxels
        private ComputeBuffer _rawVoxelBuffer;

        public Voxelizator(Shader voxelizatorShader, ComputeShader voxelizatorCompute)
        {
            _shaderTagId = new ShaderTagId("UniversalGBuffer");
            _shader = voxelizatorShader;
            _compute = voxelizatorCompute;

            _dummyDesc = new RenderTextureDescriptor()
            {
                colorFormat = RenderTextureFormat.R8,
                dimension = TextureDimension.Tex2D,
                memoryless = RenderTextureMemorylessAll,
                msaaSamples = 1,
                sRGB = false,
            };
        }

        public void VoxelizeGeometry(
            CommandBuffer cmd,
            ref ScriptableRenderContext context,
            ref RenderingData renderingData,
            ref RTHandle targetVolume
        )
        {
            // TODO:
            var volumeResolution = 256;

            ReAllocateIfNeeded(ref _rawVoxelBuffer, volumeResolution, VoxelData.Size, ComputeBufferType.Append);

            var desc = new RenderTextureDescriptor(volumeResolution, volumeResolution, RenderTextureFormat.ARGB32)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = volumeResolution,
            };
            RenderingUtils.ReAllocateIfNeeded(ref targetVolume, desc);

            // NOTE: Collect voxels into compute buffer

            // Prepare
            var drawingSettings = RenderingUtils.CreateDrawingSettings(
                _shaderTagId,
                ref renderingData,
                SortingCriteria.None
            );
            drawingSettings.overrideShader = _shader;
            drawingSettings.overrideShaderPassIndex = 0;
            drawingSettings.perObjectData = PerObjectData.None;


            var rendererListParams = new RendererListParams(
                renderingData.cullResults, // TODO: Culler camera
                drawingSettings,
                FilteringSettings.defaultValue
            );

            var rendererList = context.CreateRendererList(ref rendererListParams);

            // Rendering
            cmd.GetTemporaryRT(DummyID, _dummyDesc);
            cmd.SetRenderTarget(DummyID, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
            cmd.SetRandomWriteTarget(1, _rawVoxelBuffer);
            cmd.SetGlobalInt(Resolution, volumeResolution);

            var view = CreateViewMatrix(renderingData.cameraData.worldSpaceCameraPos, Quaternion.identity);
            var proj = CreateBoxProjection(5); // TODO
            cmd.SetViewProjectionMatrices(view, proj);

            cmd.SetGlobalInt("Axis", 0);
            cmd.DrawRendererList(rendererList);

            // TODO: Combine voxels into Texture3D
            
        }

        public static void ReAllocateIfNeeded(
            ref ComputeBuffer buffer,
            int count,
            int stride,
            ComputeBufferType type = ComputeBufferType.Default,
            ComputeBufferMode usage = ComputeBufferMode.Immutable
        )
        {
            if (buffer == null || buffer.count != count)
            {
                buffer?.Release();
                buffer = new ComputeBuffer(count, stride, type, usage);
            }
        }

        public static Matrix4x4 CreateViewMatrix(Vector3 position, Quaternion rotation)
        {
            var view = Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
            if (SystemInfo.usesReversedZBuffer)
            {
                view.m20 = -view.m20;
                view.m21 = -view.m21;
                view.m22 = -view.m22;
                view.m23 = -view.m23;
            }

            return view;
        }

        public static Matrix4x4 CreateBoxProjection(float extend)
        {
            return Matrix4x4.Ortho(-extend, extend, -extend, extend, -extend, extend);
        }
    }
}
