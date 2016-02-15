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

namespace Multiverse
{
	public sealed class PortalPacketBuffered : PortalPacket
	{
		public PortalPacketBuffered(byte[] buffer)
			: base(buffer[0], (ushort)buffer.Length)
		{
			Stream.Position = 0;

			Stream.Write(buffer);
		}
	}
}