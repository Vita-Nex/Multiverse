#region Header
//   Vorspire    _,-'/-'/  PortalServer.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
#endregion

namespace Multiverse
{
	public sealed class PortalServer : PortalTransport
	{
		public static Func<Socket, PortalClient> CreateClientHandler;

		private volatile AutoResetEvent _Sync;

		private volatile ConcurrentQueue<PortalClient> _Accepted, _Disposed;

		private volatile List<PortalClient> _Clients;

		public IEnumerable<PortalClient> Clients
		{
			get
			{
				if (_Clients == null)
				{
					yield break;
				}

				var i = _Clients.Count;

				while (--i >= 0)
				{
					if (i < _Clients.Count)
					{
						yield return _Clients[i];
					}
				}
			}
		}

		private volatile Socket _Server;

		public override Socket Socket { get { return _Server; } }

		public int Count
		{
			get
			{
				if (_Clients != null)
				{
					return _Clients.Count;
				}

				return 0;
			}
		}

		public PortalClient this[int index]
		{
			get
			{
				if (_Clients != null && index >= 0 && index < _Clients.Count)
				{
					return _Clients[index];
				}

				return null;
			}
		}

		public PortalClient this[ushort sid]
		{
			get { return Clients.FirstOrDefault(c => c != null && c.IsIdentified && c.ServerID == sid); }
		}

		public PortalServer()
		{
			_Sync = new AutoResetEvent(true);

			_Accepted = new ConcurrentQueue<PortalClient>();
			_Disposed = new ConcurrentQueue<PortalClient>();

			_Clients = new List<PortalClient>();

			_Server = new Socket(Portal.Server.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
			{
				LingerState =
				{
					Enabled = false
				},
				ExclusiveAddressUse = Portal.IsUnix
			};
		}

		public bool IsConnected(ushort serverID)
		{
			if (Portal.IsClient)
			{
				return Portal.ClientID == serverID && IsAlive;
			}

			return this[serverID] != null;
		}

		private void Slice(object state)
		{
			try
			{
				Slice();

				Thread.Sleep(10);

				if (IsAlive && ThreadPool.QueueUserWorkItem(Slice))
				{
					return;
				}
			}
			catch (Exception e)
			{
				ToConsole("Listener: Failed", e);
			}

			Dispose();
		}

		private void Slice()
		{
			if (_Sync != null)
			{
				_Sync.WaitOne();

				ProcessAccepted();
				CheckActivity();
				ProcessDisposed();

				_Sync.Set();
			}
		}

		private void ProcessAccepted()
		{
			PortalClient c;

			var esc = 100;

			while (--esc >= 0 && !_Accepted.IsEmpty)
			{
				if (!_Accepted.TryDequeue(out c) || c == null)
				{
					continue;
				}

				if (IsAlive && c.Start())
				{
					if (_Clients != null)
					{
						_Clients.Add(c);

						ToConsole("{0} Connected [{1} Active]", c, _Clients.Count);
					}

					Portal.InvokeConnected(c);
				}
				else
				{
					c.Dispose();
				}
			}
		}

		private void ProcessDisposed()
		{
			PortalClient c;

			var esc = 100;

			while (--esc >= 0 && !_Disposed.IsEmpty)
			{
				if (!_Disposed.TryDequeue(out c) || c == null)
				{
					continue;
				}

				if (IsAlive && _Clients != null && _Clients.Remove(c))
				{
					ToConsole("{0} Disconnected [{1} Active]", c, _Clients.Count);
				}

				Portal.InvokeDisposed(c);
			}
		}

		private void CheckActivity()
		{
			if (IsDisposed || IsDisposing)
			{
				return;
			}

			foreach (var c in Clients)
			{
				if (!_Disposed.Contains(c) && !c.IsAlive)
				{
					_Disposed.Enqueue(c);
				}
			}
		}

		protected override void OnStart()
		{
			try
			{
				ToConsole("Listener: Binding: {0}", Portal.Server);

				_Server.Bind(Portal.Server);
				_Server.Listen(100);

				ToConsole("Listener: Bound: {0}", Portal.Server);

				BeginAccept();
			}
			catch (Exception e)
			{
				ToConsole("Listener: Failed", e);

				Dispose();
				return;
			}

			try
			{
				Thread.Sleep(10);

				if (ThreadPool.QueueUserWorkItem(Slice))
				{
					return;
				}
			}
			catch (Exception e)
			{
				ToConsole("Listener: Failed", e);
			}

			Dispose();
		}

		private void BeginAccept()
		{
			if (_Server != null && !IsDisposed && !IsDisposing)
			{
				_Server.BeginAccept(OnAccept, null);
			}
		}

		private void OnAccept(IAsyncResult r)
		{
			PortalClient client = null;

			try
			{
				var socket = _Server.EndAccept(r);

				if (socket != null)
				{
					if (CreateClientHandler != null)
					{
						client = CreateClientHandler(socket);
					}

					if (client == null)
					{
						client = new PortalClient(socket);
					}
				}

				if (IsDisposed || IsDisposing)
				{
					if (client != null)
					{
						_Disposed.Enqueue(client);
					}

					return;
				}

				if (client != null)
				{
					_Accepted.Enqueue(client);
				}
			}
			catch
			{
				if (client != null)
				{
					_Disposed.Enqueue(client);
				}
			}

			try
			{
				BeginAccept();
				return;
			}
			catch (Exception e)
			{
				ToConsole("Listener: Failed", e);
			}

			Dispose();
		}

		public override bool Send(PortalPacket p)
		{
			if (!IsAlive)
			{
				return false;
			}

			var any = 0;

			foreach (var c in Clients)
			{
				if (c.Send(p))
				{
					++any;
				}
			}

			return any > 0;
		}

		public override bool SendExcept(PortalPacket p, ushort exceptID)
		{
			if (!IsAlive)
			{
				return false;
			}

			var any = 0;

			foreach (var c in Clients)
			{
				if (c.SendExcept(p, exceptID))
				{
					++any;
				}
			}

			return any > 0;
		}

		public override bool SendTarget(PortalPacket p, ushort targetID)
		{
			if (!IsAlive)
			{
				return false;
			}

			var any = 0;

			foreach (var c in Clients)
			{
				if (c.SendTarget(p, targetID))
				{
					++any;
				}
			}

			return any > 0;
		}

		protected override bool CheckAlive(long ticks)
		{
			if (!base.CheckAlive(ticks))
			{
				return false;
			}

			if (_Server == null || !_Server.IsBound || _Clients == null)
			{
				Dispose();
				return false;
			}

			return true;
		}

		protected override void OnBeforeDispose()
		{
			base.OnBeforeDispose();

			if (_Sync != null)
			{
				_Sync.WaitOne();
			}

			foreach (var c in Clients)
			{
				c.Dispose();
			}

			ProcessDisposed();
		}

		protected override void OnDispose()
		{
			base.OnDispose();

			try
			{
				_Sync.Close();
				_Sync.Dispose();
			}
			catch
			{ }
			finally
			{
				_Sync = null;
			}

			if (_Clients != null)
			{
				_Clients.Clear();
				_Clients = null;
			}

			_Server = null;
		}

		public override string ToString()
		{
			return String.Format("S{0}/{1}", Portal.ServerID, Portal.Server.Address);
		}
	}
}