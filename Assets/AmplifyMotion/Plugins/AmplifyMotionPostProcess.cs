// Amplify Motion - Full-scene Motion Blur for Unity Pro
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu( "" )]
[RequireComponent( typeof( Camera ) )]
sealed public class AmplifyMotionPostProcess : MonoBehaviour
{
	private AmplifyMotionEffectBase Instance = null;

	void OnEnable()
	{
		if ( Instance == null )
			Instance = AmplifyMotionEffectBase.CurrentInstance;
	}
	
	void OnRenderImage( RenderTexture source, RenderTexture destination )
	{
		if ( Instance != null )
			Instance.PostProcess( source, destination );
	}
}