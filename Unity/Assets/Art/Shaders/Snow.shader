// Snow lying on alpine tiles. A map-wide track texture (set by SnowField) says how far the snow is
// pressed at each XZ; vertices marked deformable (vertex color R) sink by that much, and top-face
// vertices (vertex color G) take their normal from the texture slope so prints catch the light.
Shader "MyGame/Snow"
{
    Properties
    {
        _BaseColor ("Snow Color", Color) = (0.93, 0.95, 1, 1)
        _PressTint ("Pressed Tint", Color) = (0.72, 0.80, 0.95, 1)
        _PressDepth ("Max Press Depth", Float) = 0.065
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _PressTint;
            float _PressDepth;
        CBUFFER_END

        TEXTURE2D(_SnowTracks);
        SAMPLER(sampler_SnowTracks);
        float4 _SnowTrackRect;   // xy: world XZ of the texture's corner, zw: 1 / world size
        float4 _SnowTrackTexel;  // xy: one texel in UV, zw: one texel in world units

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
        };

        float SamplePress(float2 uv)
        {
            return SAMPLE_TEXTURE2D_LOD(_SnowTracks, sampler_SnowTracks, uv, 0).r;
        }

        // Sinks deformable vertices; returns world position and the press amount, updates the normal.
        float3 PressSnow(Attributes input, inout float3 normalWS, out float press)
        {
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            float2 uv = (positionWS.xz - _SnowTrackRect.xy) * _SnowTrackRect.zw;
            press = SamplePress(uv) * input.color.r;
            positionWS.y -= press * _PressDepth;

            if (input.color.g > 0.5)
            {
                float dx = SamplePress(uv + float2(_SnowTrackTexel.x, 0)) - SamplePress(uv - float2(_SnowTrackTexel.x, 0));
                float dz = SamplePress(uv + float2(0, _SnowTrackTexel.y)) - SamplePress(uv - float2(0, _SnowTrackTexel.y));
                float scale = _PressDepth * 0.5;
                normalWS = normalize(float3(dx * scale / _SnowTrackTexel.z, 1.0, dz * scale / _SnowTrackTexel.w));
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
                float press : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = PressSnow(input, output.normalWS, output.press);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 direct = light.color * (saturate(dot(normalWS, light.direction)) * light.shadowAttenuation);
                half3 albedo = lerp(_BaseColor.rgb, _BaseColor.rgb * _PressTint.rgb, saturate(input.press * 1.5));
                return half4(albedo * (SampleSH(normalWS) + direct), 1.0);
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
                float press;
                return TransformWorldToHClip(PressSnow(input, normalWS, press));
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
                float press;
                output.positionCS = TransformWorldToHClip(PressSnow(input, output.normalWS, press));
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
