using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AlexMalyutinDev.RadianceCascades
{
    public class VoxelizationPass : ScriptableRenderPass, IDisposable
    {
        private RTHandle _volume;
        private readonly Voxelizator _voxelizator;
        private readonly int _resolution = 64;

        public VoxelizationPass(RadianceCascadeResources resources)
        {
            profilingSampler = new ProfilingSampler("Voxelization");
            _voxelizator = new Voxelizator(resources.Voxelizator, resources.VoxelizatorCS);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            _voxelizator.Prepare(_resolution);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.isPreviewCamera) return;

            var cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, profilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                _voxelizator.VoxelizeGeometry(cmd, ref context, ref renderingData, _resolution, ref _volume);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            _voxelizator.CleanUp();
        }

        public void Dispose()
        {
            _voxelizator?.Dispose();
        }
    }
}
