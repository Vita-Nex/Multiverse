#region Header
//   Vorspire    _,-'/-'/  PortalClient.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
#endregion

namespace Multiverse
{
	public sealed class PortalClient : PortalTransport
	{
		private readonly object _SendLock = new object();

		private readonly IPEndPoint _EndPoint;

		private long _NextAliveCheck;

		private volatile int _ServerID;

		private readonly bool _IsLocalClient;
		private readonly bool _IsRemoteClient;

		private volatile bool _IsSeeded;
		private volatile bool _IsAuthed;

		private volatile Socket _Client;

		public override Socket Socket { get { return _Client; } }

		public Dictionary<int, PortalPacketHandler> Handlers { get; private set; }

		public int ServerID { get { return _ServerID; } }

		public bool IsLocalClient { get { return _IsLocalClient; } }
		public bool IsRemoteClient { get { return _IsRemoteClient; } }

		public bool IsSeeded { get { return _IsSeeded; } set { _IsSeeded = value; } }
		public bool IsAuthed { get { return _IsAuthed; } set { _IsAuthed = value; } }

		public PortalClient()
			: this(new Socket(Portal.Server.AddressFamily, SocketType.Stream, ProtocolType.Tcp), Portal.ClientID, false)
		{ }

		public PortalClient(Socket client)
			: this(client, -1, true)
		{ }

		private PortalClient(Socket client, int serverID, bool remote)
		{
			_Client = client;

			_Client.ReceiveBufferSize = PortalPacket.MaxSize;
			_Client.SendBufferSize = PortalPacket.MaxSize;

			_Client.NoDelay = true;

			_ServerID = serverID;

			_IsRemoteClient = remote;
			_IsLocalClient = !remote;

			var ep = _IsRemoteClient
				? _Client.RemoteEndPoint ?? _Client.LocalEndPoint
				: _Client.LocalEndPoint ?? _Client.RemoteEndPoint;

			_EndPoint = (IPEndPoint)ep;

			Handlers = new Dictionary<int, PortalPacketHandler>();

			PortalPacketHandlers.RegisterHandlers(this);
		}

		public PortalPacketHandler Register(int id, int length, PortalContext context, PortalReceive onReceive)
		{
			lock (((ICollection)Handlers).SyncRoot)
			{
				return Handlers[id] = new PortalPacketHandler(id, length, context, onReceive);
			}
		}

		protected override void OnStart()
		{
			if (_IsLocalClient)
			{
				try
				{
					ToConsole("Connect: Target: {0}...", Portal.Server);

					_Client.Connect(Portal.Server);
				}
				catch (Exception e)
				{
					ToConsole("Connect: Failed: {0}", e.Message);

					Dispose();
					return;
				}

				try
				{
					if (!_Client.Connected)
					{
						ToConsole("Connect: Failed!");

						Dispose();
						return;
					}

					ToConsole("Connect: Success!");

					var sent = Send(PortalPackets.HandshakeRequest.Create, true);

					if (!sent)
					{
						Dispose();
						return;
					}

					if (!_IsSeeded)
					{
						_IsSeeded = true;
					}

					if (_IsAuthed)
					{
						ToConsole("Connect: Authorized Access");

						Portal.InvokeConnected(this);
					}
					else
					{
						ToConsole("Connect: Unauthorized Access");

						Dispose();
						return;
					}
				}
				catch (Exception e)
				{
					ToConsole("Connect: Failed: {0}", e.Message);

					Dispose();
					return;
				}
			}

			try
			{
				do
				{
					if (_Client != null && _Client.Available > 0)
					{
						Receive();
					}

					Thread.Sleep(10);

					ProcessReceiveQueue();

					Thread.Sleep(10);
				}
				while (CheckAlive());
			}
			catch (Exception e)
			{
				ToConsole("Exception: {0}", e.Message);

				Dispose();
			}
		}

		private readonly AutoResetEvent _ReceiveSync = new AutoResetEvent(true);

		private void Receive()
		{
			_ReceiveSync.WaitOne();

			var buffer = Peek.Acquire();

			var size = buffer.Length;

			while (size > 0 && _Client != null /* && _Client.Poll(-1, SelectMode.SelectRead)*/)
			{
				size -= _Client.Receive(buffer, buffer.Length - size, size, SocketFlags.Peek);

				if (!_Client.Connected)
				{
					break;
				}

				Thread.Sleep(10);
			}

			if (size > 0)
			{
				ToConsole("Recv: Peek Failed at {0}/{1} bytes", buffer.Length - size, buffer.Length);

				Peek.Free(buffer);

				Dispose();
				return;
			}

			var pid = (int)buffer[0];
			var sid = (int)BitConverter.ToInt16(buffer, 1);

			if (_IsRemoteClient && _ServerID == -1)
			{
				_ServerID = sid;

				ToConsole("Recv: Server ID Assigned ({0})", _ServerID);
			}

			size = BitConverter.ToInt16(buffer, 3);

			if (size < PortalPacket.MinSize || size > PortalPacket.MaxSize)
			{
				ToConsole(
					"Recv: Size Out Of Range for {0} at {1}/{2}-{3} bytes",
					pid,
					size,
					PortalPacket.MinSize,
					PortalPacket.MaxSize);

				Peek.Free(buffer);

				Dispose();
				return;
			}

			Peek.Exchange(ref buffer, new byte[size]);

			if (size > 0)
			{
				while (size > 0 && _Client != null /* && _Client.Poll(-1, SelectMode.SelectRead)*/)
				{
					size -= _Client.Receive(buffer, buffer.Length - size, size, SocketFlags.None);

					if (!_Client.Connected)
					{
						break;
					}

					Thread.Sleep(10);
				}

				if (size > 0)
				{
					ToConsole("Recv: Failed for {0} at {1}/{2} bytes", pid, buffer.Length - size, buffer.Length);

					Dispose();
					return;
				}
			}

			_ReceiveSync.Set();

			QueueReceive(buffer);
		}

		private readonly Queue _ReceiveQueue = Queue.Synchronized(new Queue());

		//private bool _ProcessingReceiveQueue;

		private void QueueReceive(byte[] buffer)
		{
			if (buffer != null && buffer.Length >= PortalPacket.MinSize)
			{
				_ReceiveQueue.Enqueue(buffer);
			}
		}

		private void ProcessReceiveQueue()
		{
			while (_ReceiveQueue.Count > 0)
			{
				ProcessReceiveBuffer((byte[])_ReceiveQueue.Dequeue());
			}
		}

		private void ProcessReceiveBuffer(byte[] buffer)
		{
			if (buffer == null || buffer.Length < PortalPacket.MinSize)
			{
				return;
			}

			var pid = (int)buffer[0];

			PortalPacketHandler handler;

			lock (((ICollection)Handlers).SyncRoot)
			{
				if (!Handlers.TryGetValue(pid, out handler) || handler == null)
				{
					ToConsole("Recv: Missing Handler for {0}", pid);
					return;
				}
			}

			if (handler.Length > 0 && buffer.Length != handler.Length)
			{
				ToConsole("Recv: Size Not Equal for {0} at {1}/{2} bytes", pid, buffer.Length, handler.Length);
				return;
			}

			if (handler.Context == PortalContext.Disabled)
			{
				ToConsole("Recv: Ignoring Packet {0}", pid);
				return;
			}

			if (handler.Context != PortalContext.Any && handler.Context != Portal.Context)
			{
				ToConsole("Recv: Out Of Context Packet {0} requires {1}", pid, handler.Context);
				return;
			}

			ToConsole("Recv: Received Packet {0} at {1} bytes", pid, buffer.Length);

			using (var p = new PortalPacketReader(buffer))
			{
				handler.OnReceive(this, p);
			}
		}

		public override bool Send(PortalPacket p, bool getResponse)
		{
			return CheckAlive() && InternalSend(p, getResponse);
		}

		public override bool SendTarget(PortalPacket p, int targetID, bool getResponse)
		{
			return CheckAlive() && _ServerID == targetID && InternalSend(p, getResponse);
		}

		public override bool SendExcept(PortalPacket p, int exceptID, bool getResponse)
		{
			return CheckAlive() && _ServerID != exceptID && InternalSend(p, getResponse);
		}

		private bool InternalSend(PortalPacket p, bool getResponse)
		{
			var buffer = p.Compile();

			if (buffer == null)
			{
				ToConsole("Send: Buffer Null for {0}", p.ID);

				return false;
			}

			if (buffer.Length < PortalPacket.MinSize || buffer.Length > PortalPacket.MaxSize)
			{
				ToConsole(
					"Send: Size Out Of Range for {0} at {1}/{2}-{3} bytes",
					p.ID,
					buffer.Length,
					PortalPacket.MinSize,
					PortalPacket.MaxSize);

				return false;
			}

			lock (_SendLock)
			{
				var size = buffer.Length;

				while (size > 0 && _Client != null)
				{
					size -= _Client.Send(buffer, buffer.Length - size, size, SocketFlags.None);

					if (!_Client.Connected)
					{
						break;
					}

					Thread.Sleep(10);
				}

				if (size > 0)
				{
					ToConsole("Send: Failed for {0} at {1}/{2} bytes", p.ID, buffer.Length - size, buffer.Length);

					Dispose();
					return false;
				}

				ToConsole("Send: Sent Packet {0} at {1} bytes", p.ID, buffer.Length);
			}

			if (getResponse)
			{
				Receive();
			}

			ProcessReceiveQueue();

			return true;
		}

		protected override bool CheckAlive(long ticks)
		{
			if (!base.CheckAlive(ticks))
			{
				return false;
			}

			if (_Client == null)
			{
				Dispose();
				return false;
			}

			if (!_IsSeeded)
			{
				return true;
			}

			if (!_IsAuthed)
			{
				Dispose();
				return false;
			}

			if (ticks >= _NextAliveCheck)
			{
				_NextAliveCheck = ticks + 60000;

				InternalSend(PortalPackets.PingRequest.Instance, true);
			}

			return IsAlive;
		}

		protected override void OnDispose()
		{
			_ReceiveSync.Set();

			base.OnDispose();

			if (Handlers != null)
			{
				lock (((ICollection)Handlers).SyncRoot)
				{
					Handlers.Clear();
					Handlers = null;
				}
			}

			_Client = null;

			try
			{
				_ReceiveSync.Close();
				_ReceiveSync.Dispose();
			}
			catch
			{ }
		}

		public override string ToString()
		{
			if (_EndPoint != null)
			{
				return String.Format("C{0}/{1}", _ServerID, _EndPoint.Address);
			}

			return String.Format("C{0}", _ServerID);
		}

		private static class Peek
		{
			private static readonly Queue<byte[]> _Pool = new Queue<byte[]>();
			private static readonly object _SyncRoot = ((ICollection)_Pool).SyncRoot;

			public static byte[] Acquire()
			{
				lock (_SyncRoot)
				{
					if (_Pool.Count > 0)
					{
						return _Pool.Dequeue();
					}
				}

				return new byte[PortalPacket.MinSize];
			}

			public static void Free(byte[] peek)
			{
				if (peek == null || peek.Length != PortalPacket.MinSize)
				{
					return;
				}

				for (var i = 0; i < peek.Length; i++)
				{
					peek[i] = 0;
				}

				lock (_SyncRoot)
				{
					_Pool.Enqueue(peek);
				}
			}

			public static void Exchange(ref byte[] peek, byte[] buffer)
			{
				if (buffer != null)
				{
					Buffer.BlockCopy(peek, 0, buffer, 0, Math.Min(peek.Length, buffer.Length));
				}

				buffer = Interlocked.Exchange(ref peek, buffer);

				Free(buffer);
			}
		}
	}
}