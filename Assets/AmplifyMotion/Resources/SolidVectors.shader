// Amplify Motion - Full-scene Motion Blur for Unity Pro
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

Shader "Hidden/Amplify Motion/SolidVectors" {
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_Cutoff ("Alpha cutoff", Range(0,1)) = 0.25
	}
	SubShader {
		Blend Off Cull Off Fog { Mode off }
		ZTest LEqual ZWrite On
		//Offset -1, -1
		Pass {
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma fragmentoption ARB_precision_hint_fastest
			#if SHADER_API_D3D9 || SHADER_API_D3D11_9X
				#pragma target 3.0
			#endif
				#include "Shared.cginc"

				struct appdata_t
				{
					float4 vertex : POSITION;
					float2 texcoord1 : TEXCOORD1;
				};

				struct v2f
				{
					float4 pos : POSITION;
					float4 pos_prev : TEXCOORD0;
					float4 pos_curr : TEXCOORD1;
					float4 screen_pos : TEXCOORD2;
				};				

				v2f vert( appdata_t v )
				{
					v2f o;
					float4 pos = o.pos = mul( UNITY_MATRIX_MVP, v.vertex );
					o.pos_prev = mul( _EFLOW_MATRIX_PREV_MVP, v.vertex );
					o.pos_curr = o.pos;
				#if UNITY_UV_STARTS_AT_TOP
					o.pos_curr.y = -o.pos_curr.y;
					if ( _ProjectionParams.x > 0 )
						pos.y = -pos.y;
				#endif
					o.screen_pos = ComputeScreenPos( pos );
					return o;
				}

				half4 frag( v2f i ) : COLOR
				{
					DepthTest( i.screen_pos );
					return SolidMotionVector( i.pos_prev, i.pos_curr, _EFLOW_OBJECT_ID );
				}
			ENDCG
		}
		Pass {
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma fragmentoption ARB_precision_hint_fastest
			#if SHADER_API_D3D9 || SHADER_API_D3D11_9X
				#pragma target 3.0
			#endif
				#include "Shared.cginc"

				struct appdata_t
				{
					float4 vertex : POSITION;
					float2 texcoord : TEXCOORD0;
					float2 texcoord1 : TEXCOORD1;
				};

				struct v2f
				{
					float4 pos : POSITION;
					float4 pos_prev : TEXCOORD0;
					float4 pos_curr : TEXCOORD1;
					float4 screen_pos : TEXCOORD2;
					float2 uv : TEXCOORD3;
				};

				sampler2D _MainTex;
				float4 _MainTex_ST;
				float _Cutoff;

				v2f vert( appdata_t v )
				{
					v2f o;
					float4 pos = o.pos = mul( UNITY_MATRIX_MVP, v.vertex );
					o.pos_prev = mul( _EFLOW_MATRIX_PREV_MVP, v.vertex );
					o.pos_curr = o.pos;
				#if UNITY_UV_STARTS_AT_TOP
					o.pos_curr.y = -o.pos_curr.y;
					if ( _ProjectionParams.x > 0 )
						pos.y = -pos.y;
				#endif
					o.screen_pos = ComputeScreenPos( pos );
					o.uv = TRANSFORM_TEX( v.texcoord, _MainTex );					
					return o;
				}

				half4 frag( v2f i ) : COLOR
				{
					DepthTest( i.screen_pos );
					clip( tex2D( _MainTex, i.uv ).a - _Cutoff );
					return SolidMotionVector( i.pos_prev, i.pos_curr, _EFLOW_OBJECT_ID); 
				}
			ENDCG
		}
	}

	FallBack Off
}