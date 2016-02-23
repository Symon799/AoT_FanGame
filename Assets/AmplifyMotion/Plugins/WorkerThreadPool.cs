// Amplify Motion - Full-scene Motion Blur for Unity Pro
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System;
using System.Threading;
#if NETFX_CORE
using Windows.System.Threading;
using System.Threading.Tasks;
#endif
using System.Collections.Generic;
using UnityEngine;

namespace AmplifyMotion
{
internal class WorkerThreadPool
{
#if !NETFX_CORE
	private const int ThreadStateQueueCapacity = 1024;
	internal Queue<MotionState>[] m_threadStateQueues = null;
	internal object[] m_threadStateQueueLocks = null;

	private int m_threadPoolSize = 0;	
	private ManualResetEvent m_threadPoolTerminateSignal;
	private AutoResetEvent[] m_threadPoolContinueSignals;
	private Thread[] m_threadPool = null;
	internal object m_threadPoolLock = null;
	internal int m_threadPoolIndex = 0;
#endif

	internal void InitializeAsyncUpdateThreads( int threadCount )
	{
	#if !NETFX_CORE
		m_threadPoolSize = threadCount;
		m_threadStateQueues = new Queue<MotionState>[ m_threadPoolSize ];
		m_threadStateQueueLocks = new object[ m_threadPoolSize ];
		m_threadPool = new Thread[ m_threadPoolSize ];

		m_threadPoolTerminateSignal = new ManualResetEvent( false );
		m_threadPoolContinueSignals = new AutoResetEvent[ m_threadPoolSize ];
		m_threadPoolLock = new object();
		m_threadPoolIndex = 0;

		for ( int id = 0; id < m_threadPoolSize; id++ )
		{
			m_threadStateQueues[ id ] = new Queue<MotionState>( ThreadStateQueueCapacity );
			m_threadStateQueueLocks[ id ] = new object();

			m_threadPoolContinueSignals[ id ] = new AutoResetEvent( false );

			m_threadPool[ id ] = new Thread( new ParameterizedThreadStart( AsyncUpdateThread ) );
			m_threadPool[ id ].Start( new KeyValuePair<object, int>( this, id ) );		
		}
	#endif
	}

	internal void FinalizeAsyncUpdateThreads()
	{
	#if !NETFX_CORE
		m_threadPoolTerminateSignal.Set();
		
		for ( int i = 0; i < m_threadPoolSize; i++ )
		{
			m_threadPool[ i ].Abort();
			m_threadPool[ i ].Join();
		
			lock ( m_threadStateQueueLocks[ i ] )
			{
				while ( m_threadStateQueues[ i ].Count > 0 )
					m_threadStateQueues[ i ].Dequeue().AsyncUpdate();
			}
		}
		
		m_threadStateQueues = null;
		m_threadStateQueueLocks = null;
		
		m_threadPoolSize = 0;
		m_threadPool = null;
		m_threadPoolTerminateSignal = null;
		m_threadPoolContinueSignals = null;
		m_threadPoolLock = null;
		m_threadPoolIndex = 0;
	#endif
	}

	internal void EnqueueAsyncUpdate( MotionState state )
	{
	#if NETFX_CORE
		Task.Run( () => AsyncUpdateCallback( state ) );
	#else
		lock ( m_threadStateQueueLocks[ m_threadPoolIndex ] )
		{
			m_threadStateQueues[ m_threadPoolIndex ].Enqueue( state );			
		}
		
		m_threadPoolContinueSignals[ m_threadPoolIndex ].Set();
		
		m_threadPoolIndex++;
		if ( m_threadPoolIndex >= m_threadPoolSize )
			m_threadPoolIndex = 0;
	#endif
	}

	private static void AsyncUpdateCallback( object obj )
	{
		MotionState state = ( MotionState ) obj;
		state.AsyncUpdate();
	}

	private static void AsyncUpdateThread( object obj )
	{
	#if !NETFX_CORE
		KeyValuePair<object, int> pair = ( KeyValuePair<object, int> ) obj;
		WorkerThreadPool pool = ( WorkerThreadPool ) pair.Key;
		int id = ( int ) pair.Value;
	
		while ( !pool.m_threadPoolTerminateSignal.WaitOne( 0 ) )
		{
			pool.m_threadPoolContinueSignals[ id ].WaitOne();
		
			try
			{
				while ( true )
				{
					MotionState state = null;
	
					lock ( pool.m_threadStateQueueLocks[ id ] )
					{
						if ( pool.m_threadStateQueues[ id ].Count > 0 )
							state = pool.m_threadStateQueues[ id ].Dequeue();
					}
	
					if ( state != null )
						state.AsyncUpdate();
					else
						break;
				}
			}
			catch ( System.Exception e )
			{
				if ( e.GetType() != typeof( ThreadAbortException ) )
					Debug.LogWarning( e );
			}
		}
	#endif
	}	
}
}