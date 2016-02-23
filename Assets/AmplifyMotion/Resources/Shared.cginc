// Amplify Motion - Full-scene Motion Blur for Unity Pro
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#ifndef AMPLIFY_MOTION_SHARED_INCLUDED
#define AMPLIFY_MOTION_SHARED_INCLUDED

#include "UnityCG.cginc"

uniform sampler2D _CameraDepthTexture;
uniform float4x4 _EFLOW_MATRIX_PREV_MVP;
uniform float4 _EFLOW_ZBUFFER_PARAMS;
uniform float _EFLOW_OBJECT_ID;
uniform float _EFLOW_MOTION_SCALE;
uniform float _EFLOW_MIN_VELOCITY;
uniform float _EFLOW_MAX_VELOCITY;
uniform float _EFLOW_RCP_TOTAL_VELOCITY;

inline void DepthTest( float4 screen_pos )
{
	const float epsilon = 0.001f;
	float3 uv = screen_pos.xyz / screen_pos.w;
	float behind = UNITY_SAMPLE_DEPTH( tex2D( _CameraDepthTexture, uv.xy ) );
#if SHADER_API_OPENGL
	float front = uv.z * 0.5 + 0.5;
#else
	float front = uv.z;
#endif					
	if ( behind < front - epsilon )
		discard;
}

inline half4 SolidMotionVector( half4 pos_prev, half4 pos_curr, half obj_id )
{
	pos_prev = pos_prev / pos_prev.w;
	pos_curr = pos_curr / pos_curr.w;
	half4 motion = ( pos_curr - pos_prev ) * _EFLOW_MOTION_SCALE;
	
	motion.z = length( motion.xy );
	motion.xy = ( motion.xy / motion.z ) * 0.5f + 0.5f;
	motion.z = ( motion.z < _EFLOW_MIN_VELOCITY ) ? 0 : motion.z;
	motion.z = max( min( motion.z, _EFLOW_MAX_VELOCITY ) - _EFLOW_MIN_VELOCITY, 0 ) * _EFLOW_RCP_TOTAL_VELOCITY;
	return half4( motion.xyz, obj_id );
}

inline half4 DeformableMotionVector( half4 motion )
{
	motion.z = length( motion.xy );
	motion.xy = ( motion.xy / motion.z ) * 0.5f + 0.5f;
	motion.z = ( motion.z < _EFLOW_MIN_VELOCITY ) ? 0 : motion.z;
	motion.z = max( min( motion.z, _EFLOW_MAX_VELOCITY ) - _EFLOW_MIN_VELOCITY, 0 ) * _EFLOW_RCP_TOTAL_VELOCITY;
	return half4( motion.xyz, _EFLOW_OBJECT_ID );
}

#endif