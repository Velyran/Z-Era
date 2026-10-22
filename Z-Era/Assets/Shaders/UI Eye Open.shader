// 睁眼开场遮罩：全屏黑幕从画面中央睁出一个"眼睛"形状的开口，
// 眼缝横贯整个画面宽度（_EyeWidth = 1 时两端正好收到屏幕左右缘）。
// 睑缘为伪模糊羽化：加宽的噪声调制软过渡带（不采样场景，非真实模糊）。
//
// 与 UI Wipe Dissolve（左右扫描）的区别：本 shader 的开口是
// 眼型——中央先露一条缝、向两端收窄，上下眼睑可不对称，
// 开口随 _EyeOpen 变化：0 全黑 → 1 眼睛全睁（眼型最大）
// → 2 整屏揭开——以上/下睑曲线为边界整体外推退出画面，
// 全程保持眼型弧线，收尾隐藏无跳变。
//
// 计算方式（frag）：
//   · 坐标以画面中心为原点，x 按宽高比换算成与 y 同尺度；
//   · 眼型包络 = (1 - (|x|/半宽)^2) ^ 睑形指数：中央 1、
//     到两眼角归 0，指数越大眼角越尖；
//   · 开口半高 = 最大半高 × _EyeOpen × 包络；
//   · 边缘叠一层随时间滚动的噪声扰动，睑缘不是数学曲线；
//   · _EyeOpen = 0 时噪声项乘 0，保证全黑阶段严丝合缝。
//
// 运行时参数（_EyeOpen / _Aspect / _NoiseTime）由
// Assets/Scripts/UI/EyeOpenIntroController.cs 写材质实例，
// 只用 UV0，不需要 Canvas 额外着色器通道。
//
// 形状参数（眼宽/眼高/眼角/柔化/噪声）在材质上实时调。
Shader "UI Eye Open"
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

		[Header(Eye)]
		[HideInInspector] _EyeOpen ("Eye Open (0 closed / 1 eyes open / 2 revealed)", Range(0, 2)) = 0
		_EyeWidth ("Eye Width (fraction of screen width, 1 = edge to edge)", Range(0.05, 1)) = 1
		_EyeHeight ("Eye Max Half Height (fraction of screen height)", Range(0.01, 0.5)) = 0.18
		_EyeCurve ("Lid Shape (bigger = sharper corners)", Range(0.25, 4)) = 1.75
		_LowerLidRatio ("Lower Lid Ratio (1 = symmetric)", Range(0.2, 1.5)) = 0.85
		_EdgeSoftness ("Edge Softness", Range(0.001, 0.3)) = 0.03
		_EdgeBlur ("Edge Blur (pseudo, fraction of screen height)", Range(0, 0.3)) = 0.06
		_EdgeBlurNoise ("Edge Blur Noise (band width variation)", Range(0, 1)) = 0.5

		[Header(Edge Noise)]
		_NoiseTex ("Noise Texture (A)", 2D) = "white" {}
		_NoiseScale ("Noise Scale", Float) = 6
		_NoiseAmount ("Noise Amount", Range(0, 0.2)) = 0.03
		_NoiseScroll ("Noise Scroll (UV per second)", Vector) = (0.1, 0.03, 0, 0)
		[HideInInspector] _NoiseTime ("Noise Time", Float) = 0

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
				float4 vertex        : SV_POSITION;
				fixed4 color         : COLOR;
				float2 texcoord      : TEXCOORD0;
				float4 worldPosition : TEXCOORD1;
				UNITY_VERTEX_OUTPUT_STEREO
			};

			fixed4 _Color;
			fixed4 _TextureSampleAdd;
			float4 _ClipRect;
			sampler2D _MainTex;
			sampler2D _NoiseTex;

			float _EyeOpen;
			float _EyeWidth;
			float _EyeHeight;
			float _EyeCurve;
			float _LowerLidRatio;
			float _EdgeSoftness;
			float _EdgeBlur;
			float _EdgeBlurNoise;

			float _NoiseScale;
			float _NoiseAmount;
			float4 _NoiseScroll;
			float _NoiseTime;

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

				// 以画面中心为原点，x 换算到与 y 同一尺度（按宽高比）
				float px = (IN.texcoord.x - 0.5) * _Aspect;
				float py = IN.texcoord.y - 0.5;

				// 眼型包络：中央 1，到两眼角（|px| = 半宽）归 0；
				// 指数越大收得越快，眼角越尖
				float halfWidth = max(_EyeWidth * 0.5 * _Aspect, 0.0001);
				float t = saturate(abs(px) / halfWidth);
				float envelope = pow(saturate(1.0 - t * t), _EyeCurve);

				// _EyeOpen 两段语义：0~1 睁眼（眼型张开）；
				// 1~2 揭开——以上/下睑曲线为边界整体上/下平移，
				// 保持眼型弧线退出画面（不是两道平行水平黑边），
				// 收尾隐藏黑幕时没有跳变
				float eyeOpen = saturate(_EyeOpen);
				float reveal = saturate(_EyeOpen - 1.0);

				float halfHeight = _EyeHeight * eyeOpen * envelope;

				// 当前上/下睑曲线（下睑按比例略矮）
				float topBoundary =
					halfHeight + reveal * 1.1;
				float bottomBoundary =
					-halfHeight * _LowerLidRatio - reveal * 1.1;

				// 睑缘噪声：随时间滚动，让边缘持续细微变化。
				// 乘睁眼度：全黑（0）时噪声项为 0，闭合严丝合缝
				float2 noiseUV =
					float2(IN.texcoord.x * _Aspect, IN.texcoord.y) * _NoiseScale +
					_NoiseScroll.xy * _NoiseTime;

				float noise = tex2D(_NoiseTex, noiseUV).a - 0.5;

				// 开口 = 上下两条睑曲线之间；曲线随 reveal 整体
				// 外推 0 → 1.1（超半屏 + 柔化余量），
				// 任何 Edge Softness 下收尾都能整屏透明
				float d =
					min(topBoundary - py, py - bottomBoundary) +
					noise * _NoiseAmount * eyeOpen;

				// 伪模糊羽化：带宽 = 基础柔化 + 模糊带，
				// 模糊带随睑缘噪声起伏（毛边，不是均匀渐变），
				// 再经 smoothstep 呈光学感的软过渡。
				// 带宽只作用在开口内侧，全黑侧不受影响
				float blurWidth =
					_EdgeBlur * (1.0 + noise * 2.0 * _EdgeBlurNoise);

				float softness =
					max(_EdgeSoftness + max(blurWidth, 0.0), 0.0001);

				// d > 0 的区域是"睁眼"露出的部分 → 黑幕透明
				float open = smoothstep(
					0.0, 1.0, saturate(d / softness)
				);

				color.a *= 1.0 - open;

				#ifdef UNITY_UI_ALPHACLIP
				clip(color.a - 0.01);
				#endif

				return color;
			}
		ENDCG
		}
	}
}
