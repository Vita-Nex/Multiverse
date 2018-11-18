#region Header
//   Vorspire    _,-'/-'/  PortalPacketWriter.cs
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

		public ushort PacketID { get; private set; }
		public ushort ClientID { get; private set; }

		public int Length { get { return (int)BaseStream.Length; } set { BaseStream.SetLength(value); } }

		public int Position { get { return (int)BaseStream.Position; } set { BaseStream.Position = value; } }

		public PortalPacketWriter(ushort id, ushort clientID)
			: base(new PortalStream(PortalPacket.MinSize), Encoding.UTF8)
		{
			PacketID = id;
			ClientID = clientID;

			Write(PacketID);
			Write(ClientID);
			Write(Length);
		}

		public void UpdateHeader(ushort? clientID, int? length)
		{
			if (clientID == null && length == null)
			{
				return;
			}

			if (clientID != null)
			{
				ClientID = clientID.Value;
			}

			if (length != null)
			{
				Length = length.Value;
			}

			var offset = Position;

			Position = 2;

			Write(ClientID);
			Write(Length);

			Position = offset;
		}

		public void UpdateClientID(ushort clientID)
		{
			UpdateHeader(clientID, null);
		}

		public void UpdateLength(int length)
		{
			UpdateHeader(null, length);
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
			Write((ushort)date.Year);
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