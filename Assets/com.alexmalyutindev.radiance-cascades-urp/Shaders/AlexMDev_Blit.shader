Shader "AlexMDev/Blit"
{
    Properties
    {
        _MainTex ("_MainTex", 2D) = "black" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "Combine"
            ZTest Off
            ZWrite Off
            Blend One One
            //            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            TEXTURE2D(_GBuffer0);
            TEXTURE2D(_MainTex);
            float4 _MainTex_TexelSize;

            #if SHADER_API_GLES
            struct Attributes
            {
                float4 positionOS       : POSITION;
                float2 uv               : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            #else
            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            #endif

            struct Varyings
            {
                float2 texcoord : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                #if SHADER_API_GLES
                float4 pos = input.positionOS;
                float2 uv  = input.uv;
                #else
                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv = GetFullScreenTriangleTexCoord(input.vertexID);
                #endif

                output.positionCS = pos;
                output.texcoord = uv;
                return output;
            }

            float3 _CameraForward;

            half4 Fragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half depth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_PointClamp, input.texcoord).x;
                if (depth == UNITY_RAW_FAR_CLIP_VALUE)
                {
                    clip(-1);
                }

                int2 coord = floor(input.texcoord * _MainTex_TexelSize.zw * 0.5f) * 2.0f;

                float2 uv = (coord + 1.0f) * _MainTex_TexelSize.xy;
                float3 offset = float3(_MainTex_TexelSize.xy * 2.0f, 0.0f);
                half4 a = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv);
                half4 b = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + offset.xz);
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + offset.zy);
                half4 d = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, uv + offset.xy);

                // Bilinear Interpolation.
                float2 w = fmod(input.texcoord * _MainTex_TexelSize.zw * 0.5f, 1.0f);
                half4 color = lerp(
                    lerp(a, b, w.x),
                    lerp(c, d, w.x),
                    w.y
                );

                half4 gbuffer0 = SAMPLE_TEXTURE2D(_GBuffer0, sampler_PointClamp, input.texcoord);
                return color * gbuffer0;

                half3 normalWS = SampleSceneNormals(input.texcoord);
                half bling = 1.0h - abs(dot(normalWS, -_CameraForward));

                return color * gbuffer0 * bling;
            }
            ENDHLSL
        }
    }
}