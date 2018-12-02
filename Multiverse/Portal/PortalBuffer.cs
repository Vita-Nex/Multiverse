#region Header
//   Vorspire    _,-'/-'/  PortalBuffer.cs
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
#endregion

namespace Multiverse
{
	public sealed class PortalBuffer : IEnumerable<byte>, IDisposable
	{
		private readonly byte[][] _Empty = new byte[0][];

		public const int CoalesceDefault = 8192;

		public const int CoalesceMin = 4096;
		public const int CoalesceMax = 81920;

		public const long SizeLimit = CoalesceMax * 4096L;

		//private static readonly ConcurrentQueue<byte[]>[] _Pool;
		/*
		static PortalBuffer()
		{
			_Pool = new ConcurrentQueue<byte[]>[CoalesceMax / CoalesceMin];

			for (var i = 0; i < _Pool.Length; i++)
			{
				_Pool[i] = new ConcurrentQueue<byte[]>();
			}
		}
		*/
		private static void AcquireBuffer(out byte[] buffer, int coalesce)
		{
			/*
			if (coalesce == 0 || coalesce < CoalesceMin || coalesce > CoalesceMax)
			{
				buffer = new byte[coalesce];
				return;
			}

			if (coalesce % CoalesceMin != 0)
			{
				buffer = new byte[coalesce];
				return;
			}

			var index = (coalesce / CoalesceMin) - 1;

			var pool = _Pool[index];

			if (pool.IsEmpty || !pool.TryDequeue(out buffer))
			{
				buffer = new byte[coalesce];
			}
			*/
			buffer = new byte[coalesce];
		}

		private static void FreeBuffer(ref byte[] buffer)
		{
			if (buffer == null)
			{
				return;
			}

			try
			{
				Array.Clear(buffer, 0, buffer.Length);
				/*
				if (buffer.Length < CoalesceMin || buffer.Length > CoalesceMax)
				{
					return;
				}

				var coalesce = buffer.Length;

				if (coalesce % CoalesceMin != 0)
				{
					return;
				}

				var index = (coalesce / CoalesceMin) - 1;

				var pool = _Pool[index];

				if (pool.Count < (_Pool.Length - index) * 64)
				{
					pool.Enqueue(buffer);
				}
				*/
			}
			catch
			{ }
			finally
			{
				buffer = null;
			}
		}

		private static int ComputeCoalesce(long size)
		{
			var coalesce = (1 + (size / CoalesceMin)) * CoalesceMin;

			return (int)Math.Max(CoalesceMin, Math.Min(CoalesceMax, coalesce));
		}

		private volatile byte[][] _Buffers;

		public byte this[long index]
		{
			get
			{
				if (IsDisposed)
				{
					throw new ObjectDisposedException("_Buffers");
				}

				return _Buffers[index / Coalesce][index % Coalesce];
			}
			set
			{
				if (IsDisposed)
				{
					throw new ObjectDisposedException("_Buffers");
				}

				_Buffers[index / Coalesce][index % Coalesce] = value;
			}
		}

		public byte this[int head, int tail]
		{
			get
			{
				if (IsDisposed)
				{
					throw new ObjectDisposedException("_Buffers");
				}

				return _Buffers[head][tail];
			}
			set
			{
				if (IsDisposed)
				{
					throw new ObjectDisposedException("_Buffers");
				}

				_Buffers[head][tail] = value;
			}
		}

		public long Size { get; private set; }
		public int Coalesce { get; private set; }

		public bool IsDisposed { get; private set; }

		public PortalBuffer()
			: this(0)
		{ }

		public PortalBuffer(long size)
			: this(size, size > 0)
		{ }

		public PortalBuffer(long size, bool autoCoalesce)
			: this(size, autoCoalesce ? ComputeCoalesce(size) : CoalesceDefault)
		{ }

		public PortalBuffer(long size, int coalesce)
		{
			if (size < 0 || size > SizeLimit)
			{
				throw new ArgumentOutOfRangeException("size", size, "Value must be >= 0 || <= " + SizeLimit);
			}

			if (coalesce < CoalesceMin || coalesce > CoalesceMax)
			{
				throw new ArgumentOutOfRangeException(
					"coalesce",
					coalesce,
					"Value must be >= " + CoalesceMin + " || <= " + CoalesceMax);
			}

			Coalesce = coalesce;

			SetSize(size);
		}

		~PortalBuffer()
		{
			Dispose();
		}

		public void Dispose()
		{
			if (IsDisposed)
			{
				return;
			}

			try
			{
				Free();
			}
			catch
			{ }
			finally
			{
				_Buffers = null;

				IsDisposed = true;
			}
		}

		public void SetSize(long size)
		{
			if (IsDisposed)
			{
				throw new ObjectDisposedException("_Buffers");
			}

			if (size < 0 || size > SizeLimit)
			{
				throw new ArgumentOutOfRangeException("size", size, "Value must be >= 0 || <= " + SizeLimit);
			}

			var diff = size - Size;

			if (diff == 0)
			{
				return;
			}

			Size = size;

			if (Size == 0)
			{
				_Buffers = _Empty;
				return;
			}

			if (_Buffers == null)
			{
				_Buffers = _Empty;
			}

			var count = (int)Math.Ceiling(Size / (double)Coalesce);

			if (count == _Buffers.Length)
			{
				if (count > 0 && diff < 0)
				{
					var length = (int)Math.Abs(diff);
					
					Array.Clear(_Buffers[count - 1], Coalesce - length, length);
				}

				return;
			}

			var list = _Buffers;
			var swap = new byte[count][];

			if (swap.Length > list.Length)
			{
				Array.Copy(list, swap, list.Length);

				for (var i = list.Length; i < swap.Length; i++)
				{
					AcquireBuffer(out swap[i], Coalesce);
				}
			}
			else if (swap.Length < list.Length)
			{
				Array.Copy(list, swap, swap.Length);

				for (var i = swap.Length; i < list.Length; i++)
				{
					FreeBuffer(ref list[i]);
				}
			}

			_Buffers = swap;

			if (list.Length > 0)
			{
				Array.Clear(list, 0, list.Length);
			}
		}

		public void Clear()
		{
			if (IsDisposed)
			{
				throw new ObjectDisposedException("_Buffers");
			}

			for (var i = 0; i < _Buffers.Length; i++)
			{
				Array.Clear(_Buffers[i], 0, _Buffers[i].Length);
			}
		}

		public void Free()
		{
			if (IsDisposed)
			{
				throw new ObjectDisposedException("_Buffers");
			}
			
			for (var i = 0; i < _Buffers.Length; i++)
			{
				FreeBuffer(ref _Buffers[i]);
			}

			Array.Clear(_Buffers, 0, _Buffers.Length);

			_Buffers = _Empty;
		}

		public byte[] Join(long offset, int length)
		{
			if (IsDisposed)
			{
				throw new ObjectDisposedException("_Buffers");
			}

			var joined = new byte[length];

			if (offset == 0 && length >= Size)
			{
				for (long i = 0; i < _Buffers.Length; i++)
				{
					Array.Copy(_Buffers[i], 0, joined, i * Coalesce, Coalesce);
				}
			}
			else if (offset % Coalesce == 0 && length % Coalesce == 0)
			{
				for (long i = offset / Coalesce, j = 0; i < _Buffers.Length && j < length; i++, j += Coalesce)
				{
					Array.Copy(_Buffers[i], 0, joined, i * Coalesce, Coalesce);
				}
			}
			else
			{
				for (long i = 0; i < length; i++)
				{
					joined[i] = this[offset + i];
				}
			}

			return joined;
		}

		public IEnumerator<byte> GetEnumerator()
		{
			if (IsDisposed)
			{
				throw new ObjectDisposedException("_Buffers");
			}

			return _Buffers.SelectMany(o => o).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public long Send(Socket socket, long offset, long length)
		{
			if (IsDisposed)
			{
				throw new ObjectDisposedException("_Buffers");
			}

			if (socket == null)
			{
				throw new ArgumentNullException("socket");
			}

			if (length < 0)
			{
				throw new ArgumentOutOfRangeException("length");
			}

			if (length == 0)
			{
				return 0;
			}

			long sent = 0, pos = offset;

			byte[] buffer;
			int index, chunk, write, fails = 0;

			while (sent < length)
			{
				buffer = _Buffers[pos / Coalesce];
				index = (int)(pos % Coalesce);

				chunk = (int)Math.Min(buffer.Length - index, length - sent);

				if (pos + chunk > Size)
				{
					chunk = (int)(Size - pos);
				}

				if (chunk <= 0)
				{
					break;
				}

				try
				{
					write = socket.Send(buffer, index, chunk, SocketFlags.None);
				}
				catch
				{
					break;
				}

				if (write > 0)
				{
					pos += write;
					sent += write;

					fails = 0;
				}
				else if (++fails < 100)
				{
					Thread.Sleep(1);
				}
				else
				{
					break;
				}

				if (IsDisposed || sent >= length || pos >= Size || !socket.Connected)
				{
					break;
				}
			}

			return sent;
		}

		public long Receive(Socket socket, long offset, long length)
		{
			if (IsDisposed)
			{
				throw new ObjectDisposedException("_Buffers");
			}

			if (socket == null)
			{
				throw new ArgumentNullException("socket");
			}

			if (length < 0)
			{
				throw new ArgumentOutOfRangeException("length");
			}

			if (length == 0)
			{
				return 0;
			}

			if (offset + length > Size)
			{
				SetSize(offset + length);
			}

			long recv = 0, pos = offset;

			byte[] buffer;
			int index, chunk, read, fails = 0;

			while (recv < length && !IsDisposed)
			{
				buffer = _Buffers[pos / Coalesce];
				index = (int)(pos % Coalesce);

				chunk = (int)Math.Min(buffer.Length - index, length - recv);

				if (pos + chunk > Size)
				{
					chunk = (int)(Size - pos);
				}

				if (chunk <= 0)
				{
					break;
				}

				try
				{
					read = socket.Receive(buffer, index, chunk, SocketFlags.None);
				}
				catch
				{
					break;
				}

				if (read > 0)
				{
					pos += read;
					recv += read;

					fails = 0;
				}
				else if (++fails < 100)
				{
					Thread.Sleep(1);
				}
				else
				{
					break;
				}

				if (IsDisposed || recv >= length || pos >= Size || !socket.Connected)
				{
					break;
				}
			}

			return recv;
		}

		public long WriteToStream(Stream dest, long offset, long length)
		{
			if (IsDisposed)
			{
				throw new ObjectDisposedException("_Buffers");
			}

			if (dest == null)
			{
				throw new ArgumentNullException("dest");
			}

			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException("offset");
			}

			if (length < 0)
			{
				throw new ArgumentOutOfRangeException("length");
			}

			if (length == 0)
			{
				return 0;
			}
			
			long write = 0, pos = offset;

			byte[] buffer;
			int index, chunk;

			while (write < length)
			{
				try
				{
					buffer = _Buffers[pos / Coalesce];
					index = (int)(pos % Coalesce);

					chunk = (int)Math.Min(buffer.Length - index, length - write);

					if (pos + chunk > Size)
					{
						chunk = (int)(Size - pos);
					}

					if (chunk <= 0)
					{
						break;
					}

					dest.Write(buffer, index, chunk);

					pos += chunk;
					write += chunk;
				}
				catch
				{
					break;
				}

				if (IsDisposed || write >= length || pos >= Size)
				{
					break;
				}
			}

			return write;
		}

		public long ReadFromStream(Stream source, long offset, long length)
		{
			if (IsDisposed)
			{
				throw new ObjectDisposedException("_Buffers");
			}

			if (source == null)
			{
				throw new ArgumentNullException("source");
			}

			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException("offset");
			}

			if (length < 0)
			{
				throw new ArgumentOutOfRangeException("length");
			}

			if (length == 0)
			{
				return 0;
			}

			if (offset + length > Size)
			{
				SetSize(offset + length);
			}

			long read = 0, pos = offset;

			byte[] buffer;
			int index, chunk;

			while (read < length)
			{
				try
				{
					buffer = _Buffers[pos / Coalesce];
					index = (int)(pos % Coalesce);

					chunk = (int)Math.Min(buffer.Length - index, length - read);

					if (pos + chunk > Size)
					{
						chunk = (int)(Size - pos);
					}

					if (chunk <= 0)
					{
						break;
					}

					source.Read(buffer, index, chunk);

					pos += chunk;
					read += chunk;
				}
				catch
				{
					break;
				}

				if (IsDisposed || read >= length || pos >= Size)
				{
					break;
				}
			}

			return read;
		}
	}
}