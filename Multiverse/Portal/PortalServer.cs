#region Header
//   Vorspire    _,-'/-'/  PortalServer.cs
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

		private volatile Queue<PortalClient> _Accepted = new Queue<PortalClient>();
		private volatile Queue<PortalClient> _Disposed = new Queue<PortalClient>();

		private volatile List<PortalClient> _Clients = new List<PortalClient>();
		
		public IEnumerable<PortalClient> Clients
		{
			get
			{
				lock (_Clients)
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
		}

		private volatile Socket _Server;

		public override Socket Socket { get { return _Server; } }

		public int Count
		{
			get
			{
				lock (_Clients)
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
				lock (_Clients)
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
			_Server = new Socket(Portal.Server.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
		}
		
		public bool IsConnected(ushort serverID)
		{
			if (Portal.IsClient)
			{
				return Portal.ClientID == serverID && IsAlive;
			}

			return this[serverID] != null;
		}

		public void Slice()
		{
			ProcessAccepted();
			CheckActivity();
			ProcessDisposed();
		}

		private void ProcessAccepted()
		{
			lock (_Accepted)
			{
				PortalClient c;

				var esc = 100;

				while (--esc >= 0 && _Accepted.Count > 0)
				{
					c = _Accepted.Dequeue();

					if (c == null)
					{
						continue;
					}

					var thread = new Thread(c.Start)
					{
						Name = c.ToString()
					};

					thread.Start();

					while (!thread.IsAlive)
					{
						Thread.Sleep(10);
					}

					lock (_Clients)
					{
						_Clients.Add(c);

						ToConsole("{0} Connected [{1} Active]", c, _Clients.Count);
					}

					Portal.InvokeConnected(c);
				}
			}
		}

		private void ProcessDisposed()
		{
			lock (_Disposed)
			{
				PortalClient c;

				var esc = 100;

				while (--esc >= 0 && _Disposed.Count > 0)
				{
					c = _Disposed.Dequeue();

					lock (_Clients)
					{
						if (_Clients.Remove(c))
						{
							ToConsole("{0} Disconnected [{1} Active]", c, _Clients.Count);
						}
					}

					if (c == null)
					{
						continue;
					}

					c.Dispose();

					Portal.InvokeDisposed(c);
				}
			}
		}

		private void CheckActivity()
		{
			lock (_Clients)
			{
				PortalClient c;

				var i = _Clients.Count;

				while (--i >= 0)
				{
					try
					{
						c = _Clients[i];

						if (c == null || !c.IsAlive)
						{
							lock (_Disposed)
							{
								_Disposed.Enqueue(c);
							}
						}
					}
					catch (Exception e)
					{
						ToConsole("Activity Check: Failed", e);
					}
				}
			}
		}

		protected override void OnStart()
		{
			try
			{
				ToConsole("Listener: Binding: {0}", Portal.Server);

				_Server.LingerState.Enabled = false;
#if !MONO
				_Server.ExclusiveAddressUse = false;
#endif
				_Server.Bind(Portal.Server);
				_Server.Listen(8);

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
				while (CheckAlive())
				{
					Thread.Sleep(10);

					Slice();
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
			_Server.BeginAccept(OnAccept, null);
		}

		private void OnAccept(IAsyncResult r)
		{
			PortalClient client = null;

			try
			{
				var socket = _Server.EndAccept(r);

				if (socket != null && socket.Connected)
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

				lock (_Accepted)
				{
					_Accepted.Enqueue(client);
				}
			}
			catch
			{
				lock (_Disposed)
				{
					_Disposed.Enqueue(client);
				}
			}

			try
			{
				BeginAccept();
			}
			catch (Exception e)
			{
				ToConsole("Listener: Failed", e);

				Dispose();
			}
		}

		public override bool Send(PortalPacket p)
		{
			if (!CheckAlive())
			{
				return false;
			}

			var any = false;

			try
			{
				foreach (var c in Clients)
				{
					try
					{
						if (c.Send(p))
						{
							any = true;
						}
					}
					catch
					{ }
				}
			}
			catch
			{ }

			return any;
		}

		public override bool SendExcept(PortalPacket p, ushort exceptID)
		{
			if (!CheckAlive())
			{
				return false;
			}

			var any = false;

			try
			{
				foreach (var c in Clients)
				{
					try
					{
						if (c.SendExcept(p, exceptID))
						{
							any = true;
						}
					}
					catch
					{ }
				}
			}
			catch
			{ }

			return any;
		}

		public override bool SendTarget(PortalPacket p, ushort targetID)
		{
			if (!CheckAlive())
			{
				return false;
			}

			var any = false;

			try
			{
				foreach (var c in Clients)
				{
					try
					{
						if (c.SendTarget(p, targetID))
						{
							any = true;
						}
					}
					catch
					{ }
				}
			}
			catch
			{ }

			return any;
		}

		protected override bool CheckAlive(long ticks)
		{
			if (!base.CheckAlive(ticks))
			{
				return false;
			}

			try
			{
				if (_Server == null || !_Server.IsBound || _Clients == null)
				{
					Dispose();
					return false;
				}
			}
			catch
			{
				Dispose();
				return false;
			}

			return true;
		}

		protected override void OnDispose()
		{
			base.OnDispose();

			lock (_Clients)
			{
				if (_Clients != null)
				{
					PortalClient c;

					var i = _Clients.Count;

					while (--i >= 0)
					{
						try
						{
							if (i >= _Clients.Count)
							{
								continue;
							}

							c = _Clients[i];

							if (c != null)
							{
								c.Dispose();
							}
						}
						catch
						{ }
					}

					_Clients.Clear();
					_Clients = null;
				}
			}

			_Server = null;
		}

		public override string ToString()
		{
			return String.Format("S{0}/{1}", Portal.ServerID, Portal.Server.Address);
		}
	}
}