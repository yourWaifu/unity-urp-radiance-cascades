Shader "Hidden/RadianceCascade/Blit"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "Combine"
            ZTest Greater
            ZWrite Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            TEXTURE2D_X(_BlitTexture);
            TEXTURE2D(_GBuffer0);
            float4 _BlitTexture_TexelSize;
            float3 _CameraForward;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;

                float4 pos = input.positionOS * 2.0f - 1.0f;
                float2 uv = input.uv;

                #if UNITY_UV_STARTS_AT_TOP
                uv.y = 1 - uv.y;
                #endif

                pos.z = UNITY_RAW_FAR_CLIP_VALUE;
                output.positionCS = pos;
                output.texcoord = uv;
                return output;
            }


            half4 Fragment(Varyings input) : SV_TARGET
            {
                // TODO: Bilateral Upsampling.
                // half depth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_PointClamp, input.texcoord).x;
                // if (depth == UNITY_RAW_FAR_CLIP_VALUE)
                // {
                //     clip(-1);
                // }

                const float2 temp = input.texcoord * _BlitTexture_TexelSize.zw * 0.5f;
                const float2 w = fmod(temp, 1.0f);
                const int2 coord = floor(temp) * 2.0f;

                float2 uv = (coord + 1.0f) * _BlitTexture_TexelSize.xy;
                float3 offset = float3(_BlitTexture_TexelSize.xy * 2.0f, 0.0f);
                half4 a = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv) * 4;
                half4 b = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offset.xz) * 4;
                half4 c = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offset.zy) * 4;
                half4 d = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offset.xy) * 4;

                // Bilinear Interpolation.
                half4 color = lerp(
                    lerp(a, b, w.x),
                    lerp(c, d, w.x),
                    w.y
                );

                half4 gbuffer0 = SAMPLE_TEXTURE2D(_GBuffer0, sampler_PointClamp, input.texcoord);
                return color * gbuffer0;

                half3 normalWS = SampleSceneNormals(input.texcoord);
                half angleFade = 1.0h - abs(dot(normalWS, -_CameraForward));

                return color * gbuffer0 * angleFade;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Combine3d"
            ZTest Greater
            ZWrite Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            TEXTURE2D(_GBuffer0);
            TEXTURE2D(_BlitTexture);
            float4 _BlitTexture_TexelSize;
            float3 _CameraForward;

            #if SHADER_API_GLES
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
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

                pos.z = UNITY_RAW_FAR_CLIP_VALUE;
                output.positionCS = pos;
                output.texcoord = uv;
                return output;
            }

            float4 SamlpeProbe(float2 uv, half3 normalWS)
            {
                float4 radiance = 0;

                // TODO: Use normal map for detailed lighting!
                // float3 weight = normalWS * 0.5h + 0.5h;
                // float4 x = lerp(
                //     SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv),
                //     SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(0.0f, 0.5f)),
                //     weight.x
                // );
                //
                // float4 y = lerp(
                //     SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(1.0f / 3.0f, 0.0f)),
                //     SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(2.0f / 3.0f, 0.0f)),
                //     weight.y
                // );
                //
                // float4 z = lerp(
                //     SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(2.0f / 3.0f, 0.0f)),
                //     SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + float2(2.0f / 3.0f, 0.5f)),
                //     weight.z
                // );
                // return (x + z + y) * 0.5f;


                int2 offset = _BlitTexture_TexelSize.zw / int2(3, 2);
                int2 coords = floor(uv * _BlitTexture_TexelSize.zw);
                radiance += LOAD_TEXTURE2D(_BlitTexture, coords);
                radiance += LOAD_TEXTURE2D(_BlitTexture, coords + offset.xy * int2(1, 0));
                radiance += LOAD_TEXTURE2D(_BlitTexture, coords + offset.xy * int2(2, 0));
                radiance += LOAD_TEXTURE2D(_BlitTexture, coords + offset.xy * int2(0, 1));
                radiance += LOAD_TEXTURE2D(_BlitTexture, coords + offset.xy * int2(1, 1));
                radiance += LOAD_TEXTURE2D(_BlitTexture, coords + offset.xy * int2(2, 1));

                return radiance;
            }

            float4 SamlpeSampleProbe(int2 coords)
            {
                float4 radiance = 0;

                int2 offset = _BlitTexture_TexelSize.zw / int2(3, 2);
                radiance += LOAD_TEXTURE2D(_BlitTexture, coords);
                radiance += LOAD_TEXTURE2D(_BlitTexture, coords + offset.xy * int2(1, 0));
                radiance += LOAD_TEXTURE2D(_BlitTexture, coords + offset.xy * int2(2, 0));
                radiance += LOAD_TEXTURE2D(_BlitTexture, coords + offset.xy * int2(0, 1));
                radiance += LOAD_TEXTURE2D(_BlitTexture, coords + offset.xy * int2(1, 1));
                radiance += LOAD_TEXTURE2D(_BlitTexture, coords + offset.xy * int2(2, 1));

                return radiance;
            }

            float4 SamlpeSampleProbe2x2(float2 uv)
            {
                float4 radiance = 0;

                float2 offset = 1.0f / float2(3, 2);
                radiance += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);
                radiance += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offset.xy * float2(1, 0));
                radiance += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offset.xy * float2(2, 0));
                radiance += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offset.xy * float2(0, 1));
                radiance += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offset.xy * float2(1, 1));
                radiance += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offset.xy * float2(2, 1));

                return radiance;
            }

            half4 Fragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // half depth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_PointClamp, input.texcoord).x;
                // if (depth == UNITY_RAW_FAR_CLIP_VALUE)
                // {
                //     clip(-1);
                // }

                // return SAMPLE_TEXTURE2D(_BlitTexture, sampler_PointClamp, input.texcoord);

                const int2 sideSize = _BlitTexture_TexelSize.zw / int2(3, 2);
                //  0 | 0 | 1 | 1 
                // ___|___|___|___
                //  0 | 0 | 1 | 1 
                // ___|___|___|___
                //  2 | 2 | 3 | 3 
                //    |   |   |   
                const float2 temp = input.texcoord * _BlitTexture_TexelSize.zw * 0.25f;
                const float2 w = fmod(temp, 1.0f);

                half3 normalWS = SampleSceneNormals(input.texcoord);

                int2 probeIndex = floor(sideSize * input.texcoord * 0.5f) * 2;
                // half4 a = SamlpeSampleProbe(probeIndex);
                // half4 b = SamlpeSampleProbe(probeIndex + int2(1, 0));
                // half4 c = SamlpeSampleProbe(probeIndex + int2(0, 1));
                // half4 d = SamlpeSampleProbe(probeIndex + int2(1, 1));

                half4 a = SamlpeSampleProbe2x2(probeIndex * _BlitTexture_TexelSize.xy);
                half4 b = SamlpeSampleProbe2x2(probeIndex * _BlitTexture_TexelSize.xy);
                half4 c = SamlpeSampleProbe2x2(probeIndex * _BlitTexture_TexelSize.xy);
                half4 d = SamlpeSampleProbe2x2(probeIndex * _BlitTexture_TexelSize.xy);

                // Bilinear Interpolation.
                half4 color = lerp(
                    lerp(a, b, w.x),
                    lerp(c, d, w.x),
                    w.y
                );

                half4 gbuffer0 = SAMPLE_TEXTURE2D(_GBuffer0, sampler_PointClamp, input.texcoord);
                return color * gbuffer0;

                half angleFade = 1.0h - abs(dot(normalWS, -_CameraForward));

                return color * gbuffer0 * angleFade;
            }
            ENDHLSL
        }
    }
}