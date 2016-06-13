#region Header
//   Vorspire    _,-'/-'/  PortalPackets.cs
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
	public static class PortalPackets
	{
		public sealed class HandshakeRequest : PortalPacket
		{
			public static HandshakeRequest Create { get { return new HandshakeRequest(); } }

			public override bool GetResponse { get { return true; } }

			private HandshakeRequest()
				: base(0)
			{
				Stream.Write(PortalAuthentication.Key);
			}
		}

		public sealed class HandshakeResponse : PortalPacket
		{
			public static HandshakeResponse Accepted { get; private set; }
			public static HandshakeResponse Rejected { get; private set; }

			static HandshakeResponse()
			{
				Accepted = new HandshakeResponse(true);
				Rejected = new HandshakeResponse(false);
			}

			public override bool GetResponse { get { return false; } }

			private HandshakeResponse(bool success)
				: base(1)
			{
				Stream.Write(success);
			}
		}

		public sealed class PingRequest : PortalPacket
		{
			public static PingRequest Instance { get; private set; }

			static PingRequest()
			{
				Instance = new PingRequest();
			}

			public override bool GetResponse { get { return true; } }

			private PingRequest()
				: base(2)
			{
				Stream.FillRandom(32);
			}
		}

		public sealed class PingResponse : PortalPacket
		{
			public static PingResponse Instance { get; private set; }

			static PingResponse()
			{
				Instance = new PingResponse();
			}

			public override bool GetResponse { get { return false; } }

			private PingResponse()
				: base(3)
			{
				Stream.FillRandom(32);
			}
		}

		public sealed class DisconnectNotify : PortalPacket
		{
			public static DisconnectNotify Instance { get; private set; }

			static DisconnectNotify()
			{
				Instance = new DisconnectNotify();
			}

			public override bool GetResponse { get { return false; } }

			private DisconnectNotify()
				: base(255)
			{ }
		}
	}
}