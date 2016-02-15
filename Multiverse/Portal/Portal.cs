#region Header
//   Vorspire    _,-'/-'/  Portal.cs
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
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
#endregion

namespace Multiverse
{
	public static class Portal
	{
		public static Thread Thread { get; private set; }

		public static PortalTransport Transport { get; private set; }

		public static bool IsUnix { get; private set; }

		public static IPEndPoint Server { get; set; }

		public static string AuthKey { get; set; }

		public static ushort ServerID { get; set; }
		public static ushort ClientID { get; set; }

		public static PortalContext Context { get; set; }

		public static bool IsEnabled { get { return Context != PortalContext.Disabled; } }

		public static bool IsServer { get { return Context == PortalContext.Server; } }
		public static bool IsClient { get { return Context == PortalContext.Client; } }

		public static bool IsAlive
		{
			get { return Thread != null && Thread.IsAlive && Transport != null && Transport.IsAlive; }
		}

		public static long Ticks
		{
			get
			{
				if (Stopwatch.IsHighResolution && !IsUnix)
				{
					return (long)(Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency));
				}

				return (long)(DateTime.UtcNow.Ticks * (1000.0 / TimeSpan.TicksPerSecond));
			}
		}

		public static event Action OnStart;
		public static event Action OnStop;

		public static event Action<PortalClient> OnConnected;

		static Portal()
		{
			var pid = (int)Environment.OSVersion.Platform;

			IsUnix = pid == 4 || pid == 128;

			Server = new IPEndPoint(IPAddress.Any, 3593);

			AuthKey = "Hello Dimension!";

			Context = PortalContext.Disabled;
		}

		public static void InvokeConnected(PortalClient client)
		{
			if (client != null && client.IsAlive && OnConnected != null)
			{
				OnConnected(client);
			}
		}

		private static void Configure()
		{
			if (!IsEnabled)
			{
				Stop();
				return;
			}

			if (IsAlive)
			{
				return;
			}

			Stop();

			if (!IsEnabled)
			{
				return;
			}

			Thread = new Thread(ThreadStart)
			{
				IsBackground = true,
				Name = "Portal" + (IsServer ? " Server" : IsClient ? " Client" : String.Empty)
			};

			if (IsServer)
			{
				Transport = new PortalServer();
			}
			else
			{
				Transport = new PortalClient();
			}
		}

		[STAThread]
		private static void ThreadStart()
		{
			try
			{
				Transport.Start();
			}
			catch (Exception e)
			{
				ToConsole("Start: Failed: {0}", e.Message);

				Stop();
			}
		}

		public static bool Start()
		{
			Configure();

			if (Thread == null || Transport == null || !IsEnabled)
			{
				return false;
			}

			if (!Thread.IsAlive)
			{
				Thread.Start();

				while (!Thread.IsAlive)
				{
					Thread.Sleep(1);
				}

				if (IsAlive && OnStart != null)
				{
					OnStart();
				}
			}

			return IsAlive;
		}

		public static void Stop()
		{
			if (Transport != null)
			{
				Transport.Dispose();
				Transport = null;
			}

			if (Thread != null)
			{
				if (Thread.IsAlive)
				{
					Thread.Abort();

					while (Thread.IsAlive)
					{
						Thread.Sleep(1);
					}
				}

				Thread = null;
			}

			if (OnStop != null)
			{
				OnStop();
			}
		}

		public static void Restart()
		{
			Stop();
			Start();
		}

		public static bool CanList(ushort serverID)
		{
			if (!IsEnabled || !IsAlive)
			{
				return !IsEnabled;
			}

			if (Transport is PortalClient)
			{
				return ((PortalClient)Transport).ServerID == serverID;
			}

			if (Transport is PortalServer)
			{
				return ((PortalServer)Transport).IsConnected(serverID);
			}

			return false;
		}

		public static bool Send(PortalPacket p)
		{
			return IsAlive && Transport.Send(p);
		}

		public static bool SendTarget(PortalPacket p, ushort targetID)
		{
			return IsAlive && Transport.SendTarget(p, targetID);
		}

		public static bool SendExcept(PortalPacket p, ushort exceptID)
		{
			return IsAlive && Transport.SendExcept(p, exceptID);
		}

		public static bool Send(PortalPacket p, bool getResponse)
		{
			return IsAlive && Transport.Send(p, getResponse);
		}

		public static bool SendTarget(PortalPacket p, ushort targetID, bool getResponse)
		{
			return IsAlive && Transport.SendTarget(p, targetID, getResponse);
		}

		public static bool SendExcept(PortalPacket p, ushort exceptID, bool getResponse)
		{
			return IsAlive && Transport.SendExcept(p, exceptID, getResponse);
		}

		public static void ToConsole(string message, params object[] args)
		{
			if (IsAlive)
			{
				Transport.ToConsole(message, args);
			}
			else
			{
				Console.WriteLine("[Portal] {0}", String.Format(message, args));
			}
		}

		public static void FormatBuffer(TextWriter output, Stream input, int length)
		{
			output.WriteLine("        0  1  2  3  4  5  6  7   8  9  A  B  C  D  E  F");
			output.WriteLine("       -- -- -- -- -- -- -- --  -- -- -- -- -- -- -- --");

			var byteIndex = 0;

			var whole = length >> 4;
			var rem = length & 0xF;

			for (var i = 0; i < whole; ++i, byteIndex += 16)
			{
				var bytes = new StringBuilder(49);
				var chars = new StringBuilder(16);

				for (var j = 0; j < 16; ++j)
				{
					var c = input.ReadByte();

					bytes.Append(c.ToString("X2"));

					if (j != 7)
					{
						bytes.Append(' ');
					}
					else
					{
						bytes.Append("  ");
					}

					if (c >= 0x20 && c < 0x7F)
					{
						chars.Append((char)c);
					}
					else
					{
						chars.Append('.');
					}
				}

				output.Write(byteIndex.ToString("X4"));
				output.Write("   ");
				output.Write(bytes.ToString());
				output.Write("  ");
				output.WriteLine(chars.ToString());
			}

			if (rem != 0)
			{
				var bytes = new StringBuilder(49);
				var chars = new StringBuilder(rem);

				for (var j = 0; j < 16; ++j)
				{
					if (j < rem)
					{
						var c = input.ReadByte();

						bytes.Append(c.ToString("X2"));

						if (j != 7)
						{
							bytes.Append(' ');
						}
						else
						{
							bytes.Append("  ");
						}

						if (c >= 0x20 && c < 0x7F)
						{
							chars.Append((char)c);
						}
						else
						{
							chars.Append('.');
						}
					}
					else
					{
						bytes.Append("   ");
					}
				}

				output.Write(byteIndex.ToString("X4"));
				output.Write("   ");
				output.Write(bytes.ToString());
				output.Write("  ");
				output.WriteLine(chars.ToString());
			}
		}

		private static readonly Random _Random = new Random();

		public static int Random()
		{
			return _Random.Next();
		}

		public static ushort Random(ushort value)
		{
			return (ushort)_Random.Next(value);
		}

		public static ushort RandomMinMax(ushort min, ushort max)
		{
			return (ushort)_Random.Next(min, max + 1);
		}

		public static short Random(short value)
		{
			return (short)_Random.Next(value);
		}

		public static short RandomMinMax(short min, short max)
		{
			return (short)_Random.Next(min, max + 1);
		}

		public static int Random(int value)
		{
			return _Random.Next(value);
		}

		public static int RandomMinMax(int min, int max)
		{
			return _Random.Next(min, max + 1);
		}

		public static double RandomDouble()
		{
			return _Random.NextDouble();
		}

		public static byte RandomByte()
		{
			return (byte)_Random.Next(256);
		}

		public static bool RandomBool()
		{
			return _Random.Next(0, 2) == 0;
		}
	}
}