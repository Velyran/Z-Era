// 主菜单黑屏换景专用：全屏黑幕从左往右溶解铺满 / 溶解消退。
//
// 与 UI Dissolve Effect（全局溶解、无方向）的区别：
//   · 判据是"图像 UV.x + 噪声扰动"与移动阈值 _WipeT 比较，
//     扫描有明确方向（固定从左往右），边缘因噪声扰动呈溶解状；
//   · 噪声 UV 按 _NoiseScroll * _NoiseTime 随时间平移，边缘持续变化；
//     _NoiseTime 由脚本按未缩放时间写入，timeScale = 0 也照走；
//   · 运行时参数（_WipeT / _WipeFlip / _NoiseTime / _Aspect）走材质属性，
//     由 Assets/Scripts/Assets/MainMenu/MainMenuCameraWipe.cs 写入
//     材质实例，不占用 UV 通道，因此无需 Canvas 额外着色器通道。
//
// 参数：
//   _WipeT   扫描进度 0→1；_WipeFlip = 0 为覆盖（黑在阈值左侧），
//            = 1 为消退（黑在阈值右侧），两次扫描都从左往右推进。
//   _EdgeWidth   边缘噪声扰动幅度（占图像宽度比例）。
//   _EdgeSoftness 边缘柔化宽度。
//   _Aspect  图像宽高比，由脚本每帧写入，避免噪声被拉扁。
Shader "UI Wipe Dissolve"
{
	Properties
	{
		[PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)

		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255

		_ColorMask ("Color Mask", Float) = 15

		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

		[Header(Wipe)]
		[HideInInspector] _WipeT ("Wipe Progress", Range(0, 1)) = 0
		[HideInInspector] _WipeFlip ("Wipe Flip (0 cover / 1 reveal)", Float) = 0

		_NoiseTex ("Noise Texture (A)", 2D) = "white" {}
		_NoiseScale ("Noise Scale", Float) = 2
		_NoiseScroll ("Noise Scroll (UV per second)", Vector) = (0.12, 0.04, 0, 0)
		[HideInInspector] _NoiseTime ("Noise Time", Float) = 0

		_EdgeWidth ("Edge Width", Range(0, 0.5)) = 0.12
		_EdgeSoftness ("Edge Softness", Range(0.001, 0.5)) = 0.06

		[HideInInspector] _Aspect ("Aspect Ratio", Float) = 1.7778
	}

	SubShader
	{
		Tags
		{
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"RenderType"="Transparent"
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
		}

		Stencil
		{
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}

		Cull Off
		Lighting Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask [_ColorMask]

		Pass
		{
			Name "Default"

		CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0

			#pragma multi_compile __ UNITY_UI_ALPHACLIP

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4 vertex      : SV_POSITION;
				fixed4 color       : COLOR;
				float2 texcoord    : TEXCOORD0;
				float4 worldPosition : TEXCOORD1;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			fixed4 _Color;
			fixed4 _TextureSampleAdd;
			float4 _ClipRect;
			sampler2D _MainTex;
			sampler2D _NoiseTex;

			float _WipeT;
			float _WipeFlip;
			float _NoiseScale;
			float4 _NoiseScroll;
			float _NoiseTime;
			float _EdgeWidth;
			float _EdgeSoftness;
			float _Aspect;

			v2f vert(appdata_t IN)
			{
				v2f OUT;
				UNITY_SETUP_INSTANCE_ID(IN);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

				OUT.worldPosition = IN.vertex;
				OUT.vertex = UnityObjectToClipPos(IN.vertex);
				OUT.texcoord = IN.texcoord;
				OUT.color = IN.color * _Color;

				return OUT;
			}

			fixed4 frag(v2f IN) : SV_Target
			{
				half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

				color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);

				// 噪声 UV：先按宽高比校正（噪声不被宽屏拉扁），
				// 再随时间平移，让边缘在整个扫描过程中持续变化。
				// 频率取整数倍平铺，滚动跨越贴图边界时不会跳变。
				float2 noiseUV =
					float2(IN.texcoord.x * _Aspect, IN.texcoord.y) * _NoiseScale +
					_NoiseScroll.xy * _NoiseTime;

				float noise = tex2D(_NoiseTex, noiseUV).a;

				// 扫描坐标 = 图像横向 UV + 噪声扰动：
				// 扰动只作用在边缘带内，左右两端仍能完全铺满/完全透明。
				float d = IN.texcoord.x + (noise - 0.5) * _EdgeWidth;

				// 阈值两端各留出"半扰动 + 柔化"的余量，
				// 保证进度 0 与 1 分别是完全透明与完全全黑（不留残边）。
				float pad = _EdgeWidth * 0.5 + _EdgeSoftness;
				float t = lerp(-pad, 1.0 + pad, _WipeT);

				// 覆盖：黑幕在阈值左侧；消退：黑幕在阈值右侧。
				// 两次都是阈值从左往右走，方向一致。
				float side = (_WipeFlip > 0.5) ? (d - t) : (t - d);

				color.a *= saturate(side / _EdgeSoftness);

				#ifdef UNITY_UI_ALPHACLIP
				clip(color.a - 0.01);
				#endif

				return color;
			}
		ENDCG
		}
	}
}
