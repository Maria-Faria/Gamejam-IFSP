// A puffed-out shell of smoke around a ghost monster, drawn as a second material on its mesh. The shell is
// the body pushed out along the normals (with the same breathing/misshaping as Roguelike/Ghostly) and
// filled with drifting fbm noise, so the silhouette dissolves into curling wisps; it thickens towards the
// bottom for monsters that trail smoke instead of standing on legs.
Shader "Roguelike/GhostSmoke"
{
    Properties
    {
        _SmokeColor ("Smoke Color", Color) = (0.42, 0.33, 0.62, 1)
        _SmokeDark ("Smoke Core Color", Color) = (0.12, 0.09, 0.18, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.6
        _Inflate ("Inflate (x height)", Range(0, 0.2)) = 0.04
        _NoiseScale ("Noise Scale", Float) = 3.5
        _FlowSpeed ("Rise Speed", Float) = 0.35
        _Threshold ("Wisp Threshold", Range(0, 1)) = 0.42
        _FresnelPower ("Edge Power", Float) = 1.6
        _BottomBoost ("Extra Smoke Low Down", Range(0, 2)) = 0.6
        _TopBoost ("Extra Smoke Up High", Range(0, 2)) = 0.0

        _Size ("Monster Height (m)", Float) = 1
        _BreatheAmount ("Breathe Amount", Range(0, 0.1)) = 0.02
        _BreatheSpeed ("Breathe Speed", Float) = 1.8
        _WarpAmount ("Misshape Amount", Range(0, 0.1)) = 0.03
        _WarpScale ("Misshape Noise Scale", Float) = 2.5
        _WarpSpeed ("Misshape Speed", Float) = 0.5
        _HemSway ("Bottom Sway", Range(0, 2)) = 1
        _Phase ("Phase (per instance)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "SmokeForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GhostCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SmokeColor;
                half4 _SmokeDark;
                half _Opacity;
                float _Inflate;
                float _NoiseScale;
                float _FlowSpeed;
                half _Threshold;
                half _FresnelPower;
                half _BottomBoost;
                half _TopBoost;
                float _Size;
                float _BreatheAmount;
                float _BreatheSpeed;
                float _WarpAmount;
                float _WarpScale;
                float _WarpSpeed;
                float _HemSway;
                float _Phase;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 local : TEXCOORD2;     // position relative to the pivot, in units of the height
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                positionWS += GhostWarpWS(positionWS, normalWS, _Size, _Phase, _BreatheAmount, _BreatheSpeed,
                                          _WarpAmount, _WarpScale, _WarpSpeed, _HemSway);
                float3 local = (positionWS - GhostPivotWS()) / max(_Size, 1e-3);
                // puff out unevenly: lumpy billows that roll upward
                float billow = GhostValueNoise(local * 4.0 + float3(_Phase, -_Time.y * _FlowSpeed * 2.0, 0));
                float low = saturate(0.45 - local.y) * _BottomBoost;
                positionWS += normalWS * _Inflate * _Size * (0.55 + 0.9 * billow + low);
                o.positionWS = positionWS;
                o.normalWS = normalWS;
                o.local = local;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float3 n = normalize(i.normalWS);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                // swirling, rising noise: domain-warped fbm
                float3 q = i.local * _NoiseScale + float3(_Phase * 2.7, -t * _FlowSpeed * _NoiseScale, _Phase);
                float3 w = float3(GhostValueNoise(q * 0.7 + 11.3), GhostValueNoise(q * 0.7 + 3.7), GhostValueNoise(q * 0.7 + 7.1));
                float smoke = GhostFbm(q + (w - 0.5) * 2.2);

                half ndv = saturate(abs(dot(n, viewWS)));
                half edge = pow(1.0 - ndv, _FresnelPower);
                half low = saturate(0.5 - i.local.y) * 2.0 * _BottomBoost;
                half high = saturate(i.local.y - 0.6) * 2.5 * _TopBoost;
                half density = smoke * (0.45 + edge * 0.9 + low + high);
                half alpha = smoothstep(_Threshold, _Threshold + 0.3, density) * _Opacity;
                // fade out where the shell turns away, so its own silhouette never shows as a hard outline
                alpha *= smoothstep(0.0, 0.45, ndv) * (0.6 + 0.4 * smoothstep(0.3, 0.7, smoke));

                Light mainLight = GetMainLight();
                half lit = saturate(dot(n, mainLight.direction)) * 0.5 + 0.5;
                half3 col = lerp(_SmokeDark.rgb, _SmokeColor.rgb, saturate(smoke * 1.4 - 0.1)) * lit;
                col += SampleSH(n) * _SmokeColor.rgb * 0.35;
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
