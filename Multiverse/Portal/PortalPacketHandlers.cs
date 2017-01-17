#region Header
//   Vorspire    _,-'/-'/  PortalPacketHandlers.cs
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
using System.Linq;
#endregion

namespace Multiverse
{
	public static class PortalPacketHandlers
	{
		public static event Action<PortalClient> OnRegister;

		public static void RegisterHandlers(PortalClient client)
		{
			client.Register(0, PortalContext.Server, OnHandshakeRequest);
			client.Register(1, PortalContext.Client, OnHandshakeResponse);

			client.Register(2, PortalContext.Any, OnPingRequest);
			client.Register(3, PortalContext.Any, OnPingResponse);

			client.Register(255, PortalContext.Any, OnDisconnectNotify);

			if (OnRegister != null)
			{
				OnRegister(client);
			}
		}

		private static void OnHandshakeRequest(PortalClient client, PortalPacketReader p)
		{
			if (client.IsAuthed)
			{
				client.Send(PortalPackets.HandshakeResponse.Accepted);
				return;
			}

			var key = p.ReadBytes(20);

			client.IsAuthed = PortalAuthentication.Verify(key);

			if (client.IsAuthed && Portal.UniqueIDs && Portal.Transport is PortalServer &&
				((PortalServer)Portal.Transport).Clients.Any(c => c != client && c.IsIdentified && c.ServerID == p.ServerID))
			{
				client.IsAuthed = false;
			}

			var r = client.IsAuthed //
				? PortalPackets.HandshakeResponse.Accepted
				: PortalPackets.HandshakeResponse.Rejected;

			client.Send(r);
		}

		private static void OnHandshakeResponse(PortalClient client, PortalPacketReader p)
		{
			client.IsAuthed = p.ReadBoolean();
		}

		private static void OnPingRequest(PortalClient client, PortalPacketReader p)
		{
			p.ReadToEnd(); // random bytes

			client.Ping(true);
		}

		private static void OnPingResponse(PortalClient client, PortalPacketReader p)
		{
			p.ReadToEnd(); // random bytes

			client.Pong();
		}

		private static void OnDisconnectNotify(PortalClient client, PortalPacketReader p)
		{
			client.Dispose();
		}
	}
}