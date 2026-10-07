using System;
using System.Threading;

namespace ASCOM.OnStep;

internal class GarbageCollection
{
	protected bool m_bContinueThread;

	protected bool m_GCWatchStopped;

	protected int m_iInterval;

	protected ManualResetEvent m_EventThreadEnded;

	public GarbageCollection(int iInterval)
	{
		m_bContinueThread = true;
		m_GCWatchStopped = false;
		m_iInterval = iInterval;
		m_EventThreadEnded = new ManualResetEvent(initialState: false);
	}

	public void GCWatch()
	{
		while (ContinueThread())
		{
			GC.Collect();
			Thread.Sleep(m_iInterval);
		}
		m_EventThreadEnded.Set();
	}

	protected bool ContinueThread()
	{
		lock (this)
		{
			return m_bContinueThread;
		}
	}

	public void StopThread()
	{
		lock (this)
		{
			m_bContinueThread = false;
		}
	}

	public void WaitForThreadToStop()
	{
		m_EventThreadEnded.WaitOne();
		m_EventThreadEnded.Reset();
	}
}
