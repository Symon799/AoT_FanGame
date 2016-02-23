
Shader "Hidden/Amplify Motion/ReprojectionVectors" {
	Properties {
		_MainTex ("-", 2D) = "" {}
	}
	SubShader {
		Cull Off ZTest Always ZWrite Off Blend Off Fog { Mode Off }
		Pass {
			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma fragmentoption ARB_precision_hint_fastest				
				#include "UnityCG.cginc"

				struct v2f
				{
					float4 pos : POSITION;
					float2 uv : TEXCOORD0;
					float2 uv_rt : TEXCOORD1;
				};
				
				sampler2D _CameraDepthTexture;
				sampler2D _MainTex;
				float4 _MainTex_TexelSize;

				float4x4 _EFLOW_MATRIX_CURR_REPROJ;
				float _EFLOW_MOTION_SCALE;
				float _EFLOW_MIN_VELOCITY;
				float _EFLOW_MAX_VELOCITY;
				float _EFLOW_RCP_TOTAL_VELOCITY;

				v2f vert( appdata_img v )
				{
					v2f o;
					o.pos = mul( UNITY_MATRIX_MVP, v.vertex );
					o.uv = v.texcoord.xy;
					o.uv_rt = v.texcoord.xy;
				#if UNITY_UV_STARTS_AT_TOP
					if ( _MainTex_TexelSize.y < 0 )
						o.uv_rt.y = 1 - o.uv_rt.y;	
				#endif					
					return o;
				}

				half4 frag( v2f i ) : COLOR
				{	
					float d = UNITY_SAMPLE_DEPTH( tex2D( _CameraDepthTexture, i.uv_rt ) );
					float4 pos_curr = float4( i.uv.xy * 2 - 1, d, 1 );

					// 1) unproject to world; 2) reproject into previous ViewProj
					float4 pos_prev = mul( _EFLOW_MATRIX_CURR_REPROJ, pos_curr );
				
					pos_prev = pos_prev / pos_prev.w;
					pos_curr = pos_curr / pos_curr.w;

					half4 motion = ( pos_curr - pos_prev ) * _EFLOW_MOTION_SCALE;

					motion.z = length( motion.xy );
					motion.xy = ( motion.xy / motion.z ) * 0.5f + 0.5f;
					motion.z = ( motion.z < _EFLOW_MIN_VELOCITY ) ? 0 : motion.z;
					motion.z = max( min( motion.z, _EFLOW_MAX_VELOCITY ) - _EFLOW_MIN_VELOCITY, 0 ) * _EFLOW_RCP_TOTAL_VELOCITY;
					
					return half4( motion.xyz, 0 );
				}				
			ENDCG
		}
	}
	FallBack Off
}