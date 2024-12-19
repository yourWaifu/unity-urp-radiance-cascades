using AlexMalyutinDev.RadianceCascades;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class RadianceCascadesFeature : ScriptableRendererFeature
{
    public RadianceCascadeResources Resources;

    public Material BlitMaterial;
    public ComputeShader RadianceCascades;
    public ComputeShader RadianceCascades3d;
    public bool showDebugView;

    [SerializeField]
    private RenderType _renderType;

    private RadianceCascadesPass _pass;
    private RadianceCascades3dPass _pass3d;
    private VoxelizationPass _voxelizationPass;

    private RadianceCascadesRenderingData _radianceCascadesRenderingData;

    public override void Create()
    {
        _radianceCascadesRenderingData = new RadianceCascadesRenderingData();

        _pass = new RadianceCascadesPass(Resources, showDebugView)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingDeferredLights
        };

        _voxelizationPass = new VoxelizationPass(Resources, _radianceCascadesRenderingData)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingShadows,
        };
        _pass3d = new RadianceCascades3dPass(Resources, _radianceCascadesRenderingData)
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
            renderer.EnqueuePass(_voxelizationPass);
            renderer.EnqueuePass(_pass3d);
        }
    }

    protected override void Dispose(bool disposing)
    {
        _voxelizationPass?.Dispose();
        _voxelizationPass = null;
    }

    private enum RenderType
    {
        _2D,
        _3D,
    }
}
