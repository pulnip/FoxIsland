// Voxel grass: every blade bends about its own base, driven by a travelling wind wave and by a
// trail of recent trampler positions (set by GrassField). Blade data lives in the mesh UVs:
// uv0.x = per-blade random phase, uv1 = blade base (x, z) in object space.
Shader "MyGame/Grass"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.64, 0.86, 0.36, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        #define GRASS_TRAIL_LENGTH 16

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
        CBUFFER_END

        float4 _GrassWind;                          // xy: direction on XZ (normalized), z: frequency (Hz), w: wavelength
        float4 _GrassBend;                          // x: max sway (rad), y: max trample (rad), z: trample radius, w: height tolerance
        float4 _GrassTrample[GRASS_TRAIL_LENGTH];   // xyz: trampler position, w: strength (0 = unused)

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv0 : TEXCOORD0;
            float2 uv1 : TEXCOORD1;
        };

        float3 RotateAbout(float3 v, float3 axis, float angle)
        {
            float s, c;
            sincos(angle, s, c);
            return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
        }

        // Bends one blade rigidly about its base; returns world position, rotates normalWS in place.
        float3 BendBlade(Attributes input, inout float3 normalWS)
        {
            float3 baseWS = TransformObjectToWorld(float3(input.uv1.x, 0.0, input.uv1.y));
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

            float2 windDir = _GrassWind.xy;
            float time = _Time.y * _GrassWind.z * TWO_PI;
            float phase = dot(baseWS.xz, windDir) / _GrassWind.w * TWO_PI + input.uv0.x * 1.5;
            float gust = sin(time - phase) * 0.75 + sin(time * 2.3 - phase * 1.7) * 0.25;
            float windAngle = _GrassBend.x * (0.5 + 0.5 * gust);

            float push = 0.0;
            float2 away = windDir;
            [unroll]
            for (int i = 0; i < GRASS_TRAIL_LENGTH; i++)
            {
                float4 trail = _GrassTrample[i];
                float2 offset = baseWS.xz - trail.xz;
                float dist = length(offset);
                float sameLevel = step(abs(baseWS.y - trail.y), _GrassBend.w);
                float strength = saturate(1.0 - dist / _GrassBend.z) * trail.w * sameLevel;
                if (strength > push)
                {
                    push = strength;
                    away = dist > 1e-4 ? offset / dist : windDir;
                }
            }

            push = smoothstep(0.0, 1.0, push);
            float2 lean = windDir * windAngle * (1.0 - push) + away * _GrassBend.y * push;
            float angle = length(lean);
            if (angle > 1e-5)
            {
                float2 direction = lean / angle;
                float3 axis = normalize(cross(float3(0.0, 1.0, 0.0), float3(direction.x, 0.0, direction.y)));
                positionWS = baseWS + RotateAbout(positionWS - baseWS, axis, angle);
                normalWS = RotateAbout(normalWS, axis, angle);
            }

            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = BendBlade(input, output.normalWS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 direct = light.color * (saturate(dot(normalWS, light.direction)) * light.shadowAttenuation);
                return half4(_BaseColor.rgb * (SampleSH(normalWS) + direct), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Vert(Attributes input) : SV_POSITION
            {
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                return TransformWorldToHClip(BendBlade(input, normalWS));
            }

            half Frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(BendBlade(input, output.normalWS));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}
