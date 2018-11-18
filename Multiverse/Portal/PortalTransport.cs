#region Header
//   Vorspire    _,-'/-'/  PortalTransport.cs
//   .      __,-; ,'( '/
//    \.    `-.__`-._`:_,-._       _ , . ``
//     `:-._,------' ` _,`--` -: `_ , ` ,' :
//        `---..__,,--'  (C) 2018  ` -'. -'
//        #  Vita-Nex [http://core.vita-nex.com]  #
//  {o)xxx|===============-   #   -===============|xxx(o}
//        #        The MIT License (MIT)          #
#endregion

#region References
using System;
using System.Net.Sockets;
using System.Threading;
#endregion

namespace Multiverse
{
	public abstract class PortalTransport : IDisposable
	{
		private static readonly AutoResetEvent _OutSync;

		static PortalTransport()
		{
			_OutSync = new AutoResetEvent(true);
		}

		private volatile bool _IsDisposed;
		private volatile bool _IsDisposing;

		private volatile AutoResetEvent _StartSync;

		public abstract Socket Socket { get; }

		public bool IsDisposed { get { return _IsDisposed; } }
		public bool IsDisposing { get { return _IsDisposing; } }

		public virtual bool IsAlive { get { return Socket != null && !IsDisposed; } }

		public PortalTransport()
		{
			_StartSync = new AutoResetEvent(false);
		}

		public bool Start()
		{
			if (_IsDisposed || _IsDisposing)
			{
				return false;
			}

			try
			{
				if (ThreadPool.QueueUserWorkItem(Start))
				{
					return _StartSync != null && _StartSync.WaitOne();
				}
			}
			catch (Exception e)
			{
				ToConsole("Start: Failed", e);
			}

			return false;
		}

		private void Start(object state)
		{
			try
			{
				OnStart();
			}
			catch (Exception e)
			{
				ToConsole("Start: Failed", e);
			}

			if (_StartSync != null)
			{
				_StartSync.Set();
			}
		}

		protected abstract void OnStart();

		public abstract bool Send(PortalPacket p);
		public abstract bool SendExcept(PortalPacket p, ushort exceptID);
		public abstract bool SendTarget(PortalPacket p, ushort targetID);

		public bool CheckAlive()
		{
			return Portal.TryGet(CheckAlive, Portal.Ticks);
		}

		protected virtual bool CheckAlive(long ticks)
		{
			if (_IsDisposed)
			{
				return false;
			}

			if (Socket == null)
			{
				Dispose();
				return false;
			}

			return true;
		}

		public void ToConsole(string message, params object[] args)
		{
			ToConsole(ConsoleColor.Yellow, message, args);
		}

		public void ToConsole(string message, Exception e)
		{
			if (e != null)
			{
				ToConsole(ConsoleColor.Gray, "{0}:\n{1}", message, e);
			}
			else
			{
				ToConsole(ConsoleColor.Gray, message);
			}

			Portal.Trace(message, e);
		}

		public void ToConsole(ConsoleColor color, string message, params object[] args)
		{
			_OutSync.WaitOne();

			var cc = Console.ForegroundColor;

			Console.ForegroundColor = color;

			if (args == null || args.Length == 0)
			{
				Console.WriteLine("[{0}] {1}", this, message);
			}
			else
			{
				Console.WriteLine("[{0}] {1}", this, String.Format(message, args));
			}

			Console.ForegroundColor = cc;

			_OutSync.Set();
		}

		public void Dispose()
		{
			if (_IsDisposed || _IsDisposing)
			{
				return;
			}

			_IsDisposing = true;

			Portal.Try(OnBeforeDispose);

			_IsDisposed = true;

			Portal.Try(OnDispose);

			try
			{
				_StartSync.Close();
				_StartSync.Dispose();
			}
			catch
			{ }
			finally
			{
				_StartSync = null;
			}

			_IsDisposing = false;
		}

		protected virtual void OnBeforeDispose()
		{
			Send(PortalPackets.DisconnectNotify.Instance);
		}

		protected virtual void OnDispose()
		{
			var s = Socket;

			if (s == null)
			{
				return;
			}

			Portal.Try(s.Shutdown, SocketShutdown.Both);
			Portal.Try(s.Disconnect, true);

			Portal.Try(s.Close);
			Portal.Try(s.Dispose);
		}
	}
}