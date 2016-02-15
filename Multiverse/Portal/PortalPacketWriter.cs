#region Header
//   Vorspire    _,-'/-'/  PortalPacketWriter.cs
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
using System.IO;
using System.Net;
using System.Text;
#endregion

namespace Multiverse
{
	public sealed class PortalPacketWriter : BinaryWriter
	{
		private static readonly Type _TypeOfSByte = typeof(SByte);
		private static readonly Type _TypeOfByte = typeof(Byte);
		private static readonly Type _TypeOfShort = typeof(Int16);
		private static readonly Type _TypeOfUShort = typeof(UInt16);
		private static readonly Type _TypeOfInt = typeof(Int32);
		private static readonly Type _TypeOfUInt = typeof(UInt32);
		private static readonly Type _TypeOfLong = typeof(Int64);
		private static readonly Type _TypeOfULong = typeof(UInt64);

		public int PacketID { get; private set; }
		public int ClientID { get; private set; }

		public int Length { get { return (int)BaseStream.Length; } set { BaseStream.SetLength(value); } }
		public int Position { get { return (int)BaseStream.Position; } set { BaseStream.Position = value; } }

		public PortalPacketWriter(int id, int clientID, int length)
			: base(new MemoryStream(length), Encoding.UTF8)
		{
			PacketID = id;
			ClientID = clientID;

			Length = length;
			Position = 0;

			Write((byte)PacketID);
			Write((short)ClientID);
			Write((short)Length);
		}

		public void Fill()
		{
			Fill(Length - Position);
		}

		public void Fill(int length)
		{
			var offset = Position + length;

			if (offset > Length)
			{
				Length = offset;
			}

			Position = offset;
		}

		public void FillRandom()
		{
			FillRandom(Position - Length);
		}

		public void FillRandom(int length)
		{
			var offset = Position + length;

			if (offset > Length)
			{
				Length = offset;
			}

			while (Position < offset)
			{
				Write(Portal.RandomByte());
			}
		}

		public void Write(DateTime date)
		{
			Write((short)date.Year);
			Write((byte)date.Month);
			Write((byte)date.Day);
			Write(date.TimeOfDay);
			Write(date.Kind);
		}

		public void Write(TimeSpan time)
		{
			Write(time.TotalMilliseconds);
		}

		public void Write(IPAddress ip)
		{
			var bytes = ip.GetAddressBytes();

			Write((byte)bytes.Length);
			Write(bytes);
		}

		public void Write(Enum flag)
		{
			var ut = Enum.GetUnderlyingType(flag.GetType());

			if (ut == _TypeOfSByte)
			{
				Write((byte)1);
				Write(Convert.ToSByte(flag));
			}
			else if (ut == _TypeOfByte)
			{
				Write((byte)2);
				Write(Convert.ToByte(flag));
			}
			else if (ut == _TypeOfShort)
			{
				Write((byte)3);
				Write(Convert.ToInt16(flag));
			}
			else if (ut == _TypeOfUShort)
			{
				Write((byte)4);
				Write(Convert.ToUInt16(flag));
			}
			else if (ut == _TypeOfInt)
			{
				Write((byte)5);
				Write(Convert.ToInt32(flag));
			}
			else if (ut == _TypeOfUInt)
			{
				Write((byte)6);
				Write(Convert.ToUInt32(flag));
			}
			else if (ut == _TypeOfLong)
			{
				Write((byte)7);
				Write(Convert.ToInt64(flag));
			}
			else if (ut == _TypeOfULong)
			{
				Write((byte)8);
				Write(Convert.ToUInt64(flag));
			}
			else
			{
				Write((byte)0);
			}
		}

		public override void Write(string value)
		{
			InternalWrite(value);
		}

		private void InternalWrite(string value)
		{
			base.Write(value ?? "\0");
		}

		public byte[] ToArray()
		{
			return ((MemoryStream)BaseStream).ToArray();
		}

		public void Trace()
		{
			var pos = Position;

			Position = 0;

			using (TextWriter traceLog = File.CreateText("PortalSend.log"))
			{
				traceLog.WriteLine();
				traceLog.WriteLine("Packet: {0} ({1} bytes)", PacketID, Length);
				traceLog.WriteLine("From: #{0}", ClientID);
				traceLog.WriteLine();
				Portal.FormatBuffer(traceLog, BaseStream, Length);
				traceLog.WriteLine();
			}

			Position = pos;
		}
	}
}