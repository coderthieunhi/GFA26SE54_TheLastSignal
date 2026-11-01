Shader "Universal Render Pipeline/2D/Sprite-Lit-PixelSmooth"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_MainTex);

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = (Varyings) 0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(input.positionOS);
#if defined(DEBUG_DISPLAY)
                o.positionWS = TransformObjectToWorld(input.positionOS);
                o.normalWS = TransformObjectToWorldDir(input.normal);
#endif
                o.uv = input.uv;
                o.lightingUV = half2(ComputeScreenPos(o.positionCS / o.positionCS.w).xy);
                o.color = input.color * _Color * unity_SpriteColor;

                return o;
            }

            float2 SmoothPointUV(float2 uv, float4 texelSize)
            {
                float2 pixel = uv * texelSize.zw;
                float2 seam = floor(pixel + 0.5);
                float2 duv = max(fwidth(pixel), 0.0001);
                float2 fracCoord = pixel - seam;
                float2 smoothFrac = clamp(fracCoord / duv, -0.5, 0.5);
                return (seam + smoothFrac) * texelSize.xy;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                float2 smoothUV = SmoothPointUV(input.uv, _MainTex_TexelSize);
                const half4 main = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, smoothUV);
                const half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_LinearClamp, smoothUV);
                const half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_LinearClamp, smoothUV));

                SurfaceData2D surfaceData;
                InputData2D inputData;

                InitializeSurfaceData(main.rgb, main.a, mask, normalTS, surfaceData);
                InitializeInputData(input.uv, input.lightingUV, inputData);

#if defined(DEBUG_DISPLAY)
                SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, input.positionWS, input.positionCS, _MainTex);
                surfaceData.normalWS = input.normalWS;
#endif

                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4   color           : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/NormalsRenderingShared.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_MainTex);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START( UnityPerMaterial )
                half4 _Color;
            CBUFFER_END

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = (Varyings) 0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.normalWS = TransformObjectToWorldDir(input.normal);
                o.tangentWS = TransformObjectToWorldDir(input.tangent.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * input.tangent.w;
                o.color = input.color * _Color * unity_SpriteColor;

                return o;
            }

            float2 SmoothPointUV(float2 uv, float4 texelSize)
            {
                float2 pixel = uv * texelSize.zw;
                float2 seam = floor(pixel + 0.5);
                float2 duv = max(fwidth(pixel), 0.0001);
                float2 fracCoord = pixel - seam;
                float2 smoothFrac = clamp(fracCoord / duv, -0.5, 0.5);
                return (seam + smoothFrac) * texelSize.xy;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                // Setup instancing for SpriteFlip is used in NormalsRenderingShared
                SetUpSpriteInstanceProperties();

                float2 smoothUV = SmoothPointUV(input.uv, _MainTex_TexelSize);
                const half4 mainTex = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, smoothUV);
                const half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_LinearClamp, smoothUV));

                return NormalsRenderingShared(mainTex, normalTS, input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            float4 _MainTex_TexelSize;

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color *_Color * unity_SpriteColor;
                return o;
            }

            float2 SmoothPointUV(float2 uv, float4 texelSize)
            {
                float2 pixel = uv * texelSize.zw;
                float2 seam = floor(pixel + 0.5);
                float2 duv = max(fwidth(pixel), 0.0001);
                float2 fracCoord = pixel - seam;
                float2 smoothFrac = clamp(fracCoord / duv, -0.5, 0.5);
                return (seam + smoothFrac) * texelSize.xy;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                float2 smoothUV = SmoothPointUV(input.uv, _MainTex_TexelSize);
                float4 mainTex = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, smoothUV);

#if defined(DEBUG_DISPLAY)
                SurfaceData2D surfaceData;
                InputData2D inputData;
                half4 debugColor = 0;

                InitializeSurfaceData(mainTex.rgb, mainTex.a, surfaceData);
                InitializeInputData(input.uv, inputData);
                SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, input.positionWS, input.positionCS, _MainTex);

                if (CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
                {
                    return debugColor;
                }
#endif

                return mainTex;
            }
            ENDHLSL
        }
    }
}
