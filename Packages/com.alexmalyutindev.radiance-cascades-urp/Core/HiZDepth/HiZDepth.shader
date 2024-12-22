Shader "Hidden/HiZDepth"
{
    Properties {}

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "DownSampleDepthMax2x2"

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            Texture2D<float> _InputDepth;
            float4 _InputDepth_TexelSize;
            float2 _Resolution;
            int _MipLevel;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = float4(input.positionOS.xy * 2 - 1, 0, 1);
                output.uv = input.texcoord;
                #if UNITY_UV_STARTS_AT_TOP
                output.uv.y = 1 - output.uv.y;
                #endif
                return output;
            }

            float Fragment(Varyings input) : SV_TARGET
            {
                int2 coord = input.uv * _Resolution;

                float a = LOAD_TEXTURE2D_LOD(_InputDepth, coord, _MipLevel);
                float b = LOAD_TEXTURE2D_LOD(_InputDepth, coord + int2(1, 0), _MipLevel);
                float c = LOAD_TEXTURE2D_LOD(_InputDepth, coord + int2(0, 1), _MipLevel);
                float d = LOAD_TEXTURE2D_LOD(_InputDepth, coord + int2(1, 1), _MipLevel);

                return max(max(a, b), max(c, d));
            }
            ENDHLSL
        }
    }
}