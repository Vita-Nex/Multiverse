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
using System.Threading.Tasks;
#endregion

namespace Multiverse
{
	public abstract class PortalTransport : IDisposable
	{
		private static readonly object _OutSync;

		static PortalTransport()
		{
			_OutSync = new object();
		}
		
		private volatile bool _IsDisposed;
		private volatile bool _IsDisposing;

		private volatile bool _IsRunning, _IsInitializing;

		public abstract Socket Socket { get; }

		public bool IsDisposed { get { return _IsDisposed; } }
		public bool IsDisposing { get { return _IsDisposing; } }

		public bool IsInitializing { get { return _IsInitializing; } }
		public bool IsRunning { get { return _IsRunning; } }

		public bool IsAlive { get { return Socket != null && !_IsDisposed; } }

		public bool Start()
		{
			if (_IsDisposed || _IsDisposing)
			{
				return false;
			}

			if (_IsRunning)
			{
				return true;
			}

			_IsInitializing = true;

			try
			{
				OnStart();
			}
			finally
			{
				_IsInitializing = false;
			}

			_IsRunning = true;

			if (Socket != null && !_IsDisposed && !_IsDisposing)
			{
				OnStarted();
			}

			return true;
		}

		protected abstract void OnStart();
		protected abstract void OnStarted();

		public abstract bool Send(PortalPacket p);
		public abstract bool SendExcept(PortalPacket p, ushort exceptID);
		public abstract bool SendTarget(PortalPacket p, ushort targetID);
		
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
		}

		public void ToConsole(ConsoleColor color, string message, params object[] args)
		{
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

			_IsRunning = false;
			_IsInitializing = false;
			
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

			try
			{
				s.Shutdown(SocketShutdown.Both);
			}
			catch
			{ }

			try
			{
				s.Disconnect(true);
			}
			catch
			{ }

			try
			{
				s.Close();
			}
			catch
			{ }

			try
			{
				s.Dispose();
			}
			catch
			{ }
		}
	}
}