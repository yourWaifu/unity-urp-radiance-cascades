using System;
using AlexMalyutinDev.RadianceCascades;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

public class RadianceCascadesPass : ScriptableRenderPass, IDisposable
{
    private const int CascadesCount = 5;
    private static readonly string[] CascadeNames = GenNames("_Cascade", CascadesCount);
    private static Vector2Int[] Resolutions =
    {
        new(32 * 16, 32 * 9), // 256x144 probes0
        new(32 * 10, 32 * 6), // 160x96 probes0
        new(32 * 7, 32 * 4), // 112x64 probes0
        new(32 * 4, 32 * 3), // 64x48 probes0
        new(32 * 3, 32 * 2), // 48x32 probes0
    };

    private readonly ProfilingSampler _profilingSampler;
    private readonly Material _blit;
    private readonly ComputeShader _radianceCascadesCs;
    private readonly RadianceCascadeCS _radianceCascadeCs;
    private readonly bool _showDebugPreview;

    private readonly RTHandle[] _cascades = new RTHandle[CascadesCount];


    public RadianceCascadesPass(
        RadianceCascadeResources resources,
        bool showDebugView
    )
    {
        _profilingSampler = new ProfilingSampler(nameof(RadianceCascadesPass));
        _radianceCascadeCs = new RadianceCascadeCS(resources.RadianceCascades);
        _showDebugPreview = showDebugView;
        _blit = resources.BlitMaterial;

        // BUG: Configuring with Depth and Color buffer dependency will cause to additional
        // resolve of this buffers before RadianceCascadesPass
        // ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        // TODO: Resolution settings?
        var desc = new RenderTextureDescriptor(
            Resolutions[0].x,
            Resolutions[0].y
        )
        {
            colorFormat = RenderTextureFormat.ARGBHalf,
            enableRandomWrite = true,
        };

        for (int i = 0; i < _cascades.Length; i++)
        {
            RenderingUtils.ReAllocateIfNeeded(
                ref _cascades[i],
                desc,
                filterMode: FilterMode.Point,
                wrapMode: TextureWrapMode.Clamp,
                name: CascadeNames[i]
            );
        }
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        var cmd = CommandBufferPool.Get();

        var colorTexture = renderingData.cameraData.renderer.cameraColorTargetHandle;
        var depthTexture = renderingData.cameraData.renderer.cameraDepthTargetHandle;

        var colorTextureRT = colorTexture.rt;
        if (colorTextureRT == null)
        {
            return;
        }

        using (new ProfilingScope(cmd, _profilingSampler))
        {
            RenderCascades(renderingData, cmd, colorTexture, depthTexture);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        CommandBufferPool.Release(cmd);
    }

    private void RenderCascades(
        RenderingData renderingData,
        CommandBuffer cmd,
        RTHandle colorTexture,
        RTHandle depthTexture
    )
    {
        var sampleKey = "RenderCascades";
        cmd.BeginSample(sampleKey);
        {
            for (int level = 0; level < _cascades.Length; level++)
            {
                // TODO: Use Hi-Z Depth
                _radianceCascadeCs.RenderCascade(
                    cmd,
                    colorTexture,
                    depthTexture,
                    2 << level,
                    level,
                    _cascades[level]
                );
            }
        }
        cmd.EndSample(sampleKey);

        if (_showDebugPreview)
        {
            PreviewCascades(cmd, _cascades);
        }

        sampleKey = "MergeCascades";
        cmd.BeginSample(sampleKey);
        {
            for (int level = _cascades.Length - 1; level > 0; level--)
            {
                var lowerLevel = level - 1;
                _radianceCascadeCs.MergeCascades(
                    cmd,
                    _cascades[lowerLevel],
                    _cascades[level],
                    lowerLevel
                );
            }
        }
        cmd.EndSample(sampleKey);

        if (_showDebugPreview)
        {
            PreviewCascades(cmd, _cascades, 1.0f);
        }

        sampleKey = "Combine";
        cmd.BeginSample(sampleKey);
        {
            cmd.SetRenderTarget(colorTexture, depthTexture);
            // TODO: Do blit into intermediate buffer with bilinear filter, then blit onto the screen
            BlitUtils.BlitTexture(cmd, _cascades[0], _blit, 0);
        }
        cmd.EndSample(sampleKey);
    }

    private void PreviewCascades(CommandBuffer cmd, RTHandle[] rtHandles, float offset = 0.0f)
    {
        cmd.BeginSample("Preview");

        const float scale = 1f / 8f;
        for (int i = 0; i < rtHandles.Length; i++)
        {
            Blitter.BlitQuad(
                cmd,
                rtHandles[i],
                new Vector4(1, 1f, 0, 0),
                new Vector4(scale, scale, scale * offset, 1.0f - scale * (i + 1)),
                0,
                false
            );
        }

        cmd.EndSample("Preview");
    }

#if UNITY_6000_0_OR_NEWER
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        TextureHandle sourceTexture = resourceData.activeColorTexture;
        TextureHandle depthTexture = resourceData.activeDepthTexture;
        TextureHandle normalsTexture = resourceData.cameraNormalsTexture;
        Vector3 forwardVector = cameraData.camera.transform.forward;

        RenderTextureDescriptor descriptor = new RenderTextureDescriptor(Resolutions[0].x, Resolutions[0].y)
        {
            colorFormat = RenderTextureFormat.ARGBHalf,
            enableRandomWrite = true,
        };

        TextureHandle[] cascades = new TextureHandle[_Cascades.Length];
        for (int i = 0; i < _Cascades.Length; i += 1)
        {
            RenderingUtils.ReAllocateHandleIfNeeded(
                ref _Cascades[i],
                descriptor,
                filterMode: FilterMode.Point,
                wrapMode: TextureWrapMode.Clamp,
                name: _cascadeNames[i]
            );
            cascades[i] = renderGraph.ImportTexture(_Cascades[i]);
        }

        using (var builder = renderGraph.AddComputePass<PassDataRenderCascades>("Render Cascades", out var passData, profilingSampler))
        {
            passData.ColorTexture = sourceTexture;
            builder.UseTexture(passData.ColorTexture);
            passData.NormalsTexture = normalsTexture;
            builder.UseTexture(passData.NormalsTexture);
            passData.DepthTexture = depthTexture;
            builder.UseTexture(passData.DepthTexture);
            passData.Compute = _compute;

            passData.Cascades = cascades;
            for (int i = 0; i < passData.Cascades.Length; i += 1)
            {
                builder.UseTexture(passData.Cascades[i], AccessFlags.ReadWrite);
            }

            builder.SetRenderFunc((PassDataRenderCascades data, ComputeGraphContext context) => ExecuteRenderCascades(data, context));
        }

        using (var builder = renderGraph.AddComputePass<PassDataMergeCascades>("Merge Cascades", out var passData, profilingSampler))
        {
            passData.Compute = _compute;

            passData.Cascades = cascades;
            for (int i = 0; i < passData.Cascades.Length; i += 1)
            {
                builder.UseTexture(passData.Cascades[i], AccessFlags.ReadWrite);
            }

            builder.SetRenderFunc((PassDataMergeCascades data, ComputeGraphContext context) => ExecuteMergeCascades(data, context));
        }

        if (_showDebugPreview)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassDataPreview>("Preview Cascades 1", out var passData, profilingSampler))
            {

                passData.Cascades = cascades;
                for (int i = 0; i < passData.Cascades.Length; i += 1)
                {
                    builder.UseTexture(passData.Cascades[i], AccessFlags.Read);
                }

                builder.SetRenderFunc((PassDataPreview data, RasterGraphContext context) => ExecitePreviewCascades(data, context, 1.0f));
            }
        }

        using (var builder = renderGraph.AddRasterRenderPass<PassDataCombine>("Combine", out var passData, profilingSampler))
        {
            passData.BlitMaterial = _blit;
            passData.Cascades = cascades;
            passData.ColorTexture = resourceData.activeColorTexture;
            passData.NormalsTexture = normalsTexture;
            builder.UseTexture(passData.NormalsTexture);
            passData.DepthTexture = depthTexture;
            passData.forward = forwardVector;

            builder.SetRenderAttachment(passData.ColorTexture, 0, AccessFlags.Write);
            builder.SetRenderAttachmentDepth(passData.DepthTexture, AccessFlags.Read);
            passData.GBuffer0 = resourceData.gBuffer[0];
            builder.UseTexture(passData.GBuffer0);
            builder.AllowGlobalStateModification(true); // needed later

            for (int i = 0; i < passData.Cascades.Length; i += 1)
            {
                builder.UseTexture(passData.Cascades[i], AccessFlags.ReadWrite);
            }

            builder.SetRenderFunc((PassDataCombine data, RasterGraphContext context) => ExecuteCombine(data, context));
        }
    }

    class PassDataRenderCascades
    {
        public TextureHandle[] Cascades { get; set; }
        public TextureHandle ColorTexture { get; set; }
        public TextureHandle DepthTexture { get; set; }
        public TextureHandle NormalsTexture { get; set; }
        public RadianceCascadeCompute Compute { get; set; }
    }

    // Static method is used as a RenderFunc delegate without issues
    static void ExecuteRenderCascades(PassDataRenderCascades data, ComputeGraphContext context)
    {
        for (int level = 0; level < data.Cascades.Length; level += 1)
        {
            data.Compute.RenderCascade(context.cmd, data.ColorTexture, data.DepthTexture, data.NormalsTexture, 2 << level, level, data.Cascades[level]);
        }
    }

    class PassDataMergeCascades
    {
        public TextureHandle[] Cascades { get; set; }
        public RadianceCascadeCompute Compute { get; set; }
    }
    static void ExecuteMergeCascades(PassDataMergeCascades data, ComputeGraphContext context)
    {
        for (int level = data.Cascades.Length - 1; level > 0; level--)
        {
            data.Compute.MergeCascades(context.cmd, data.Cascades[level - 1], data.Cascades[level], level - 1);
        }
    }

    class PassDataCombine
    {
        public Material BlitMaterial { get; set; }
        public TextureHandle[] Cascades { get; set; }
        public TextureHandle ColorTexture { get; set; }
        public TextureHandle DepthTexture { get; set; }
        public TextureHandle NormalsTexture { get; set; }
        public TextureHandle GBuffer0 { get; set; }
        public Vector3 forward { get; set; }
    }
    static void ExecuteCombine(PassDataCombine data, RasterGraphContext context)
    {
        // this line needs AllowGlobalStateModification true
        context.cmd.SetGlobalVector("_CameraForward", data.forward);
        BlitUtils.BlitTexture(context.cmd, data.Cascades[0], data.BlitMaterial, 0);
    }

    class PassDataPreview
    {
        public TextureHandle[] Cascades { get; set; }
    }
    static void ExecitePreviewCascades(PassDataPreview data, RasterGraphContext context, float offset = 0.0f)
    {
        context.cmd.BeginSample("Preview");
        Blitter.BlitTexture(context.cmd, data.Cascades[0], new Vector4(1f, 1f, 0f, 0f), 0, false);
        context.cmd.EndSample("Preview");
    }
#endif

    public void Dispose()
    {
        for (int i = 0; i < _cascades.Length; i++)
        {
            _cascades[i]?.Release();
        }
    }

    private static string[] GenNames(string name, int n)
    {
        var names = new string[n];
        for (int i = 0; i < n; i++)
        {
            names[i] = name + i;
        }

        return names;
    }
}
