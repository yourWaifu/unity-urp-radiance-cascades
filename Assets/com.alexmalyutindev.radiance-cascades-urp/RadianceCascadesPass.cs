using AlexMalyutinDev.RadianceCascades;
using InternalBridge;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RadianceCascadesPass : ScriptableRenderPass
{
    private const int CascadesCount = 5;
    private readonly ProfilingSampler _profilingSampler;

    private RTHandle _Cascades0;

    private RTHandle[] _Cascades = new RTHandle[CascadesCount];
    private static readonly string[] _cascadeNames = GenCascadeNames(CascadesCount);

    private RTHandle _BlurBuffer0;
    private RTHandle _BlurBuffer1;

    private readonly Material _blit;

    private readonly ComputeShader _radianceCascadesCs;
    private readonly RadianceCascadeCompute _compute;
    private readonly int _renderCascadeKernel;


    // High:
    // Using aspect 7/4(14/8) to be closer to 16/9.
    // 128 => [224, 112, 56, 28, 14, 7] - horizontal probes
    //        [128,  64, 32, 16,  8, 4] - vertical probes 
    // Medium:
    // ...
    private static Vector2Int[] Resolutions = {
        new(32 * 16, 32 * 9), // 256x144 probes0
        new(32 * 10, 32 * 6), // 160x96 probes0
        new(32 * 7, 32 * 4), // 112x64 probes0
        new(32 * 4, 32 * 3), // 64x48 probes0
        new(32 * 3, 32 * 2), // 48x32 probes0
    };

    public RadianceCascadesPass(ComputeShader radianceCascadesCs, Material blit)
    {
        _profilingSampler = new ProfilingSampler(nameof(RadianceCascadesPass));
        _radianceCascadesCs = radianceCascadesCs;
        _compute = new RadianceCascadeCompute(_radianceCascadesCs);

        _blit = blit;
        _renderCascadeKernel = _radianceCascadesCs.FindKernel("RenderCascade");
        ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        // var aspect = cameraTextureDescriptor.height / (float) cameraTextureDescriptor.width;
        // var probesCountX = cameraTextureDescriptor.width / 4;
        // var probesCountY = cameraTextureDescriptor.height / 4;

        var desc = new RenderTextureDescriptor(
            Resolutions[1].x,
            Resolutions[1].y
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

        using (new ProfilingScope(cmd, _profilingSampler))
        {
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

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

            PreviewCascades(cmd);

            sampleKey = "MergeCascades";
            cmd.BeginSample(sampleKey);
            {
                for (int level = _Cascades.Length - 1; level > 0; level--)
                {
                    _compute.MergeCascades(cmd, _Cascades[level - 1], _Cascades[level], level - 1);
                }
            }
            cmd.EndSample(sampleKey);

            PreviewCascades(cmd, 1.0f);

            sampleKey = "Combine";
            cmd.BeginSample(sampleKey);
            {
                cmd.SetGlobalVector("_CameraForward", renderingData.cameraData.camera.transform.forward);
                Blitter.BlitTexture(cmd, _Cascades[0], new Vector4(0, 0, 1, 1), _blit, 0);
            }
            cmd.EndSample(sampleKey);
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        CommandBufferPool.Release(cmd);
    }

    private void PreviewCascades(CommandBuffer cmd, float offset = 0.0f)
    {
        cmd.BeginSample("Preview");

        const float scale = 1f / 8f;
        for (int i = 0; i < _Cascades.Length; i++)
        {
            Blitter.BlitQuad(
                cmd,
                _Cascades[i],
                new Vector4(1, 1f, 0, 0),
                new Vector4(scale, scale, scale * offset, 1.0f - scale * (i + 1)),
                0,
                false
            );
        }

        cmd.EndSample("Preview");
    }


    private static string[] GenCascadeNames(int n)
    {
        var names = new string[n];
        for (int i = 0; i < n; i++)
        {
            names[i] = "_Cascade" + i;
        }

        return names;
    }
}
