// Amplify Motion - Full-scene Motion Blur for Unity Pro
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

Shader "Hidden/Amplify Motion/MotionBlur" {
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
				sampler2D _MotionTex;
				sampler2D _CameraDepthTexture;

				float4 _EFLOW_BLUR_STEP;
			
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
					// 5-TAP
					const half4 zero = half4( 0, 0, 0, 0 );

					half3 motion = tex2D( _MotionTex, i.uv.zw ).xyz;
					
					half2 dir_step = _EFLOW_BLUR_STEP.xy * ( motion.xy * 2.0 - 1.0 ) * motion.z;
					half2 dir_step1 = dir_step * 0.5;

					half4 color = tex2D( _MainTex, i.uv.xy );
					half depth = UNITY_SAMPLE_DEPTH( tex2D( _CameraDepthTexture, i.uv.xy ) );
					half4 accum = half4( color.xyz, 1 );
			
					half ref_depth = depth;
					half ref_id = color.a;

					color = tex2D( _MainTex, i.uv.xy - dir_step );
					depth = UNITY_SAMPLE_DEPTH( tex2D( _CameraDepthTexture, i.uv.xy - dir_step ) );
					accum += ( color.a == ref_id || depth > ref_depth ) ? half4( color.xyz, 1 ) : zero;
					
					color = tex2D( _MainTex, i.uv.xy - dir_step1 );
					depth = UNITY_SAMPLE_DEPTH( tex2D( _CameraDepthTexture, i.uv.xy - dir_step1 ) );
					accum += ( color.a == ref_id || depth > ref_depth ) ? half4( color.xyz, 1 ) : zero;
					
					color = tex2D( _MainTex, i.uv.xy + dir_step1 );
					depth = UNITY_SAMPLE_DEPTH( tex2D( _CameraDepthTexture, i.uv.xy + dir_step1 ) );
					accum += ( color.a == ref_id || depth > ref_depth ) ? half4( color.xyz, 1 ) : zero;
					
					color = tex2D( _MainTex, i.uv.xy + dir_step );
					depth = UNITY_SAMPLE_DEPTH( tex2D( _CameraDepthTexture, i.uv.xy + dir_step ) );
					accum += ( color.a == ref_id || depth > ref_depth ) ? half4( color.xyz, 1 ) : zero;

					return half4( accum.xyz / accum.w, ref_id );
				}
			ENDCG
		}
	}

	Fallback Off
}