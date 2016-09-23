using System;
using System.Collections.Generic;
using System.IO;
using YGOSharp.OCGWrapper.Enums;

namespace YGOSharp
{
    public class GameAnalyser
    {
        public Game Game { get; private set; }
        public GameMessage LastMessage { get; private set; }

        public GameAnalyser(Game game)
        {
            Game = game;
        }

        public int Analyse(GameMessage msg, BinaryReader reader, byte[] raw)
        {
            int re = 0;
            miaowu.MessageBundle bundle = new miaowu.MessageBundle();
            bundle.PushBytes(new miaowu.bytes(raw));
            List<miaowu.GameMessage> messages_for_client0 = new List<miaowu.GameMessage>();
            List<miaowu.GameMessage> messages_for_client1 = new List<miaowu.GameMessage>();
            List<miaowu.GameMessage> messages_for_client2 = new List<miaowu.GameMessage>();
            List<miaowu.GameMessage> messages_for_client3 = new List<miaowu.GameMessage>();
            List<miaowu.GameMessage> messages_for_watcher = new List<miaowu.GameMessage>();
            re = message_diver(re, bundle, messages_for_client0, messages_for_client1, messages_for_client2, messages_for_client3, messages_for_watcher);
            for (int i = 0; i < Game.Observers.Count; i++)
            {
                MemoryStream s = new MemoryStream(get_messages_bytes(messages_for_watcher));
                Game.Observers[i].Send(new BinaryWriter(s));
            }
            if (Game.Players.Length > 0 && Game.Players[0] != null)
            {
                MemoryStream s = new MemoryStream(get_messages_bytes(messages_for_client0));
                Game.Players[0].Send(new BinaryWriter(s));
            }
            if (Game.Players.Length > 1 && Game.Players[1] != null)
            {
                MemoryStream s = new MemoryStream(get_messages_bytes(messages_for_client1));
                Game.Players[1].Send(new BinaryWriter(s));
            }
            if (Game.Players.Length > 2 && Game.Players[2] != null)
            {
                MemoryStream s = new MemoryStream(get_messages_bytes(messages_for_client2));
                Game.Players[2].Send(new BinaryWriter(s));
            }
            if (Game.Players.Length > 3 && Game.Players[3] != null)
            {
                MemoryStream s = new MemoryStream(get_messages_bytes(messages_for_client3));
                Game.Players[3].Send(new BinaryWriter(s));
            }


            return re;
        }

        private int message_diver(int re, miaowu.MessageBundle bundle, List<miaowu.GameMessage> messages_for_client0, List<miaowu.GameMessage> messages_for_client1, List<miaowu.GameMessage> messages_for_client2, List<miaowu.GameMessage> messages_for_client3, List<miaowu.GameMessage> messages_for_watcher)
        {
            for (int i = 0; i < bundle.GameMessages.Count; i++)
            {
                miaowu.GameMessage message = bundle.GameMessages[i];
                re = message_driver_2(re, messages_for_client0, messages_for_client1, messages_for_client2, messages_for_client3, messages_for_watcher, message);
            }
            return re;
        }

        private int message_driver_2(int re, List<miaowu.GameMessage> messages_for_client0, List<miaowu.GameMessage> messages_for_client1, List<miaowu.GameMessage> messages_for_client2, List<miaowu.GameMessage> messages_for_client3, List<miaowu.GameMessage> messages_for_watcher, miaowu.GameMessage message)
        {
            GameMessage message_type = (GameMessage)message.FuctionIndex;
            switch (message_type)
            {
                case GameMessage.Win:
                    re = 2;
                    break;
                case GameMessage.Retry:
                case GameMessage.SelectBattleCmd:
                case GameMessage.SelectIdleCmd:
                case GameMessage.SelectEffectYn:
                case GameMessage.SelectYesNo:
                case GameMessage.SelectOption:
                case GameMessage.SelectCard:
                case GameMessage.SelectChain:
                case GameMessage.SelectPlace:
                case GameMessage.SelectPosition:
                case GameMessage.SelectTribute:
                case GameMessage.SortChain:
                case GameMessage.SelectCounter:
                case GameMessage.SelectSum:
                case GameMessage.SelectDisfield:
                case GameMessage.SortCard:
                case GameMessage.AnnounceRace:
                case GameMessage.AnnounceAttrib:
                case GameMessage.AnnounceCard:
                case GameMessage.AnnounceNumber:
                    int player = 0;
                    if ((message.Description & (int)miaowu.sender.send_player_0_operator_0) > 0)
                    {
                        player = 0;
                    }
                    if ((message.Description & (int)miaowu.sender.send_player_0_operator_1) > 0)
                    {
                        player = 0;
                    }
                    if ((message.Description & (int)miaowu.sender.send_player_1_operator_0) > 0)
                    {
                        player = 1;
                    }
                    if ((message.Description & (int)miaowu.sender.send_player_1_operator_1) > 0)
                    {
                        player = 1;
                    }
                    Game.WaitForResponse(player);
                    break;
                default:

                    break;
            }
            //if (message.FuctionIndex == 5)
            //{
            //    re = 2;
            //}
            if ((message.Description & (int)miaowu.sender.send_player_0_operator_0) > 0)
            {
                if (Game.IsTag)
                {
                    messages_for_client0.Add(message);
                }
                else
                {
                    messages_for_client0.Add(message);
                }
            }
            if ((message.Description & (int)miaowu.sender.send_player_0_operator_1) > 0)
            {
                if (Game.IsTag)
                {
                    messages_for_client1.Add(message);
                }
            }
            if ((message.Description & (int)miaowu.sender.send_player_1_operator_0) > 0)
            {
                if (Game.IsTag)
                {
                    messages_for_client2.Add(message);
                }
                else
                {
                    messages_for_client1.Add(message);
                }
            }
            if ((message.Description & (int)miaowu.sender.send_player_1_operator_1) > 0)
            {
                if (Game.IsTag)
                {
                    messages_for_client3.Add(message);
                }
            }
            if ((message.Description & (int)miaowu.sender.send_watcher) > 0)
            {
                messages_for_watcher.Add(message);
            }
            return re;
        }

        private static byte[] get_messages_bytes(List<miaowu.GameMessage> messages_for_client0)
        {
            miaowu.bytes bbbbb = new miaowu.bytes();
            bbbbb.writer.Write((byte)1);
            for (int i = 0; i < messages_for_client0.Count; i++)
            {
                byte[] data = messages_for_client0[i].Params.get();
                bbbbb.writer.Write((UInt16)(data.Length + 1));
                bbbbb.writer.Write((byte)messages_for_client0[i].FuctionIndex);
                bbbbb.writer.Write(data);
            }

            byte[] res = bbbbb.get();
            return res;
        }
    }
}
