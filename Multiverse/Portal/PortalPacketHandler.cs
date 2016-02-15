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
		public int ID { get; private set; }
		public int Length { get; private set; }
		public PortalContext Context { get; private set; }

		public PortalReceive OnReceive { get; set; }

		public PortalPacketHandler(int packetID, int length, PortalContext context, PortalReceive onReceive)
		{
			ID = packetID;
			Length = length;
			Context = context;

			OnReceive = onReceive;
		}
	}
}