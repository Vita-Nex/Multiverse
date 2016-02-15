#region Header
//   Vorspire    _,-'/-'/  Client.cs
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
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
#endregion

namespace Multiverse
{
	public static class Client
	{
		public static bool Closing { get; set; }

		internal static void Main(string[] args)
		{
			Domain.Config();

			Portal.ServerID = 0;
			Portal.ClientID = (ushort)(Portal.Random(UInt16.MaxValue) + 1);

			Portal.Server = new IPEndPoint(IPAddress.Loopback, 3593);

			Portal.Context = PortalContext.Client;

			while (!Closing)
			{
				if (!Portal.IsAlive)
				{
					Portal.ToConsole("Press any key to start...");

					Console.ReadKey();

					Portal.ToConsole("Starting...");

					if (!Portal.Start())
					{
						Portal.ToConsole("Could not start, retrying in 3 seconds...");
						Thread.Sleep(3000);
					}
				}
				else
				{
					Thread.Sleep(10);
				}
			}
		}

		public static void Close()
		{
			Portal.Stop();

			Closing = true;
		}

		private static class Domain
		{
			public static void Config()
			{
				AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
				AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

				SetConsoleCtrlHandler(OnConsoleEvent, true);
			}

			private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
			{
				if (!e.IsTerminating)
				{
					return;
				}

				Portal.ToConsole(e.ExceptionObject.ToString());
				Portal.ToConsole("Exception thrown, press any key to exit...");

				Console.ReadKey();

				Close();
			}

			private enum ConsoleEventType
			{
				CTRL_C_EVENT,
				CTRL_BREAK_EVENT,
				CTRL_CLOSE_EVENT,
				CTRL_LOGOFF_EVENT = 5,
				CTRL_SHUTDOWN_EVENT
			}

			private delegate bool ConsoleEventHandler(ConsoleEventType type);

			[DllImport("Kernel32")]
			private static extern bool SetConsoleCtrlHandler(ConsoleEventHandler callback, bool add);

			private static bool OnConsoleEvent(ConsoleEventType type)
			{
				Close();

				return true;
			}

			private static void OnProcessExit(object sender, EventArgs e)
			{
				Close();
			}
		}
	}
}