// The monsters' projectiles: small ghosts of glowing smoke (meshes from Tools/make_projectiles.py). Like the
// monsters they breathe and wobble out of shape, the tail whips about, and they are only half there: the core
// is see-through, the rim brighter, the tail fades away. Vertex colour R marks what glows hottest (eyes,
// cracks, a cutting edge), G runs 0 at the head to 1 at the tail tip. _Phase is set per projectile.
Shader "Roguelike/GhostBolt"
{
    Properties
    {
        [HDR] _CoreColor ("Core", Color) = (1.2, 0.5, 2.4, 1)
        [HDR] _RimColor ("Rim", Color) = (2.4, 1.4, 4.0, 1)
        [HDR] _EyeColor ("Eyes / Glow", Color) = (4, 3.5, 5, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.7
        _Density ("Core Density (0 = see-through middle)", Range(0, 1)) = 0
        _Size ("Size (m)", Float) = 0.3
        _BreatheAmount ("Breathe Amount", Range(0, 0.3)) = 0.08
        _BreatheSpeed ("Breathe Speed", Float) = 7
        _WarpAmount ("Misshape Amount", Range(0, 0.3)) = 0.08
        _WarpScale ("Misshape Noise Scale", Float) = 3
        _WarpSpeed ("Misshape Speed", Float) = 2.5
        _TailWhip ("Tail Whip", Range(0, 1)) = 0.35
        _Phase ("Phase (per projectile)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+15" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "GhostBolt"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GhostCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _RimColor;
                half4 _EyeColor;
                half _Opacity;
                half _Density;
                float _Size;
                float _BreatheAmount;
                float _BreatheSpeed;
                float _WarpAmount;
                float _WarpScale;
                float _WarpSpeed;
                float _TailWhip;
                float _Phase;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half2 mask : TEXCOORD2;        // glow, tail
                float fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float t = _Time.y;
                float tail = v.color.g;
                float3 p = v.positionOS.xyz;
                float3 q = p / max(_Size, 1e-3) * _WarpScale + float3(_Phase * 3.1, -t * _WarpSpeed, _Phase);
                float breathe = sin(t * _BreatheSpeed + _Phase) * _BreatheAmount;
                float bulge = GhostValueNoise(q) * 2.0 - 1.0;
                p += v.normalOS * (breathe + bulge * _WarpAmount) * _Size * (1.0 - 0.5 * tail);
                // the tail lashes from side to side, more towards its tip
                float whip = sin(t * 11.0 + tail * 5.0 + _Phase) * tail * tail * _TailWhip * _Size;
                p.x += whip;
                p.y += cos(t * 9.0 + tail * 4.0 + _Phase * 1.7) * tail * tail * _TailWhip * _Size * 0.5;
                o.positionWS = TransformObjectToWorld(p);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.mask = half2(v.color.r, tail);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 viewWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half ndv = saturate(abs(dot(n, viewWS)));
                half rim = pow(1.0 - ndv, 2.0);
                // drifting smoke inside the body
                float3 local = (i.positionWS - GhostPivotWS()) / max(_Size, 1e-3);
                half smoke = GhostFbm(local * 3.5 + float3(_Phase, -_Time.y * 2.0, _Phase * 0.7));
                half flicker = 0.85 + 0.15 * sin(_Time.y * 23.0 + _Phase * 5.0);
                half3 col = lerp(_CoreColor.rgb * (0.6 + 0.6 * smoke), _RimColor.rgb, rim) * flicker;
                half core = lerp(0.35 + 0.4 * smoke, 1.0, _Density);
                half alpha = _Opacity * lerp(core, 1.0, rim) * (1.0 - i.mask.y * i.mask.y * 0.9);
                half glow = saturate(i.mask.x);
                col = lerp(col, _EyeColor.rgb, glow);
                alpha = max(alpha, glow * 0.95);
                col = MixFog(col, i.fogFactor);
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
