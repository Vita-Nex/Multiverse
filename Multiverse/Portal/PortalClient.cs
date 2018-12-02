#region Header
//   Vorspire    _,-'/-'/  PortalClient.cs
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
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
#endregion

namespace Multiverse
{
	public class PortalClient : PortalTransport
	{
		private static readonly byte[] _OneByte = new byte[1];

		private long _Buffered;

		private ConcurrentQueue<PortalBuffer> _ReceiveQueue;

		private readonly IPEndPoint _EndPoint;

		private ushort? _ServerID;

		private long _NextPing, _PingExpire, _AuthExpire;

		private readonly bool _IsLocalClient, _IsRemoteClient;
		
		private volatile bool _IsSeeded, _IsAuthed;
		private volatile bool _DisplaySendOutput, _DisplayRecvOutput;

		private Socket _Client;

		public sealed override Socket Socket { get { return _Client; } }

		public Dictionary<ushort, PortalPacketHandler> Handlers { get; private set; }

		public ushort ServerID { get { return _ServerID ?? UInt16.MaxValue; } }

		public bool IsIdentified { get { return _ServerID.HasValue; } }

		public bool IsLocalClient { get { return _IsLocalClient; } }
		public bool IsRemoteClient { get { return _IsRemoteClient; } }

		public bool IsSeeded { get { return _IsSeeded; } set { _IsSeeded = value; } }
		public bool IsAuthed { get { return _IsAuthed; } set { _IsAuthed = value; } }

		public bool DisplaySendOutput { get { return _DisplaySendOutput; } set { _DisplaySendOutput = value; } }
		public bool DisplayRecvOutput { get { return _DisplayRecvOutput; } set { _DisplayRecvOutput = value; } }

		public int Pending { get { return _Client != null ? _Client.Available : -1; } }

		public long Buffered { get { return _Buffered; } }

		public bool IsConnected
		{
			get
			{
				if (_Client == null || IsDisposed)
				{
					return false;
				}
				/*
				if (_IsLocalClient)
				{
					if (GetState() == TcpState.Established)
					{
						return true;
					}
				}
				*/
				//if (_IsRemoteClient)
				{
					var b = _Client.Blocking;

					try
					{
						_Client.Blocking = false;

						try
						{
							_Client.Send(_OneByte, 0, 0, 0);
						}
						catch (SocketException)
						{ }
						catch
						{
							return false;
						}
					}
					finally
					{
						_Client.Blocking = b;
					}

					if (_Client.Connected)
					{
						return true;
					}
				}

				return false;
			}
		}

		public PortalClient()
			: this(new Socket(Portal.Server.AddressFamily, SocketType.Stream, ProtocolType.Tcp), Portal.ClientID, false)
		{ }

		public PortalClient(Socket client)
			: this(client, null, true)
		{ }

		private PortalClient(Socket client, ushort? serverID, bool remote)
		{
			_ReceiveQueue = new ConcurrentQueue<PortalBuffer>();

			_Client = client;

			//_Client.ReceiveBufferSize = PortalPacket.MaxSize;
			//_Client.SendBufferSize = PortalPacket.MaxSize;

			_Client.ReceiveTimeout = 10000;
			_Client.SendTimeout = 10000;

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
			if (Handlers.ContainsKey(id))
			{
				ToConsole("Warning: Replacing Packet Handler for {0}", id);
			}

			return Handlers[id] = new PortalPacketHandler(id, context, onReceive);
		}

		public PortalPacketHandler Unregister(ushort id)
		{
			PortalPacketHandler h;

			if (Handlers.TryGetValue(id, out h))
			{
				Handlers.Remove(id);
			}

			return h;
		}

		public PortalPacketHandler GetHandler(ushort id)
		{
			PortalPacketHandler h;

			Handlers.TryGetValue(id, out h);

			return h;
		}

		public TcpState GetState()
		{
			if (_Client == null)
			{
				return TcpState.Unknown;
			}

			try
			{
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
			catch
			{
				return TcpState.Unknown;
			}
		}

		private bool Connect()
		{
			if (_Client == null)
			{
				return false;
			}

			if (_Client.Connected)
			{
				return true;
			}

			ToConsole("Connecting: {0}...", Portal.Server);

			try
			{
				_Client.Connect(Portal.Server);

				return true;
			}
			catch
			{ }

			return false;
		}

		protected override void OnStart()
		{
			if (IsDisposed || IsDisposing || _Client == null)
			{
				return;
			}

			if (_IsLocalClient)
			{
				try
				{
					var attempts = 3;

					while (--attempts >= 0)
					{
						if (Connect())
						{
							break;
						}

						Thread.Sleep(10);
					}

					if (attempts < 0)
					{
						ToConsole("Connect: Failed");

						Dispose();
						return;
					}

					ToConsole("Connect: Success");

					_AuthExpire = Portal.Ticks + 30000;
				}
#if DEBUG
				catch (Exception e)
				{
					ToConsole("Connect: Failed", e);

					Dispose();
					return;
				}
#else
				catch
				{
					ToConsole("Connect: Failed");

					Dispose();
					return;
				}
#endif
			}

			if (!_IsAuthed)
			{
				_AuthExpire = Portal.Ticks + 30000;
			}
		}

		protected override void OnStarted()
		{
			if (_IsLocalClient)
			{
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

			Portal.InvokeConnected(this);

			try
			{
				if (!IsAlive || ThreadPool.QueueUserWorkItem(Receive))
				{
					Thread.Sleep(0);
					return;
				}
			}
			catch (Exception e)
			{
				ToConsole("Exception Thrown", e);
			}

			Dispose();
		}

		private volatile bool _NoReceive;

		private void Receive(object state)
		{
			try
			{
				if (!_NoReceive && Receive(false) && !_NoReceive)
				{
					ProcessReceiveQueue();
				}

				if (!IsAlive || ThreadPool.QueueUserWorkItem(Receive))
				{
					Thread.Sleep(0);
					return;
				}
			}
			catch (Exception e)
			{
				ToConsole("Exception Thrown", e);
			}

			Dispose();
		}
		
		private bool Receive(bool wait)
		{
			try
			{
				if ((!wait && _NoReceive) || !IsAlive)
				{
					return false;
				}
				
				if (wait)
				{
					var timer = new Stopwatch();
					var timeout = _Client.ReceiveTimeout;

					timer.Start();

					while (Pending < Peek.Size)
					{
						if (!IsAlive || (Portal.Ticks % 1000 == 0 && !IsConnected))
						{
							timer.Stop();
							return false;
						}

						if (Pending < 0 || Pending >= Peek.Size)
						{
							timer.Reset();
							break;
						}

						if (timeout > 0 && timer.ElapsedMilliseconds >= timeout)
						{
							timer.Stop();
							break;
						}

						Thread.Sleep(1);
					}
				}

				if ((!wait && _NoReceive) || !IsAlive || Pending < Peek.Size)
				{
					return false;
				}

				var peek = Peek.Acquire();
				var head = 0;

				while (head < peek.Length && _Client != null)
				{
					head += _Client.Receive(peek, head, peek.Length - head, SocketFlags.None);

					if (_Client == null || !_Client.Connected || head >= peek.Length)
					{
						break;
					}
				}

				if (head < peek.Length)
				{
					if (_DisplayRecvOutput)
					{
						ToConsole("Recv: Peek Failed at {0} / {1} bytes", head, peek.Length);
					}

					Peek.Free(ref peek);

					Dispose();
					return false;
				}

				var pid = BitConverter.ToUInt16(peek, 0);

				if (GetHandler(pid) == null)
				{
					if (_DisplayRecvOutput)
					{
						ToConsole("Recv: Unknown Packet ({0})", pid);
					}

					Peek.Free(ref peek);

					Dispose();
					return false;
				}

				var sid = BitConverter.ToUInt16(peek, 2);

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

						Peek.Free(ref peek);

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

				var size = BitConverter.ToInt32(peek, 4);

				if (size < PortalPacket.MinSize || size > PortalPacket.MaxSize)
				{
					if (_DisplayRecvOutput)
					{
						ToConsole(
							"Recv: Size Out Of Range for {0} at {1} / {2} - {3} bytes",
							pid,
							size,
							PortalPacket.MinSize,
							PortalPacket.MaxSize);
					}

					Peek.Free(ref peek);

					Dispose();
					return false;
				}

				var buffer = new PortalBuffer(size);

				for (var i = 0; i < peek.Length; i++)
				{
					buffer[i] = peek[i];
				}

				Peek.Free(ref peek);

				if (size > head)
				{
					var length = head + buffer.Receive(_Client, head, size - head);

					if (length < size)
					{
						if (_DisplayRecvOutput)
						{
							ToConsole("Recv: Failed for {0} at {1} / {2} bytes", pid, length, size);
						}

						buffer.Dispose();

						Dispose();
						return false;
					}
				}

				if (!QueueReceive(buffer))
				{
					buffer.Dispose();
					return false;
				}

				return true;
			}
			catch (Exception e)
			{
				ToConsole("Recv: Exception Thrown", e);

				Dispose();
				return false;
			}
		}

		private bool QueueReceive(PortalBuffer buffer)
		{
			if (buffer == null)
			{
				return false;
			}

			if (_ReceiveQueue == null || buffer.Size < PortalPacket.MinSize)
			{
				return false;
			}

			if (_Buffered + buffer.Size > PortalPacket.MaxSize * 8L)
			{
				if (_DisplayRecvOutput)
				{
					ToConsole("Recv: Too much data pending: {0} + {1} bytes", _Buffered, buffer.Size);
				}

				Dispose();
				return false;
			}

			_ReceiveQueue.Enqueue(buffer);
			_Buffered += buffer.Size;

			return true;
		}

		private void ProcessReceiveQueue()
		{
			PortalBuffer buffer;

			while (_ReceiveQueue != null && !_ReceiveQueue.IsEmpty)
			{
				if (_ReceiveQueue.TryDequeue(out buffer) && buffer != null)
				{
					_Buffered -= buffer.Size;

					using (buffer)
					{
						ProcessReceiveBuffer(buffer);
					}
				}

				Thread.Sleep(0);
			}
			
			if (_Buffered < 0 || _ReceiveQueue == null)
			{
				_Buffered = 0;
			}
		}

		private void ProcessReceiveBuffer(PortalBuffer buffer)
		{
			if (buffer == null || buffer.Size < PortalPacket.MinSize)
			{
				return;
			}

			var pid = BitConverter.ToUInt16(buffer.Join(0, 2), 0);

			PortalPacketHandler handler;

			if (!Handlers.TryGetValue(pid, out handler) || handler == null)
			{
				if (_DisplayRecvOutput)
				{
					ToConsole("Recv: Missing Handler for {0}", pid);
				}

				return;
			}

			if (handler.Context == PortalContext.Disabled)
			{
				if (_DisplayRecvOutput)
				{
					ToConsole("Recv: Ignoring Packet {0}", pid);
				}

				return;
			}

			if (handler.Context != PortalContext.Any && ((handler.Context == PortalContext.Server && !IsRemoteClient) ||
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
				ToConsole("Recv: Packet {0} at {1} bytes", pid, buffer.Size);
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
			if (p == null)
			{
				return false;
			}

			if (IsDisposing && p.ID != 255)
			{
				return false;
			}

			if (!IsAlive)
			{
				return false;
			}
			/*
			while (!_ReceiveSync.WaitOne(10))
			{
				if (!IsAlive || (Portal.Ticks % 1000 == 0 && !IsConnected))
				{
					return false;
				}

				Thread.Sleep(1);
			}
			*/
			if (IsDisposing && p.ID != 255)
			{
				return false;
			}

			if (!IsAlive)
			{
				return false;
			}

			try
			{
				_NoReceive = true;

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

				var size = buffer.Size;

				if (size < PortalPacket.MinSize || size > PortalPacket.MaxSize)
				{
					if (_DisplaySendOutput)
					{
						ToConsole(
							"Send: Size Out Of Range for {0} at {1} / {2} - {3} bytes",
							p.ID,
							size,
							PortalPacket.MinSize,
							PortalPacket.MaxSize);
					}

					Dispose();
					return false;
				}

				var length = buffer.Send(_Client, 0, size);

				if (length < size)
				{
					if (_DisplaySendOutput)
					{
						ToConsole("Send: Failed for {0} at {1} / {2} bytes", p.ID, length, size);
					}

					Dispose();
					return false;
				}

				if (_DisplaySendOutput)
				{
					ToConsole("Send: Packet {0} at {1} bytes", p.ID, size);
				}

				if (p.GetResponse)
				{
					if (!Receive(true))
					{
						if (_DisplaySendOutput)
						{
							ToConsole("Send: {0} requires a response which could not be handled.", p.ID);
						}

						Dispose();
						return false;
					}

					ProcessReceiveQueue();
				}

				_NoReceive = false;

				return true;
			}
			catch (Exception e)
			{
				ToConsole("Send: Exception Thrown", e);

				Dispose();
				return false;
			}
			finally
			{
				_NoReceive = false;
			}
		}

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

		protected override void OnBeforeDispose()
		{
			Portal.InvokeDisposed(this);

			base.OnBeforeDispose();
		}
		
		protected override void OnDispose()
		{
			base.OnDispose();
			
			if (Handlers != null)
			{
				Handlers.Clear();
				Handlers = null;
			}

			_ReceiveQueue = null;

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

			public static void Free(ref byte[] peek)
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

				peek = null;
			}

			public static void Exchange(ref byte[] peek, byte[] buffer)
			{
				if (buffer != null)
				{
					Buffer.BlockCopy(peek, 0, buffer, 0, peek.Length);
				}

				buffer = Interlocked.Exchange(ref peek, buffer);

				Free(ref buffer);
			}
		}
	}
}