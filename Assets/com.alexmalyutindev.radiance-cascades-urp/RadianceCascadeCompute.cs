using UnityEngine;
using UnityEngine.Rendering;

namespace AlexMalyutinDev.RadianceCascades
{
    public class RadianceCascadeCompute
    {
        private readonly ComputeShader _compute;
        private readonly int _mainKernel;
        private readonly int _mergeKernel;
        private readonly int _renderKernel;

        public RadianceCascadeCompute(ComputeShader compute)
        {
            _compute = compute;
            _mainKernel = _compute.FindKernel("Main");
            _mergeKernel = _compute.FindKernel("Merge");
            _renderKernel = _compute.FindKernel("RenderCascade");
        }

        public void RenderCascade(CommandBuffer cmd, RTHandle color, RTHandle depth, int probeSize, RTHandle target)
        {
            var rt = target.rt;

            cmd.SetComputeFloatParam(_compute, "_ProbeSize", probeSize);
            cmd.SetComputeVectorParam(
                _compute,
                "_OutCascadeSize",
                new Vector4(
                    rt.width,
                    rt.height,
                    1.0f / rt.width,
                    1.0f / rt.height
                )
            );

            cmd.SetComputeTextureParam(
                _compute,
                _renderKernel,
                "_ColorTexture",
                color
            );
            cmd.SetComputeTextureParam(
                _compute,
                _renderKernel,
                "_DepthTexture",
                depth
            );

            // Output
            cmd.SetComputeTextureParam(
                _compute,
                _renderKernel,
                "_OutCascade",
                target
            );

            cmd.DispatchCompute(
                _compute,
                _renderKernel,
                rt.width / 8,
                rt.height / 8,
                1
            );
        }
    }
}
