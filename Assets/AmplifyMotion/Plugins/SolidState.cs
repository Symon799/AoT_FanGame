// Amplify Motion - Full-scene Motion Blur for Unity Pro
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System;
using UnityEngine;

namespace AmplifyMotion
{
internal class SolidState : MotionState
{
	public MeshRenderer m_meshRenderer;

	public Matrix4x4 m_prevModelViewProj;
	public Matrix4x4 m_currModelViewProj;

	public Vector3 m_lastPosition;
	public Quaternion m_lastRotation;
	public Vector3 m_lastScale;	

	private Mesh m_mesh;

	private Material[] m_sharedMaterials;
	private bool[] m_sharedMaterialCoverage;

	public bool m_moved = false;
	private bool m_wasVisible;

	public SolidState( AmplifyMotionCamera owner, AmplifyMotionObjectBase obj )
		: base( owner, obj )
	{
		m_meshRenderer = m_obj.GetComponent<MeshRenderer>();
	}

	internal override void Initialize()
	{
		MeshFilter meshFilter = m_obj.GetComponent<MeshFilter>();
		if ( meshFilter == null || meshFilter.mesh == null )
		{
			Debug.LogError( "[AmplifyMotion] Invalid MeshFilter/Mesh in object " + m_obj.name );
			m_error = true;
			return;
		}

		base.Initialize();
			
		m_mesh = meshFilter.mesh;

		m_sharedMaterials = m_meshRenderer.sharedMaterials;
		m_sharedMaterialCoverage = new bool[ m_sharedMaterials.Length ];
		for ( int i = 0; i < m_sharedMaterials.Length; i++ )
			m_sharedMaterialCoverage[ i ] = ( m_sharedMaterials[ i ].GetTag( "RenderType", false ) == "TransparentCutout" );

		m_wasVisible = false;
	}

	bool VectorChanged( Vector3 a, Vector3 b )
	{
		return Vector3.SqrMagnitude( a - b ) > 0.0f;
	}

	bool RotationChanged( Quaternion a, Quaternion b )
	{
		Vector4 diff = new Vector4( a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w );
		return Vector4.SqrMagnitude( diff ) > 0.0f;
	}

	internal override void UpdateTransform( bool starting )
	{
		if ( !m_initialized )
		{
			Initialize();
			return;
		}

		if ( !starting && m_wasVisible )
			m_prevModelViewProj = m_currModelViewProj;

		Transform transform = m_obj.transform;

		m_moved = true;
		if ( !m_owner.Overlay )
		{
			Vector3 position = transform.position;
			Quaternion rotation = transform.rotation;
			Vector3 scale = transform.lossyScale;

			m_moved = starting ||
				VectorChanged( position, m_lastPosition ) ||
				RotationChanged( rotation, m_lastRotation ) ||
				VectorChanged( scale, m_lastScale );

			if ( m_moved )
			{
				m_lastPosition = position;
				m_lastRotation = rotation;
				m_lastScale = scale;
			}
		}

		m_currModelViewProj = m_owner.ViewProjMatrix * transform.localToWorldMatrix;

		if ( starting || !m_wasVisible )
			m_prevModelViewProj = m_currModelViewProj;

		m_wasVisible = m_meshRenderer.isVisible;
	}

	internal override void RenderVectors( Camera camera, float scale )
	{
		if ( m_initialized && !m_error && m_meshRenderer.isVisible )
		{
			bool mask = ( m_owner.Instance.CullingMask & ( 1 << m_obj.gameObject.layer ) ) != 0;				
			if ( !mask || ( mask && m_moved ) )
			{
				const float rcp255 = 1 / 255.0f;
				int objectId = mask ? m_owner.Instance.GenerateObjectId( m_obj.gameObject ) : 255;
		
				Shader.SetGlobalMatrix( "_EFLOW_MATRIX_PREV_MVP", m_prevModelViewProj );
				Shader.SetGlobalFloat( "_EFLOW_OBJECT_ID", objectId * rcp255 );
				Shader.SetGlobalFloat( "_EFLOW_MOTION_SCALE", mask ? scale : 0 );
		
				for ( int i = 0; i < m_sharedMaterials.Length; i++ )
				{
					Material mat = m_sharedMaterials[ i ];
					bool coverage = m_sharedMaterialCoverage[ i ];
					int pass = coverage ? 1 : 0;					
		
					if ( coverage )
					{
						m_owner.Instance.SolidVectorsMaterial.mainTexture = mat.mainTexture;
						m_owner.Instance.SolidVectorsMaterial.SetFloat( "_Cutoff", mat.GetFloat( "_Cutoff" ) );
					}
		
					if ( m_owner.Instance.SolidVectorsMaterial.SetPass( pass ) )
						Graphics.DrawMeshNow( m_mesh, m_obj.transform.localToWorldMatrix, i );
				}
			}
		}
	}
}
}