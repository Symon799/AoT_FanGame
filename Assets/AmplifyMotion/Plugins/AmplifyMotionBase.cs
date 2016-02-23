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

//#define DEBUG

using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AmplifyMotion
{
	public enum Quality
	{
		Mobile,
		Standard,
		SoftEdge_SM3
	}
}

[RequireComponent( typeof( Camera ) )]
[AddComponentMenu( "" )]
public class AmplifyMotionEffectBase : MonoBehaviour
{
	public AmplifyMotion.Quality QualityLevel = AmplifyMotion.Quality.Standard;
	public bool AutoRegisterObjs = true;
	public Camera[] OverlayCameras = new Camera[ 0 ];
	public LayerMask CullingMask = ~0;
	public int QualitySteps = 1;
	public float MotionScale = 3.0f;
	public float MinVelocity = 1.0f;
	public float MaxVelocity = 10.0f;
	public int workerThreads = 0;
	public bool DebugMode = false;

	private bool m_starting = true;

	private int m_width, m_height;	
#if DEBUG
	public RenderTexture m_motionRT;
	public RenderTexture m_combinedRT;
	public RenderTexture m_blurRT;
	public Texture m_dummyTex;
#else
	private RenderTexture m_motionRT;
	private RenderTexture m_combinedRT;
	private RenderTexture m_blurRT;
	private Texture m_dummyTex;
#endif
	private Material m_blurMaterial;
	private Material m_solidVectorsMaterial;
	private Material m_skinnedVectorsMaterial;
	private Material m_reprojectionMaterial;
	private Material m_combineMaterial;
	private Material m_dilationMaterial;
	private Material m_debugMaterial;

	internal Material SolidVectorsMaterial { get { return m_solidVectorsMaterial; } }
	internal Material SkinnedVectorsMaterial { get { return m_skinnedVectorsMaterial; } }

#if UNITY_3 || UNITY_4
	private Material m_clothVectorsMaterial;
	internal Material ClothVectorsMaterial { get { return m_clothVectorsMaterial; } }
#endif

	internal RenderTexture MotionRenderTexture { get { return m_motionRT; } }

#if TRIAL
private Texture2D m_watermark;
#endif

	// GLOBAL OBJECT MANAGEMENT	
	public static Dictionary<GameObject, AmplifyMotionObjectBase> m_activeObjects = new Dictionary<GameObject, AmplifyMotionObjectBase>();
	public static Dictionary<Camera, AmplifyMotionCamera> m_activeCameras = new Dictionary<Camera, AmplifyMotionCamera>();

	private Dictionary<Camera, AmplifyMotionCamera> m_linkedCameras = new Dictionary<Camera, AmplifyMotionCamera>();
	internal Camera[] m_linkedCameraKeys = null;
	internal AmplifyMotionCamera[] m_linkedCameraValues = null;
	internal bool m_linkedCamerasChanged = true;

	private AmplifyMotionPostProcess m_currentPostProcess = null;

	private int m_globalObjectId = 1;
	private float m_deltaTime;

	private float m_normalizedMotionScale;
	internal float NormalizedMotionScale { get { return m_normalizedMotionScale; } }

	private AmplifyMotion.Quality m_qualityLevel;

	public static bool IgnoreMotionScaleWarning = false;

	private AmplifyMotionCamera m_baseCamera = null;
	public AmplifyMotionCamera BaseCamera { get { return m_baseCamera; } }

	private static AmplifyMotionEffectBase m_firstInstance = null;
	public static AmplifyMotionEffectBase FirstInstance { get { return m_firstInstance; } }
	public static AmplifyMotionEffectBase Instance { get { return m_firstInstance; } }

	private AmplifyMotion.WorkerThreadPool m_workerThreadPool = null;
	internal AmplifyMotion.WorkerThreadPool WorkerPool { get { return m_workerThreadPool; } }

	// HACKS. Unity does not allow assigning component vars before Awake/OnEnable/Start	
	internal static AmplifyMotionEffectBase CurrentInstance = null;
	internal static AmplifyMotionCamera CurrentOwner = null;

	//public string DebugString = "";

	void Awake()
	{
		if ( m_firstInstance == null )
			m_firstInstance = this;

		m_globalObjectId = 1;

		m_width = m_height = 0;
	}

	internal void ResetObjectId()
	{
		m_globalObjectId = 1;
	}

	internal int GenerateObjectId( GameObject obj )
	{
		// id = 0, static objs
		// id = 255, excluded objs

		if ( obj.isStatic )
			return 0; // same as background
		
		m_globalObjectId++;

		// TEMPORARY FIX: wrap around; may cause artifacts on id collision of nearby objs
		if ( m_globalObjectId > 254 )
			m_globalObjectId = 1;

		return m_globalObjectId;
	}

	void SafeCreateRenderTexture( ref RenderTexture rt, string name, int depth, RenderTextureFormat fmt, RenderTextureReadWrite rw )
	{
		if ( rt == null )
		{
			rt = new RenderTexture( m_width, m_height, depth, fmt, rw );
			rt.name = name;
			rt.wrapMode = TextureWrapMode.Clamp;
			rt.filterMode = FilterMode.Point;
			rt.hideFlags = HideFlags.DontSave;
			rt.Create();
		}
	}

	void SafeDestroyRenderTexture( ref RenderTexture rt )
	{
		if ( rt != null )
		{
			DestroyImmediate( rt );
			rt = null;
		}
	}

	void SafeDestroyTexture( ref Texture tex )
	{
		if ( tex != null )
		{
			DestroyImmediate( tex );
			tex = null;
		}
	}

	void SafeDestroyMaterial( ref Material mat )
	{
		if ( mat != null )
		{
			DestroyImmediate( mat );
			mat = null;
		}
	}

	void DestroyMaterials()
	{
		SafeDestroyMaterial( ref m_blurMaterial );
		SafeDestroyMaterial( ref m_solidVectorsMaterial );
		SafeDestroyMaterial( ref m_skinnedVectorsMaterial );
	#if UNITY_3 || UNITY_4
		SafeDestroyMaterial( ref m_clothVectorsMaterial );
	#endif
		SafeDestroyMaterial( ref m_reprojectionMaterial );
		SafeDestroyMaterial( ref m_combineMaterial );
		SafeDestroyMaterial( ref m_dilationMaterial );
		SafeDestroyMaterial( ref m_debugMaterial );
	}

	void DestroyRenderTextures()
	{
		RenderTexture.active = null;

		SafeDestroyRenderTexture( ref m_motionRT );
		SafeDestroyRenderTexture( ref m_combinedRT );
		SafeDestroyRenderTexture( ref m_blurRT );

		SafeDestroyTexture( ref m_dummyTex );

		if ( m_dummyTex != null )
		{
			DestroyImmediate( m_dummyTex );
			m_dummyTex = null;
		}
	}

	void CheckMaterialAndShader( Material material, string name )
	{
		if ( material == null || material.shader == null )
			Debug.LogError( "[AmplifyMotion] Error creating " + name + " material" );
		else if ( !material.shader.isSupported )
			Debug.LogError( "[AmplifyMotion] " + name + " shader not supported on this platform" );
		else
			material.hideFlags = HideFlags.DontSave;
	}

	void CreateMaterials()
	{
		DestroyMaterials();

		string mobile = ( QualityLevel == AmplifyMotion.Quality.Mobile ) ? "Mobile" : "";
		string soft = ( QualityLevel == AmplifyMotion.Quality.SoftEdge_SM3 ) ? "Soft" : "";

		string blurShader = "Hidden/Amplify Motion/MotionBlur" + mobile + soft;
		string solidVectorsShader = "Hidden/Amplify Motion/SolidVectors" + mobile;
		string skinnedVectorsShader = "Hidden/Amplify Motion/SkinnedVectors" + mobile;
		string reprojectionVectorsShader = "Hidden/Amplify Motion/ReprojectionVectors";
		string combineShader = "Hidden/Amplify Motion/Combine";
		string dilationShader = "Hidden/Amplify Motion/Dilation";
		string debugShader = "Hidden/Amplify Motion/Debug";

		m_blurMaterial = new Material( Shader.Find( blurShader ) );
		m_solidVectorsMaterial = new Material( Shader.Find( solidVectorsShader ) );
		m_skinnedVectorsMaterial = new Material( Shader.Find( skinnedVectorsShader ) );
		m_reprojectionMaterial = new Material( Shader.Find( reprojectionVectorsShader ) );
		m_combineMaterial = new Material( Shader.Find( combineShader ) );
		m_dilationMaterial = new Material( Shader.Find( dilationShader ) );
		m_debugMaterial = new Material( Shader.Find( debugShader ) );

		CheckMaterialAndShader( m_blurMaterial, blurShader );
		CheckMaterialAndShader( m_solidVectorsMaterial, solidVectorsShader );
		CheckMaterialAndShader( m_skinnedVectorsMaterial, skinnedVectorsShader );
		CheckMaterialAndShader( m_reprojectionMaterial, reprojectionVectorsShader );
		CheckMaterialAndShader( m_combineMaterial, combineShader );
		CheckMaterialAndShader( m_dilationMaterial, combineShader );
		CheckMaterialAndShader( m_debugMaterial, combineShader );

	#if UNITY_3 || UNITY_4		
		string clothVectorsShader = "Hidden/Amplify Motion/ClothVectors" + mobile;		
		m_clothVectorsMaterial = new Material( Shader.Find( clothVectorsShader ) );		
		CheckMaterialAndShader( m_clothVectorsMaterial, clothVectorsShader );
	#endif
	}

	void UpdateRenderTextures()
	{
		Camera camera = GetComponent<Camera>();

		int screenWidth = Mathf.FloorToInt( camera.pixelWidth + 0.5f );
		int screenHeight = Mathf.FloorToInt( camera.pixelHeight + 0.5f );

		if ( QualityLevel == AmplifyMotion.Quality.Mobile )
		{
			screenWidth /= 2;
			screenHeight /= 2;
		}

		if ( m_width != screenWidth || m_height != screenHeight || QualityLevel != m_qualityLevel )
		{
			m_qualityLevel = QualityLevel;

			m_width = screenWidth;
			m_height = screenHeight;

			DestroyRenderTextures();
		}

		RenderTextureFormat auxRGB = RenderTextureFormat.ARGB32;
		RenderTextureReadWrite auxRW = RenderTextureReadWrite.Linear;
		RenderTextureFormat colRGB = RenderTextureFormat.ARGB32;
		RenderTextureReadWrite colRW = RenderTextureReadWrite.Default;

		SafeCreateRenderTexture( ref m_motionRT, "Motion", 24, auxRGB, auxRW );
		SafeCreateRenderTexture( ref m_combinedRT, "Combined", 0, colRGB, colRW );
		if ( QualityLevel == AmplifyMotion.Quality.Mobile )
			SafeCreateRenderTexture( ref m_blurRT, "Blur", 0, colRGB, colRW );

		if ( m_dummyTex == null )
		{
			m_dummyTex = new Texture2D( 4, 4, TextureFormat.ARGB32, false, true );
			m_dummyTex.wrapMode = TextureWrapMode.Clamp;
			m_dummyTex.hideFlags = HideFlags.DontSave;
		}
	}

	public bool CheckSupport()
	{
		if ( !SystemInfo.supportsImageEffects || !SystemInfo.supportsRenderTextures )
		{
			Debug.LogError( "[AmplifyMotion] Initialization failed. This plugin requires support for Image Effects and Render Textures." );
			return false;
		}
		return true;
	}

	void OnEnable()
	{
		if ( !CheckSupport() )
		{
			enabled = false;
			return;
		}

		if ( workerThreads <= 0 )
			workerThreads = Environment.ProcessorCount;

		m_workerThreadPool = new AmplifyMotion.WorkerThreadPool();
		m_workerThreadPool.InitializeAsyncUpdateThreads( workerThreads );

		m_starting = true;

		CreateMaterials();

		if ( AutoRegisterObjs )
			UpdateActiveObjects();

		InitializeCameras();

		UpdateRenderTextures();

		m_linkedCameras.TryGetValue( this.GetComponent<Camera>(), out m_baseCamera );

		if ( m_currentPostProcess != null )
			m_currentPostProcess.enabled = true;

		m_qualityLevel = QualityLevel;
	}

	void OnDisable()
	{
		if ( m_currentPostProcess != null )
			m_currentPostProcess.enabled = false;

		m_workerThreadPool.FinalizeAsyncUpdateThreads();
		m_workerThreadPool = null;
	}

	void Start()
	{
		UpdatePostProcess();

#if TRIAL
	m_watermark = new Texture2D( 4, 4 );
	m_watermark.LoadImage( AmplifyMotion.Watermark.ImageData );
#endif
	}

	internal void RemoveCamera( Camera reference )
	{
		m_linkedCameras.Remove( reference );
	}

	void OnDestroy()
	{
		AmplifyMotionCamera[] prevLinkedCams = m_linkedCameras.Values.ToArray<AmplifyMotionCamera>();

		foreach ( AmplifyMotionCamera cam in prevLinkedCams )
		{
			if ( cam.gameObject != gameObject )
			{
				cam.GetComponent<Camera>().targetTexture = null;
				DestroyImmediate( cam );
			}
		}

		DestroyRenderTextures();
		DestroyMaterials();
#if TRIAL
	DestroyImmediate( m_watermark );
#endif
	}

	GameObject RecursiveFindCamera( GameObject obj, string auxCameraName )
	{
		GameObject cam = null;
		if ( obj.name == auxCameraName )
			cam = obj;
		else
		{
			foreach ( Transform child in obj.transform )
			{
				cam = RecursiveFindCamera( child.gameObject, auxCameraName );
				if ( cam != null )
					break;
			}
		}
		return cam;
	}

	void InitializeCameras()
	{
		List<Camera> cleanOverlayCameras = new List<Camera>( OverlayCameras.Length );
		for ( int i = 0; i < OverlayCameras.Length; i++ )
		{
			if ( OverlayCameras[ i ] != null )
				cleanOverlayCameras.Add( OverlayCameras[ i ] );
		}

		Camera[] referenceCameras = new Camera[ cleanOverlayCameras.Count + 1 ];

		referenceCameras[ 0 ] = this.GetComponent<Camera>();
		for ( int i = 0; i < cleanOverlayCameras.Count; i++ )
			referenceCameras[ i + 1 ] = cleanOverlayCameras[ i ];

		m_linkedCameras.Clear();

		for ( int i = 0; i < referenceCameras.Length; i++ )
		{
			Camera reference = referenceCameras[ i ];
			if ( !m_linkedCameras.ContainsKey( reference ) )
			{
				AmplifyMotionCamera cam = reference.gameObject.GetComponent<AmplifyMotionCamera>();
				if ( cam != null && cam.Instance != this )
				{
					DestroyImmediate( cam );
					cam = null;
				}

				if ( cam == null )
				{
					CurrentInstance = this;
					cam = reference.gameObject.AddComponent<AmplifyMotionCamera>();
					cam.SetOverlay( i > 0 );
					CurrentInstance = null;
				}

				m_linkedCameras.Add( reference, cam );
				m_linkedCamerasChanged = true;
			}
		}
	}

	public void UpdateActiveCameras()
	{
		InitializeCameras();
	}

	internal static void RegisterCamera( AmplifyMotionCamera cam )
	{
		//Debug.Log( "Registering AmplifyMotionCamera: " + cam.name );

		m_activeCameras.Add( cam.GetComponent<Camera>(), cam );
		foreach ( AmplifyMotionObjectBase obj in m_activeObjects.Values )
			obj.RegisterCamera( cam );
	}

	internal static void UnregisterCamera( AmplifyMotionCamera cam )
	{
		//Debug.Log( "Unregistering AmplifyMotionCamera: " + cam.name );

		foreach ( AmplifyMotionObjectBase obj in m_activeObjects.Values )
			obj.UnregisterCamera( cam );
		m_activeCameras.Remove( cam.GetComponent<Camera>() );
	}

	public void UpdateActiveObjects()
	{
		GameObject[] gameObjs = FindObjectsOfType( typeof( GameObject ) ) as GameObject[];
		for ( int i = 0; i < gameObjs.Length; i++ )
		{
			if ( !m_activeObjects.ContainsKey( gameObjs[ i ] ) )
				TryRegister( gameObjs[ i ] );
		}
	}

	internal static void RegisterObject( AmplifyMotionObjectBase obj )
	{
		m_activeObjects.Add( obj.gameObject, obj );
		foreach ( AmplifyMotionCamera cam in m_activeCameras.Values )
			obj.RegisterCamera( cam );
	}

	internal static void UnregisterObject( AmplifyMotionObjectBase obj )
	{
		foreach ( AmplifyMotionCamera cam in m_activeCameras.Values )
			obj.UnregisterCamera( cam );
		m_activeObjects.Remove( obj.gameObject );
	}

	internal static bool FindValidTag( Material[] materials )
	{
		for ( int i = 0; i < materials.Length; i++ )
		{
			if ( materials[ i ] != null )
			{
				string tag = materials[ i ].GetTag( "RenderType", false );
				if ( tag == "Opaque" || tag == "TransparentCutout" )
					return true;
			}
		}
		return false;
	}

	internal static void TryRegister( GameObject gameObj )
	{
		// Ignore static objects
		if ( gameObj.isStatic )
			return;

		// Ignore invalid materials; Ignore static batches
		Renderer renderer = gameObj.GetComponent<Renderer>();
		if ( renderer == null || renderer.sharedMaterials == null || renderer.isPartOfStaticBatch )
			return;

		// Ignore unsupported RenderType
		if ( !renderer.enabled || !FindValidTag( renderer.sharedMaterials ) )
			return;

	#if UNITY_3 || UNITY_4	
		bool cloth = false;
		if ( renderer.GetType() == typeof( MeshRenderer ) || renderer.GetType() == typeof( SkinnedMeshRenderer ) ||
			( cloth = ( renderer.GetType() == typeof( ClothRenderer ) ) ) )
		{
			if ( cloth && gameObj.GetComponent<InteractiveCloth>().tearFactor != 0.0f )
				Debug.LogWarning( "[AmplifyMotion] Tearable cloth objects are not supported at this time. Ignoring cloth object \"" + renderer.name + "\"" );
			else if ( gameObj.GetComponent<AmplifyMotionObjectBase>() == null )
			
	#else
		if ( renderer.GetType() == typeof( MeshRenderer ) || renderer.GetType() == typeof( SkinnedMeshRenderer ) )
		{
			if ( gameObj.GetComponent<AmplifyMotionObjectBase>() == null )
	#endif
			{
				AmplifyMotionObjectBase.ApplyToChildren = false;
				gameObj.AddComponent<AmplifyMotionObjectBase>();
				AmplifyMotionObjectBase.ApplyToChildren = true;
			}
		}
	}

	internal static void TryUnregister( GameObject gameObj )
	{
		AmplifyMotionObjectBase comp = gameObj.GetComponent<AmplifyMotionObjectBase>();
		if ( comp != null )
			Destroy( comp );
	}

	public void Register( GameObject gameObj )
	{
		if ( !m_activeObjects.ContainsKey( gameObj ) )
			TryRegister( gameObj );
	}

	public static void RegisterS( GameObject gameObj )
	{
		if ( !m_activeObjects.ContainsKey( gameObj ) )
			TryRegister( gameObj );
	}

	public void RegisterRecursively( GameObject gameObj )
	{
		if ( !m_activeObjects.ContainsKey( gameObj ) )
			TryRegister( gameObj );

		foreach ( Transform child in gameObj.transform )
			RegisterRecursively( child.gameObject );
	}

	public static void RegisterRecursivelyS( GameObject gameObj )
	{
		if ( !m_activeObjects.ContainsKey( gameObj ) )
			TryRegister( gameObj );

		foreach ( Transform child in gameObj.transform )
			RegisterRecursivelyS( child.gameObject );
	}

	public void Unregister( GameObject gameObj )
	{
		if ( m_activeObjects.ContainsKey( gameObj ) )
			TryUnregister( gameObj );
	}

	public static void UnregisterS( GameObject gameObj )
	{
		if ( m_activeObjects.ContainsKey( gameObj ) )
			TryUnregister( gameObj );
	}

	public void UnregisterRecursively( GameObject gameObj )
	{
		if ( m_activeObjects.ContainsKey( gameObj ) )
			TryUnregister( gameObj );

		foreach ( Transform child in gameObj.transform )
			UnregisterRecursively( child.gameObject );
	}

	public static void UnregisterRecursivelyS( GameObject gameObj )
	{
		if ( m_activeObjects.ContainsKey( gameObj ) )
			TryUnregister( gameObj );

		foreach ( Transform child in gameObj.transform )
			UnregisterRecursivelyS( child.gameObject );
	}

	void UpdatePostProcess()
	{
		Camera highestReference = null;
		float highestDepth = -float.MaxValue;

		if ( m_linkedCamerasChanged )
			UpdateLinkedCameras();

		for ( int i = 0; i < m_linkedCameraKeys.Length; i++ )
		{
			if ( m_linkedCameraKeys[ i ].depth > highestDepth )
			{
				highestReference = m_linkedCameraKeys[ i ];
				highestDepth = m_linkedCameraKeys[ i ].depth;
			}
		}

		if ( m_currentPostProcess != null && m_currentPostProcess.gameObject != highestReference.gameObject )
		{
			DestroyImmediate( m_currentPostProcess );
			m_currentPostProcess = null;
		}

		if ( m_currentPostProcess == null && highestReference != null && highestReference != GetComponent<Camera>() )
		{
			CurrentInstance = this;

			AmplifyMotionPostProcess[] runtimes = gameObject.GetComponents<AmplifyMotionPostProcess>();
			if ( runtimes != null && runtimes.Length > 0 )
			{
				for ( int i = 0; i < runtimes.Length; i++ )
					DestroyImmediate( runtimes[ i ] );
			}
			m_currentPostProcess = highestReference.gameObject.AddComponent<AmplifyMotionPostProcess>();

			CurrentInstance = null;
		}
	}

	void LateUpdate()
	{
		if ( m_baseCamera.AutoStep )
		{
			float delta = Application.isPlaying ? Time.deltaTime : Time.fixedDeltaTime;
			m_deltaTime = ( delta > float.Epsilon ) ? delta : m_deltaTime;
		}

		MotionScale = Mathf.Max( MotionScale, 0 );
		MinVelocity = Mathf.Min( MinVelocity, MaxVelocity );

		UpdatePostProcess();
	}

	public void StopAutoStep()
	{
		foreach ( AmplifyMotionCamera cam in m_linkedCameras.Values )
			cam.StopAutoStep();
	}

	public void StartAutoStep()
	{
		foreach ( AmplifyMotionCamera cam in m_linkedCameras.Values )
			cam.StartAutoStep();
	}

	public void Step( float delta )
	{
		m_deltaTime = delta;
		foreach ( AmplifyMotionCamera cam in m_linkedCameras.Values )
			cam.Step();
	}

	void UpdateLinkedCameras()
	{
		Dictionary<Camera, AmplifyMotionCamera>.KeyCollection keys = m_linkedCameras.Keys;
		Dictionary<Camera, AmplifyMotionCamera>.ValueCollection values = m_linkedCameras.Values;

		if ( m_linkedCameraKeys == null || keys.Count != m_linkedCameraKeys.Length )
			m_linkedCameraKeys = new Camera[ keys.Count ];

		if ( m_linkedCameraValues == null || values.Count != m_linkedCameraValues.Length )
			m_linkedCameraValues = new AmplifyMotionCamera[ values.Count ];

		keys.CopyTo( m_linkedCameraKeys, 0 );
		values.CopyTo( m_linkedCameraValues, 0 );

		m_linkedCamerasChanged = false;
	}

	void OnPreRender()
	{
		if ( GetComponent<Camera>().enabled && ( Time.frameCount == 1 || Mathf.Abs( Time.deltaTime ) > float.Epsilon ) )
		{
			if ( m_linkedCamerasChanged )
				UpdateLinkedCameras();

			for ( int i = 0; i < m_linkedCameraValues.Length; i++ )
				m_linkedCameraValues[ i ].UpdateTransform();
		}
	}

	void RenderReprojectionVectors( RenderTexture destination, float scale )
	{
		Shader.SetGlobalMatrix( "_EFLOW_MATRIX_CURR_REPROJ", m_baseCamera.PrevViewProjMatrix * m_baseCamera.InvViewProjMatrix );
		Shader.SetGlobalFloat( "_EFLOW_MOTION_SCALE", scale );

		Graphics.Blit( m_dummyTex, destination, m_reprojectionMaterial );
	}

	void OnPostRender()
	{
		UpdateRenderTextures();

		RenderTexture prevRT = RenderTexture.active;

		m_motionRT.DiscardContents();
		Graphics.SetRenderTarget( m_motionRT );
		GL.Clear( true, m_starting, Color.black );

		Shader.SetGlobalFloat( "_EFLOW_MIN_VELOCITY", MinVelocity );
		Shader.SetGlobalFloat( "_EFLOW_MAX_VELOCITY", MaxVelocity );
		Shader.SetGlobalFloat( "_EFLOW_RCP_TOTAL_VELOCITY", 1.0f / ( MaxVelocity - MinVelocity ) );

		m_normalizedMotionScale = MotionScale * ( 1.0f / m_deltaTime );
		float scale = !m_starting ? m_normalizedMotionScale : 0;

		RenderReprojectionVectors( m_motionRT, scale );

		ResetObjectId();

		m_baseCamera.RenderVectors( scale );

		m_starting = false;

		Graphics.SetRenderTarget( prevRT );
		RenderTexture.active = prevRT;
	}

	void RenderMobile( RenderTexture source, RenderTexture destination, RenderTexture motion, Vector4 blurStep )
	{
		m_combinedRT.DiscardContents();
		m_combineMaterial.SetTexture( "_MotionTex", motion );
		Graphics.Blit( source, m_combinedRT, m_combineMaterial, 0 );

		m_blurMaterial.SetTexture( "_MotionTex", motion );

		if ( QualitySteps > 1 )
		{
			RenderTexture temp = RenderTexture.GetTemporary( m_width, m_height, 0, RenderTextureFormat.ARGB32 );

			float step = 1.0f / QualitySteps;
			float scale = 1.0f;
			RenderTexture src = m_combinedRT;
			RenderTexture dst = temp;

			for ( int i = 0; i < QualitySteps; i++ )
			{
				if ( dst != destination )
					dst.DiscardContents();

				m_blurMaterial.SetVector( "_EFLOW_BLUR_STEP", blurStep * scale );
				Graphics.Blit( src, dst, m_blurMaterial );

				if ( i < QualitySteps - 2 )
				{
					RenderTexture tmp = dst;
					dst = src;
					src = tmp;
				}
				else
				{
					src = dst;
					dst = m_blurRT;
				}
				scale -= step;
			}

			RenderTexture.ReleaseTemporary( temp );
		}
		else
		{
			m_blurRT.DiscardContents();
			m_blurMaterial.SetVector( "_EFLOW_BLUR_STEP", blurStep );
			Graphics.Blit( m_combinedRT, m_blurRT, m_blurMaterial );
		}

		// we need the full res here
		m_combineMaterial.SetTexture( "_MotionTex", motion );
		m_combineMaterial.SetTexture( "_BlurredTex", m_blurRT );

		Graphics.Blit( source, destination, m_combineMaterial, 1 );
	}

	void RenderStandard( RenderTexture source, RenderTexture destination, RenderTexture motion, Vector4 blurStep )
	{
		m_combinedRT.DiscardContents();
		m_combineMaterial.SetTexture( "_MotionTex", motion );
		Graphics.Blit( source, m_combinedRT, m_combineMaterial, 0 );

		m_blurMaterial.SetTexture( "_MotionTex", motion );

		if ( QualitySteps > 1 )
		{
			RenderTexture temp = RenderTexture.GetTemporary( m_width, m_height, 0, RenderTextureFormat.ARGB32 );

			float step = 1.0f / QualitySteps;
			float scale = 1.0f;
			RenderTexture src = m_combinedRT;
			RenderTexture dst = temp;

			for ( int i = 0; i < QualitySteps; i++ )
			{
				if ( dst != destination )
					dst.DiscardContents();

				m_blurMaterial.SetVector( "_EFLOW_BLUR_STEP", blurStep * scale );
				Graphics.Blit( src, dst, m_blurMaterial );

				if ( i < QualitySteps - 2 )
				{
					RenderTexture tmp = dst;
					dst = src;
					src = tmp;
				}
				else
				{
					src = dst;
					dst = destination;
				}
				scale -= step;
			}

			RenderTexture.ReleaseTemporary( temp );
		}
		else
		{
			m_blurMaterial.SetVector( "_EFLOW_BLUR_STEP", blurStep );
			Graphics.Blit( m_combinedRT, destination, m_blurMaterial );
		}
	}

	void OnRenderImage( RenderTexture source, RenderTexture destination )
	{
		if ( m_currentPostProcess == null )
			PostProcess( source, destination );
		else
			Graphics.Blit( source, destination );
	}

	public void PostProcess( RenderTexture source, RenderTexture destination )
	{
		//Graphics.Blit( m_motionRT, destination );
		//return;

		if ( DebugMode )
		{
			Graphics.Blit( m_motionRT, destination, m_debugMaterial );
			return;
		}

		Vector4 blurStep = Vector4.zero;
		blurStep.x = MaxVelocity / 1000.0f;
		blurStep.y = MaxVelocity / 1000.0f;

		RenderTexture motionRT = m_motionRT;
		bool msaa = ( QualitySettings.antiAliasing > 1 );
		if ( msaa )
		{
			motionRT = RenderTexture.GetTemporary( m_width, m_height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear );
			motionRT.filterMode = FilterMode.Point;

			m_dilationMaterial.SetTexture( "_MotionTex", m_motionRT );
			Graphics.Blit( m_motionRT, motionRT, m_dilationMaterial, 0 );
		}

		if ( QualityLevel == AmplifyMotion.Quality.Mobile )
			RenderMobile( source, destination, motionRT, blurStep );
		else if ( QualityLevel >= AmplifyMotion.Quality.Standard )
			RenderStandard( source, destination, motionRT, blurStep );

		if ( msaa )
			RenderTexture.ReleaseTemporary( motionRT );
	}

#if TRIAL
void OnGUI()
{
	GUI.DrawTexture( new Rect( 15, Screen.height - m_watermark.height - 12, m_watermark.width, m_watermark.height ), m_watermark );
}
#endif
}