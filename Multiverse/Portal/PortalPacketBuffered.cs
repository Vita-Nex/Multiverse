#region Header
//   Vorspire    _,-'/-'/  PortalPacketBuffered.cs
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
	public sealed class PortalPacketBuffered : PortalPacket
	{
		public PortalPacketBuffered(byte[] buffer)
			: base(BitConverter.ToUInt16(buffer, 0))
		{
			Stream.Position = 0;

			Stream.Write(buffer);
		}
	}
}