#region Header
//   Vorspire    _,-'/-'/  PortalAuthentication.cs
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
using System.Security.Cryptography;
using System.Text;
#endregion

namespace Multiverse
{
	public static class PortalAuthentication
	{
		private static readonly SHA1CryptoServiceProvider _SHA1;
		private static readonly object _SHA1Lock;

		private static DateTime _Regenerate;

		private static byte[] _Key;

		public static byte[] Key
		{
			get
			{
				lock (_SHA1Lock)
				{
					var now = DateTime.UtcNow;

					if (now.TimeOfDay.Hours == _Regenerate.TimeOfDay.Hours)
					{
						return _Key;
					}

					_Regenerate = now;

					var seed = String.Concat(Portal.AuthKey, now.TimeOfDay.Hours);
					var buffer = Encoding.ASCII.GetBytes(seed);

					_Key = _SHA1.ComputeHash(buffer);

					//Console.WriteLine(String.Join(String.Empty, _Key.Select(b => b.ToString("X2"))));

					return _Key;
				}
			}
		}

		static PortalAuthentication()
		{
			_SHA1 = new SHA1CryptoServiceProvider();
			_SHA1Lock = new object();

			_Regenerate = DateTime.MinValue;
		}
	}
}