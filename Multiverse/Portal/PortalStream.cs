#region Header
//   Vorspire    _,-'/-'/  PortalStream.cs
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
using System.IO;
#endregion

namespace Multiverse
{
	public sealed class PortalStream : Stream
	{
		private PortalBuffer _Buffer;

		public override long Length
		{
			get
			{
				if (_Buffer == null || _Buffer.IsDisposed)
				{
					throw new ObjectDisposedException("_Buffer");
				}

				return _Buffer.Size;
			}
		}

		private long _Position;

		public override long Position
		{
			get { return _Position; }
			set
			{
				if (_Buffer == null || _Buffer.IsDisposed)
				{
					throw new ObjectDisposedException("_Buffer");
				}

				if (value < 0 || value > _Buffer.Size)
				{
					throw new ArgumentOutOfRangeException("value", value, "Value must be >= 0 || <= " + _Buffer.Size);
				}

				_Position = value;
			}
		}

		private bool _Readable = true, _Writable = true, _Seekable = true;

		public override bool CanRead { get { return _Readable; } }
		public override bool CanWrite { get { return _Writable; } }
		public override bool CanSeek { get { return _Seekable; } }

		public PortalStream()
		{
			_Buffer = new PortalBuffer();
		}

		public PortalStream(long size)
		{
			_Buffer = new PortalBuffer(size);
		}

		public PortalStream(long size, int coalesce)
		{
			_Buffer = new PortalBuffer(size, coalesce);
		}

		public PortalStream(PortalBuffer buffer)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException("buffer");
			}

			if (buffer.IsDisposed)
			{
				throw new ObjectDisposedException("buffer");
			}

			_Buffer = buffer;
		}

		protected override void Dispose(bool disposing)
		{
			try
			{
				if (disposing)
				{
					_Readable = false;
					_Writable = false;
					_Seekable = false;

					_Buffer = null;
				}
			}
			finally
			{
				base.Dispose(disposing);
			}
		}

		public override void Flush()
		{ }

		public override long Seek(long offset, SeekOrigin origin)
		{
			if (_Buffer == null || _Buffer.IsDisposed)
			{
				throw new ObjectDisposedException("_Buffer");
			}

			if (!_Seekable)
			{
				throw new InvalidOperationException();
			}

			lock (_Buffer)
			{
				switch (origin)
				{
					case SeekOrigin.Begin:
					{
						if (offset < 0 || offset >= _Buffer.Size)
						{
							throw new ArgumentOutOfRangeException("offset", origin, "Result must be >= 0 || <= " + _Buffer.Size);
						}

						_Position = offset;
					}
						break;
					case SeekOrigin.Current:
					{
						if (_Position + offset < 0 || _Position + offset >= _Buffer.Size)
						{
							throw new ArgumentOutOfRangeException("offset", origin, "Result must be >= 0 || <= " + _Buffer.Size);
						}

						_Position += offset;
					}
						break;
					case SeekOrigin.End:
					{
						if (_Buffer.Size - offset < 0 || _Buffer.Size - offset >= _Buffer.Size)
						{
							throw new ArgumentOutOfRangeException("offset", origin, "Result must be >= 0 || <= " + _Buffer.Size);
						}

						_Position = _Buffer.Size - offset;
					}
						break;
					default:
						throw new ArgumentOutOfRangeException("origin", origin, null);
				}

				return _Position;
			}
		}

		public override void SetLength(long size)
		{
			if (_Buffer == null || _Buffer.IsDisposed)
			{
				throw new ObjectDisposedException("_Buffer");
			}

			lock (_Buffer)
			{
				_Buffer.SetSize(size);
			}
		}

		public PortalBuffer GetBuffer()
		{
			if (_Buffer == null || _Buffer.IsDisposed)
			{
				throw new ObjectDisposedException("_Buffer");
			}

			return _Buffer;
		}

		public override int ReadByte()
		{
			if (!_Readable)
			{
				throw new InvalidOperationException();
			}

			if (_Buffer == null || _Buffer.IsDisposed)
			{
				throw new ObjectDisposedException("_Buffer");
			}

			lock (_Buffer)
			{
				if (_Position < _Buffer.Size)
				{
					return _Buffer[_Position++];
				}
			}

			return -1;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (!_Readable)
			{
				throw new InvalidOperationException();
			}

			if (_Buffer == null || _Buffer.IsDisposed)
			{
				throw new ObjectDisposedException("_Buffer");
			}

			lock (_Buffer)
			{
				for (int i = 0, o = offset; i < count; i++, o++)
				{
					if (_Position >= _Buffer.Size)
					{
						throw new EndOfStreamException();
					}

					buffer[o] = _Buffer[_Position++];
				}
			}

			return count;
		}

		public override void WriteByte(byte value)
		{
			if (!_Writable)
			{
				throw new InvalidOperationException();
			}

			if (_Buffer == null || _Buffer.IsDisposed)
			{
				throw new ObjectDisposedException("_Buffer");
			}

			lock (_Buffer)
			{
				if (_Position + 1 > _Buffer.Size)
				{
					_Buffer.SetSize(_Position + 1);
				}

				_Buffer[_Position++] = value;
			}
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			if (!_Writable)
			{
				throw new InvalidOperationException();
			}

			if (_Buffer == null || _Buffer.IsDisposed)
			{
				throw new ObjectDisposedException("_Buffer");
			}

			lock (_Buffer)
			{
				if (_Position + count > _Buffer.Size)
				{
					_Buffer.SetSize(_Position + count);
				}

				for (int i = 0, o = offset; i < count; i++, o++)
				{
					_Buffer[_Position++] = buffer[o];
				}
			}
		}
	}
}