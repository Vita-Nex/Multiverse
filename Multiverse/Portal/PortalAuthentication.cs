#region Header
//   Vorspire    _,-'/-'/  PortalAuthentication.cs
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
using System.Linq;
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
		private static byte[] _TimeKey;

		public static byte[] Key
		{
			get
			{
				lock (_SHA1Lock)
				{
					if (_Key != null)
					{
						return _Key;
					}

					var buffer = Encoding.ASCII.GetBytes(Portal.AuthKey);

					return _Key = _SHA1.ComputeHash(buffer);
				}
			}
		}

		public static byte[] TimeKey
		{
			get
			{
				lock (_SHA1Lock)
				{
					var now = DateTime.UtcNow;

					if (_TimeKey != null && now.TimeOfDay.Hours == _Regenerate.TimeOfDay.Hours)
					{
						return _TimeKey;
					}

					_Regenerate = now;

					var seed = String.Concat(Portal.AuthKey, now.TimeOfDay.Hours);
					var buffer = Encoding.ASCII.GetBytes(seed);

					return _TimeKey = _SHA1.ComputeHash(buffer);
				}
			}
		}

		static PortalAuthentication()
		{
			_SHA1 = new SHA1CryptoServiceProvider();
			_SHA1Lock = new object();

			_Regenerate = DateTime.MinValue;
		}

		public static bool Verify(byte[] key)
		{
			return Key.SequenceEqual(key) || TimeKey.SequenceEqual(key);
		}
	}
}