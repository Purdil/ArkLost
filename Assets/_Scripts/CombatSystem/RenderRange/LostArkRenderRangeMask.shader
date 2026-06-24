Shader "ArkLost/Combat/LostArkRenderRangeMask"
{
    Properties
    {
        _BaseMap ("Texture Mask", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 0.08, 0.02, 0.6)
        _EmissionColor ("Emission Color", Color) = (1, 0.45, 0.1, 0)
        _SolidAlpha ("Solid Alpha", Range(0, 1)) = 0.45
        _MaskAlpha ("Mask Alpha", Range(0, 2)) = 0.55
        _MaskPower ("Mask Power", Range(0.25, 8)) = 1.7
        _Intensity ("Intensity", Range(0, 4)) = 1
        _SrcBlend ("Source Blend", Float) = 5
        _DstBlend ("Destination Blend", Float) = 10
        _ZWrite ("Z Write", Float) = 0
        _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _SolidAlpha;
                half _MaskAlpha;
                half _MaskPower;
                half _Intensity;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half luminanceMask = max(max(tex.r, tex.g), tex.b);
                half textureMask = pow(saturate(luminanceMask), max(_MaskPower, 0.001));
                half alpha = saturate((_SolidAlpha + textureMask * _MaskAlpha) * _BaseColor.a * _Intensity);

                half3 innerColor = _BaseColor.rgb * 0.62;
                half3 litColor = _BaseColor.rgb + _EmissionColor.rgb;
                half3 color = lerp(innerColor, litColor, textureMask) * _Intensity;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
