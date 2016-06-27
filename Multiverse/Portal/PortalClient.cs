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
	public class PortalClient : PortalTransport
	{
		private readonly Queue _ReceiveQueue = Queue.Synchronized(new Queue());

		private readonly AutoResetEvent _ReceiveSync = new AutoResetEvent(true);

		private readonly object _SendLock = new object();
		private readonly object _GetResponseLock = new object();

		private readonly IPEndPoint _EndPoint;

		private long _NextAliveCheck;

		private ushort? _ServerID;

		private readonly bool _IsLocalClient;
		private readonly bool _IsRemoteClient;

		private volatile bool _IsSeeded;
		private volatile bool _IsAuthed;

		private volatile bool _DisplaySendOutput;
		private volatile bool _DisplayRecvOutput;

		private volatile Socket _Client;

		public sealed override Socket Socket { get { return _Client; } }

		public Dictionary<ushort, PortalPacketHandler> Handlers { get; private set; }

		public ushort ServerID { get { return _ServerID ?? UInt16.MaxValue; } }

		public bool IsLocalClient { get { return _IsLocalClient; } }
		public bool IsRemoteClient { get { return _IsRemoteClient; } }

		public bool IsSeeded { get { return _IsSeeded; } set { _IsSeeded = value; } }
		public bool IsAuthed { get { return _IsAuthed; } set { _IsAuthed = value; } }

		public bool DisplaySendOutput { get { return _DisplaySendOutput; } set { _DisplaySendOutput = value; } }
		public bool DisplayRecvOutput { get { return _DisplayRecvOutput; } set { _DisplayRecvOutput = value; } }

		public PortalClient()
			: this(new Socket(Portal.Server.AddressFamily, SocketType.Stream, ProtocolType.Tcp), Portal.ClientID, false)
		{ }

		public PortalClient(Socket client)
			: this(client, null, true)
		{ }

		private PortalClient(Socket client, ushort? serverID, bool remote)
		{
			_Client = client;

			_Client.ReceiveBufferSize = PortalPacket.MaxSize;
			_Client.SendBufferSize = PortalPacket.MaxSize;

			_Client.NoDelay = true;

			_ServerID = serverID;

			_IsRemoteClient = remote;
			_IsLocalClient = !remote;

#if DEBUG
			_DisplaySendOutput = true;
			_DisplayRecvOutput = true;
#endif

			var ep = _IsRemoteClient
				? _Client.RemoteEndPoint ?? _Client.LocalEndPoint
				: _Client.LocalEndPoint ?? _Client.RemoteEndPoint;

			_EndPoint = (IPEndPoint)ep;

			Handlers = new Dictionary<ushort, PortalPacketHandler>();

			PortalPacketHandlers.RegisterHandlers(this);
		}

		public PortalPacketHandler Register(ushort id, PortalContext context, PortalReceive onReceive)
		{
			lock (((ICollection)Handlers).SyncRoot)
			{
				if (Handlers.ContainsKey(id))
				{
					ToConsole("Warning: Replacing Packet Handler for {0}", id);
				}

				return Handlers[id] = new PortalPacketHandler(id, context, onReceive);
			}
		}

		public PortalPacketHandler Unregister(ushort id)
		{
			PortalPacketHandler h;

			lock (((ICollection)Handlers).SyncRoot)
			{
				if (Handlers.TryGetValue(id, out h))
				{
					Handlers.Remove(id);
				}
			}

			return h;
		}

		public PortalPacketHandler GetHandler(byte id)
		{
			PortalPacketHandler h;

			lock (((ICollection)Handlers).SyncRoot)
			{
				Handlers.TryGetValue(id, out h);
			}

			return h;
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
					ToConsole("Connect: Failed", e);

					Dispose();
					return;
				}

				try
				{
					if (!_Client.Connected)
					{
						ToConsole("Connect: Failed");

						Dispose();
						return;
					}

					ToConsole("Connect: Success");

					var sent = Send(PortalPackets.HandshakeRequest.Create);

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
					ToConsole("Connect: Failed", e);

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
				ToConsole("Exception Thrown", e);

				Dispose();
			}
		}

		private void Receive()
		{
			if (IsDisposing || IsDisposed)
			{
				return;
			}

			_ReceiveSync.WaitOne();

			try
			{
				var buffer = Peek.Acquire();
				var length = buffer.Length;

				while (length > 0 && _Client != null)
				{
					length -= _Client.Receive(buffer, buffer.Length - length, length, SocketFlags.None);

					if (!_Client.Connected || length <= 0)
					{
						break;
					}

					Thread.Sleep(10);
				}

				if (length > 0)
				{
					if (_DisplayRecvOutput)
					{
						ToConsole("Recv: Peek Failed at {0}/{1} bytes", buffer.Length - length, buffer.Length);
					}

					Peek.Free(buffer);
					return;
				}

				var pid = BitConverter.ToUInt16(buffer, 0);
				var sid = BitConverter.ToUInt16(buffer, 2);

				if (!_ServerID.HasValue)
				{
					_ServerID = sid;

					if (_DisplayRecvOutput)
					{
						ToConsole("Recv: Server ID Assigned ({0})", _ServerID);
					}
				}

				var size = BitConverter.ToInt32(buffer, 4);

				if (size < PortalPacket.MinSize || size > PortalPacket.MaxSize)
				{
					if (_DisplayRecvOutput)
					{
						ToConsole(
							"Recv: Size Out Of Range for {0} at {1}/{2}-{3} bytes",
							pid,
							size,
							PortalPacket.MinSize,
							PortalPacket.MaxSize);
					}

					Peek.Free(buffer);
					return;
				}

				length = size - buffer.Length;

				if (length > 0 && buffer.Length != size)
				{
					Peek.Exchange(ref buffer, new byte[size]);
				}

				while (length > 0 && _Client != null)
				{
					length -= _Client.Receive(buffer, size - length, length, SocketFlags.None);

					if (!_Client.Connected || length <= 0)
					{
						break;
					}

					Thread.Sleep(10);
				}

				if (length > 0)
				{
					if (_DisplayRecvOutput)
					{
						ToConsole("Recv: Failed for {0} at {1}/{2} bytes", pid, size - length, size);
					}

					return;
				}

				QueueReceive(buffer);
			}
			catch (Exception e)
			{
				ToConsole("Recv: Exception Thrown", e);
				return;
			}

			_ReceiveSync.Set();
		}

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

			var pid = BitConverter.ToUInt16(buffer, 0);

			PortalPacketHandler handler;

			lock (((ICollection)Handlers).SyncRoot)
			{
				if (!Handlers.TryGetValue(pid, out handler) || handler == null)
				{
					if (_DisplayRecvOutput)
					{
						ToConsole("Recv: Missing Handler for {0}", pid);
					}

					return;
				}
			}

			if (handler.Context == PortalContext.Disabled)
			{
				if (_DisplayRecvOutput)
				{
					ToConsole("Recv: Ignoring Packet {0}", pid);
				}

				return;
			}

			if (handler.Context != PortalContext.Any &&
				((handler.Context == PortalContext.Server && !IsRemoteClient) ||
				 (handler.Context == PortalContext.Client && !IsLocalClient)))
			{
				if (_DisplayRecvOutput)
				{
					ToConsole("Recv: Out Of Context Packet {0} requires {1}", pid, handler.Context);
				}

				return;
			}

			if (_DisplayRecvOutput)
			{
				ToConsole("Recv: Received Packet {0} at {1} bytes", pid, buffer.Length);
			}

			using (var p = new PortalPacketReader(buffer))
			{
				handler.OnReceive(this, p);
			}
		}

		public override bool Send(PortalPacket p)
		{
			return CheckAlive() && InternalSend(p);
		}

		public override bool SendTarget(PortalPacket p, ushort targetID)
		{
			return CheckAlive() && _ServerID == targetID && InternalSend(p);
		}

		public override bool SendExcept(PortalPacket p, ushort exceptID)
		{
			return CheckAlive() && _ServerID != exceptID && InternalSend(p);
		}

		private bool InternalSend(PortalPacket p)
		{
			var buffer = p.Compile();

			if (buffer == null)
			{
				if (_DisplaySendOutput)
				{
					ToConsole("Send: Buffer Null for {0}", p.ID);
				}

				return false;
			}

			var size = buffer.Length;

			if (size < PortalPacket.MinSize || size > PortalPacket.MaxSize)
			{
				if (_DisplaySendOutput)
				{
					ToConsole(
						"Send: Size Out Of Range for {0} at {1}/{2}-{3} bytes",
						p.ID,
						size,
						PortalPacket.MinSize,
						PortalPacket.MaxSize);
				}

				return false;
			}

			var length = size;

			lock (_SendLock)
			{
				while (length > 0 && _Client != null)
				{
					length -= _Client.Send(buffer, size - length, length, SocketFlags.None);

					if (!_Client.Connected || length <= 0)
					{
						break;
					}

					Thread.Sleep(10);
				}
			}

			if (length > 0)
			{
				if (_DisplaySendOutput)
				{
					ToConsole("Send: Failed for {0} at {1}/{2} bytes", p.ID, size - length, size);
				}

				return false;
			}

			if (_DisplaySendOutput)
			{
				ToConsole("Send: Sent Packet {0} at {1} bytes", p.ID, size);
			}

			lock (_GetResponseLock)
			{
				if (p.GetResponse)
				{
					Receive();
					ProcessReceiveQueue();
				}
			}

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

			if (IsAlive && ticks >= _NextAliveCheck)
			{
				_NextAliveCheck = ticks + 60000;

				if (!InternalSend(PortalPackets.PingRequest.Instance))
				{
					Dispose();
					return false;
				}
			}

			return IsAlive;
		}

		protected override void OnDispose()
		{
			_ReceiveSync.Set();

			base.OnDispose();

			Portal.InvokeDisposed(this);

			if (Handlers != null)
			{
				lock (((ICollection)Handlers).SyncRoot)
				{
					Handlers.Clear();
				}
			}

			try
			{
				_ReceiveSync.Close();
				_ReceiveSync.Dispose();
			}
			catch
			{ }

			Handlers = null;

			_Client = null;
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
			private static readonly Queue _Pool = Queue.Synchronized(new Queue());

			public static byte[] Acquire()
			{
				if (_Pool.Count > 0)
				{
					return (byte[])_Pool.Dequeue();
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

				_Pool.Enqueue(peek);
			}

			public static void Exchange(ref byte[] peek, byte[] buffer)
			{
				if (buffer != null)
				{
					Buffer.BlockCopy(peek, 0, buffer, 0, peek.Length);
				}

				buffer = Interlocked.Exchange(ref peek, buffer);

				Free(buffer);
			}
		}
	}
}