// Amplify Motion - Full-scene Motion Blur for Unity Pro
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

Shader "Hidden/Amplify Motion/Combine" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_MotionTex ("Motion (RGB)", 2D) = "white" {}
	}
	SubShader {
		ZTest Always Cull Off ZWrite Off Fog { Mode off }	
		CGINCLUDE
			#include "UnityCG.cginc"

			float4 _MainTex_TexelSize;
			sampler2D _MainTex;
			sampler2D _BlurredTex;
			sampler2D _MotionTex;
				
			struct v2f
			{
				float4 position : POSITION;
				float2 uv : TEXCOORD0;
				float2 uv_rt : TEXCOORD1;
			};		

			v2f vert( appdata_img v )
			{
				v2f o;
				o.position = mul( UNITY_MATRIX_MVP, v.vertex );
				o.uv = v.texcoord.xy;
				o.uv_rt = v.texcoord.xy;
			#if UNITY_UV_STARTS_AT_TOP
				if ( _MainTex_TexelSize.y < 0 )
					o.uv_rt.y = 1 - o.uv_rt.y;	
			#endif
				return o;
			}
		ENDCG
		Pass {
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma fragmentoption ARB_precision_hint_fastest
						
				half4 frag( v2f i ) : COLOR
				{
					return half4( tex2D( _MainTex, i.uv ).xyz, tex2D( _MotionTex, i.uv_rt ).a + 0.0000001f ); // hack to trick Unity into behaving
				}
			ENDCG
		}
		Pass {		
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma fragmentoption ARB_precision_hint_fastest

				half4 frag( v2f i ) : COLOR
				{
					half4 source = tex2D( _MainTex, i.uv );
					half4 blurred = tex2D( _BlurredTex, i.uv_rt );
					half mag = 2 * tex2D( _MotionTex, i.uv_rt ).z;					
					return lerp( source, blurred, saturate( mag * 1.5 ) );
				}					
			ENDCG
		}
	}

	Fallback Off
}