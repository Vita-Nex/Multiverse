#region Header
//   Vorspire    _,-'/-'/  Server.cs
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
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
#endregion

namespace Multiverse
{
	public static class Server
	{
		public static bool Closing { get; set; }

		internal static void Main(string[] args)
		{
			Domain.Config();

			Portal.ServerID = 0;
			Portal.ClientID = 0;

			Portal.Server = new IPEndPoint(IPAddress.Loopback, 3593);

			Portal.Context = PortalContext.Server;

			while (!Closing)
			{
				if (Portal.IsEnabled && !Portal.IsAlive)
				{
					Portal.ToConsole("Starting...");

					if (!Portal.Start())
					{
						Portal.ToConsole("Press any key to continue...");
						Console.ReadKey();
					}
				}

				Thread.Sleep(1);
			}
		}

		public static void Close()
		{
			Portal.Stop();

			Closing = true;
		}

		private static class Domain
		{
			private static readonly ConsoleEventHandler _Handler = OnConsoleEvent;

			public static void Config()
			{
				AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
				AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

				SetConsoleCtrlHandler(_Handler, true);
			}

			private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
			{
				try
				{
					if (e.ExceptionObject is Exception)
					{
						Portal.ToConsole("Unhandled Exception", (Exception)e.ExceptionObject);
					}

					if (e.IsTerminating)
					{
						Portal.ToConsole("Press any key to exit...");
						Console.ReadKey();

						Close();
					}
				}
				catch
				{ }
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