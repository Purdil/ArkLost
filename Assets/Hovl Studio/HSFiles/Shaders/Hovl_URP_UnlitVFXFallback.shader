Shader "Hovl/URP/Unlit VFX Fallback"
{
    Properties
    {
        _MainTex("Main Tex", 2D) = "white" {}
        _MainTexture("Main Texture", 2D) = "white" {}
        _BaseMap("Base Map", 2D) = "white" {}
        _Color("Color", Color) = (1, 1, 1, 1)
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _TintColor("Tint Color", Color) = (1, 1, 1, 1)
        _Emission("Emission", Float) = 1
        _Opacity("Opacity", Range(0, 3)) = 1
        _Cull("Cull", Float) = 0
        _CullMode("Cull Mode", Float) = 0
        _ZWrite("ZWrite", Float) = 0
        _SrcBlend("Src Blend", Float) = 5
        _DstBlend("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _BaseColor;
                half4 _TintColor;
                half _Emission;
                half _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positionInputs.positionCS;
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 color = tex * _Color * _BaseColor * _TintColor * input.color;
                color.rgb *= max(_Emission, 0.0h);
                color.a *= _Opacity;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
