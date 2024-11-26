#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

float4 _ColorTexture_TexelSize;
Texture2D _ColorTexture;
Texture2D<float> _DepthTexture;
Texture2D<half3> _NormalsTexture;

float GetSectorId(int2 texCoord, float probeSize)
{
    return texCoord.x % probeSize + (texCoord.y % probeSize) * probeSize;
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
    float rawDepth = SAMPLE_TEXTURE2D_LOD(_DepthTexture, sampler_LinearClamp, uv, 0).r;
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
        if (sceneDepth > currentDepth && sceneDepth - currentDepth < .2f)
        {
            float3 color = SAMPLE_TEXTURE2D_LOD(_ColorTexture, sampler_LinearClamp, uv, 0).rgb;
            // color *= dot(color, 1.0f) > 0.7f;
            return float4(color.rgb, 1.0f);
        }
    }

    return half4(0.0f, 0.0f, 0.0f, 0.0f);
}