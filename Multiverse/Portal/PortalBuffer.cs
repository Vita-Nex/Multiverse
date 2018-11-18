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
using System.Net.Sockets;
using System.Threading;
#endregion

namespace Multiverse
{
	public sealed class PortalBuffer : IEnumerable<byte>, IDisposable
	{
		private static readonly byte[][] _Empty = new byte[0][];

		public const int CoalesceMin = 8;
		public const int CoalesceMax = 81920;

		public const int CoalesceDefault = 8192;

		public const long SizeLimit = CoalesceMax * 65535L;

		private static int ComputeCoalesce(long size)
		{
			const int delta = 1024;

			var coalesce = CoalesceDefault;

			if (coalesce > size)
			{
				while (coalesce - delta >= size && coalesce - delta >= CoalesceMin)
				{
					coalesce -= delta;
				}
			}
			else if (coalesce < size)
			{
				while (coalesce + delta <= size && coalesce + delta <= CoalesceMax)
				{
					coalesce += delta;
				}
			}

			return coalesce;
		}

		private byte[][] _Buffers;

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
			: this(size, true)
		{ }

		public PortalBuffer(long size, bool autoCoalesce)
			: this(size, !autoCoalesce ? CoalesceDefault : ComputeCoalesce(size))
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

			Coalesce = Math.Max(CoalesceMin, coalesce);

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

			IsDisposed = true;

			if (_Buffers != null)
			{
				Array.Clear(_Buffers, 0, _Buffers.Length);

				_Buffers = null;
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
					diff = Math.Abs(diff);

					Array.Clear(_Buffers[count - 1], (int)(Coalesce - diff), (int)diff);
				}

				return;
			}

			var list = _Buffers;
			var swap = new byte[count][];

			if (count > list.Length)
			{
				Array.Copy(list, swap, list.Length);

				for (var i = list.Length; i < count; i++)
				{
					swap[i] = new byte[Coalesce];
				}
			}
			else if (count < list.Length)
			{
				Array.Copy(list, swap, count);
			}

			Array.Clear(list, 0, list.Length);

			_Buffers = swap;
		}

		public void Clear()
		{
			if (IsDisposed)
			{
				throw new ObjectDisposedException("_Buffers");
			}

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

			foreach (var buffer in _Buffers)
			{
				for (var i = 0; i < buffer.Length; i++)
				{
					yield return buffer[i];
				}
			}
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

			long sent = 0;

			byte[] buffer;
			int index;

			do
			{
				try
				{
					buffer = _Buffers[(offset + sent) / Coalesce];
					index = (int)((offset + sent) % Coalesce);

					sent += socket.Send(buffer, index, (int)Math.Min(buffer.Length - index, length - sent), SocketFlags.None);
				}
				catch
				{
					break;
				}

				Thread.Sleep(0);
			}
			while (socket.Connected && sent < length);

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

			var recv = 0;

			byte[] buffer;
			int index;

			do
			{
				try
				{
					buffer = _Buffers[(offset + recv) / Coalesce];
					index = (int)((offset + recv) % Coalesce);

					recv += socket.Receive(buffer, index, (int)Math.Min(buffer.Length - index, length - recv), SocketFlags.None);
				}
				catch
				{
					break;
				}

				Thread.Sleep(0);
			}
			while (socket.Connected && recv < length);

			return recv;
		}
	}
}