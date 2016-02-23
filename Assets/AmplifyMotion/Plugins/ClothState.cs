// Amplify Motion - Full-scene Motion Blur for Unity Pro
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#if UNITY_4_0 || UNITY_4_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_4  || UNITY_4_5 || UNITY_4_6 || UNITY_4_7 || UNITY_4_8 || UNITY_4_9
#define UNITY_4
#endif
#if UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3 || UNITY_5_4  || UNITY_5_5 || UNITY_5_6 || UNITY_5_7 || UNITY_5_8 || UNITY_5_9
#define UNITY_5
#endif
#if !UNITY_4 && !UNITY_5
#define UNITY_3
#endif

#if UNITY_3 || UNITY_4

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace AmplifyMotion
{
internal class ClothState : MotionState
{
	private InteractiveCloth m_cloth;

	private int m_vertexCount;
	private Vector2[] m_prevProj;
	private Vector3[] m_vertices;
	private Vector2[] m_motions;

	private Mesh m_clonedMesh;

	private Material[] m_sharedMaterials;
	private bool[] m_sharedMaterialCoverage;

	private ManualResetEvent m_asyncUpdateSignal = null;
	private bool m_asyncUpdateTriggered = false;

	private bool m_mask;
	private bool m_starting;
	private bool m_wasVisible;

	public ClothState( AmplifyMotionCamera owner, AmplifyMotionObjectBase obj )
		: base( owner, obj )
	{
		m_cloth = m_obj.GetComponent<InteractiveCloth>();
	}

	internal override void Initialize()
	{
		if ( m_cloth.vertices == null )
		{
			Debug.LogError( "[AmplifyMotion] Invalid InteractiveCloth Vertices on object " + m_obj.name );
			m_error = true;
			return;
		}

		if ( m_cloth.mesh == null || m_cloth.mesh.vertices == null || m_cloth.mesh.triangles == null )
		{
			Debug.LogError( "[AmplifyMotion] Invalid InteractiveCloth Mesh on object " + m_obj.name );
			m_error = true;
			return;
		}

		base.Initialize();

		int sourceVertexCount = m_cloth.mesh.vertexCount;
		Vector3[] sourceVertices = m_cloth.mesh.vertices;
		int[] sourceTriangles = m_cloth.mesh.triangles;

		Vector3[] vertices;
		int[] triangles;

		if ( m_cloth.vertices.Length == m_cloth.mesh.vertices.Length )
		{
			vertices = sourceVertices;
			triangles = sourceTriangles;
		}
		else
		{
			// a) May contains duplicated verts, optimization/cleanup is required
			Dictionary<Vector3, int> dict = new Dictionary<Vector3, int>();
			int[] remap = new int[ sourceVertexCount ];
			int original, vertexCount = 0;

			for ( int i = 0; i < sourceVertexCount; i++ )
			{
				if ( dict.TryGetValue( sourceVertices[ i ], out original ) )
					remap[ i ] = original;
				else
				{
					remap[ i ] = vertexCount;
					dict.Add( sourceVertices[ i ], vertexCount++ );
				}
			}

			vertices = new Vector3[ vertexCount ];
			dict.Keys.CopyTo( vertices, 0 );

			int indexCount = sourceTriangles.Length;
			triangles = new int[ indexCount ];

			for ( int i = 0; i < indexCount; i++ )
				triangles[ i ] = remap[ sourceTriangles[ i ] ];

			// b) Tear is activated, creates extra verts (NOT SUPPORTED, POOL OF VERTS USED, NO ACCESS TO TRIANGLES)
		}

		m_vertexCount = vertices.Length;
		m_prevProj = new Vector2[ m_vertexCount ];
		m_vertices = new Vector3[ m_vertexCount ];
		m_motions = new Vector2[ m_vertexCount ];

		m_clonedMesh = new Mesh();
		m_clonedMesh.vertices = m_cloth.mesh.vertices;
		m_clonedMesh.uv = m_cloth.mesh.uv;
		m_clonedMesh.uv2 = m_motions;
		m_clonedMesh.triangles = triangles;

		m_sharedMaterials = m_obj.renderer.sharedMaterials;
		m_sharedMaterialCoverage = new bool[ m_sharedMaterials.Length ];
		for ( int i = 0; i < m_sharedMaterials.Length; i++ )
			m_sharedMaterialCoverage[ i ] = ( m_sharedMaterials[ i ].GetTag( "RenderType", false ) == "TransparentCutout" );

		m_asyncUpdateSignal = new ManualResetEvent( false );
		m_asyncUpdateTriggered = false;

		m_wasVisible = false;
	}

	void UpdateVertices( bool starting )
	{
		Array.Copy( m_cloth.vertices, m_vertices, m_vertexCount );
	}

	void UpdateMotions( bool starting )
	{
		Matrix4x4 viewProjMatrix = m_owner.ViewProjMatrix;
		Vector4 currProj, currPos = Vector4.one;

		for ( int i = 0; i < m_vertexCount; i++ )
		{
			currPos.x = m_vertices[ i ].x;
			currPos.y = m_vertices[ i ].y;
			currPos.z = m_vertices[ i ].z;

			currProj = viewProjMatrix * currPos;

			float rcp_curr_w = 1.0f / currProj.w;
			currProj.x *= rcp_curr_w;
			currProj.y *= rcp_curr_w;
					
			if ( m_mask && !starting )
			{
				m_motions[ i ].x = currProj.x - m_prevProj[ i ].x;
				m_motions[ i ].y = currProj.y - m_prevProj[ i ].y;
			}
			else
			{
				m_motions[ i ].x = 0;
				m_motions[ i ].y = 0;
			}

			m_prevProj[ i ].x = currProj.x;
			m_prevProj[ i ].y = currProj.y;
		}
	}

	internal override void AsyncUpdate()
	{
		try
		{
			UpdateMotions( m_starting );
		}
		catch ( System.Exception e )
		{
			Debug.LogError( "[AmplifyMotion] Failed on InteractiveCloth data. Please contact support.\n" + e.Message );
		}
		finally
		{
			m_asyncUpdateSignal.Set();
		}
	}

	internal override void UpdateTransform( bool starting )
	{
		if ( !m_initialized )
		{
			Initialize();
			return;
		}

		bool isVisible = m_obj.renderer.isVisible;

		if ( !m_error && ( isVisible || starting ) )
		{
			UpdateVertices( starting );

			m_mask = ( m_owner.Instance.CullingMask & ( 1 << m_obj.gameObject.layer ) ) != 0;
			m_starting = !m_wasVisible || starting;

			m_asyncUpdateSignal.Reset();
			m_asyncUpdateTriggered = true;
			
			m_owner.Instance.WorkerPool.EnqueueAsyncUpdate( this );

			//// DEFORMATION PROFILING
			//float start = Time.realtimeSinceStartup;
			//Transform parent = m_obj.transform.parent;
			//bool isIt = true;
			//int n = isIt ? 1 : 1;
			//for ( int j = 0; j < n; j++ )
			//{
			//	UpdateMotions( starting );
			//}
			//if ( isIt )
			//	Debug.Log( "ClothDeform: " + ( Time.realtimeSinceStartup - start ) * 1000 );
		}

		m_wasVisible = isVisible;
	}

	internal override void RenderVectors( Camera camera, float scale )
	{
		if ( m_initialized && !m_error && m_obj.renderer.isVisible )
		{
			if ( m_asyncUpdateTriggered )
			{
				if ( !m_asyncUpdateSignal.WaitOne( MotionState.AsyncUpdateTimeout ) )
				{
					Debug.LogWarning( "[AmplifyMotion] Aborted abnormally long Async Skin deform operation. Not a critical error but might indicate a problem. Please contact support." );
					return;
				}
				m_asyncUpdateTriggered = false;
			}

			m_clonedMesh.vertices = m_cloth.vertices;
			m_clonedMesh.uv2 = m_motions;

			const float rcp255 = 1 / 255.0f;
			int objectId = m_mask ? m_owner.Instance.GenerateObjectId( m_obj.gameObject ) : 255;

			Shader.SetGlobalFloat( "_EFLOW_OBJECT_ID", objectId * rcp255 );
			Shader.SetGlobalFloat( "_EFLOW_MOTION_SCALE", m_mask ? scale : 0 );

			for ( int i = 0; i < m_sharedMaterials.Length; i++ )
			{
				Material mat = m_sharedMaterials[ i ];
				bool coverage = m_sharedMaterialCoverage[ i ];
				int pass = coverage ? 1 : 0;

				if ( coverage )
				{
					m_owner.Instance.ClothVectorsMaterial.mainTexture = mat.mainTexture;
					m_owner.Instance.ClothVectorsMaterial.SetFloat( "_Cutoff", mat.GetFloat( "_Cutoff" ) );
				}

				if ( m_owner.Instance.ClothVectorsMaterial.SetPass( pass ) )
					Graphics.DrawMeshNow( m_clonedMesh, Matrix4x4.identity, i );
			}
		}
	}
}
}

#endif