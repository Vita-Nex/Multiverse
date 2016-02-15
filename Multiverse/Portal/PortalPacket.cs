#region Header
//   Vorspire    _,-'/-'/  PortalPacket.cs
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
#endregion

namespace Multiverse
{
	public abstract class PortalPacket
	{
		public static ushort MinSize = 5;
		public static ushort MaxSize = 65535;

		private static readonly byte[] _EmptyBuffer = new byte[0];

		private volatile byte[] _Buffer;

		public byte ID { get; private set; }
		public ushort ClientID { get; private set; }
		public ushort Length { get; private set; }

		protected PortalPacketWriter Stream { get; private set; }

		protected PortalPacket(byte packetID)
			: this(packetID, 0)
		{ }

		protected PortalPacket(byte packetID, ushort length)
		{
			ClientID = Portal.ClientID;

			ID = packetID;
			Length = length;

			Stream = new PortalPacketWriter(ID, ClientID, Length);
		}

		public byte[] Compile()
		{
			if (_Buffer != null)
			{
				return _Buffer;
			}

			if (Stream == null)
			{
				Console.WriteLine("{0}: {1}: Bad Packet: Stream is null", GetType().Name, ID);

				return _Buffer = _EmptyBuffer;
			}

			using (Stream)
			{
				if (Length == 0)
				{
					Stream.Position = 3;
					Stream.Write(Stream.Length);
				}
				else if (Stream.Length != Length)
				{
					var diff = Stream.Length - Length;

					Console.WriteLine(
						"{0}: {1}: Bad Packet Length: {2}{3} bytes",
						GetType().Name,
						ID,
						diff > 0 ? "+" : String.Empty,
						diff);

					_Buffer = _EmptyBuffer;
				}

				if (_Buffer == null)
				{
					_Buffer = Stream.ToArray();
				}

				Stream.Close();
				Stream = null;
			}

			return _Buffer;
		}
	}
}