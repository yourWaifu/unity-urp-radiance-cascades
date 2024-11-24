using InternalBridge;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RadianceCascadesPass : ScriptableRenderPass
{
    private readonly ProfilingSampler _profilingSampler;

    private RTHandle _RadianceCascades;
    private RTHandle _Cascades0;

    private RTHandle _BlurBuffer0;
    private RTHandle _BlurBuffer1;

    private ComputeShader _radianceCascadesCs;
    private readonly Material _blit;
    private readonly int _mainKernel;
    private readonly int _mergeKernel;


    public RadianceCascadesPass(ComputeShader radianceCascadesCs, Material blit)
    {
        _profilingSampler = new ProfilingSampler(nameof(RadianceCascadesPass));
        _radianceCascadesCs = radianceCascadesCs;
        _blit = blit;
        _mainKernel = _radianceCascadesCs.FindKernel("Main");
        _mergeKernel = _radianceCascadesCs.FindKernel("Merge");
        ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        var aspect = cameraTextureDescriptor.height / (float) cameraTextureDescriptor.width;

        var probesCount = 128;

        // 16 probes for 1st cascade (4 rays => 2x2)
        var desc = new RenderTextureDescriptor(probesCount * 2 * 3, (int) (probesCount * 2 * aspect))
        {
            colorFormat = RenderTextureFormat.ARGBFloat,
            enableRandomWrite = true,
        };
        RenderingUtils.ReAllocateIfNeeded(
            ref _RadianceCascades,
            desc,
            name: nameof(_RadianceCascades),
            filterMode: FilterMode.Point
        );

        // One cascade target!
        desc.width = probesCount * 2;
        desc.height = (int) (probesCount * 2 * aspect);
        RenderingUtils.ReAllocateIfNeeded(
            ref _Cascades0,
            desc,
            filterMode: FilterMode.Point,
            wrapMode: TextureWrapMode.Clamp,
            name: nameof(_Cascades0)
        );

        var desc1 = new RenderTextureDescriptor(cameraTextureDescriptor.width / 2, cameraTextureDescriptor.height / 2)
        {
            colorFormat = RenderTextureFormat.ARGB2101010,
            depthStencilFormat = GraphicsFormat.None,
        };
        RenderingUtils.ReAllocateIfNeeded(ref _BlurBuffer0, desc1, FilterMode.Bilinear);
        RenderingUtils.ReAllocateIfNeeded(ref _BlurBuffer1, desc1, FilterMode.Bilinear);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get();

        var colorTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;

        var colorTextureRT = colorTexture.rt;
        if (colorTextureRT == null)
        {
            return;
        }

        var colorTextureTexelSize = new Vector4(
            colorTextureRT.width,
            colorTextureRT.height,
            1.0f / colorTextureRT.width,
            1.0f / colorTextureRT.height
        );

        using (new ProfilingScope(cmd, _profilingSampler))
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            cmd.BeginSample("Cascades");

            cmd.SetRenderTarget(_RadianceCascades);
            cmd.ClearRenderTarget(RTClearFlags.Color, Color.black, 0, 0);

            // Shared
            var radianceCascadesRT = _RadianceCascades.rt;
            cmd.SetComputeVectorParam(
                _radianceCascadesCs,
                "_RadianceCascades_TexelSize",
                new Vector4(
                    radianceCascadesRT.width,
                    radianceCascadesRT.height,
                    1.0f / radianceCascadesRT.width,
                    1.0f / radianceCascadesRT.height
                )
            );
            cmd.SetComputeVectorParam(
                _radianceCascadesCs,
                "_CascadeRect",
                new Vector4(0.0f, 0.0f, radianceCascadesRT.width / 3.0f, radianceCascadesRT.height)
            );


            // Input
            cmd.SetComputeTextureParam(_radianceCascadesCs, _mainKernel, nameof(_RadianceCascades), _RadianceCascades);
            cmd.SetComputeTextureParam(_radianceCascadesCs, _mainKernel, nameof(_Cascades0), _Cascades0);


            cmd.SetComputeVectorParam(_radianceCascadesCs, "_ColorTexture_TexelSize", colorTextureTexelSize);
            cmd.SetComputeTextureParam(
                _radianceCascadesCs,
                _mainKernel,
                "_ColorTexture",
                colorTexture // gBuffer0 //
            );
            cmd.SetComputeTextureParam(
                _radianceCascadesCs,
                _mainKernel,
                "_NormalsTexture",
                renderingData.cameraData.renderer.GetGBuffer(2) // Normals
            );
            cmd.SetComputeTextureParam(
                _radianceCascadesCs,
                _mainKernel,
                "_DepthTexture",
                renderingData.cameraData.renderer.cameraDepthTargetHandle
            );


            cmd.DispatchCompute(
                _radianceCascadesCs,
                _mainKernel,
                radianceCascadesRT.width / 8 / 3,
                radianceCascadesRT.height / 8,
                1
            );


            // Merge
            cmd.SetComputeTextureParam(
                _radianceCascadesCs,
                _mergeKernel,
                nameof(_RadianceCascades),
                _RadianceCascades
            );
            cmd.SetComputeTextureParam(
                _radianceCascadesCs,
                _mergeKernel,
                nameof(_Cascades0),
                _Cascades0
            );
            cmd.DispatchCompute(
                _radianceCascadesCs,
                _mergeKernel,
                radianceCascadesRT.width / 8 / 3,
                radianceCascadesRT.height / 8,
                1
            );

            cmd.EndSample("Cascades");

            
            
            cmd.BeginSample("Blit");

            cmd.Blit(_Cascades0, _BlurBuffer0, _blit, 0);
            // cmd.Blit(_BlurBuffer0, _BlurBuffer1, _blit, 1);
            // cmd.Blit(_BlurBuffer1, _BlurBuffer0, _blit, 2);
            // cmd.Blit(_BlurBuffer0, _BlurBuffer1, _blit, 2);
            // cmd.Blit(_BlurBuffer1, colorTexture, _blit, 3);
            // cmd.Blit(_BlurBuffer0, colorTexture, _blit, 3);
            cmd.Blit(_BlurBuffer0, colorTexture, _blit, 3);

            const float scale = 0.2f;
            Blitter.BlitQuad(
                cmd,
                _RadianceCascades,
                new Vector4(1, 1f, 0, 0),
                new Vector4(3 * scale, scale, 0, 1.0f - scale),
                0,
                false
            );

            // Blitter.BlitQuad(
            //     cmd,
            //     _RadianceCascades,
            //     new Vector4(1, 1f, 0, 0),
            //     new Vector4(2, 1, 0, 0),
            //     0,
            //     true
            // );

            cmd.EndSample("Blit");
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        CommandBufferPool.Release(cmd);
    }
}
