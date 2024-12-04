using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

public class RadianceCascadesFeature : ScriptableRendererFeature
{
    public Material BlitMaterial;
    [FormerlySerializedAs("_radianceCascadesCs")]
    public ComputeShader RadianceCascades;
    [FormerlySerializedAs("_radianceCascadesCs")]
    public ComputeShader RadianceCascades3d;


    private RadianceCascadesPass _pass;

    public override void Create()
    {
        _pass = new RadianceCascadesPass(RadianceCascades, RadianceCascades3d, BlitMaterial)
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

        renderer.EnqueuePass(_pass);
    }
}
