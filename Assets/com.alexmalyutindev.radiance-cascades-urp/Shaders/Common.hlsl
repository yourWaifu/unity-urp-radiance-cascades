#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

float4 _ColorTexture_TexelSize;
Texture2D _ColorTexture;
Texture2D<float> _DepthTexture;
Texture2D<half3> _NormalsTexture;

float GetSectorId(int2 texCoord, float probeSize)
{
    return texCoord.x % probeSize + (texCoord.y % probeSize) * probeSize;
}

float GetAngle(int2 texCoord, float probeSize)
{
    float sectorId = texCoord.x % probeSize + (texCoord.y % probeSize) * probeSize;
    float angleStep = TWO_PI / (probeSize * probeSize);
    return (sectorId + 0.5f) * angleStep;
}

float2 GetRayDirection(int2 texCoord, float probeSize)
{
    float sectorId = GetSectorId(texCoord, probeSize);
    float2 direction;
    sincos((sectorId + 0.5f) * PI * 4.0f / (probeSize * probeSize), direction.y, direction.x);
    return direction;
}

inline float SampleLinearDepth(float2 uv)
{
    float rawDepth = SAMPLE_TEXTURE2D_LOD(_DepthTexture, sampler_PointClamp, uv, 0).r;
    return LinearEyeDepth(rawDepth, _ZBufferParams);
}

float4 RayTrace(float2 probeUV, float2 ray, float sceneDepth, int stepsCount)
{
    for (int i = 0; i < stepsCount; i++)
    {
        float2 offset = i * ray;

        float2 uv = probeUV + offset;
        if (any(uv < 0) || any(uv > 1))
        {
            return half4(0.0f, 0.0f, 0.0f, 0.0f);
        }

        float currentDepth = SampleLinearDepth(uv);
        // Intersection
        // (0)----------------| scene depth
        // (1)----------|     | current depth
        //              |     |
        if (sceneDepth > currentDepth && sceneDepth - currentDepth < 0.2f)
        {
            float3 color = SAMPLE_TEXTURE2D_LOD(_ColorTexture, sampler_PointClamp, uv, 0).rgb;
            return float4(color.rgb, 1.0f);
        }
    }

    return half4(0.0f, 0.0f, 0.0f, 0.0f);
}


int AngleToIndex(float angle, int dim) {
    float t = angle / (2 * PI);
    int index = floor(t * float(dim * dim));
    return index;
}

int2 IndexToCoords(int index, int dim) {
    //in case the index is lower than 0 or higher than the number of angles
    index = index % (dim * dim);
    int x = index % dim;
    int y = index / dim;

    return int2(x, y);
}
