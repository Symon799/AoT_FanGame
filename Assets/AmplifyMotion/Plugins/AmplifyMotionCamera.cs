// Amplify Motion - Full-scene Motion Blur for Unity Pro
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu( "" )]
[RequireComponent( typeof( Camera ) )]
public class AmplifyMotionCamera : MonoBehaviour
{
	internal AmplifyMotionEffectBase Instance = null;

	internal Matrix4x4 PrevViewProjMatrix;
	internal Matrix4x4 ViewProjMatrix;
	internal Matrix4x4 InvViewProjMatrix;

	private bool m_starting = true;
	private bool m_autoStep = true;
	private bool m_step = false;
	private bool m_overlay = false;

	public bool AutoStep { get { return m_autoStep; } }
	public bool Overlay { get { return m_overlay; } }

	private bool m_d3d = false;
	private int m_prevFrameCount = 0;

	internal HashSet<AmplifyMotionObjectBase> m_affectedObjectsTable = new HashSet<AmplifyMotionObjectBase>();
	internal AmplifyMotionObjectBase[] m_affectedObjects = null;
	internal bool m_affectedObjectsChanged = true;

	internal void RegisterObject( AmplifyMotionObjectBase obj )
	{
		m_affectedObjectsTable.Add( obj );
		m_affectedObjectsChanged = true;
	}

	internal void UnregisterObject( AmplifyMotionObjectBase obj )
	{
		m_affectedObjectsTable.Remove( obj );
		m_affectedObjectsChanged = true;
	}

	void UpdateAffectedObjects()
	{
		if ( m_affectedObjects == null || m_affectedObjectsTable.Count != m_affectedObjects.Length )
			m_affectedObjects = new AmplifyMotionObjectBase[ m_affectedObjectsTable.Count ];

		m_affectedObjectsTable.CopyTo( m_affectedObjects );
		m_affectedObjectsChanged = false;
	}

	void OnEnable()
	{
		AmplifyMotionEffectBase.RegisterCamera( this );

		// Assign reference only on first initialization, which is always made by Motion
		if ( Instance == null )
			Instance = AmplifyMotionEffectBase.CurrentInstance;

		GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;

		m_d3d = SystemInfo.graphicsDeviceVersion.StartsWith( "Direct3D" );

		m_step = false;
		UpdateMatrices();
	}

	void OnDisable()
	{
		AmplifyMotionEffectBase.UnregisterCamera( this );
	}

	void OnDestroy()
	{
		if ( Instance != null )
			Instance.RemoveCamera( GetComponent<Camera>() );
	}

	internal void StopAutoStep()
	{
		if ( m_autoStep )
		{
			m_autoStep = false;
			m_step = true;
		}
	}

	internal void StartAutoStep()
	{
		m_autoStep = true;
	}

	internal void Step()
	{
		m_step = true;
	}

	internal void SetOverlay( bool state )
	{
		m_overlay = state;
	}

	void FixedUpdate()
	{
		if ( Instance != null )
		{
			if ( m_affectedObjectsChanged )
				UpdateAffectedObjects();

			for ( int i = 0; i < m_affectedObjects.Length; i++ )
			{
				if ( m_affectedObjects[ i ].FixedStep )
					m_affectedObjects[ i ].OnUpdateTransform( this, m_starting );
			}
		}

	}

	void Update()
	{
		Camera camera = GetComponent<Camera>();
		if ( ( camera.depthTextureMode & DepthTextureMode.Depth ) == 0 )
			camera.depthTextureMode |= DepthTextureMode.Depth;
	}

	void UpdateMatrices()
	{
		if ( !m_starting )
			PrevViewProjMatrix = ViewProjMatrix;

		Camera camera = GetComponent<Camera>();
		Matrix4x4 view = camera.worldToCameraMatrix;
		Matrix4x4 proj = GL.GetGPUProjectionMatrix( camera.projectionMatrix, false );
		ViewProjMatrix = proj * view;
		InvViewProjMatrix = Matrix4x4.Inverse( ViewProjMatrix );

		if ( m_starting )
			PrevViewProjMatrix = ViewProjMatrix;
	}

	internal void UpdateTransform()
	{
		if ( Time.frameCount > m_prevFrameCount && ( m_autoStep || m_step ) )
		{
			UpdateMatrices();

			if ( m_affectedObjectsChanged )
				UpdateAffectedObjects();

			for ( int i = 0; i < m_affectedObjects.Length; i++ )							
			{
				if ( !m_affectedObjects[ i ].FixedStep )
					m_affectedObjects[ i ].OnUpdateTransform( this, m_starting );
			}

			m_starting = false;
			m_step = false;

			m_prevFrameCount = Time.frameCount;
		}
	}

	internal void RenderVectors( float scale )
	{
		if ( Instance != null )
		{
			Camera camera = GetComponent<Camera>();

			// For some reason Unity's own values weren't working correctly on Windows/OpenGL
			float near = camera.nearClipPlane;
			float far = camera.farClipPlane;
			Vector4 zparam;

			if ( m_d3d )
			{
				zparam.x = 1.0f - far / near;
				zparam.y = far / near;				
			}
			else
			{
				// OpenGL
				zparam.x = ( 1.0f - far / near ) / 2.0f;
				zparam.y = ( 1.0f + far / near ) / 2.0f;
			}

			zparam.z = zparam.x / far;
			zparam.w = zparam.y / far;
			Shader.SetGlobalVector( "_EFLOW_ZBUFFER_PARAMS", zparam );

			if ( m_affectedObjectsChanged )
				UpdateAffectedObjects();

			for ( int i = 0; i < m_affectedObjects.Length; i++ )
				m_affectedObjects[ i ].OnRenderVectors( camera, scale );			
		}
	}

	void OnPostRender()
	{
		if ( m_overlay )
		{
			RenderTexture prevRT = RenderTexture.active;

			Graphics.SetRenderTarget( Instance.MotionRenderTexture );
			RenderVectors( Instance.NormalizedMotionScale );

			RenderTexture.active = prevRT;
		}
	}
}
