// Assets/Shaders/LowPolySmoke.shader
// Z-Era 项目专用 - 低多边形风格烟雾 Shader (URP 兼容)
// 特点：锯齿边缘、硬裁剪、半透明混合

Shader "Z-Era/LowPolySmoke"
{
    Properties
    {
        _MainTex("贴图", 2D) = "white" {}
        _Color("基础色", Color) = (1,1,1,1)
        _Cutoff("Alpha 裁剪阈值", Range(0.0,1.0)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+100"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(positionInputs.positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_TARGET
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;
                
                // 硬 Alpha 裁剪（产生锯齿边缘）
                clip(col.a - _Cutoff);
                
                return col;
            }
            ENDHLSL
        }
    }

    Fallback "Hidden/InternalErrorShader"
}