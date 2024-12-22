using UnityEngine;

namespace AlexMalyutinDev.RadianceCascades
{
    public class RadianceCascadeResources : ScriptableObject
    {
        public Material BlitMaterial;
        public ComputeShader RadianceCascades;
        public ComputeShader RadianceCascades3d;

        [Space]
        public Shader Voxelizator;
        public ComputeShader VoxelizatorCS;

        [Space]
        public Material HiZDepthMaterial;
    }
}
