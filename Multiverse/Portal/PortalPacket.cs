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
		public static int MinSize = 5;
		public static int MaxSize = 2097152;

		private static readonly byte[] _EmptyBuffer = new byte[0];

		private volatile byte[] _Buffer;

		public int ID { get; private set; }
		public int ClientID { get; private set; }
		public int Length { get; private set; }

		protected PortalPacketWriter Stream { get; private set; }

		protected PortalPacket(int packetID)
			: this(packetID, 0)
		{ }

		protected PortalPacket(int packetID, int length)
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
				Console.WriteLine("{0}: {1}: Bad gateway packet! (Stream is null)", GetType().Name, ID);

				return _Buffer = _EmptyBuffer;
			}

			using (Stream)
			{
				if (Length == 0)
				{
					Stream.Position = MinSize - 2;
					Stream.Write((short)Stream.Length);
				}
				else if (Stream.Length != Length)
				{
					var diff = Stream.Length - Length;

					Console.WriteLine(
						"{0}: {1}: Bad gateway packet length! ({2}{3} bytes)",
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