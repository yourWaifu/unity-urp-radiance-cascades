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
            Name "Blit"
            ZTest Off
            ZWrite Off
            // Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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

            float4 _CameraDepthTexture_TexelSize;

            inline float SampleLinearDepth(float2 uv)
            {
                float rawDepth = SAMPLE_TEXTURE2D_LOD(_CameraDepthTexture, sampler_LinearClamp, uv, 0).r;
                return LinearEyeDepth(rawDepth, _ZBufferParams);
            }


            half4 SampleProbe(uint2 coord)
            {
                int2 probId0 = floor(coord / 2) * 2;
                half4 color = 0.0h;

                UNITY_UNROLL
                for (int i = 0; i < 4; i++)
                {
                    int2 offset0 = int2(i % 2, i / 2);
                    half4 s0 = LOAD_TEXTURE2D(_MainTex, probId0 + offset0);
                    if (s0.a < 0.5h)
                    {
                        s0 = 0;
                        // Continue sampler along ray!
                        int2 probId1 = floor(coord / 4) * 4;
                        probId1.x += _MainTex_TexelSize.z / 3.0f;
                        for (int i1 = 0; i1 < 4; i1++)
                        {
                            int ray1 = i * 4 + i1;
                            int2 offset1 = int2(ray1 % 4, ray1 / 4);
                            half4 s1 = LOAD_TEXTURE2D(_MainTex, probId1 + offset1);
                            if (false && s1.a < 0.5h)
                            {
                                s1 = 0;
                                int2 probId2 = floor(coord / 4) * 4;
                                probId2.x += _MainTex_TexelSize.z / 3.0f;
                                for (int i2 = 0; i2 < 4; i2++)
                                {
                                    int ray2 = ray1 * 4 + i2;
                                    int2 offset2 = int2(ray2 % 8, ray2 / 8);
                                    half4 s2 = LOAD_TEXTURE2D(_MainTex, probId2 + offset2);
                                    s1 += s2 * 0.25f;
                                }
                            }

                            s0 += s1 * 0.25f;
                        }
                    }
                    color += s0 * 0.25f;
                }
                return color;
            }

            half4 SampleProbe0(uint2 coord)
            {
                int2 probId0 = floor(coord / 2) * 2;
                half4 color = 0.0h;

                UNITY_UNROLL
                for (int i = 0; i < 4; i++)
                {
                    int2 offset0 = int2(i % 2, i / 2);
                    half4 s0 = LOAD_TEXTURE2D(_MainTex, probId0 + offset0);
                    color += s0 * 0.25f;
                }
                return color;
            }

            half4 SampleProbe0_Bileaner(uint2 coord)
            {
                int2 probId0 = floor(coord / 2) * 2;
                float2 probUV = (probId0 + 1) * _MainTex_TexelSize.xy;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, probUV);
            }

            half4 Fragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                int2 coord = floor(input.texcoord * _MainTex_TexelSize.zw - 1);

                // TODO: Bilinear blend 
                float2 offset = input.texcoord * _MainTex_TexelSize.zw - floor(coord * 0.5f) * 2;
                // return half4(offset - 2, 0, 1);

                // TODO: blend 4 probes
                half4 radience = SampleProbe0_Bileaner(coord);
                // return radience;
                radience += SampleProbe0_Bileaner(coord + int2(2, 0));
                radience += SampleProbe0_Bileaner(coord + int2(0, 2));
                radience += SampleProbe0_Bileaner(coord + int2(2, 2));

                return radience * 0.25;

                // Old
                // float2 uv2 = coord * _MainTex_TexelSize.xy;
                // half4 radiance = SampleProbe(coord);
                // return radiance;
                //
                // radiance += SampleProbe(coord) * length((input.texcoord - uv2) * _MainTex_TexelSize.zw * 0.5f);
                //
                // uv2 = (coord + int2(2, 2)) * _MainTex_TexelSize.xy;
                // radiance += SampleProbe(coord + int2(2, 2)) * length(
                //     (input.texcoord - uv2) * _MainTex_TexelSize.zw * 0.5f);
                //
                // uv2 = (coord + int2(2, 0)) * _MainTex_TexelSize.xy;
                // radiance += SampleProbe(coord + int2(2, 0)) * length(
                //     (input.texcoord - uv2) * _MainTex_TexelSize.zw * 0.5f);
                //
                // uv2 = (coord + int2(0, 2)) * _MainTex_TexelSize.xy;
                // radiance += SampleProbe(coord + int2(2, 0)) * length(
                //     (input.texcoord - uv2) * _MainTex_TexelSize.zw * 0.5f);
                //
                // return radiance;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Blur"
            ZTest Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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

            half4 Fragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 offset = float4(_MainTex_TexelSize.xy, -_MainTex_TexelSize.xy) * 8;
                half4 color = 0;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.texcoord + offset.xy);
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.texcoord + offset.zy);
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.texcoord + offset.xw);
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.texcoord + offset.zw);
                color *= 0.26f;

                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Blur2"
            ZTest Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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

            half4 Fragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 offset = float4(_MainTex_TexelSize.xy, -_MainTex_TexelSize.xy) * 4;
                half4 color = 0;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.texcoord + offset.xy);
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.texcoord + offset.zy);
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.texcoord + offset.xw);
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.texcoord + offset.zw);
                color *= 0.26f;

                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Combine"
            ZTest Off
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

                float4 offset = float4(_MainTex_TexelSize.xy, -_MainTex_TexelSize.xy) * 2;
                half4 color = 0;
                color += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.texcoord + offset.xy);
                // color += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.texcoord + offset.zy);
                // color += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.texcoord + offset.xw);
                // color += SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, input.texcoord + offset.zw);
                // color *= 0.26f;

                
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