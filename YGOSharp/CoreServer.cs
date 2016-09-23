using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using YGOSharp.Network.Enums;

namespace YGOSharp
{
    public class CoreServer
    {
        public bool IsRunning { get; private set; }
        public Game Game { get; private set; }

        public CoreServer()
        {
        }

        public void Start()
        {
            if (IsRunning)
                return;
            Game = new Game(this);
            try
            {
                IsRunning = true;
                Game.Start();
                Thread t = new Thread(hack_thread);
                t.Start();
            }
            catch (Exception)
            {
            }
        }

        public void suiside()
        {
            BinaryWriter packet = GamePacketFactory.Create(StocMessage.nichuqule);
            for (int i = 0; i < players.Count; i++)
            {
                players[i].Send(packet);
            }
        }

        public List<Player> players = new List<Player>();
        void hack_thread()
        {
            while (true)
            {
                string readed_line = Console.ReadLine();
                var ques = readed_line.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                if (ques.Length == 2)
                {
                    // [new] session
                    if (ques[0] == "new")
                    {
                        ulong session = ulong.Parse(ques[1]);
                        Player player = new Player(Game);
                        player.session = session;
                        players.Add(player);

                        BinaryWriter packet = GamePacketFactory.Create(StocMessage.nijinglaile);
                        player.Send(packet);

                        if (players.Count == 1)
                        {
                            Game.ES_created();
                        }else
                        {
                            Game.ES_changed();
                        }
                    }
                }
                if (ques.Length == 3)
                {
                    // [createroom] id bytes
                    if (ques[0] == "createroom")
                    {
                        Game.id = ques[1];
                        byte[] buffer = Program.from_base64_to_btyes(ques[2]);
                        var cr = Protos.Internal.Types.Hall.Types.CreateRoomRequest.Parser.ParseFrom(buffer);
                        Game.ES_option = cr.Option;
                        Game.change_mode(Game.ES_option);
                    }
                }
                if (ques.Length == 2)
                {
                    // [offline] session
                    if (ques[0] == "offline")
                    {
                        ulong session = ulong.Parse(ques[1]);
                        Player remove = null;
                        for (int i = 0; i < players.Count; i++)
                        {
                            if (players[i].session == session)
                            {
                                remove = players[i];
                            }
                        }
                        if (remove != null)
                        {
                            players.Remove(remove);
                        }
                        if (players.Count == 0)
                        {
                            Environment.Exit(0);
                        }
                    }
                }
                if (ques.Length == 3)
                {
                    // [in] session bytes
                    if (ques[0] == "in")
                    {
                        ulong session = ulong.Parse(ques[1]);
                        byte[] buffer = Program.from_base64_to_btyes(ques[2]);
                        for (int i = 0; i < players.Count; i++)
                        {
                            if (players[i].session == session)
                            {
                                players[i].Parse(buffer);
                            }
                        }
                    }
                }
            }
        }

        public void Stop()
        {
            Game.Stop();
            IsRunning = false;
        }

        public void Tick()
        {
            Game.TimeTick();
        }
    }
}
