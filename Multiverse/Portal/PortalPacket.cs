#region Header
//   Vorspire    _,-'/-'/  PortalPacket.cs
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
#endregion

namespace Multiverse
{
	public abstract class PortalPacket : IDisposable
	{
		public static readonly int MinSize = 8;

		private static int _MaxSize = 1048576 * 32;

		public static int MaxSize { get { return _MaxSize; } set { _MaxSize = Math.Max(MinSize, value); } }

		private readonly object _SyncRoot = new object();

		public ushort ID { get; private set; }
		public ushort ClientID { get; private set; }

		public abstract bool GetResponse { get; }

		private volatile PortalPacketWriter _Stream;

		protected PortalPacketWriter Stream { get { return _Stream; } }

		protected PortalPacket(ushort packetID)
		{
			ID = packetID;

			ClientID = Portal.ClientID;

			_Stream = new PortalPacketWriter(ID, ClientID);
		}

		~PortalPacket()
		{
			Dispose();
		}

		public PortalBuffer Compile()
		{
			PortalBuffer buffer;

			lock (_SyncRoot)
			{
				if (_Stream == null)
				{
					Portal.ToConsole("{0}: {1}: Bad Packet: Stream is null", GetType().Name, ID);

					return null;
				}

				ClientID = Portal.ClientID;

				_Stream.UpdateHeader(ClientID, _Stream.Length);

				OnCompile();

				buffer = ((PortalStream)_Stream.BaseStream).GetBuffer();

				OnCompile(ref buffer);
			}

			return buffer;
		}

		protected virtual void OnCompile()
		{ }

		protected virtual void OnCompile(ref PortalBuffer buffer)
		{ }

		public void Dispose()
		{
			lock (_SyncRoot)
			{
				if (_Stream != null)
				{
					_Stream.Dispose();
					_Stream = null;
				}
			}
		}
	}
}