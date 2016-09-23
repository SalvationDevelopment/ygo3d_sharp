using System;
using System.Collections.Generic;
using System.IO;
using YGOSharp;
using YGOSharp.Network.Enums;

namespace miaowu
{
    public enum sender
    {
        send_player_0_operator_0 = 1,
        send_player_0_operator_1 = 2,
        send_player_0_operator_2 = 4,
        send_player_1_operator_0 = 8,
        send_player_1_operator_1 = 16,
        send_player_1_operator_2 = 32,
        send_god = 64,
        send_watcher = 128,
    }
    public class bytes
    {
        MemoryStream memstream = null;
        public BinaryReader reader = null;
        public BinaryWriter writer = null;
        public bytes(byte[] raw = null)
        {
            if (raw == null)
            {
                memstream = new MemoryStream();
            }
            else
            {
                memstream = new MemoryStream(raw);
            }
            reader = new BinaryReader(memstream);
            writer = new BinaryWriter(memstream);
        }
        public void set(byte[] raw)
        {
            memstream = new MemoryStream(raw);
            reader = new BinaryReader(memstream);
            writer = new BinaryWriter(memstream);
        }
        public byte[] get()
        {
            byte[] bytes = memstream.ToArray();
            return bytes;
        }
        public int getLength()
        {
            return (int)memstream.Length;
        }
        public override string ToString()
        {
            string return_value = "";
            byte[] bytes = get();
            for (int i = 0; i < bytes.Length; i++)
            {
                return_value += ((int)bytes[i]).ToString();
                if (i < bytes.Length - 1) return_value += ",";
            }
            return return_value;
        }

    }
    public class GameMessage
    {
        public int Description = 0;
        public int FuctionIndex = 0;
        public bytes Params = null;
        public BinaryWriter ParamsWriter = null;
        public BinaryReader ParamsReader = null;
        public GameMessage()
        {
            Description = 0;
            FuctionIndex = 0;
            Params = new bytes();
            ParamsWriter = Params.writer;
            ParamsReader = Params.reader;
        }
        public BinaryWriter get_ygo_sharp_binary()
        {
            BinaryWriter re = GamePacketFactory.Create(StocMessage.GameMsg);
            byte[] data = Params.get();
            re.Write((UInt16)(data.Length + 1));
            re.Write((byte)FuctionIndex);
            re.Write(data);
            return re;
        }
    }
    public class MessageBundle
    {
        private bytes buffer;
        public List<GameMessage> GameMessages = new List<GameMessage>();
        public GameMessage CreateMessage()
        {
            GameMessage m = new GameMessage();
            GameMessages.Add(m);
            return m;
        }
        public bytes ToBytes()
        {
            buffer = new bytes();
            foreach (GameMessage m in GameMessages)
            {
                buffer.writer.Write((UInt32)m.Params.getLength() + 3);
                buffer.writer.Write((byte)m.Description);
                buffer.writer.Write((ushort)m.FuctionIndex);
                buffer.writer.Write(m.Params.get());
            }
            return buffer;
        }
        public override string ToString()
        {
            string return_value = "";
            foreach (GameMessage g in GameMessages)
            {
                return_value += g.Description.ToString() + "::" + g.FuctionIndex.ToString() + "(" + g.Params.ToString() + ");\r\n";
            }
            return return_value;
        }
        public void PushBytes(bytes b)
        {
            b.reader.BaseStream.Seek(0, 0);
            while (true)
            {
                if (b.reader.BaseStream.Position >= b.reader.BaseStream.Length)
                {
                    break;
                }
                try
                {
                    GameMessage a = CreateMessage();
                    UInt32 length = b.reader.ReadUInt32();
                    a.Description = b.reader.ReadByte();
                    a.FuctionIndex = b.reader.ReadUInt16();
                    a.Params = new bytes();
                    for (int i = 0; i < length - 3; i++)
                    {
                        a.Params.writer.Write(b.reader.ReadByte());
                    }

                }
                catch (Exception)
                {

                }
            }
        }
    }
}



