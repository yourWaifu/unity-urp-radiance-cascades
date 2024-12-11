using UnityEngine;
using UnityEngine.Rendering.Universal;

public class RadianceCascadesFeature : ScriptableRendererFeature
{
    public Material BlitMaterial;
    public ComputeShader RadianceCascades;
    public ComputeShader RadianceCascades3d;
    public bool showDebugView;

    [SerializeField]
    private RenderType _renderType;

    private RadianceCascadesPass _pass;
    private RadianceCascades3dPass _pass3d;

    public override void Create()
    {
        _pass = new RadianceCascadesPass(RadianceCascades, RadianceCascades3d, BlitMaterial, showDebugView)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingDeferredLights
        };
        _pass3d = new RadianceCascades3dPass(RadianceCascades3d, BlitMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingDeferredLights
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (RadianceCascades == null || RadianceCascades3d == null || BlitMaterial == null)
        {
            return;
        }

        if (renderingData.cameraData.isPreviewCamera)
        {
            return;
        }

        if (_renderType == RenderType._2D)
        {
            renderer.EnqueuePass(_pass);
        }
        else if (_renderType == RenderType._3D)
        {
            renderer.EnqueuePass(_pass3d);
        }
    }

    private enum RenderType
    {
        _2D,
        _3D,
    }
}
