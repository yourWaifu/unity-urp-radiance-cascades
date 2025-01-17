using AlexMalyutinDev.RadianceCascades;
using AlexMalyutinDev.RadianceCascades.HiZDepth;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class RadianceCascadesFeature : ScriptableRendererFeature
{
    public RadianceCascadeResources Resources;

    public bool showDebugView;

    [SerializeField]
    private RenderType _renderType;

    private RadianceCascadesPass _radianceCascadesPass;
    private RadianceCascades3dPass _radianceCascadesPass3d;
    private VoxelizationPass _voxelizationPass;
    private HiZDepthPass _hiZDepthPass;

    private RadianceCascadesRenderingData _radianceCascadesRenderingData;

    public override void Create()
    {
        _radianceCascadesRenderingData = new RadianceCascadesRenderingData();

        _radianceCascadesPass = new RadianceCascadesPass(Resources, showDebugView)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingDeferredLights
        };

        _voxelizationPass = new VoxelizationPass(Resources, _radianceCascadesRenderingData)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingShadows,
        };
        _radianceCascadesPass3d = new RadianceCascades3dPass(Resources, _radianceCascadesRenderingData)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingDeferredLights
        };

        _hiZDepthPass = new HiZDepthPass(Resources.HiZDepthMaterial, null)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingGbuffer
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.isPreviewCamera)
        {
            return;
        }
        
        renderer.EnqueuePass(_hiZDepthPass);

        if (_renderType == RenderType._2D)
        {
            renderer.EnqueuePass(_radianceCascadesPass);
        }
        else if (_renderType == RenderType._3D)
        {
            renderer.EnqueuePass(_voxelizationPass);
            renderer.EnqueuePass(_radianceCascadesPass3d);
        }
    }

    protected override void Dispose(bool disposing)
    {
        _radianceCascadesPass?.Dispose();
        _radianceCascadesPass3d?.Dispose();
        _voxelizationPass?.Dispose();
        _hiZDepthPass?.Dispose();
    }

    private enum RenderType
    {
        _2D,
        _3D,
    }
}
