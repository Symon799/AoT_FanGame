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

using System;
using System.Collections.Generic;
using UnityEngine;

internal enum AmplifyMotionObjectType
{
	None,
	Solid,
	Skinned,
#if UNITY_3 || UNITY_4
	Cloth
#endif
}

[Serializable]
internal abstract class MotionState
{
	// TEMPORARY
	//public string m_name = "";
	//public string Name { get { return m_name; } }

	public const int AsyncUpdateTimeout = 100;

	protected bool m_error;
	protected bool m_initialized;

	protected AmplifyMotionCamera m_owner;
	protected AmplifyMotionObjectBase m_obj;

	public AmplifyMotionCamera Owner { get { return m_owner; } }	

	public MotionState( AmplifyMotionCamera owner, AmplifyMotionObjectBase obj )
	{
		// TEMPORARY
		//m_name = obj.name;

		m_error = false;
		m_initialized = false;		

		m_owner = owner;
		m_obj = obj;		
	}

	internal virtual void Initialize() { m_initialized = true; }
	internal abstract void UpdateTransform( bool starting );
	internal virtual void AsyncUpdate() { }
	internal virtual void RenderVectors( Camera camera, float scale ) { }
}

[AddComponentMenu( "" )]
public class AmplifyMotionObjectBase : MonoBehaviour
{
	internal static bool ApplyToChildren = true;
	[SerializeField] private bool m_applyToChildren = ApplyToChildren;

	private AmplifyMotionObjectType m_type = AmplifyMotionObjectType.None;
	private Dictionary<Camera, MotionState> m_states = new Dictionary<Camera, MotionState>();

	private bool m_initialized = false;
	private bool m_fixedStep = false;
	private int m_objectId = 0;

	internal bool FixedStep { get { return m_fixedStep; } }
	internal int ObjectId { get { return m_objectId; } }

	internal void RegisterCamera( AmplifyMotionCamera camera )
	{
		Camera actual = camera.GetComponent<Camera>();
		if ( ( actual.cullingMask & ( 1 << gameObject.layer ) ) != 0 && !m_states.ContainsKey( actual ) )
		{
			MotionState state =null;
			switch ( m_type )
			{
				case AmplifyMotionObjectType.Solid:
					state = new AmplifyMotion.SolidState( camera, this ); break;
				case AmplifyMotionObjectType.Skinned:
					state = new AmplifyMotion.SkinnedState( camera, this );	break;
			#if UNITY_3 || UNITY_4
				case AmplifyMotionObjectType.Cloth:
					state = new AmplifyMotion.ClothState( camera, this ); break;
			#endif
				default:
					throw new Exception( "[AmplifyMotion] Invalid object type." );
			}

			m_fixedStep = false;
		#if UNITY_3 || UNITY_4
			if ( m_type == AmplifyMotionObjectType.Cloth )
				m_fixedStep = true;
			else if ( m_type == AmplifyMotionObjectType.Solid )
		#else
			if ( m_type == AmplifyMotionObjectType.Solid )
		#endif
			{
				Rigidbody rb = GetComponent<Rigidbody>();
				if ( rb != null && rb.interpolation == RigidbodyInterpolation.None )
					m_fixedStep = true;
			}

			camera.RegisterObject( this );

			m_states.Add( actual, state );
		}		
	}

	internal void UnregisterCamera( AmplifyMotionCamera camera )
	{
		MotionState state;
		Camera actual = camera.GetComponent<Camera>();
		if ( m_states.TryGetValue( actual, out state ) )
		{
			camera.UnregisterObject( this );		

			m_states.Remove( actual );
		}
	}

	void OnEnable()
	{
		m_initialized = false;
		Renderer renderer = GetComponent<Renderer>();
		if ( renderer != null )
		{
			// At this point, Renderer is guaranteed to be one of the following
			if ( renderer.GetType() == typeof( MeshRenderer ) )
				m_type = AmplifyMotionObjectType.Solid;
			else if ( renderer.GetType() == typeof( SkinnedMeshRenderer ) )
				m_type = AmplifyMotionObjectType.Skinned;
		#if UNITY_3 || UNITY_4
			else if ( renderer.GetType() == typeof( ClothRenderer ) )
				m_type = AmplifyMotionObjectType.Cloth;
		#endif

			AmplifyMotionEffectBase.RegisterObject( this );
		}

		if ( m_applyToChildren )
		{
			foreach ( Transform child in gameObject.transform )
				AmplifyMotionEffectBase.RegisterRecursivelyS( child.gameObject );
		}

		// No renderer? disable it, it is here just for adding children
		if ( renderer == null )
			enabled = false;
	}

	void OnDisable()
	{
		AmplifyMotionEffectBase.UnregisterObject( this );
		m_initialized = false;
	}

	void TryInitialize()
	{		
		foreach ( MotionState state in m_states.Values )
			state.Initialize();

		m_initialized = true;
	}

	void Start()
	{
		if ( AmplifyMotionEffectBase.Instance != null && !m_initialized )
			TryInitialize();
	}

	void Update()
	{
		if ( AmplifyMotionEffectBase.Instance != null && !m_initialized )
			TryInitialize();
	}

	internal void OnUpdateTransform( AmplifyMotionCamera owner, bool starting )
	{
		MotionState state;
		if ( m_states.TryGetValue( owner.GetComponent<Camera>(), out state ) )
			state.UpdateTransform( starting );
	}

	internal void OnRenderVectors( Camera camera, float scale )
	{
		MotionState state = null;
		if ( m_states.TryGetValue( Camera.current, out state ) )
			state.RenderVectors( camera, scale );
	}
}