#region Header
//   Vorspire    _,-'/-'/  PortalPacketHandler.cs
//   .      __,-; ,'( '/
//    \.    `-.__`-._`:_,-._       _ , . ``
//     `:-._,------' ` _,`--` -: `_ , ` ,' :
//        `---..__,,--'  (C) 2016  ` -'. -'
//        #  Vita-Nex [http://core.vita-nex.com]  #
//  {o)xxx|===============-   #   -===============|xxx(o}
//        #        The MIT License (MIT)          #
#endregion

namespace Multiverse
{
	public class PortalPacketHandler
	{
		public byte ID { get; private set; }
		public ushort Length { get; private set; }
		public PortalContext Context { get; private set; }

		public PortalReceive OnReceive { get; set; }

		public PortalPacketHandler(byte packetID, ushort length, PortalContext context, PortalReceive onReceive)
		{
			ID = packetID;
			Length = length;
			Context = context;

			OnReceive = onReceive;
		}
	}
}