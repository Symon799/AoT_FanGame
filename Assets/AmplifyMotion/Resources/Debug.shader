// Amplify Motion - Full-scene Motion Blur for Unity Pro
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

Shader "Hidden/Amplify Motion/Debug" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_MotionTex ("Motion (RGB)", 2D) = "white" {}
	}
	SubShader {
		Pass {		
			ZTest Always Cull Off ZWrite Off Fog { Mode off }
		
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma fragmentoption ARB_precision_hint_fastest
				#pragma exclude_renderers flash
				#include "UnityCG.cginc"			
			
				float4 _MainTex_TexelSize;
				sampler2D _MainTex;
				sampler2D _CameraDepthTexture;
			
				struct v2f
				{
					float4 pos : POSITION;
					float4 uv : TEXCOORD0;
				};

				v2f vert( appdata_img v )
				{
					v2f o;
					o.pos = mul( UNITY_MATRIX_MVP, v.vertex );
					o.uv.xy = v.texcoord.xy;
					o.uv.zw = v.texcoord.xy;
				#if UNITY_UV_STARTS_AT_TOP
					if ( _MainTex_TexelSize.y < 0 )
						o.uv.w = 1 - o.uv.w;
				#endif
					return o;
				}

				half4 frag( v2f i ) : COLOR
				{
					half id = tex2D( _MainTex, i.uv.xy ).w;
					half depth = UNITY_SAMPLE_DEPTH( tex2D( _CameraDepthTexture, i.uv.zw ) );
					return half4( depth, depth, id, 1 );
				}
			ENDCG
		}
	}

	Fallback Off
}