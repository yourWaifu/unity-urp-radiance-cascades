using AlexMalyutinDev.RadianceCascades;
using InternalBridge;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RadianceCascadesPass : ScriptableRenderPass
{
    private const int CascadesCount = 5;
    private readonly ProfilingSampler _profilingSampler;

    private RTHandle[] _Cascades = new RTHandle[CascadesCount];
    private static readonly string[] _cascadeNames = GenNames("_Cascade", CascadesCount);

    private readonly Material _blit;
    private readonly ComputeShader _radianceCascadesCs;
    private readonly RadianceCascadeCompute _compute;
    private readonly int _renderCascadeKernel;

    private bool _showDebugPreview = true;


    // High:
    // Using aspect 7/4(14/8) to be closer to 16/9.
    // 128 => [224, 112, 56, 28, 14, 7] - horizontal probes
    //        [128,  64, 32, 16,  8, 4] - vertical probes 
    // Medium:
    // ...
    private static Vector2Int[] Resolutions =
    {
        new(32 * 16, 32 * 9), // 256x144 probes0
        new(32 * 10, 32 * 6), // 160x96 probes0
        new(32 * 7, 32 * 4), // 112x64 probes0
        new(32 * 4, 32 * 3), // 64x48 probes0
        new(32 * 3, 32 * 2), // 48x32 probes0
    };

    public RadianceCascadesPass(
        RadianceCascadeResources resources,
        bool showDebugView
    )
    {
        _profilingSampler = new ProfilingSampler(nameof(RadianceCascadesPass));
        _radianceCascadesCs = resources.RadianceCascades;

        _compute = new RadianceCascadeCompute(_radianceCascadesCs);

        _blit = resources.BlitMaterial;
        _renderCascadeKernel = _radianceCascadesCs.FindKernel("RenderCascade");
        
        // BUG: Configuring with Depth and Color buffer dependency will cause to additional resolve of
        // this buffers before RadianceCascadesPass
        // ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);

        _showDebugPreview = showDebugView;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        // var aspect = cameraTextureDescriptor.height / (float) cameraTextureDescriptor.width;
        // var probesCountX = cameraTextureDescriptor.width / 4;
        // var probesCountY = cameraTextureDescriptor.height / 4;

        var desc = new RenderTextureDescriptor(
            Resolutions[0].x,
            Resolutions[0].y
        )
        {
            colorFormat = RenderTextureFormat.ARGBHalf,
            enableRandomWrite = true,
        };

        for (int i = 0; i < _Cascades.Length; i++)
        {
            RenderingUtils.ReAllocateIfNeeded(
                ref _Cascades[i],
                desc,
                filterMode: FilterMode.Point,
                wrapMode: TextureWrapMode.Clamp,
                name: _cascadeNames[i]
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
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            Cascades2d(renderingData, cmd, colorTexture, depthTexture);
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        CommandBufferPool.Release(cmd);
    }

    private void Cascades2d(
        RenderingData renderingData,
        CommandBuffer cmd,
        RTHandle colorTexture,
        RTHandle depthTexture
    )
    {
        var sampleKey = "RenderCascades";
        cmd.BeginSample(sampleKey);
        {
            // Shared
            // TODO: Move into arguments of RenderCascade(...)
            cmd.SetComputeTextureParam(
                _radianceCascadesCs,
                _renderCascadeKernel,
                "_ColorTexture",
                colorTexture // gBuffer0 //
            );
            cmd.SetComputeTextureParam(
                _radianceCascadesCs,
                _renderCascadeKernel,
                "_NormalsTexture",
                renderingData.cameraData.renderer.GetGBuffer(2) // Normals
            );
            cmd.SetComputeTextureParam(
                _radianceCascadesCs,
                _renderCascadeKernel,
                "_DepthTexture",
                renderingData.cameraData.renderer.cameraDepthTargetHandle
            );


            for (int level = 0; level < _Cascades.Length; level++)
            {
                _compute.RenderCascade(
                    cmd,
                    colorTexture,
                    renderingData.cameraData.renderer.cameraDepthTargetHandle,
                    2 << level,
                    level,
                    _Cascades[level]
                );
            }
        }
        cmd.EndSample(sampleKey);

        PreviewCascades(cmd, _Cascades);

        sampleKey = "MergeCascades";
        cmd.BeginSample(sampleKey);
        {
            for (int level = _Cascades.Length - 1; level > 0; level--)
            {
                var lowerLevel = level - 1;
                _compute.MergeCascades(cmd, _Cascades[lowerLevel], _Cascades[level], lowerLevel);
            }
        }
        cmd.EndSample(sampleKey);

        PreviewCascades(cmd, _Cascades, 1.0f);

        sampleKey = "Combine";
        cmd.BeginSample(sampleKey);
        {
            cmd.SetRenderTarget(colorTexture, depthTexture);
            cmd.SetGlobalVector("_CameraForward", renderingData.cameraData.camera.transform.forward);
            BlitUtils.BlitTexture(cmd, _Cascades[0], _blit, 0);
        }
        cmd.EndSample(sampleKey);
    }

    private void PreviewCascades(CommandBuffer cmd, RTHandle[] rtHandles, float offset = 0.0f)
    {
        if (!_showDebugPreview) { return; }
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
