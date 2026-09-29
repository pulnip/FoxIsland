// Lake water: a grid mesh bobbed by travelling sine waves in the vertex shader. Color and opacity
// follow the water depth read from the camera depth texture, with a foam band where the surface
// meets anything opaque (tile pillars, the sinking fox) and a second band drifting off the shore.
Shader "MyGame/Water"
{
    Properties
    {
        _ShallowColor ("Shallow Color (A = opacity)", Color) = (0.36, 0.78, 0.80, 0.45)
        _DeepColor ("Deep Color (A = opacity)", Color) = (0.12, 0.38, 0.62, 0.92)
        _FoamColor ("Foam Color", Color) = (0.92, 0.97, 1, 1)
        _DepthFade ("Depth Fade", Float) = 0.8
        _FoamWidth ("Foam Width", Float) = 0.06
        _WaveHeight ("Wave Height", Float) = 0.02
        _WaveLength ("Wave Length", Float) = 2.5
        _WaveSpeed ("Wave Speed", Float) = 0.4
        _NormalStrength ("Normal Strength", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float _DepthFade;
                float _FoamWidth;
                float _WaveHeight;
                float _WaveLength;
                float _WaveSpeed;
                float _NormalStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            // Adds one travelling sine to the height and its gradient (dh/dx, dh/dz) to the slope.
            void AddWave(float2 xz, float2 direction, float wavelength, float amplitude, inout float height, inout float2 slope)
            {
                float k = TWO_PI / wavelength;
                float phase = k * (dot(direction, xz) - _WaveSpeed * _Time.y);
                float s, c;
                sincos(phase, s, c);
                height += amplitude * s;
                slope += amplitude * k * c * direction;
            }

            float Waves(float2 xz, out float2 slope)
            {
                float height = 0.0;
                slope = 0.0;
                AddWave(xz, float2(0.96, 0.28), _WaveLength, _WaveHeight, height, slope);
                AddWave(xz, float2(-0.37, 0.93), _WaveLength * 0.63, _WaveHeight * 0.6, height, slope);
                AddWave(xz, float2(0.66, -0.75), _WaveLength * 0.41, _WaveHeight * 0.4, height, slope);
                return height;
            }

            // Eye depth of the opaque scene behind the pixel, for perspective and orthographic cameras.
            float SceneEyeDepth(float2 screenUV)
            {
                float raw = SampleSceneDepth(screenUV);
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                    raw = 1.0 - raw;
                    #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                }

                return LinearEyeDepth(raw, _ZBufferParams);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float2 slope;
                positionWS.y += Waves(positionWS.xz, slope);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 positionWS = input.positionWS;
                float2 slope;
                Waves(positionWS.xz, slope);
                float ripple = 0.0;
                AddWave(positionWS.xz, float2(0.5, 0.87), 0.55, 0.004, ripple, slope);
                AddWave(positionWS.xz, float2(-0.9, 0.44), 0.43, 0.003, ripple, slope);
                float3 normalWS = normalize(float3(-slope.x * _NormalStrength, 1.0, -slope.y * _NormalStrength));

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float surfaceEye = -TransformWorldToView(positionWS).z;
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
                float depth = max(0.0, SceneEyeDepth(screenUV) - surfaceEye) * abs(viewDirWS.y);

                float wobble = sin(dot(positionWS.xz, float2(5.1, 3.7)) + _Time.y * 1.7)
                             * sin(dot(positionWS.xz, float2(-3.3, 4.9)) - _Time.y * 1.3);
                float foam = step(depth, _FoamWidth * (1.0 + 0.5 * wobble));
                float drift = frac(_Time.y * 0.35);
                float driftDepth = _FoamWidth * (1.5 + 2.5 * drift);
                foam = max(foam, step(abs(depth - driftDepth), _FoamWidth * 0.2) * step(drift, 0.8 + 0.2 * wobble));

                float deep = saturate(depth / _DepthFade);
                half4 water = lerp(_ShallowColor, _DeepColor, deep);

                Light light = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half3 direct = light.color * (saturate(dot(normalWS, light.direction)) * light.shadowAttenuation);
                half3 lighting = SampleSH(normalWS) + direct;

                half3 color = lerp(water.rgb, _FoamColor.rgb, foam) * lighting;
                half alpha = max(water.a, foam);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
