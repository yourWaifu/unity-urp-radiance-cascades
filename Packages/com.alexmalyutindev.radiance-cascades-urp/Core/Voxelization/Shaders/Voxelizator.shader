Shader "AlexM/Voxelization"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
            "ShaderModel"="4.5"
        }
        LOD 300

        Pass
        {
            Name "Voxelization"
            Tags
            {
                "LightMode" = "Voxelization"
            }

            // -------------------------------------
            // Render State Commands
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5

            // -------------------------------------
            // Shader Stages
            #pragma vertex Vertex
            #pragma fragment Fragment

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "VoxelData.hlsl"

            struct Attribute
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 texcoord : TEXCOORD;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;

                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Material Input
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            TEXTURE3D(_Radiance);
            SAMPLER(sampler_Radiance);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _Cutoff;
            CBUFFER_END

            #define FORWARD_PROJ 0
            #define RIGHT_PROJ 1
            #define TOP_PROJ 2

            uint Axis;
            uint Resolution;
            RWTexture3D<float4> _Volume;

            float3 TransformClipSpaceToVoxel(float4 positionCS)
            {
                positionCS.z *= Resolution;
                // TODO: Optimize out switch!
                switch (Axis)
                {
                case RIGHT_PROJ:
                    return float3(
                        positionCS.z,
                        positionCS.y,
                        Resolution - positionCS.x
                    );
                case TOP_PROJ:
                    return float3(
                        positionCS.x,
                        Resolution - positionCS.z,
                        positionCS.y
                    );
                default: // FORWARD_PROJ
                    return positionCS.xyz;
                }
            }

            Varyings Vertex(Attribute input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.normalWS = TransformObjectToWorldNormal(input.normalOS.xyz);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);

                #ifdef UNITY_REVERSED_Z
                output.positionCS.z = 1.0 - output.positionCS.z;
                #endif

                #ifdef UNITY_UV_STARTS_AT_TOP
                output.positionCS.y = -output.positionCS.y;
                #endif

                return output;
            }

            half Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // NOTE: XS is a voxel space
                float3 positionXS = TransformClipSpaceToVoxel(input.positionCS);
                float3 positionWS = input.positionWS;

                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                #if defined(_EMISSION)
                half4 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv) * _EmissionColor;
                #else
                half4 emission = 0;
                #endif

                if (albedoAlpha.a > _Cutoff)
                {
                    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);

                    Light mainLight = GetMainLight(shadowCoord);
                    float NdotL = max(0.0f, dot(input.normalWS, mainLight.direction));
                    float3 color = albedoAlpha.rgb * NdotL * saturate(mainLight.shadowAttenuation) + emission.rgb;

                    // TODO: Occlusion voxel, agregate with InterlockedMax()
                    _Volume[positionXS] = float4(saturate(color), albedoAlpha.a);
                }

                return 0.0h;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}