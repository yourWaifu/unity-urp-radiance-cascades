using UnityEngine;
using UnityEngine.Rendering.Universal;

public class RadianceCascadesFeature : ScriptableRendererFeature
{
    public Material BlitMaterial;
    public ComputeShader _radianceCascadesCs;

    private RadianceCascadesPass _pass;

    public override void Create()
    {
        _pass = new RadianceCascadesPass(_radianceCascadesCs, BlitMaterial)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingSkybox
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_radianceCascadesCs == null || BlitMaterial == null)
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


