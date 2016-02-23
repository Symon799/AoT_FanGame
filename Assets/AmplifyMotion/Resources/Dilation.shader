// Amplify Motion - Full-scene Motion Blur for Unity Pro
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

Shader "Hidden/Amplify Motion/Dilation" {
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
					half2 texel = _MainTex_TexelSize.xy;

					half2 offset0 = i.uv.zw;
					half2 offset1 = i.uv.zw - texel;
					half2 offset2 = i.uv.zw + float2(  texel.x, -texel.y );
					half2 offset3 = i.uv.zw + float2( -texel.x,  texel.y );
					half2 offset4 = i.uv.zw + texel;

					half4 motion0 = tex2D( _MotionTex, offset0 );
					half4 motion1 = tex2D( _MotionTex, offset1 );
					half4 motion2 = tex2D( _MotionTex, offset2 );
					half4 motion3 = tex2D( _MotionTex, offset3 );
					half4 motion4 = tex2D( _MotionTex, offset4 );

					half depth0 = UNITY_SAMPLE_DEPTH( tex2D( _CameraDepthTexture, offset0 ) );
					half depth1 = UNITY_SAMPLE_DEPTH( tex2D( _CameraDepthTexture, offset1 ) );
					half depth2 = UNITY_SAMPLE_DEPTH( tex2D( _CameraDepthTexture, offset2 ) );
					half depth3 = UNITY_SAMPLE_DEPTH( tex2D( _CameraDepthTexture, offset3 ) );
					half depth4 = UNITY_SAMPLE_DEPTH( tex2D( _CameraDepthTexture, offset4 ) );

					motion0.xyz = ( motion1.a > 0 && depth1 < depth0 ) ? motion1.xyz : motion0.xyz;
					motion0.xyz = ( motion2.a > 0 && depth2 < depth0 ) ? motion2.xyz : motion0.xyz;
					motion0.xyz = ( motion3.a > 0 && depth3 < depth0 ) ? motion3.xyz : motion0.xyz;
					motion0.xyz = ( motion4.a > 0 && depth4 < depth0 ) ? motion4.xyz : motion0.xyz;

					return motion0;
				}
			ENDCG
		}
	}

	Fallback Off
}