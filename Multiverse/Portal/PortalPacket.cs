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
		public static readonly int MinSize = 8;

		private static int _MaxSize = 2097152; // 2M

		public static int MaxSize { get { return _MaxSize; } set { _MaxSize = Math.Max(MinSize, value); } }

		private static readonly byte[] _EmptyBuffer = new byte[0];

		private volatile byte[] _Buffer;

		public ushort ID { get; private set; }
		public ushort ClientID { get; private set; }

		public abstract bool GetResponse { get; }

		protected PortalPacketWriter Stream { get; private set; }

		protected PortalPacket(ushort packetID)
		{
			ID = packetID;

			ClientID = Portal.ClientID;

			Stream = new PortalPacketWriter(ID, ClientID);
		}

		public byte[] Compile()
		{
			if (_Buffer != null)
			{
				if (ClientID != Portal.ClientID)
				{
					ClientID = Portal.ClientID;

					if (_Buffer.Length >= MinSize)
					{
						Buffer.BlockCopy(BitConverter.GetBytes(ClientID), 0, _Buffer, 2, 2);
					}
				}

				return _Buffer;
			}

			if (ClientID != Portal.ClientID)
			{
				ClientID = Portal.ClientID;

				if (Stream != null)
				{
					Stream.UpdateClientID(ClientID);
				}
			}

			if (Stream == null)
			{
				Console.WriteLine("{0}: {1}: Bad Packet: Stream is null", GetType().Name, ID);

				return _Buffer = _EmptyBuffer;
			}

			using (Stream)
			{
				Stream.Position = MinSize - 4;
				Stream.Write(Stream.Length);
				Stream.Position = Stream.Length;

				_Buffer = Stream.ToArray();

				Stream.Close();
				Stream = null;
			}

			return _Buffer;
		}
	}
}