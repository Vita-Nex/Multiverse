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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
#endregion

namespace Multiverse
{
	public sealed class PortalServer : PortalTransport
	{
		private ConcurrentQueue<PortalClient> _Accepted, _Disposed;

		private object _Sync;

		private List<PortalClient> _Clients;

		public IEnumerable<PortalClient> Clients
		{
			get
			{
				if (_Clients == null)
				{
					yield break;
				}

				lock (_Sync)
				{
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
		}

		private Socket _Server;

		public override Socket Socket { get { return _Server; } }

		public int Count
		{
			get
			{
				lock (_Sync)
				{
					if (_Clients != null)
					{
						return _Clients.Count;
					}
				}

				return 0;
			}
		}

		public PortalClient this[int index]
		{
			get
			{
				lock (_Sync)
				{
					if (_Clients != null && index >= 0 && index < _Clients.Count)
					{
						return _Clients[index];
					}
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
			_Accepted = new ConcurrentQueue<PortalClient>();
			_Disposed = new ConcurrentQueue<PortalClient>();

			_Clients = new List<PortalClient>();

			_Sync = ((ICollection)_Clients).SyncRoot;

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

				lock (_Sync)
				{
					if (_Clients != null)
					{
						_Clients.Add(c);

						ToConsole("{0} Connected [{1} Active]", c, _Clients.Count);
					}
				}

				if (!c.Start())
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

				lock (_Sync)
				{
					if (_Clients != null)
					{
						var any = false;

						while (_Clients.Remove(c))
						{
							any = true;
						}

						if (any)
						{
							ToConsole("{0} Disconnected [{1} Active]", c, _Clients.Count);
						}
					}
				}
			}
		}

		private void CheckActivity()
		{
			ProcessAccepted();

			foreach (var c in Clients)
			{
				if (c.IsDisposed)
				{
					_Disposed.Enqueue(c);
				}
				else if (!c.IsDisposing && !c.IsConnected)
				{
					c.Dispose();
				}
			}

			ProcessDisposed();
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
			}
		}

		protected override void OnStarted()
		{
			Task.Factory.StartNew(Slice, TaskCreationOptions.LongRunning);
		}

		private void Slice()
		{
			do
			{
				if (IsRunning)
				{
					CheckActivity();
				}

				Thread.Sleep(10);
			}
			while (!IsDisposed);
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
					if (Portal.CreateClientHandler != null)
					{
						client = Portal.CreateClientHandler(socket);
					}

					if (client == null)
					{
						client = new PortalClient(socket);
					}
					
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
		
		protected override void OnBeforeDispose()
		{
			base.OnBeforeDispose();

			foreach (var c in Clients)
			{
				c.Dispose();
			}
		}

		protected override void OnDispose()
		{
			base.OnDispose();

			lock (_Sync)
			{
				if (_Clients != null)
				{
					_Clients.Clear();
					_Clients = null;
				}
			}

			_Accepted = null;
			_Disposed = null;

			_Server = null;

			_Sync = null;
		}

		public override string ToString()
		{
			return String.Format("S{0}/{1}", Portal.ServerID, Portal.Server.Address);
		}
	}
}