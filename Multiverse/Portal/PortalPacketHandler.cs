#region Header
//   Vorspire    _,-'/-'/  PortalPacketHandler.cs
//   .      __,-; ,'( '/
//    \.    `-.__`-._`:_,-._       _ , . ``
//     `:-._,------' ` _,`--` -: `_ , ` ,' :
//        `---..__,,--'  (C) 2018  ` -'. -'
//        #  Vita-Nex [http://core.vita-nex.com]  #
//  {o)xxx|===============-   #   -===============|xxx(o}
//        #        The MIT License (MIT)          #
#endregion

namespace Multiverse
{
	public class PortalPacketHandler
	{
		public ushort ID { get; private set; }

		public PortalContext Context { get; private set; }

		public PortalReceive OnReceive { get; set; }

		public PortalPacketHandler(ushort packetID, PortalContext context, PortalReceive onReceive)
		{
			ID = packetID;
			Context = context;
			OnReceive = onReceive;
		}
	}
}