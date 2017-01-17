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
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
#endregion

namespace Multiverse
{
	public class PortalClient : PortalTransport
	{
		private readonly Queue _ReceiveQueue = Queue.Synchronized(new Queue());

		private readonly AutoResetEvent _ReceiveSync = new AutoResetEvent(true);
		private readonly AutoResetEvent _SendSync = new AutoResetEvent(true);

		private readonly IPEndPoint _EndPoint;

		private long _NextPing, _AuthExpire;

		private ushort? _ServerID;

		private readonly bool _IsLocalClient;
		private readonly bool _IsRemoteClient;

		private volatile bool _IsSeeded;
		private volatile bool _IsAuthed;

		private volatile bool _IsSending;
		private volatile bool _IsReceiving;

		private volatile bool _DisplaySendOutput;
		private volatile bool _DisplayRecvOutput;

		private volatile Socket _Client;

		public sealed override Socket Socket { get { return _Client; } }

		public Dictionary<ushort, PortalPacketHandler> Handlers { get; private set; }

		public ushort ServerID { get { return _ServerID ?? UInt16.MaxValue; } }

		public bool IsIdentified { get { return _ServerID.HasValue; } }

		public bool IsSending { get { return _IsSending; } }
		public bool IsReceiving { get { return _IsReceiving; } }
		public bool IsBusy { get { return _IsSending || _IsReceiving; } }

		public bool IsLocalClient { get { return _IsLocalClient; } }
		public bool IsRemoteClient { get { return _IsRemoteClient; } }

		public bool IsSeeded { get { return _IsSeeded; } set { _IsSeeded = value; } }
		public bool IsAuthed { get { return _IsAuthed; } set { _IsAuthed = value; } }

		public bool DisplaySendOutput { get { return _DisplaySendOutput; } set { _DisplaySendOutput = value; } }
		public bool DisplayRecvOutput { get { return _DisplayRecvOutput; } set { _DisplayRecvOutput = value; } }

		public override bool IsAlive
		{
			get { return base.IsAlive && (_IsLocalClient || GetState() == TcpState.Established); }
		}

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

			_Client.ReceiveTimeout = 1000;
			_Client.SendTimeout = 1000;

			_Client.Blocking = true;
			_Client.NoDelay = true;

			_NextPing = Portal.Ticks + 60000;
			_AuthExpire = _PingExpire = Int64.MaxValue;

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

		public PortalPacketHandler GetHandler(ushort id)
		{
			PortalPacketHandler h;

			lock (((ICollection)Handlers).SyncRoot)
			{
				Handlers.TryGetValue(id, out h);
			}

			return h;
		}

		public TcpState GetState()
		{
			if (_Client == null)
			{
				return TcpState.Unknown;
			}

			TcpConnectionInformation[] a;
			TcpConnectionInformation b;

			if (_IsRemoteClient)
			{
				a = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
				b = a.SingleOrDefault(o => o.RemoteEndPoint.Equals(_Client.RemoteEndPoint));
			}
			else if (_IsLocalClient)
			{
				a = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
				b = a.SingleOrDefault(o => o.LocalEndPoint.Equals(_Client.LocalEndPoint));
			}
			else
			{
				return TcpState.Unknown;
			}

			return b != null ? b.State : TcpState.Unknown;
		}

		private bool Connect(int retry)
		{
			var success = false;

			try
			{
				var ar = _Client.BeginConnect(
					Portal.Server,
					r =>
					{
						try
						{
							((Socket)r.AsyncState).EndConnect(r);
						}
						catch
						{
							success = false;
						}
					},
					_Client);

				try
				{
					success = ar.AsyncWaitHandle.WaitOne(3000, true);
				}
				catch
				{
					success = false;
				}
			}
			catch
			{
				success = false;
			}

			if (!success && --retry >= 0)
			{
				return Connect(retry);
			}

			return success;
		}

		protected override void OnStart()
		{
			if (_IsLocalClient)
			{
				bool success;

				try
				{
					ToConsole("Connect: Target: {0}...", Portal.Server);

					success = Connect(10);
				}
				catch (Exception e)
				{
					ToConsole("Connect: Failed", e);

					Dispose();
					return;
				}

				try
				{
					if (!success || !_Client.Connected)
					{
						ToConsole("Connect: Failed");

						Dispose();
						return;
					}

					ToConsole("Connect: Success");

					_AuthExpire = Portal.Ticks + 30000;

					if (!Send(PortalPackets.HandshakeRequest.Create))
					{
						Dispose();
						return;
					}

					if (_IsAuthed)
					{
						ToConsole("Connect: Authorized Access");
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
				if (!_IsAuthed)
				{
					_AuthExpire = Portal.Ticks + 30000;
				}

				if (_IsLocalClient)
				{
					Portal.InvokeConnected(this);
				}

				do
				{
					if (_Client != null && _Client.Available >= Peek.Size && Receive())
					{
						ProcessReceiveQueue();
					}

					Thread.Sleep(10);
				}
				while (CheckAlive());
			}
			catch (Exception e)
			{
				ToConsole("Exception Thrown", e);
			}

			Dispose();
		}

		private bool Receive()
		{
			try
			{
				if (IsDisposed || _Client == null)
				{
					return false;
				}

				if (!_ReceiveSync.WaitOne())
				{
					return false;
				}

				if (IsDisposed || _Client == null)
				{
					return false;
				}

				var buffer = Peek.Acquire();
				int length = buffer.Length, oldLength;
				var timeout = Portal.Ticks + Math.Max(1000, _Client.ReceiveTimeout);
				var timedout = false;

				_IsReceiving = true;

				while (length > 0 && _Client != null)
				{
					oldLength = length;

					try
					{
						length -= _Client.Receive(buffer, buffer.Length - length, length, SocketFlags.None);
					}
					catch (SocketException)
					{
						Thread.Sleep(10);
					}

					if (_Client == null || !_Client.Connected || length <= 0)
					{
						break;
					}

					if (oldLength != length)
					{
						timeout = Portal.Ticks + Math.Max(1000, _Client.ReceiveTimeout);
					}
					else if (Portal.Ticks >= timeout)
					{
						timedout = true;
						break;
					}

					Thread.Sleep(10);
				}

				_IsReceiving = false;

				if (length > 0)
				{
					if (_DisplayRecvOutput)
					{
						if (timeout > 0 && timedout)
						{
							ToConsole("Recv: Peek Failed (Timeout) at {0}/{1} bytes", buffer.Length - length, buffer.Length);
						}
						else
						{
							ToConsole("Recv: Peek Failed at {0}/{1} bytes", buffer.Length - length, buffer.Length);
						}
					}

					Peek.Free(buffer);

					Dispose();
					return false;
				}

				var pid = BitConverter.ToUInt16(buffer, 0);

				if (GetHandler(pid) == null)
				{
					if (_DisplayRecvOutput)
					{
						ToConsole("Recv: Unknown Packet ({0})", pid);
					}

					Peek.Free(buffer);

					Dispose();
					return false;
				}

				var sid = BitConverter.ToUInt16(buffer, 2);

				if (IsRemoteClient)
				{
					if (!_IsSeeded || !_ServerID.HasValue)
					{
						_ServerID = sid;
						_IsSeeded = true;
					}
					else if (_ServerID != sid)
					{
						if (_DisplayRecvOutput)
						{
							ToConsole("Recv: Server ID Spoofed ({0})", sid);
						}

						Peek.Free(buffer);

						Dispose();
						return false;
					}
				}
				else if (IsLocalClient)
				{
					if (!_IsSeeded)
					{
						_IsSeeded = true;
					}

					if (!_ServerID.HasValue)
					{
						_ServerID = sid;
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

					Dispose();
					return false;
				}

				length = size - buffer.Length;

				if (length > 0 && buffer.Length != size)
				{
					Peek.Exchange(ref buffer, new byte[size]);
				}

				timeout = Portal.Ticks + Math.Max(1000, _Client.ReceiveTimeout);
				timedout = false;

				_IsReceiving = true;

				while (length > 0 && _Client != null)
				{
					oldLength = length;

					try
					{
						length -= _Client.Receive(buffer, size - length, length, SocketFlags.None);
					}
					catch (SocketException)
					{
						Thread.Sleep(10);
					}

					if (_Client == null || !_Client.Connected || length <= 0)
					{
						break;
					}

					if (oldLength != length)
					{
						timeout = Portal.Ticks + Math.Max(1000, _Client.ReceiveTimeout);
					}
					else if (Portal.Ticks >= timeout)
					{
						timedout = true;
						break;
					}

					Thread.Sleep(10);
				}

				_IsReceiving = false;

				if (length > 0)
				{
					if (_DisplayRecvOutput)
					{
						if (timeout > 0 && timedout)
						{
							ToConsole("Recv: Failed (Timeout) for {0} at {1}/{2} bytes", pid, size - length, size);
						}
						else
						{
							ToConsole("Recv: Failed for {0} at {1}/{2} bytes", pid, size - length, size);
						}
					}

					Dispose();
					return false;
				}

				QueueReceive(buffer);
			}
			catch (Exception e)
			{
				ToConsole("Recv: Exception Thrown", e);

				Dispose();
				return false;
			}

			_ReceiveSync.Set();

			return true;
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
				ToConsole("Recv: Packet {0} at {1} bytes", pid, buffer.Length);
			}

			using (var p = new PortalPacketReader(buffer))
			{
				handler.OnReceive(this, p);
			}
		}

		public override bool Send(PortalPacket p)
		{
			return InternalSend(p);
		}

		public override bool SendTarget(PortalPacket p, ushort targetID)
		{
			return _ServerID == targetID && InternalSend(p);
		}

		public override bool SendExcept(PortalPacket p, ushort exceptID)
		{
			return _ServerID != exceptID && InternalSend(p);
		}

		private bool InternalSend(PortalPacket p)
		{
			try
			{
				if (IsDisposed || _Client == null)
				{
					return false;
				}

				if (!_SendSync.WaitOne())
				{
					return false;
				}

				if (IsDisposed || _Client == null)
				{
					return false;
				}

				var buffer = p.Compile();

				if (buffer == null)
				{
					if (_DisplaySendOutput)
					{
						ToConsole("Send: Buffer Null for {0}", p.ID);
					}

					Dispose();
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

					Dispose();
					return false;
				}

				int length = size, oldLength;
				var timeout = Portal.Ticks + Math.Max(1000, _Client.SendTimeout);

				_IsSending = true;

				while (length > 0 && _Client != null)
				{
					oldLength = length;

					try
					{
						length -= _Client.Send(buffer, size - length, length, SocketFlags.None);
					}
					catch (SocketException)
					{
						Thread.Sleep(10);
					}

					if (_Client == null || !_Client.Connected || length <= 0)
					{
						break;
					}

					if (oldLength != length)
					{
						timeout = Portal.Ticks + Math.Max(1000, _Client.SendTimeout);
					}
					else if (Portal.Ticks >= timeout)
					{
						break;
					}

					Thread.Sleep(10);
				}

				_IsSending = false;

				if (length > 0)
				{
					if (_DisplaySendOutput)
					{
						ToConsole("Send: Failed for {0} at {1}/{2} bytes", p.ID, size - length, size);
					}

					Dispose();
					return false;
				}

				if (_DisplaySendOutput)
				{
					ToConsole("Send: Packet {0} at {1} bytes", p.ID, size);
				}
			}
			catch (Exception e)
			{
				ToConsole("Send: Exception Thrown", e);

				Dispose();
				return false;
			}

			if (p.GetResponse)
			{
				if (Receive())
				{
					_SendSync.Set();

					ProcessReceiveQueue();
				}
				else
				{
					if (_DisplaySendOutput)
					{
						ToConsole("Send: {0} requires a response which could not be handled.", p.ID);
					}

					Dispose();
					return false;
				}
			}
			else
			{
				_SendSync.Set();
			}

			return true;
		}

		private long _PingExpire;

		public bool Ping(bool respond)
		{
			if (respond)
			{
				return InternalSend(PortalPackets.PingResponse.Instance);
			}

			_PingExpire = Portal.Ticks + 10000;
			_NextPing = Portal.Ticks + 60000;

			return InternalSend(PortalPackets.PingRequest.Instance);
		}

		public void Pong()
		{
			_PingExpire = Int64.MaxValue;
			_NextPing = Portal.Ticks + 60000;
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
			
			if (!_IsAuthed && ticks >= _AuthExpire)
			{
				Dispose();
				return false;
			}

			if (ticks >= _PingExpire)
			{
				Dispose();
				return false;
			}

			return IsAlive;
		}

		protected override void OnBeforeDispose()
		{
			try
			{
				_ReceiveSync.Set();
			}
			catch
			{ }

			try
			{
				_SendSync.Set();
			}
			catch
			{ }

			base.OnBeforeDispose();
		}

		protected override void OnDispose()
		{
			base.OnDispose();

			if (_IsLocalClient)
			{
				Portal.InvokeDisposed(this);
			}

			if (Handlers != null)
			{
				lock (((ICollection)Handlers).SyncRoot)
				{
					Handlers.Clear();
				}

				Handlers = null;
			}

			try
			{
				_ReceiveSync.Close();
				_ReceiveSync.Dispose();
			}
			catch
			{ }

			try
			{
				_SendSync.Close();
				_SendSync.Dispose();
			}
			catch
			{ }

			_IsSending = _IsReceiving = false;

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
			public static int Size { get { return PortalPacket.MinSize; } }

			private static readonly Queue _Pool = Queue.Synchronized(new Queue());

			public static byte[] Acquire()
			{
				if (_Pool.Count > 0)
				{
					return (byte[])_Pool.Dequeue();
				}

				return new byte[Size];
			}

			public static void Free(byte[] peek)
			{
				if (peek == null)
				{
					return;
				}

				for (var i = 0; i < peek.Length; i++)
				{
					peek[i] = 0;
				}

				if (peek.Length == Size)
				{
					_Pool.Enqueue(peek);
				}
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