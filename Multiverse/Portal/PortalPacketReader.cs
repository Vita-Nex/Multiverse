#region Header
//   Vorspire    _,-'/-'/  PortalPacketReader.cs
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
	public sealed class PortalPacketReader : BinaryReader
	{
		private static readonly byte[] _EmptyBytes = new byte[0];

		private static readonly Type _TypeOfSByte = typeof(SByte);
		private static readonly Type _TypeOfByte = typeof(Byte);
		private static readonly Type _TypeOfShort = typeof(Int16);
		private static readonly Type _TypeOfUShort = typeof(UInt16);
		private static readonly Type _TypeOfInt = typeof(Int32);
		private static readonly Type _TypeOfUInt = typeof(UInt32);
		private static readonly Type _TypeOfLong = typeof(Int64);
		private static readonly Type _TypeOfULong = typeof(UInt64);

		private static TEnum ToEnum<TEnum>(object val) where TEnum : struct
		{
			var flag = default(TEnum);

			if (!typeof(TEnum).IsEnum)
			{
				return flag;
			}

			Enum.TryParse(val.ToString(), out flag);

			return flag;
		}

		public ushort PacketID { get; private set; }
		public ushort ServerID { get; private set; }

		public int Length { get; private set; }

		public int Position { get { return (int)BaseStream.Position; } set { BaseStream.Position = value; } }

		public PortalPacketReader(byte[] buffer)
			: base(new MemoryStream(buffer, false), Encoding.UTF8)
		{
			PacketID = ReadUInt16();
			ServerID = ReadUInt16();
			Length = ReadInt32();
		}

		public byte[] ReadToEnd()
		{
			if (Position >= Length)
			{
				return _EmptyBytes;
			}

			return ReadBytes(Length - Position);
		}

		public DateTime ReadDateTime()
		{
			var year = ReadUInt16();
			var month = ReadByte();
			var day = ReadByte();
			var time = ReadTimeSpan();
			var kind = ReadFlag<DateTimeKind>();

			return new DateTime(year, month, day, time.Hours, time.Minutes, time.Seconds, kind);
		}

		public TimeSpan ReadTimeSpan()
		{
			return TimeSpan.FromMilliseconds(ReadDouble());
		}

		public IPAddress ReadIPAddress()
		{
			var length = ReadByte();
			var bytes = ReadBytes(length);

			return new IPAddress(bytes);
		}

		public TEnum ReadFlag<TEnum>() where TEnum : struct, IConvertible
		{
			var flag = default(TEnum);

			if (!typeof(TEnum).IsEnum)
			{
				return flag;
			}

			switch (ReadByte())
			{
				case 1:
					flag = ToEnum<TEnum>(ReadSByte());
					break;
				case 2:
					flag = ToEnum<TEnum>(ReadByte());
					break;
				case 3:
					flag = ToEnum<TEnum>(ReadInt16());
					break;
				case 4:
					flag = ToEnum<TEnum>(ReadUInt16());
					break;
				case 5:
					flag = ToEnum<TEnum>(ReadInt32());
					break;
				case 6:
					flag = ToEnum<TEnum>(ReadUInt32());
					break;
				case 7:
					flag = ToEnum<TEnum>(ReadInt64());
					break;
				case 8:
					flag = ToEnum<TEnum>(ReadUInt64());
					break;
			}

			return flag;
		}

		public override string ReadString()
		{
			return InternalReadString();
		}
		
		private string InternalReadString()
		{
			var value = base.ReadString();

			if (String.Equals(value, "\0"))
			{
				value = null;
			}

			return value;
		}

		public byte[] GetBuffer()
		{
			return ((MemoryStream)BaseStream).GetBuffer();
		}

		public void Trace()
		{
			var pos = Position;

			Position = 0;

			using (TextWriter traceLog = File.CreateText("PortalReceive.log"))
			{
				traceLog.WriteLine();
				traceLog.WriteLine("Packet: {0} ({1} bytes)", PacketID, Length);
				traceLog.WriteLine("From: #{0}", ServerID);
				traceLog.WriteLine();
				Portal.FormatBuffer(traceLog, BaseStream, Length);
				traceLog.WriteLine();
			}

			Position = pos;
		}
	}
}