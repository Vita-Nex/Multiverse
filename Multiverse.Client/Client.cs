#region Header
//   Vorspire    _,-'/-'/  Client.cs
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
using System.Diagnostics;
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
			Portal.ClientID = Portal.Random(UInt16.MaxValue);

			Portal.Server = new IPEndPoint(IPAddress.Loopback, 3593);

			Portal.Context = PortalContext.Client;

			Portal.OnConnected += OnConnected;

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

		private static void OnConnected(PortalClient c)
		{
			PingTest(c);

			c.Dispose();
		}

		private static void PingTest(PortalClient c)
		{
			if (Closing || !c.IsAlive)
			{
				return;
			}

			var count = 0;

			do
			{
				Portal.ToConsole("Test: Enter the number of ping requests to send...");
			}
			while (!Closing && c.IsAlive && (!Int32.TryParse(Console.ReadLine(), out count) || count < 0));

			if (Closing || !c.IsAlive)
			{
				Portal.ToConsole("Test: Skipped ping testing...");
				return;
			}

			if (count <= 0)
			{
				Portal.ToConsole("Test: Skipped ping testing...");
				return;
			}

			var samples = new long[count];

			Portal.ToConsole("Ping: {0:#,0} requests...", samples.Length);

			var time = 0L;

			var watch = new Stopwatch();

			for (var i = 0; i < samples.Length; i++)
			{
				watch.Start();

				Portal.ToConsole("Ping: ...");

				var result = c.Ping(false);

				watch.Stop();

				samples[i] = watch.ElapsedMilliseconds;

				watch.Reset();

				time += samples[i];

				Portal.ToConsole("Pong: {0:#,0}ms", samples[i]);

				if (!result)
				{
					break;
				}
			}

			Portal.ToConsole("Completed: T:{0:#,0}ms A:{1:#,0}ms", time, time / samples.Length);

			PingTest(c);
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