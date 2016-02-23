// Amplify Motion - Full-scene Motion Blur for Unity Pro
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System;
using System.Threading;
using UnityEngine;

namespace AmplifyMotion
{
internal class SkinnedState : MotionState
{
	private SkinnedMeshRenderer m_skinnedRenderer;	

	private int m_boneCount;
	private Transform[] m_boneTransforms;
	private Matrix4x4[] m_bones;

	private int m_weightCount;
	private int[] m_boneIndices;
	private float[] m_boneWeights;

	private int m_vertexCount;
	private Vector2[] m_prevProj;
	private Vector4[] m_baseVertices;
	private Vector3[] m_vertices;
	private Vector2[] m_motions;	

	private Mesh m_clonedMesh;
	private Matrix4x4 m_worldToLocalMatrix;
	private Matrix4x4 m_localToWorldMatrix;
	private Matrix4x4 m_worldViewProjMatrix;

	private Material[] m_sharedMaterials;
	private bool[] m_sharedMaterialCoverage;

	private ManualResetEvent m_asyncUpdateSignal = null;
	private bool m_asyncUpdateTriggered = false;
	
	private bool m_mask;
	private bool m_starting;
	private bool m_wasVisible;

	public SkinnedState( AmplifyMotionCamera owner, AmplifyMotionObjectBase obj )
		: base( owner, obj )
	{
		m_skinnedRenderer = m_obj.GetComponent<SkinnedMeshRenderer>();
	}

	internal override void Initialize()
	{		
		if ( m_skinnedRenderer.bones == null || m_skinnedRenderer.bones.Length == 0 )
		{
			Debug.LogWarning( "[AmplifyMotion] Bones not found on " + m_obj.name + ". Please note that 'Optimize Game Object' Rig import setting is not yet supported. Motion blur was disabled for this object." );
			m_error = true;
			return;
		}

		base.Initialize();		

		m_vertexCount = m_skinnedRenderer.sharedMesh.vertexCount;
		m_weightCount = ( m_skinnedRenderer.quality == SkinQuality.Auto ) ? ( int ) QualitySettings.blendWeights : ( int ) m_skinnedRenderer.quality;

		m_boneTransforms = m_skinnedRenderer.bones;
		m_boneCount = m_skinnedRenderer.bones.Length;
		m_bones = new Matrix4x4[ m_boneCount ];
		
		m_boneIndices = new int[ m_vertexCount * m_weightCount ];
		if ( m_weightCount > 1 )
			m_boneWeights = new float[ m_vertexCount * m_weightCount ];
		else
			m_boneWeights = null;

		m_prevProj = new Vector2[ m_vertexCount ];
		m_baseVertices = new Vector4[ m_vertexCount * m_weightCount ];
		m_vertices = new Vector3[ m_vertexCount ];
		m_motions = new Vector2[ m_vertexCount ];
		
		if ( m_weightCount == 1 )
			InitializeBone1();
		else if ( m_weightCount == 2 )
			InitializeBone2();
		else
			InitializeBone4();

		Mesh skinnedMesh = m_skinnedRenderer.sharedMesh;
		m_clonedMesh = new Mesh();
		m_clonedMesh.vertices = skinnedMesh.vertices;
		m_clonedMesh.uv2 = m_motions;
		m_clonedMesh.uv = skinnedMesh.uv;		
		m_clonedMesh.subMeshCount = skinnedMesh.subMeshCount;
		for ( int i = 0; i < skinnedMesh.subMeshCount; i++ )
			m_clonedMesh.SetTriangles( skinnedMesh.GetTriangles( i ), i );

		m_sharedMaterials = m_skinnedRenderer.sharedMaterials;
		m_sharedMaterialCoverage = new bool [ m_sharedMaterials.Length ];
		for ( int i = 0; i < m_sharedMaterials.Length; i++ )
			m_sharedMaterialCoverage[ i ] = ( m_sharedMaterials[ i ].GetTag( "RenderType", false ) == "TransparentCutout" );

		m_asyncUpdateSignal = new ManualResetEvent( false );
		m_asyncUpdateTriggered = false;

		m_wasVisible = false;
	}

	void UpdateBones()
	{
		for ( int i = 0; i < m_boneCount; i++ )
			m_bones[ i ] = m_boneTransforms[ i ].localToWorldMatrix;

		m_worldToLocalMatrix = m_obj.transform.worldToLocalMatrix;
		m_localToWorldMatrix = m_obj.transform.localToWorldMatrix;
		m_worldViewProjMatrix = m_owner.ViewProjMatrix * m_localToWorldMatrix;
	}

	void UpdateVertices( bool starting )
	{
		for ( int i = 0; i < m_boneCount; i++ )
			m_bones[ i ] = m_worldToLocalMatrix * m_bones[ i ];

		if ( m_weightCount == 1 )
			UpdateVerticesBone1();
		else if ( m_weightCount == 2 )
			UpdateVerticesBone2();
		else
			UpdateVerticesBone4();
	}

	void UpdateMotions( bool starting )
	{
		Vector4 currProj = Vector4.zero;
		Vector4 currPos = Vector4.one;
		Matrix4x4 mvp = m_worldViewProjMatrix;

		for ( int i = 0; i < m_vertexCount; i++ )
		{
			currPos.x = m_vertices[ i ].x;
			currPos.y = m_vertices[ i ].y;
			currPos.z = m_vertices[ i ].z;

			MulPoint4x4_XYZW( ref currProj, ref mvp, currPos );

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

	void InitializeBone1()
	{
		Vector3[] vertices = m_skinnedRenderer.sharedMesh.vertices;
		Matrix4x4[] bindPoses = m_skinnedRenderer.sharedMesh.bindposes;
		BoneWeight[] boneWeights = m_skinnedRenderer.sharedMesh.boneWeights;

		for ( int i = 0; i < m_vertexCount; i++ )
		{
			int o0 = i * m_weightCount;
			int b0 = m_boneIndices[ o0 ] = boneWeights[ i ].boneIndex0;
			m_baseVertices[ o0 ] = bindPoses[ b0 ].MultiplyPoint3x4( vertices[ i ] );
		}
	}

	void InitializeBone2()
	{
		Vector3[] vertices = m_skinnedRenderer.sharedMesh.vertices;
		Matrix4x4[] bindPoses = m_skinnedRenderer.sharedMesh.bindposes;
		BoneWeight[] boneWeights = m_skinnedRenderer.sharedMesh.boneWeights;

		for ( int i = 0; i < m_vertexCount; i++ )
		{
			int o0 = i * m_weightCount;
			int o1 = o0 + 1;

			BoneWeight bw = boneWeights[ i ];
			int b0 = m_boneIndices[ o0 ] = bw.boneIndex0;
			int b1 = m_boneIndices[ o1 ] = bw.boneIndex1;

			float w0 = bw.weight0;
			float w1 = bw.weight1;

			float rcpSum = 1.0f / ( w0 + w1 );
			m_boneWeights[ o0 ] = w0 = w0 * rcpSum;
			m_boneWeights[ o1 ] = w1 = w1 * rcpSum;

			Vector3 bv0 = w0 * bindPoses[ b0 ].MultiplyPoint3x4( vertices[ i ] );
			Vector3 bv1 = w1 * bindPoses[ b1 ].MultiplyPoint3x4( vertices[ i ] );

			m_baseVertices[ o0 ] = new Vector4( bv0.x, bv0.y, bv0.z, w0 );
			m_baseVertices[ o1 ] = new Vector4( bv1.x, bv1.y, bv1.z, w1 );
		}
	}

	void InitializeBone4()
	{
		Vector3[] vertices = m_skinnedRenderer.sharedMesh.vertices;
		Matrix4x4[] bindPoses = m_skinnedRenderer.sharedMesh.bindposes;
		BoneWeight[] boneWeights = m_skinnedRenderer.sharedMesh.boneWeights;

		for ( int i = 0; i < m_vertexCount; i++ )
		{
			int o0 = i * m_weightCount;
			int o1 = o0 + 1;
			int o2 = o0 + 2;
			int o3 = o0 + 3;

			BoneWeight bw = boneWeights[ i ];
			int b0 = m_boneIndices[ o0 ] = bw.boneIndex0;
			int b1 = m_boneIndices[ o1 ] = bw.boneIndex1;
			int b2 = m_boneIndices[ o2 ] = bw.boneIndex2;
			int b3 = m_boneIndices[ o3 ] = bw.boneIndex3;

			float w0 = m_boneWeights[ o0 ] = bw.weight0;
			float w1 = m_boneWeights[ o1 ] = bw.weight1;
			float w2 = m_boneWeights[ o2 ] = bw.weight2;
			float w3 = m_boneWeights[ o3 ] = bw.weight3;

			Vector3 bv0 = w0 * bindPoses[ b0 ].MultiplyPoint3x4( vertices[ i ] );
			Vector3 bv1 = w1 * bindPoses[ b1 ].MultiplyPoint3x4( vertices[ i ] );
			Vector3 bv2 = w2 * bindPoses[ b2 ].MultiplyPoint3x4( vertices[ i ] );
			Vector3 bv3 = w3 * bindPoses[ b3 ].MultiplyPoint3x4( vertices[ i ] );

			m_baseVertices[ o0 ] = new Vector4( bv0.x, bv0.y, bv0.z, w0 );
			m_baseVertices[ o1 ] = new Vector4( bv1.x, bv1.y, bv1.z, w1 );
			m_baseVertices[ o2 ] = new Vector4( bv2.x, bv2.y, bv2.z, w2 );
			m_baseVertices[ o3 ] = new Vector4( bv3.x, bv3.y, bv3.z, w3 );
		}
	}

	void MulPoint4x4_XYZW( ref Vector4 result, ref Matrix4x4 mat, Vector4 vec )
	{
		result.x = mat.m00 * vec.x + mat.m01 * vec.y + mat.m02 * vec.z + mat.m03 * vec.w;
		result.y = mat.m10 * vec.x + mat.m11 * vec.y + mat.m12 * vec.z + mat.m13 * vec.w;
		result.z = mat.m20 * vec.x + mat.m21 * vec.y + mat.m22 * vec.z + mat.m23 * vec.w;
		result.w = mat.m30 * vec.x + mat.m31 * vec.y + mat.m32 * vec.z + mat.m33 * vec.w;
	}

	void MulPoint3x4_XYZ( ref Vector3 result, ref Matrix4x4 mat, Vector4 vec )
	{
		result.x = mat.m00 * vec.x + mat.m01 * vec.y + mat.m02 * vec.z + mat.m03;
		result.y = mat.m10 * vec.x + mat.m11 * vec.y + mat.m12 * vec.z + mat.m13;
		result.z = mat.m20 * vec.x + mat.m21 * vec.y + mat.m22 * vec.z + mat.m23;
	}

	void MulPoint3x4_XYZW( ref Vector3 result, ref Matrix4x4 mat, Vector4 vec )
	{
		result.x = mat.m00 * vec.x + mat.m01 * vec.y + mat.m02 * vec.z + mat.m03 * vec.w;
		result.y = mat.m10 * vec.x + mat.m11 * vec.y + mat.m12 * vec.z + mat.m13 * vec.w;
		result.z = mat.m20 * vec.x + mat.m21 * vec.y + mat.m22 * vec.z + mat.m23 * vec.w;
	}

	void MulAddPoint3x4_XYZW( ref Vector3 result, ref Matrix4x4 mat, Vector4 vec )
	{
		result.x += mat.m00 * vec.x + mat.m01 * vec.y + mat.m02 * vec.z + mat.m03 * vec.w;
		result.y += mat.m10 * vec.x + mat.m11 * vec.y + mat.m12 * vec.z + mat.m13 * vec.w;
		result.z += mat.m20 * vec.x + mat.m21 * vec.y + mat.m22 * vec.z + mat.m23 * vec.w;
	}

	void UpdateVerticesBone1()
	{
		for ( int i = 0; i < m_vertexCount; i++ )
			MulPoint3x4_XYZ( ref m_vertices[ i ], ref m_bones[ m_boneIndices[ i ] ], m_baseVertices[ i ] );
	}

	void UpdateVerticesBone2()
	{
		Vector3 deformedVertex = Vector3.zero;
		for ( int i = 0; i < m_vertexCount; i++ )
		{
			int o0 = i * 2;
			int o1 = o0 + 1;

			int b0 = m_boneIndices[ o0 ];
			int b1 = m_boneIndices[ o1 ];
			float w1 = m_boneWeights[ o1 ];

			MulPoint3x4_XYZW( ref deformedVertex, ref m_bones[ b0 ], m_baseVertices[ o0 ] );
			if ( w1 != 0 )
				MulAddPoint3x4_XYZW( ref deformedVertex, ref m_bones[ b1 ], m_baseVertices[ o1 ] );

			m_vertices[ i ] = deformedVertex;
		}
	}

	void UpdateVerticesBone4()
	{
		Vector3 deformedVertex = Vector3.zero;
		for ( int i = 0; i < m_vertexCount; i++ )
		{
			int o0 = i * 4;
			int o1 = o0 + 1;
			int o2 = o0 + 2;
			int o3 = o0 + 3;

			int b0 = m_boneIndices[ o0 ];
			int b1 = m_boneIndices[ o1 ];
			int b2 = m_boneIndices[ o2 ];
			int b3 = m_boneIndices[ o3 ];

			float w1 = m_boneWeights[ o1 ];
			float w2 = m_boneWeights[ o2 ];
			float w3 = m_boneWeights[ o3 ];

			MulPoint3x4_XYZW( ref deformedVertex, ref m_bones[ b0 ], m_baseVertices[ o0 ] );
			if ( w1 != 0 )
				MulAddPoint3x4_XYZW( ref deformedVertex, ref m_bones[ b1 ], m_baseVertices[ o1 ] );
			if ( w2 != 0 )
				MulAddPoint3x4_XYZW( ref deformedVertex, ref m_bones[ b2 ], m_baseVertices[ o2 ] );
			if ( w3 != 0 )
				MulAddPoint3x4_XYZW( ref deformedVertex, ref m_bones[ b3 ], m_baseVertices[ o3 ] );

			m_vertices[ i ] = deformedVertex;
		}
	}	

	internal override void AsyncUpdate()
	{		
		try
		{
			UpdateVertices( m_starting );
			UpdateMotions( m_starting );
		}
		catch ( System.Exception e )
		{
			Debug.LogError( "[AmplifyMotion] Failed on SkinnedMeshRenderer data. Please contact support.\n" + e.Message );
		}
		finally
		{
			//Debug.Log( "Finished " + Name );
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

		bool isVisible = m_skinnedRenderer.isVisible;

		if ( !m_error && ( isVisible || starting ) )
		{
			UpdateBones();
			
			m_mask = ( m_owner.Instance.CullingMask & ( 1 << m_obj.gameObject.layer ) ) != 0;
			m_starting = !m_wasVisible || starting;

			m_asyncUpdateSignal.Reset();
			m_asyncUpdateTriggered = true;
			
			m_owner.Instance.WorkerPool.EnqueueAsyncUpdate( this );

			// DEFORMATION PROFILING
			//float start = Time.realtimeSinceStartup;
			//Transform parent = m_obj.transform.parent;
			//bool isIt = ( parent != null && parent.parent != null && parent.parent.name == "Mutant_1_Prefab_2" );
			//int n = isIt ? 100 : 1;
			//for ( int j = 0; j < n; j++ )
			//{
			//	UpdateVertices( m_starting );
			//	UpdateMotions( m_starting );
			//}
			//if ( isIt )
			//	Debug.Log( "SkinDeform " + m_obj.name + ": " + ( Time.realtimeSinceStartup - start ) * 1000 );
		}

		m_wasVisible = isVisible;
	}	

	internal override void RenderVectors( Camera camera, float scale )
	{
		if ( m_initialized && !m_error && m_skinnedRenderer.isVisible )
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
		
			m_clonedMesh.vertices = m_vertices;
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
					m_owner.Instance.SkinnedVectorsMaterial.mainTexture = mat.mainTexture;
					m_owner.Instance.SkinnedVectorsMaterial.SetFloat( "_Cutoff", mat.GetFloat( "_Cutoff" ) );
				}
		
				if ( m_owner.Instance.SkinnedVectorsMaterial.SetPass( pass ) )
					Graphics.DrawMeshNow( m_clonedMesh, m_obj.transform.localToWorldMatrix, i );
			}
		}
	}
}
}