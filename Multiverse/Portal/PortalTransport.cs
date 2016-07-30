#region Header
//   Vorspire    _,-'/-'/  PortalTransport.cs
//   .      __,-; ,'( '/
//    \.    `-.__`-._`:_,-._       _ , . ``
//     `:-._,------' ` _,`--` -: `_ , ` ,' :
//        `---..__,,--'  (C) 2016  ` -'. -'
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
		private volatile bool _CheckingAlive;

		private volatile bool _IsDisposed;
		private volatile bool _IsDisposing;

		public Thread Thread { get; private set; }

		public abstract Socket Socket { get; }

		public bool IsDisposed { get { return _IsDisposed; } }
		public bool IsDisposing { get { return _IsDisposing; } }

		public bool IsAlive { get { return Socket != null && !IsDisposed; } }

		[STAThread]
		public void Start()
		{
			Thread = Thread.CurrentThread;

			try
			{
				OnStart();
			}
			catch (Exception e)
			{
				ToConsole("Start: Failed", e);

				Dispose();
			}
		}

		protected abstract void OnStart();

		public abstract bool Send(PortalPacket p);
		public abstract bool SendExcept(PortalPacket p, ushort exceptID);
		public abstract bool SendTarget(PortalPacket p, ushort targetID);
		
		public bool CheckAlive()
		{
			if (_CheckingAlive)
			{
				return IsAlive;
			}

			_CheckingAlive = true;
			var result = CheckAlive(Portal.Ticks);
			_CheckingAlive = false;

			return result;
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
			var cc = Console.ForegroundColor;

			Console.ForegroundColor = ConsoleColor.Yellow;

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

		public void ToConsole(string message, Exception e)
		{
			var cc = Console.ForegroundColor;

			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("[{0}] {1}: {2}", this, message, e);
			Console.ForegroundColor = cc;
		}

		public void Dispose()
		{
			if (_IsDisposed || _IsDisposing)
			{
				return;
			}

			_IsDisposing = true;

			try
			{
				Send(PortalPackets.DisconnectNotify.Instance);
			}
			catch
			{ }

			_IsDisposed = true;

			try
			{
				OnDispose();
			}
			catch
			{ }

			_IsDisposing = false;
		}

		protected virtual void OnDispose()
		{
			if (Socket == null)
			{
				return;
			}

			try
			{
				Socket.Shutdown(SocketShutdown.Both);
				Socket.Disconnect(true);
			}
			catch
			{ }

			try
			{
				Socket.Close();
				Socket.Dispose();
			}
			catch
			{ }
		}
	}
}