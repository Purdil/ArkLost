Shader "ArkLost/Combat/BossHitWrapOverlay"
{
    Properties
    {
        _HitWrapColor ("Hit Wrap Color", Color) = (1, 1, 1, 0.32)
        _RimColor ("Rim Color", Color) = (0.92, 0.98, 1, 0.62)
        _ShellWidth ("Shell Width", Float) = 0.012
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.2
        _RimIntensity ("Rim Intensity", Range(0, 4)) = 1.4
        _NoiseScale ("Noise Scale", Range(0.1, 60)) = 18
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.42
        _DistortionScale ("Distortion Scale", Range(0.1, 40)) = 7
        _DistortionStrength ("Distortion Strength", Range(0, 1)) = 0.24
        _PulseSpeed ("Pulse Speed", Range(0, 20)) = 8
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+40"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Boss Hit Wrap Overlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HitWrapColor;
                half4 _RimColor;
                float _ShellWidth;
                float _RimPower;
                float _RimIntensity;
                float _NoiseScale;
                float _NoiseStrength;
                float _DistortionScale;
                float _DistortionStrength;
                float _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 uv)
            {
                float2 id = floor(uv);
                float2 f = frac(uv);
                float a = Hash21(id);
                float b = Hash21(id + float2(1.0, 0.0));
                float c = Hash21(id + float2(0.0, 1.0));
                float d = Hash21(id + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float FractalNoise(float2 uv)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                for (int i = 0; i < 3; i++)
                {
                    value += ValueNoise(uv * frequency) * amplitude;
                    frequency *= 2.03;
                    amplitude *= 0.5;
                }

                return value;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 shellPositionOS = input.positionOS.xyz + normalize(input.normalOS) * _ShellWidth;
                output.positionWS = TransformObjectToWorld(shellPositionOS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirectionWS = normalize(GetWorldSpaceViewDir(input.positionWS));

                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDirectionWS)), _RimPower);
                float timeOffset = _Time.y * _PulseSpeed;
                float2 noiseUv = input.positionWS.xz * _NoiseScale + float2(timeOffset * 0.09, -timeOffset * 0.06);
                float2 distortionUv = input.positionWS.xy * _DistortionScale + float2(-timeOffset * 0.07, timeOffset * 0.11);
                float distortion = (FractalNoise(distortionUv) - 0.5) * _DistortionStrength;
                float surfaceNoise = FractalNoise(noiseUv + distortion);
                float vein = smoothstep(0.58, 0.94, surfaceNoise + fresnel * 0.18);
                float pulse = 0.82 + sin(timeOffset + surfaceNoise * 5.0) * 0.18;

                float texturedAlpha = _HitWrapColor.a * lerp(1.0 - _NoiseStrength, 1.0, surfaceNoise);
                texturedAlpha += vein * _HitWrapColor.a * _NoiseStrength * 0.75;

                float rimAlpha = fresnel * _RimColor.a * _RimIntensity;
                float finalAlpha = saturate((texturedAlpha + rimAlpha) * pulse);
                float3 finalColor = _HitWrapColor.rgb + _RimColor.rgb * rimAlpha + vein * _RimColor.rgb * 0.35;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
}
