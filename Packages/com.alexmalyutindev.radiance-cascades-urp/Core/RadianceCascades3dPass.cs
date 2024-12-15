using AlexMalyutinDev.RadianceCascades;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RadianceCascades3dPass : ScriptableRenderPass
{
    private const int CascadesCount = 5;
    private readonly ProfilingSampler _profilingSampler;

    private RTHandle[] _Cascades = new RTHandle[CascadesCount];
    private static readonly string[] _cascade3dNames = GenNames("_Cascade", CascadesCount);

    private readonly Material _blitMaterial;
    private readonly RadianceCascade3dRenderer _radianceCascadeRenderer;

    public RadianceCascades3dPass(RadianceCascadeResources resources)
    {
        _profilingSampler = new ProfilingSampler(nameof(RadianceCascadesPass));
        _radianceCascadeRenderer = new RadianceCascade3dRenderer(resources.RadianceCascades3d);
        _blitMaterial = resources.BlitMaterial;
    }

    public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
    {
        const int scale = 4;
        var decs = new RenderTextureDescriptor(
            (2 << CascadesCount) * 2 * scale,
            (1 << CascadesCount) * 3 * scale
        )
        {
            colorFormat = RenderTextureFormat.ARGBHalf,
            enableRandomWrite = true,
            mipCount = 0,
            depthBufferBits = 0,
            depthStencilFormat = GraphicsFormat.None,
        };
        for (int i = 0; i < _Cascades.Length; i++)
        {
            RenderingUtils.ReAllocateIfNeeded(
                ref _Cascades[i],
                decs,
                name: _cascade3dNames[i],
                filterMode: FilterMode.Point,
                wrapMode: TextureWrapMode.Clamp
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

            Render(cmd, ref renderingData, colorTexture, depthTexture);
        }

        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        CommandBufferPool.Release(cmd);
    }

    private void Render(
        CommandBuffer cmd,
        ref RenderingData renderingData,
        RTHandle colorTexture,
        RTHandle depthTexture
    )
    {
        var sampleKey = "RenderCascades";
        cmd.BeginSample(sampleKey);
        {
            for (int level = 0; level < _Cascades.Length; level++)
            {
                _radianceCascadeRenderer.RenderCascade(
                    cmd,
                    ref renderingData,
                    colorTexture,
                    depthTexture,
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
                _radianceCascadeRenderer.MergeCascades(
                    cmd,
                    _Cascades[level - 1],
                    _Cascades[level],
                    level - 1
                );
            }
        }
        cmd.EndSample(sampleKey);


        sampleKey = "Combine";
        cmd.BeginSample(sampleKey);
        Blitter.BlitTexture(cmd, _Cascades[0], new Vector4(1f / 2f, 1f / 3f, 0, 0), _blitMaterial, 1);
        cmd.EndSample(sampleKey);


        PreviewCascades(cmd, _Cascades, 1.0f);
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
