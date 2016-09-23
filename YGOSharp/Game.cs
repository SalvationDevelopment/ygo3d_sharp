﻿using System;
using System.Collections.Generic;
using System.IO;
using YGOSharp.Network.Enums;
using YGOSharp.Network.Utils;
using YGOSharp.OCGWrapper;
using YGOSharp.OCGWrapper.Enums;

namespace YGOSharp
{
    public class Game : IGame
    {
        public const int DEFAULT_LIFEPOINTS = 8000;
        public const int DEFAULT_START_HAND = 5;
        public const int DEFAULT_DRAW_COUNT = 1;
        public const int DEFAULT_TIMER = 2400;

        public Banlist Banlist { get; private set; }
        public int Mode { get; set; }
        public int Rule { get; set; }
        public int StartLp { get; set; }
        public int StartHand { get; set; }
        public int DrawCount { get; set; }
        public int Timer { get; set; }
        public bool EnablePriority { get; set; }
        public bool NoCheckDeck { get; set; }
        public bool NoShuffleDeck { get; set; }
        public bool IsMatch { get; private set; }
        public bool IsTag { get; private set; }
        public bool IsTpSelect { get; private set; }

        public GameState State { get; private set; }
        public DateTime SideTimer { get; private set; }
        public DateTime TpTimer { get; private set; }
        public DateTime RpsTimer { get; private set; }
        public int TurnCount { get; set; }
        public int CurrentPlayer { get; set; }
        public int[] LifePoints { get; set; }

        public Player[] Players { get; private set; }
        public Player[] CurPlayers { get; private set; }
        public bool[] IsReady { get; private set; }
        public List<Player> Observers { get; private set; }
        public Player HostPlayer { get; private set; }

        public int Winner { get; private set; }
        public int[] MatchResults { get; private set; }
        public int[] MatchReasons { get; private set; }
        public int DuelCount;

        private CoreServer _server;
        private Duel _duel;
        private GameAnalyser _analyser;
        private int[] _handResult;
        private int _startplayer;
        private int _lastresponse;

        private int[] _timelimit;
        private DateTime? _time;

        private bool _matchKill;
        private bool _swapped;
        internal string id;
        internal Protos.Common.Types.RoomOption ES_option;

        public event Action<object, EventArgs> OnNetworkReady;
        public event Action<object, EventArgs> OnNetworkEnd;
        public event Action<object, EventArgs> OnGameStart;
        public event Action<object, EventArgs> OnGameEnd;
        public event Action<object, EventArgs> OnDuelEnd;
        public event Action<object, PlayerEventArgs> OnPlayerJoin;
        public event Action<object, PlayerEventArgs> OnPlayerLeave;
        public event Action<object, PlayerMoveEventArgs> OnPlayerMove;
        public event Action<object, PlayerEventArgs> OnPlayerReady;
        public event Action<object, PlayerChatEventArgs> OnPlayerChat;

        public Game(CoreServer server)
        {
            change_mode(0);

            _server = server;
            _analyser = new GameAnalyser(this);
        }

        private void change_mode(int mode)
        {
            State = GameState.Lobby;
            Mode = mode;
            Rule = 0;
            IsMatch = Mode == 1;
            IsTag = Mode == 2;
            CurrentPlayer = 0;
            LifePoints = new int[2];
            Players = new Player[IsTag ? 4 : 2];
            CurPlayers = new Player[2];
            IsReady = new bool[IsTag ? 4 : 2];
            _handResult = new int[2];
            _timelimit = new int[2];
            Winner = -1;
            MatchResults = new int[3];
            MatchReasons = new int[3];
            Observers = new List<Player>();
            int lfList = 0;
            if (lfList >= 0 && lfList < BanlistManager.Banlists.Count)
                Banlist = BanlistManager.Banlists[lfList];
            StartLp = DEFAULT_LIFEPOINTS;
            LifePoints[0] = StartLp;
            LifePoints[1] = StartLp;
            StartHand = DEFAULT_START_HAND;
            DrawCount = DEFAULT_DRAW_COUNT;
            EnablePriority = false;
            NoCheckDeck = false;
            NoShuffleDeck = false;
            Timer = DEFAULT_TIMER;
        }

        public void change_mode(Protos.Common.Types.RoomOption op)
        {
            switch (op.Mode)
            {
                case Protos.Common.Types.RoomMode.Single:
                    change_mode(0);
                    break;
                case Protos.Common.Types.RoomMode.Match:
                    change_mode(1);
                    break;
                case Protos.Common.Types.RoomMode.Tag:
                    change_mode(2);
                    break;
                default:
                    break;
            }
        }

        public void SetRules(BinaryReader packet)
        {
            uint lfList = packet.ReadUInt32();
            if (lfList >= 0 && lfList < BanlistManager.Banlists.Count)
                Banlist = BanlistManager.Banlists[BanlistManager.GetIndex(lfList)];
            Rule = packet.ReadByte();
            Mode = packet.ReadByte();
            IsMatch = Mode == 1;
            IsTag = Mode == 2;
            IsReady = new bool[IsTag ? 4 : 2];
            Players = new Player[IsTag ? 4 : 2];
            EnablePriority = packet.ReadByte() > 0;
            NoCheckDeck = packet.ReadByte() > 0;
            NoShuffleDeck = packet.ReadByte() > 0;
            //C++ padding: 5 bytes + 3 bytes = 8 bytes
            for (int i = 0; i < 3; i++)
                packet.ReadByte();
            int lifePoints = packet.ReadInt32();
            LifePoints[0] = lifePoints;
            LifePoints[1] = lifePoints;
            StartHand = packet.ReadByte();
            DrawCount = packet.ReadByte();
            Timer = packet.ReadInt16();
        }

        public void Start()
        {
            OnNetworkReady?.Invoke(this, EventArgs.Empty);
        }

        public void Stop()
        {
            OnNetworkEnd?.Invoke(this, EventArgs.Empty);
        }

        public void SendToAll(BinaryWriter packet)
        {
            SendToPlayers(packet);
            SendToObservers(packet);
        }

        public void SendToAllBut(BinaryWriter packet, Player except)
        {
            foreach (Player player in Players)
                if (player != null && !player.Equals(except))
                    player.Send(packet);
            foreach (Player player in Observers)
                if (!player.Equals(except))
                    player.Send(packet);
        }

        public void SendToAllBut(BinaryWriter packet, int except)
        {
            if (except < CurPlayers.Length)
                SendToAllBut(packet, CurPlayers[except]);
            else
                SendToAll(packet);
        }

        public void SendToPlayers(BinaryWriter packet)
        {
            foreach (Player player in Players)
                if (player != null)
                    player.Send(packet);
        }

        public void SendToObservers(BinaryWriter packet)
        {
            foreach (Player player in Observers)
                player.Send(packet);
        }

        public void SendToTeam(BinaryWriter packet, int team)
        {
            if (!IsTag)
                Players[team].Send(packet);
            else if (team == 0)
            {
                Players[0].Send(packet);
                Players[1].Send(packet);
            }
            else
            {
                Players[2].Send(packet);
                Players[3].Send(packet);
            }
        }

        public void AddPlayer(Player player)
        {
            if (State != GameState.Lobby)
            {
                player.Type = (int)PlayerType.Observer;
                if (State != GameState.End)
                {
                    SendJoinGame(player);
                    player.SendTypeChange();
                    player.Send(GamePacketFactory.Create(StocMessage.DuelStart));
                    Observers.Add(player);
                }
                OnPlayerJoin?.Invoke(this, new PlayerEventArgs(player));
                return;
            }

            if (HostPlayer == null)
                HostPlayer = player;

            int pos = GetAvailablePlayerPos();
            if (pos != -1)
            {
                BinaryWriter enter = GamePacketFactory.Create(StocMessage.HsPlayerEnter);
                enter.WriteUnicode(player.Name, 20);
                enter.Write((byte)pos);
                //padding
                enter.Write((byte)0);
                SendToAll(enter);

                Players[pos] = player;
                IsReady[pos] = false;
                player.Type = pos;
            }
            else
            {
                BinaryWriter watch = GamePacketFactory.Create(StocMessage.HsWatchChange);
                watch.Write((short)(Observers.Count + 1));
                SendToAll(watch);

                player.Type = (int)PlayerType.Observer;
                Observers.Add(player);
            }

            SendJoinGame(player);

            player.SendTypeChange();

            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i] != null)
                {
                    BinaryWriter enter = GamePacketFactory.Create(StocMessage.HsPlayerEnter);
                    enter.WriteUnicode(Players[i].Name, 20);
                    enter.Write((byte)i);
                    //padding
                    enter.Write((byte)0);
                    player.Send(enter);

                    if (IsReady[i])
                    {
                        BinaryWriter change = GamePacketFactory.Create(StocMessage.HsPlayerChange);
                        change.Write((byte)((i << 4) + (int)PlayerChange.Ready));
                        player.Send(change);
                    }
                }
            }

            if (Observers.Count > 0)
            {
                BinaryWriter nwatch = GamePacketFactory.Create(StocMessage.HsWatchChange);
                nwatch.Write((short)Observers.Count);
                player.Send(nwatch);
            }

            OnPlayerJoin?.Invoke(this, new PlayerEventArgs(player));
        }

        public void RemovePlayer(Player player)
        {
            if (player.Equals(HostPlayer) && State == GameState.Lobby)
            {
                _server.Stop();
                return;
            }

            if (player.Type == (int)PlayerType.Observer)
            {
                Observers.Remove(player);
                if (State == GameState.Lobby)
                {
                    BinaryWriter nwatch = GamePacketFactory.Create(StocMessage.HsWatchChange);
                    nwatch.Write((short)Observers.Count);
                    SendToAll(nwatch);
                }
                player.Disconnect();
            }
            else if (State == GameState.Lobby)
            {
                Players[player.Type] = null;
                IsReady[player.Type] = false;
                BinaryWriter change = GamePacketFactory.Create(StocMessage.HsPlayerChange);
                change.Write((byte)((player.Type << 4) + (int)PlayerChange.Leave));
                SendToAll(change);
                player.Disconnect();
            }
            else
                Surrender(player, 4, true);

            OnPlayerLeave?.Invoke(this, new PlayerEventArgs(player));
        }

        public void MoveToRandom(Player player)
        {
            if ((player.Type < IsReady.Length && IsReady[player.Type] == false) || player.Type == (int)PlayerType.Observer)
            {
                if (GetAvailablePlayerPos() == -1)
                {
                    if (player.Type != (int)PlayerType.Observer)
                    {
                        MoveToObserver(player);
                    }
                }
                else
                {
                    if (player.Type == (int)PlayerType.Observer)
                    {
                        MoveToDuelist(player);
                    }
                    else
                    {
                        if (IsTag == false)
                        {
                            MoveToObserver(player);
                        }
                        else
                        {
                            int pos = (player.Type + 1) % 4;
                            while (Players[pos] != null)
                                pos = (pos + 1) % 4;
                            if (player.Type > pos)
                            {
                                MoveToObserver(player);
                            }
                            else
                            {
                                MoveToDuelist(player);
                            }
                        }

                    }
                }
            }
        }

        public void MoveToDuelist(Player player)
        {
            if (State != GameState.Lobby)
                return;
            int pos = GetAvailablePlayerPos();
            if (pos == -1)
                return;

            int oldType = player.Type;

            if (player.Type != (int)PlayerType.Observer)
            {
                if (!IsTag || IsReady[player.Type])
                    return;

                pos = (player.Type + 1) % 4;
                while (Players[pos] != null)
                    pos = (pos + 1) % 4;

                BinaryWriter change = GamePacketFactory.Create(StocMessage.HsPlayerChange);
                change.Write((byte)((player.Type << 4) + pos));
                SendToAll(change);

                Players[player.Type] = null;
                Players[pos] = player;
                player.Type = pos;
                player.SendTypeChange();
            }
            else
            {
                Observers.Remove(player);
                Players[pos] = player;
                player.Type = pos;

                BinaryWriter enter = GamePacketFactory.Create(StocMessage.HsPlayerEnter);
                enter.WriteUnicode(player.Name, 20);
                enter.Write((byte)pos);
                //padding
                enter.Write((byte)0);
                SendToAll(enter);

                BinaryWriter nwatch = GamePacketFactory.Create(StocMessage.HsWatchChange);
                nwatch.Write((short)Observers.Count);
                SendToAll(nwatch);

                player.SendTypeChange();
            }
            OnPlayerMove?.Invoke(this, new PlayerMoveEventArgs(player, oldType));
        }

        public void MoveToObserver(Player player)
        {
            if (State != GameState.Lobby)
                return;
            if (player.Type == (int)PlayerType.Observer)
                return;
            if (IsReady[player.Type])
                return;

            int oldType = player.Type;

            Players[player.Type] = null;
            IsReady[player.Type] = false;
            Observers.Add(player);

            BinaryWriter change = GamePacketFactory.Create(StocMessage.HsPlayerChange);
            change.Write((byte)((player.Type << 4) + (int)PlayerChange.Observe));
            SendToAll(change);

            player.Type = (int)PlayerType.Observer;
            player.SendTypeChange();

            if (OnPlayerMove != null)
            {
                OnPlayerMove(this, new PlayerMoveEventArgs(player, oldType));
            }
        }

        public void Chat(Player player, string msg)
        {
            BinaryWriter packet = GamePacketFactory.Create(StocMessage.Chat);
            packet.Write((short)player.Type);
            string to_send = player.Name + ": " + msg;
            packet.WriteUnicode(to_send, to_send.Length + 1);
            SendToAll(packet);
            OnPlayerChat?.Invoke(this, new PlayerChatEventArgs(player, msg));
        }

        public void CustomMessage(Player player, string msg)
        {
            string finalmsg = msg;
            BinaryWriter packet = GamePacketFactory.Create(StocMessage.Chat);
            packet.Write((short)PlayerType.Yellow);
            packet.WriteUnicode(finalmsg, finalmsg.Length + 1);
            SendToAllBut(packet, player);
        }

        public void SetReady(Player player, bool ready)
        {
            if (State != GameState.Lobby)
                return;
            if (player.Type == (int)PlayerType.Observer)
                return;
            if (IsReady[player.Type] == ready)
                return;

            if (ready)
            {
                bool ocg = Rule == 0 || Rule == 2;
                bool tcg = Rule == 1 || Rule == 2;
                int result = 1;
                if (player.Deck != null)
                {
                    result = NoCheckDeck ? 0 : player.Deck.Check(Banlist, ocg, tcg);
                }
                if (result != 0)
                {
                    BinaryWriter rechange = GamePacketFactory.Create(StocMessage.HsPlayerChange);
                    rechange.Write((byte)((player.Type << 4) + (int)(PlayerChange.NotReady)));
                    player.Send(rechange);
                    BinaryWriter error = GamePacketFactory.Create(StocMessage.ErrorMsg);
                    error.Write((byte)2); // ErrorMsg.DeckError
                    // C++ padding: 1 byte + 3 bytes = 4 bytes
                    for (int i = 0; i < 3; i++)
                        error.Write((byte)0);
                    error.Write(result);
                    player.Send(error);
                    return;
                }
            }

            IsReady[player.Type] = ready;

            BinaryWriter change = GamePacketFactory.Create(StocMessage.HsPlayerChange);
            change.Write((byte)((player.Type << 4) + (int)(ready ? PlayerChange.Ready : PlayerChange.NotReady)));
            SendToAll(change);

            OnPlayerReady?.Invoke(this, new PlayerEventArgs(player));
        }

        public void KickPlayer(Player player, int pos)
        {
            if (State != GameState.Lobby)
                return;
            if (pos >= Players.Length || !player.Equals(HostPlayer) || player.Equals(Players[pos]) || Players[pos] == null)
                return;
            RemovePlayer(Players[pos]);
        }

        public void StartDuel(Player player)
        {
            if (State != GameState.Lobby)
                return;
            if (!player.Equals(HostPlayer))
                return;
            for (int i = 0; i < Players.Length; i++)
            {
                if (!IsReady[i])
                    return;
                if (Players[i] == null)
                    return;
            }

            State = GameState.Hand;
            SendToAll(GamePacketFactory.Create(StocMessage.DuelStart));

            SendHand();

            OnGameStart?.Invoke(this, EventArgs.Empty);
        }

        public void HandResult(Player player, int result)
        {
            if (State != GameState.Hand)
                return;
            if (player.Type == (int)PlayerType.Observer)
                return;
            if (result < 1 || result > 3)
                return;
            if (IsTag && player.Type != 0 && player.Type != 2)
                return;
            int type = player.Type;
            if (IsTag && player.Type == 2)
                type = 1;
            if (_handResult[type] != 0)
                return;
            _handResult[type] = result;
            if (_handResult[0] != 0 && _handResult[1] != 0)
            {
                BinaryWriter packet = GamePacketFactory.Create(StocMessage.HandResult);
                packet.Write((byte)_handResult[0]);
                packet.Write((byte)_handResult[1]);
                SendToTeam(packet, 0);
                SendToObservers(packet);

                packet = GamePacketFactory.Create(StocMessage.HandResult);
                packet.Write((byte)_handResult[1]);
                packet.Write((byte)_handResult[0]);
                SendToTeam(packet, 1);

                if (_handResult[0] == _handResult[1])
                {
                    _handResult[0] = 0;
                    _handResult[1] = 0;
                    SendHand();
                    return;
                }
                if ((_handResult[0] == 1 && _handResult[1] == 2) ||
                    (_handResult[0] == 2 && _handResult[1] == 3) ||
                    (_handResult[0] == 3 && _handResult[1] == 1))
                    _startplayer = IsTag ? 2 : 1;
                else
                    _startplayer = 0;
                State = GameState.Starting;
                Players[_startplayer].Send(GamePacketFactory.Create(StocMessage.SelectTp));
                TpTimer = DateTime.UtcNow;
            }
        }

        public void TpResult(Player player, bool result)
        {
            if (State != GameState.Starting)
                return;
            if (player.Type != _startplayer)
                return;

            _swapped = false;
            if (result && player.Type == (IsTag ? 2 : 1) || !result && player.Type == 0)
            {
                _swapped = true;
                if (IsTag)
                {
                    Player temp = Players[0];
                    Players[0] = Players[2];
                    Players[2] = temp;

                    temp = Players[1];
                    Players[1] = Players[3];
                    Players[3] = temp;

                    Players[0].Type = 0;
                    Players[1].Type = 1;
                    Players[2].Type = 2;
                    Players[3].Type = 3;
                }
                else
                {
                    Player temp = Players[0];
                    Players[0] = Players[1];
                    Players[1] = temp;
                    Players[0].Type = 0;
                    Players[1].Type = 1;
                }
            }
            CurPlayers[0] = Players[0];
            CurPlayers[1] = Players[IsTag ? 3 : 1];

            State = GameState.Duel;
            int seed = Environment.TickCount;
            _duel = Duel.Create((uint)seed);
            Random rand = new Random(seed);

            _duel.SetAnalyzer(_analyser.Analyse);
            _duel.SetErrorHandler(HandleError);

            _duel.InitPlayers(StartLp, StartHand, DrawCount);

            int opt = 0;
            if (EnablePriority)
                opt += 0x08;
            if (NoShuffleDeck)
                opt += 0x10;
            if (IsTag)
                opt += 0x20;

            for (int i = 0; i < Players.Length; i++)
            {
                Player dplayer = Players[i == 2 ? 3 : (i == 3 ? 2 : i)];
                int pid = i;
                if (IsTag)
                    pid = i >= 2 ? 1 : 0;
                if (!NoShuffleDeck)
                {
                    List<int> cards = ShuffleCards(rand, dplayer.Deck.Main);
                    foreach (int id in cards)
                    {
                        if (IsTag && (i == 1 || i == 3))
                            _duel.AddTagCard(id, pid, CardLocation.Deck);
                        else
                            _duel.AddCard(id, pid, CardLocation.Deck);
                    }
                }
                else
                {
                    for (int j = dplayer.Deck.Main.Count - 1; j >= 0; j--)
                    {
                        int id = dplayer.Deck.Main[j];
                        if (IsTag && (i == 1 || i == 3))
                            _duel.AddTagCard(id, pid, CardLocation.Deck);
                        else
                            _duel.AddCard(id, pid, CardLocation.Deck);
                    }
                }
                foreach (int id in dplayer.Deck.Extra)
                {
                    if (IsTag && (i == 1 || i == 3))
                        _duel.AddTagCard(id, pid, CardLocation.Extra);
                    else
                        _duel.AddCard(id, pid, CardLocation.Extra);
                }
            }

            _duel.Start(opt);

            TurnCount = 0;
            LifePoints[0] = StartLp;
            LifePoints[1] = StartLp;
            TimeReset();

            Process();
        }

        public void Surrender(Player player, int reason, bool force = false)
        {
            if (State == GameState.End)
                return;
            if (!force && State != GameState.Duel)
                return;
            if (player.Type == (int)PlayerType.Observer)
                return;
            int team = player.Type;
            if (IsTag)
                team = player.Type >= 2 ? 1 : 0;
            else if (State == GameState.Hand)
                team = 1 - team;

            miaowu.GameMessage win = new miaowu.GameMessage();
            win.FuctionIndex = (int)GameMessage.Win;
            win.Params.writer.Write((byte)(1 - team));
            win.Params.writer.Write((byte)reason);
            SendToAll(win.get_ygo_sharp_binary());

            MatchSaveResult(1 - team, reason);

            EndDuel(reason == 4);
        }

        public int WaitForResponse()
        {
            WaitForResponse(_lastresponse);
            return _lastresponse;
        }

        public void WaitForResponse(int player)
        {
            _lastresponse = player;
            CurPlayers[player].State = PlayerState.Response;
            TimeStart();
            BinaryWriter packet = GamePacketFactory.Create(StocMessage.TimeLimit);
            packet.Write((byte)player);
            packet.Write((byte)0); // C++ padding
            packet.Write((short)_timelimit[player]);
            SendToPlayers(packet);
        }

        public void SetResponse(int resp)
        {
            TimeStop();
            _duel.SetResponse(resp);
        }

        public void SetResponse(byte[] resp)
        {
            TimeStop();
            _duel.SetResponse(resp);
            Process();
        }

        public void EndDuel(bool force)
        {
            if (State == GameState.End)
            {
                return;
            }

            if (State == GameState.Duel)
            {
                _duel.End();
            }

            if (_swapped)
            {
                _swapped = false;
                if (IsTag)
                {
                    Player temp = Players[0];
                    Players[0] = Players[2];
                    Players[2] = temp;

                    temp = Players[1];
                    Players[1] = Players[3];
                    Players[3] = temp;

                    Players[0].Type = 0;
                    Players[1].Type = 1;
                    Players[2].Type = 2;
                    Players[3].Type = 3;
                }
                else
                {
                    Player temp = Players[0];
                    Players[0] = Players[1];
                    Players[1] = temp;
                    Players[0].Type = 0;
                    Players[1].Type = 1;
                }
            }

            if (IsMatch && !force && !MatchIsEnd())
            {
                IsReady[0] = false;
                IsReady[1] = false;
                State = GameState.Side;
                SideTimer = DateTime.UtcNow;
                SendToPlayers(GamePacketFactory.Create(StocMessage.ChangeSide));
                SendToObservers(GamePacketFactory.Create(StocMessage.WaitingSide));
            }
            else
            {
                CalculateWinner();
                End();
            }
        }

        public void End()
        {
            State = GameState.End;
            SendToAll(GamePacketFactory.Create(StocMessage.DuelEnd));
            OnGameEnd?.Invoke(this, EventArgs.Empty);
        }

        public void TimeReset()
        {
            _timelimit[0] = Timer;
            _timelimit[1] = Timer;
        }

        public void TimeStart()
        {
            _time = DateTime.UtcNow;
        }

        public void TimeStop()
        {
            if (_time != null)
            {
                TimeSpan elapsed = DateTime.UtcNow - _time.Value;
                _timelimit[_lastresponse] -= (int)elapsed.TotalSeconds;
                if (_timelimit[_lastresponse] < 0)
                    _timelimit[_lastresponse] = 0;
                _time = null;
            }
        }

        public void TimeTick()
        {
            if (State == GameState.Duel)
            {
                if (_time != null)
                {
                    TimeSpan elapsed = DateTime.UtcNow - _time.Value;
                    if ((int)elapsed.TotalSeconds > _timelimit[_lastresponse])
                    {
                        Surrender(CurPlayers[_lastresponse], 3);
                    }
                }
            }

            if (State == GameState.Side)
            {
                TimeSpan elapsed = DateTime.UtcNow - SideTimer;

                if (elapsed.TotalMilliseconds >= 120000)
                {
                    if (!IsReady[0] && !IsReady[1])
                    {
                        EndDuel(true);
                        return;
                    }

                    Surrender(!IsReady[0] ? Players[0] : Players[1], 3, true);
                }
            }

            if (State == GameState.Starting)
            {
                if (IsTpSelect)
                {
                    TimeSpan elapsed = DateTime.UtcNow - TpTimer;

                    if (elapsed.TotalMilliseconds >= 30000)
                    {
                        Surrender(CurPlayers[_startplayer], 3, true);
                    }

                }
            }
            if (State == GameState.Hand)
            {
                TimeSpan elapsed = DateTime.UtcNow - RpsTimer;

                if ((int)elapsed.TotalMilliseconds >= 60000)
                {
                    if (_handResult[0] != 0)
                        Surrender(Players[IsTag ? 2 : 1], 3, true);
                    else if (_handResult[1] != 0)
                        Surrender(Players[0], 3, true);
                    else
                        EndDuel(true);
                }
            }
        }

        public void MatchSaveResult(int player, int reason)
        {
            if (player < 2 && _swapped)
                player = 1 - player;
            if (player < 2)
                _startplayer = 1 - player;
            else
                _startplayer = 1 - _startplayer;
            MatchResults[DuelCount] = player;
            MatchReasons[DuelCount++] = reason;

            OnDuelEnd?.Invoke(this, EventArgs.Empty);
        }

        public void MatchKill()
        {
            _matchKill = true;
        }

        public bool MatchIsEnd()
        {
            if (_matchKill)
                return true;
            int[] wins = new int[3];
            for (int i = 0; i < DuelCount; i++)
                wins[MatchResults[i]]++;
            return wins[0] == 2 || wins[1] == 2 || wins[0] + wins[1] + wins[2] == 3;
        }

        public void MatchSide()
        {
            if (IsReady[0] && IsReady[1])
            {
                State = GameState.Starting;
                IsTpSelect = true;
                TpTimer = DateTime.UtcNow;
                TimeReset();
                Players[_startplayer].Send(GamePacketFactory.Create(StocMessage.SelectTp));
            }
        }

        private int GetAvailablePlayerPos()
        {
            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i] == null)
                    return i;
            }
            return -1;
        }

        private void SendHand()
        {
            RpsTimer = DateTime.UtcNow;
            BinaryWriter hand = GamePacketFactory.Create(StocMessage.SelectHand);
            if (IsTag)
            {
                Players[0].Send(hand);
                Players[2].Send(hand);
            }
            else
                SendToPlayers(hand);
        }

        private void Process()
        {
            int result = _duel.Process();
            switch (result)
            {
                case -1:
                    EndDuel(true);
                    break;
                case 2:
                    EndDuel(false);
                    break;
            }
        }

        private void SendJoinGame(Player player)
        {
            BinaryWriter join = GamePacketFactory.Create(StocMessage.JoinGame);
            join.WriteUnicode(Banlist.name, 20);
            join.Write((byte)Rule);
            join.Write((byte)Mode);
            join.Write(EnablePriority);
            join.Write(NoCheckDeck);
            join.Write(NoShuffleDeck);
            // C++ padding: 5 bytes + 3 bytes = 8 bytes
            for (int i = 0; i < 3; i++)
                join.Write((byte)0);
            join.Write(StartLp);
            join.Write((byte)StartHand);
            join.Write((byte)DrawCount);
            join.Write((short)Timer);
            player.Send(join);

            if (State != GameState.Lobby)
                SendDuelingPlayers(player);
        }

        private void SendDuelingPlayers(Player player)
        {
            for (int i = 0; i < Players.Length; i++)
            {
                BinaryWriter enter = GamePacketFactory.Create(StocMessage.HsPlayerEnter);
                int id = i;
                if (_swapped)
                {
                    if (IsTag)
                    {
                        if (i == 0 || id == 1)
                            id = i + 2;
                        else
                            id = i - 2;
                    }
                    else
                        id = 1 - i;
                }
                enter.WriteUnicode(Players[id].Name, 20);
                enter.Write((byte)i);
                //padding
                enter.Write((byte)0);
                player.Send(enter);
            }
        }

        private void HandleError(string error)
        {
            BinaryWriter packet = GamePacketFactory.Create(StocMessage.Chat);
            packet.Write((short)PlayerType.Observer);
            packet.WriteUnicode(error, error.Length + 1);
            SendToAll(packet);

            File.WriteAllText("lua_" + DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss") + ".txt", error);
        }

        private static List<int> ShuffleCards(Random rand, IEnumerable<int> cards)
        {
            List<int> shuffled = new List<int>(cards);
            for (int i = shuffled.Count - 1; i > 0; --i)
            {
                int pos = rand.Next(i + 1);
                int tmp = shuffled[i];
                shuffled[i] = shuffled[pos];
                shuffled[pos] = tmp;
            }
            return shuffled;
        }

        private void CalculateWinner()
        {
            int winner = -1;
            if (DuelCount > 0)
            {
                if (!_matchKill && DuelCount != 1)
                {
                    int[] wins = new int[3];
                    for (int i = 0; i < DuelCount; i++)
                        wins[MatchResults[i]]++;
                    if (wins[0] > wins[1])
                        winner = 0;
                    else if (wins[1] > wins[0])
                        winner = 1;
                    else
                        winner = 2;
                }
                else
                    winner = MatchResults[DuelCount - 1];
            }

            Winner = winner;
        }

        public void ES_created()
        {
            var package = new Protos.Hall.Types.RoomCreated()
            {
                Room = new Protos.Common.Types.Room()
                {
                    Id = ulong.Parse(id),
                    Option = ES_option,
                    State = (this.State != GameState.Duel) ? Protos.Common.Types.RoomState.Ready : Protos.Common.Types.RoomState.Duel,
                }
            };

            for (int i = 0; i < _server.players.Count; i++)
            {
                package.Room.Players.Add(_server.players[i].Name);
            }

            var pkg = String.Format("{0} {1}", "ES_created", Program.from_byte_to_base64(ProtobufHelper.Marshal(package)));
            Console.WriteLine(pkg);
        }

        public void ES_changed()
        {
            var package = new Protos.Hall.Types.RoomStateChanged()
            {
                Room = new Protos.Common.Types.Room()
                {
                    Id = ulong.Parse(id),
                    Option = ES_option,
                    State = (this.State != GameState.Duel) ? Protos.Common.Types.RoomState.Ready : Protos.Common.Types.RoomState.Duel,
                }
            };

            for (int i = 0; i < _server.players.Count; i++)
            {
                package.Room.Players.Add(_server.players[i].Name);
            }

            var pkg = String.Format("{0} {1}", "ES_changed", Program.from_byte_to_base64(ProtobufHelper.Marshal(package)));
            Console.WriteLine(pkg);
        }

        public void ES_userexit(Player p)
        {
            var pkg = String.Format("{0} {1}", "ES_userexit", p.session.ToString());
            Console.WriteLine(pkg);
        }

    }
}