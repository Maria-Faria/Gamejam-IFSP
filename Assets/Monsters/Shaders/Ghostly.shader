// Body of a ghost monster: lit like URP/Lit (albedo, normal, metallic/smoothness), drawn 95% opaque, with
// the eyes (bright, cool parts of the albedo) glowing, a smoky rim, and the vertices breathing and wobbling
// (GhostCommon.hlsl). A depth pre-pass keeps the see-through body from showing its own back faces.
Shader "Roguelike/Ghostly"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        _MetallicGlossMap ("Metallic (R) Smoothness (A)", 2D) = "black" {}
        _Smoothness ("Smoothness Scale", Range(0, 1)) = 0.6
        _Opacity ("Opacity", Range(0, 1)) = 0.95

        _Size ("Monster Height (m)", Float) = 1
        _BreatheAmount ("Breathe Amount", Range(0, 0.1)) = 0.02
        _BreatheSpeed ("Breathe Speed", Float) = 1.8
        _WarpAmount ("Misshape Amount", Range(0, 0.1)) = 0.018
        _WarpScale ("Misshape Noise Scale", Float) = 2.5
        _WarpSpeed ("Misshape Speed", Float) = 0.5
        _HemSway ("Bottom Sway", Range(0, 2)) = 0.8

        _RimColor ("Smoke Rim Color", Color) = (0.42, 0.3, 0.7, 1)
        _RimPower ("Smoke Rim Power", Float) = 2.5
        _RimStrength ("Smoke Rim Strength", Range(0, 2)) = 0.6
        _GlowThreshold ("Eye Glow Threshold", Range(0, 1.5)) = 0.55
        [HDR] _GlowColor ("Eye Glow", Color) = (2.2, 1.7, 3.5, 1)
        _Phase ("Phase (per instance)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "GhostCommon.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _Smoothness;
            half _Opacity;
            float _Size;
            float _BreatheAmount;
            float _BreatheSpeed;
            float _WarpAmount;
            float _WarpScale;
            float _WarpSpeed;
            float _HemSway;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half _GlowThreshold;
            half4 _GlowColor;
            float _Phase;
        CBUFFER_END

        float3 WarpedPositionWS(float3 positionOS, float3 normalOS, out float3 normalWS)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            normalWS = TransformObjectToWorldNormal(normalOS);
            return positionWS + GhostWarpWS(positionWS, normalWS, _Size, _Phase, _BreatheAmount, _BreatheSpeed,
                                            _WarpAmount, _WarpScale, _WarpSpeed, _HemSway);
        }
        ENDHLSL

        // 1) depth only, so the 95% opaque body hides its own far side
        Pass
        {
            Name "GhostDepth"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 n;
                o.positionCS = TransformWorldToHClip(WarpedPositionWS(v.positionOS.xyz, v.normalOS, n));
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        // 2) lit colour, alpha blended over what is behind
        Pass
        {
            Name "GhostForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 normalWS;
                o.positionWS = WarpedPositionWS(v.positionOS.xyz, v.normalOS, normalWS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = normalWS;
                o.tangentWS = float4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half4 ms = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, i.uv);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);

                float3 n = normalize(i.normalWS);
                float3 t = normalize(i.tangentWS.xyz);
                float3 b = cross(n, t) * i.tangentWS.w;
                float3 normalWS = normalize(mul(normalTS, float3x3(t, b, n)));
                float3 viewWS = GetWorldSpaceNormalizeViewDir(i.positionWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogFactor;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                // eyes: the bright, cool (blue/violet) parts of the albedo; bone-white masks stay unlit
                half lum = dot(albedo.rgb, half3(0.299, 0.587, 0.114));
                half cool = saturate((albedo.b - albedo.r) * 4.0 + 0.35);
                half eye = smoothstep(_GlowThreshold, _GlowThreshold + 0.15, lum) * cool;
                half rim = pow(1.0 - saturate(dot(n, viewWS)), _RimPower) * _RimStrength;

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo.rgb;
                s.metallic = ms.r;
                s.smoothness = ms.a * _Smoothness;
                s.normalTS = normalTS;
                s.occlusion = 1;
                s.alpha = 1;
                s.emission = _GlowColor.rgb * eye + _RimColor.rgb * rim;

                half4 color = UniversalFragmentPBR(inputData, s);
                color.rgb = MixFog(color.rgb, i.fogFactor);
                color.a = _Opacity * albedo.a;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 normalWS;
                float3 positionWS = WarpedPositionWS(v.positionOS.xyz, v.normalOS, normalWS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = positionCS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
