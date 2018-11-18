#region Header
//   Vorspire    _,-'/-'/  PortalPacketBuffered.cs
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
	public sealed class PortalPacketBuffered : PortalPacket
	{
		private readonly bool _GetResponse;

		public override bool GetResponse { get { return _GetResponse; } }

		public PortalPacketBuffered(byte[] buffer, bool getResponse)
			: base(BitConverter.ToUInt16(buffer, 0))
		{
			_GetResponse = getResponse;

			Stream.Position = 0;

			Stream.Write(buffer);
		}
	}
}