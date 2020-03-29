using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class Player {
  public string Name;
  public IPAddress IP;
  public int Avatar;
  public DateTime LastAccess;
  public ulong ID;
  public TcpClient tcpClient; // Used in client mode to communicate with the server
  public NetworkStream stream; // Used in both client and server to exchange data
  public Thread communicationThread;
  public EventHandler<NetworkManager.ServerMessage> OnServerMessage;
  public EventHandler<NetworkManager.ChatMessage> OnChat;
  public EventHandler<NetworkManager.GameMessage> OnGame;
  public string CurrentGame = "";
  public Game TheGame;
  public StatusOfPlayer Status = StatusOfPlayer.Waiting;
  public bool RemoteManager = false;
  private bool localListenerAlive = true;

  public override string ToString() {
    return "\"" + Name.Trim() + "[" + Avatar + "]" + ID + "\"";
  }

  public Player(string name, int avatar) {
    try {
      string pip = new WebClient().DownloadString("http://ipinfo.io/ip");
      if (!IPAddress.TryParse(pip.Trim(), out IP))
        IP = IPAddress.Parse("127.0.0.1");
    } catch (Exception) {
      IP = IPAddress.Parse("127.0.0.1");
    }
    Name = name.Trim();
    if (Name.Length > 64) Name = Name.Substring(0, 64);
    Avatar = avatar;
    LastAccess = DateTime.Now;
    ID = GenerateID();
  }

  public Player(string name) {
    try {
      string pip = new WebClient().DownloadString("http://ipinfo.io/ip");
      if (!IPAddress.TryParse(pip.Trim(), out IP))
        IP = IPAddress.Parse("127.0.0.1");
    } catch (Exception) {
      IP = IPAddress.Parse("127.0.0.1");
    }
    Name = name.Trim();
    RemoteManager = true;
    if (Name.Length > 64) Name = Name.Substring(0, 64);
    Avatar = 0;
    LastAccess = DateTime.Now;
    ID = GenerateID();
  }

/*
 * ***** Player data *****
1 byte len name
n bytes for encoded string name
1 byte avatar
1 byte len of IPAddr
n bytes for encoded string IP
8 bytes for ID
1 byte to check if we are a remote connection
*/

  public Player(byte[] data, int pos) {
    bool full = false;
    int nameL = data[pos];
    if (nameL > 128) {
      full = true;
      nameL -= 128;
    }
    Name = Encoding.UTF8.GetString(data, pos + 1, nameL).Trim();
    if (Name.Length > 64) Name = Name.Substring(0, 64);
    Avatar = data[pos + 1 + nameL];
    int ipL = data[pos + nameL + 2];
    string ip = Encoding.UTF8.GetString(data, pos + nameL + 3, ipL);
    if (!IPAddress.TryParse(ip, out IP))
      IP = IPAddress.Parse("127.0.0.1");
    ID = BitConverter.ToUInt64(data, pos + 3 + nameL + ipL);
    RemoteManager = data[pos + 3 + nameL + ipL + 8] != 0;
    if (!full) {
      LastAccess = DateTime.Now;
      return;
    }

    pos += 3 + nameL + ipL + 8 + 1;

    long time = ((long)data[pos + 0]) + ((long)data[pos + 1] << 8) + ((long)data[pos + 2] << 16) + ((long)data[pos + 3] << 24) +
          ((long)data[pos + 4] << 32) + ((long)data[pos + 5] << 40) + ((long)data[pos + 6] << 48) + ((long)data[pos + 7] << 56);
    LastAccess = DateTime.FromBinary(time);
    pos += 8;

    int cGameL = data[pos];
    CurrentGame = Encoding.UTF8.GetString(data, pos + 1, cGameL).Trim();
    pos += cGameL + 1;
    Status = (StatusOfPlayer)data[pos];
  }

  internal byte[] Stringify() {
    byte[] nameB = Encoding.UTF8.GetBytes(Name);
    byte[] ipB = Encoding.UTF8.GetBytes(IP.ToString());
    byte[] idB = BitConverter.GetBytes(ID);
    int len = 1 + nameB.Length + 1 + 1 + ipB.Length + idB.Length + 8 + 1;
    byte[] res = new byte[len];
    res[0] = (byte)nameB.Length;
    for (int i = 0; i < nameB.Length; i++)
      res[i+1] = nameB[i];
    res[1 + nameB.Length] = (byte)Avatar;
    res[2 + nameB.Length] = (byte)ipB.Length;
    for (int i = 0; i < ipB.Length; i++)
      res[3 + nameB.Length + i] = ipB[i];
    for (int i = 0; i < 8; i++)
      res[3 + nameB.Length + ipB.Length + i] = idB[i];
    res[3 + nameB.Length + ipB.Length + idB.Length] = (byte)(RemoteManager ? 0xff : 0);
    return res;
  }

  internal byte[] StringifyFull() {
    byte[] nameB = Encoding.UTF8.GetBytes(Name);
    byte[] ipB = Encoding.UTF8.GetBytes(IP.ToString());
    byte[] idB = BitConverter.GetBytes(ID);
    byte[] cGameB = Encoding.UTF8.GetBytes(CurrentGame);
    int len = /*name*/ 1 + nameB.Length +
      /*avatar*/ 1 +
      /*ip*/ 1 + ipB.Length +
      /*id*/idB.Length + 8 +
      /*remote*/ 1 +
      /*lastaccess*/ 8 +
      /*currentgame*/ 1 + cGameB.Length +
      /*status*/ 1;
    byte[] res = new byte[len];
    int pos = 0;
    res[pos] = (byte)(nameB.Length + 128);
    for (int i = 0; i < nameB.Length; i++)
      res[pos + i + 1] = nameB[i];
    pos = nameB.Length + 1;
    res[pos] = (byte)Avatar;
    pos++;
    res[pos] = (byte)ipB.Length;
    for (int i = 0; i < ipB.Length; i++)
      res[pos + i + 1] = ipB[i];
    pos += ipB.Length + 1;
    for (int i = 0; i < 8; i++)
      res[pos + i] = idB[i];
    pos += 8;
    res[pos] = (byte)(RemoteManager ? 0xff : 0);
    pos++;
    long time = LastAccess.ToBinary();
    res[pos + 0] = (byte)(time & 0xff);
    res[pos + 1] = (byte)((time >> 8) & 0xff);
    res[pos + 2] = (byte)((time >> 16) & 0xff);
    res[pos + 3] = (byte)((time >> 24) & 0xff);
    res[pos + 4] = (byte)((time >> 32) & 0xff);
    res[pos + 5] = (byte)((time >> 40) & 0xff);
    res[pos + 6] = (byte)((time >> 48) & 0xff);
    res[pos + 7] = (byte)((time >> 56) & 0xff);
    pos += 8;
    res[pos] = (byte)cGameB.Length;
    for (int i = 0; i < cGameB.Length; i++)
      res[pos + i + 1] = cGameB[i];
    pos += cGameB.Length + 1;
    res[pos] = (byte)Status;
    pos++;
    res[pos] = (byte)Status;

    return res;
  }

  internal int StringifyFullLen() {
    int nameL = Encoding.UTF8.GetByteCount(Name);
    int ipL = Encoding.UTF8.GetByteCount(IP.ToString());
    int cGameL = Encoding.UTF8.GetByteCount(CurrentGame);
    return /*name*/ 1 + nameL +
      /*avatar*/ 1 +
      /*ip*/ 1 + ipL +
      /*id*/8 + 8 +
      /*remote*/ 1 +
      /*lastaccess*/ 8 +
      /*currentgame*/ 1 + cGameL +
      /*status*/ 1;
  }

  public void StartClientThread() {
    GD.weAreAlive = true;
    localListenerAlive = true;
    communicationThread = new Thread(new ThreadStart(ClientListener));
    communicationThread.Start();
  }

  public void Kill() {
    localListenerAlive = false;
    Thread.Sleep(100);
    communicationThread.Interrupt();
    Thread.Sleep(100);
    communicationThread.Abort();
    Thread.Sleep(100);
    stream?.Close();
    tcpClient?.Close();
  }

  private ulong GenerateID() {
    int pos = 0;
    byte[] res = new byte[8];
    byte[] bytes = Encoding.UTF8.GetBytes(Name);
    for (int i = 0; i < bytes.Length; i++) {
      res[pos] = (byte)(res[pos] ^ (bytes[i] + i));
      pos++;
      if (pos == 7) pos = 0;
    }
    res[0] = (byte)(res[0] ^ (bytes.Length & 0xff));
    res[1] = (byte)(res[1] ^ (Avatar & 0xff));
    byte[] ipval = IP.GetAddressBytes();
    pos = 0;
    for (int i = 0; i < ipval.Length; i++) {
      res[pos] = (byte)(res[pos] ^ (ipval[i] + i));
      pos++;
      if (pos == 7) pos = 0;
    }
    if (RemoteManager) {
      for (int i = 0; i < res.Length; i++)
        res[i] = (byte)(res[i] ^ 0xff);
    }
    return BitConverter.ToUInt64(res, 0);
  }


  private async void ClientListener() {
    byte[] cmdBuffer = new byte[16];
    byte[] dataBuffer = new byte[64 * 1024];
    int errors = 0;

    while (GD.weAreAlive && localListenerAlive) {
      try {
        // Enter the listening loop.
        int len = 0;
        while (GD.weAreAlive && localListenerAlive) {
          len += await stream.ReadAsync(cmdBuffer, 0, 16);
          if (len == 0) {
            // We did not receive anything, just wait a couple of seconds
            Thread.Sleep(2000);
            continue;
          }
          if (len < 16) continue;

          NetworkCommand cmd = NetworkManager.GetCommand(cmdBuffer, out int paramLen);
          len = 0;
          while (len < paramLen) {
            len += await stream.ReadAsync(dataBuffer, len, dataBuffer.Length - len);
            if (len == 0) {
              // We did not receive anything, just wait a couple of seconds
              Thread.Sleep(2000);
              continue;
            }
          }

          GD.DebugLog("Received command: " + cmd.ToString(), GD.LT.Debug);
          // Process the command
          switch (cmd) {
            case NetworkCommand.UpdateGames:
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.GameListUpdated });
              break;
            case NetworkCommand.Pong:
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.PingAnswer, message = Encoding.UTF8.GetString(dataBuffer, 0, paramLen) });
              break;

            case NetworkCommand.GamesList:
              OnServerMessage?.Invoke(this, ReceiveGameList(dataBuffer, paramLen));
              break;

            case NetworkCommand.PlayersList:
              OnServerMessage?.Invoke(this, ReceivePlayersList(dataBuffer, paramLen));
              break;

            case NetworkCommand.GameCreated:
              CurrentGame = Encoding.UTF8.GetString(dataBuffer, 0, paramLen);
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.GameCreated, message = "Game \"" + CurrentGame + "\" created and joined" });
              break;

            case NetworkCommand.Joined:
              CurrentGame = Encoding.UTF8.GetString(dataBuffer, 0, paramLen);
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.GameJoined, message = CurrentGame });
              break;

            case NetworkCommand.Left:
              CurrentGame = null;
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.GameLeft, message = Encoding.UTF8.GetString(dataBuffer, 0, paramLen) });
              break;

            case NetworkCommand.GameDeleted:
              string theDeletedGame = Encoding.UTF8.GetString(dataBuffer, 0, paramLen);
              if (CurrentGame == theDeletedGame)
                CurrentGame = null;
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.GameDeleted, message = theDeletedGame });
              break;

            case NetworkCommand.GameCanStart:
              CurrentGame = Encoding.UTF8.GetString(dataBuffer, 2, dataBuffer[1]);
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.GameCanStart, message = (dataBuffer[0] == 1 ? "Y" : "N") + CurrentGame });
              break;

            case NetworkCommand.Error:
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.Error, message = Encoding.UTF8.GetString(dataBuffer, 0, paramLen) });
              break;

            case NetworkCommand.ReceiveChat:
              OnChat?.Invoke(this, ReceiveChat(dataBuffer));
              break;

            case NetworkCommand.SetPlayersFromGame:
              OnServerMessage?.Invoke(this, ReceiveGamePlayersList(dataBuffer));
              break;

            case NetworkCommand.StartingTheGame:
              OnServerMessage?.Invoke(this, StartingTheGame(dataBuffer));
              break;

            case NetworkCommand.GetRunningGame:
              DecodeReceivedMultiplayerGame(dataBuffer);
              break;

            case NetworkCommand.PlayerDeath:
              HandlePlayerDeath(dataBuffer);
              break;

            case NetworkCommand.GameProgressUpdate:
              HandleGameProgressUpdate(dataBuffer);
              break;

            case NetworkCommand.GameTurn:
              HandleGameTurnUpdate(dataBuffer);
              break;

            default:
              GD.DebugLog("Unknown command: |" + cmd.ToString() + "|", GD.LT.Log);
              break;
          }
          errors = 0;
        }
      } catch (ThreadAbortException) {
        localListenerAlive = false;
        OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { message = Name + " Thread terminated", type = ServerMessages.Info });
      } catch (ThreadInterruptedException) {
        localListenerAlive = false;
        OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { message = Name + " Thread terminated", type = ServerMessages.Info });
      } catch (SocketException e) {
        GD.DebugLog("SocketException when communicating with server: " + e.Message, GD.LT.Warning);
        OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { message = "SocketException when communicating with server: " + e.Message, type = ServerMessages.Error });
        if (!tcpClient.Connected) errors = 10; // To quit quickly
        errors++;
      } catch (ObjectDisposedException e) {
        if (GD.weAreAlive) {
          GD.DebugLog("Disposed Object Exception when communicating with server: " + e?.Message, GD.LT.Warning);
          OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { message = "Disposed Object Exception when communicating with server: " + e?.Message, type = ServerMessages.Error });
          errors = 10;
        }
      } catch (System.AccessViolationException e) {
        GD.DebugLog("Exception when communicating with server: " + e?.Message, GD.LT.DebugST);
        OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { message = "Exception when communicating with server: " + e?.Message, type = ServerMessages.Error });
        if (!tcpClient.Connected) errors = 10; // To quit quickly
        errors++;
      }
      if (errors > 10) {
        GD.DebugLog(Name + ": exiting for consecutive errors. Probably the server connection is gone.", GD.LT.Warning);
        OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "Connection with the server is lost!" });
        GD.weAreAlive = false;
      }
    }
  }


  private NetworkManager.ServerMessage ReceiveGameList(byte[] data, int len) {
    try {
      /*
       * GAMELIST
       * 4 digits numgames
       * 4 digits num players registered on the server
       * [ each game ]
       */
      if (len < 8) return null;
      int num = BitConverter.ToInt32(data, 0);
      int numPlayers = BitConverter.ToInt32(data, 4);
      List<Game> res = new List<Game>();
      int pos = 8;
      for (int i = 0; i < num; i++) {
        res.Add(new Game(data, pos));
        pos += BitConverter.ToInt16(data, pos);
      }
      return new NetworkManager.ServerMessage { type = ServerMessages.GameList, gameList = res, num = numPlayers };
    } catch (IOException e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "IOException: " + e.Message, gameList = null }; ;
    } catch (SocketException e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "SocketException: " + e.Message, gameList = null }; ;
    } catch (Exception e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "Exception: " + e.Message, gameList = null }; ;
    }
  }

  private NetworkManager.ServerMessage ReceivePlayersList(byte[] data, int len) {
    try {
      /*
       * GAMELIST
       * 4 digits num players registered on the server
       * [ each player in full mode]
       */
      if (len < 4) return null;
      int num = data[0] + ((int)data[1] << 8) + ((int)data[2] << 16) + ((int)data[3] << 24);

      List<Player> res = new List<Player>();
      int pos = 4;
      for (int i = 0; i < num; i++) {
        Player p = new Player(data, pos);
        res.Add(p);
        pos += p.StringifyFullLen();
      }
      return new NetworkManager.ServerMessage { type = ServerMessages.PlayersList, playerList = res };
    } catch (IOException e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "IOException: " + e.Message, playerList = null }; ;
    } catch (SocketException e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "SocketException: " + e.Message, playerList = null }; ;
    } catch (Exception e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "Exception: " + e.Message, playerList = null }; ;
    }
  }

  private NetworkManager.ServerMessage ReceiveGamePlayersList(byte[] data) {
    try {
      /*
       * 1 byte game name len
       * n bytes game name
       * serialized playerlist
       */
      int pos = data[0];
      string gamename = Encoding.UTF8.GetString(data, 1, pos);
      pos++;
      SimpleList sl = new SimpleList(data, pos);
      return new NetworkManager.ServerMessage { type = ServerMessages.PlayersOfGame, gamePlayersList = sl , message = gamename };

    } catch (IOException e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "IOException: " + e.Message, playerList = null }; ;
    } catch (SocketException e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "SocketException: " + e.Message, playerList = null }; ;
    } catch (Exception e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "Exception: " + e.Message, playerList = null }; ;
    }
  }

  private NetworkManager.ServerMessage StartingTheGame(byte[] data) {
    try {
      /* Return a command including all the players (id, name, and avatar) and if they are starting or not, add a last byte to tell if the game is actually ready to begin
       1 byte -> game name len
       n bytes -> game name
       1 byte -> can actually begin (0 = no, 1 = yes)
       serialized game players list
       */

      string gamename = System.Text.Encoding.UTF8.GetString(data, 1, data[0]);
      int pos = data[0] + 1;
      bool canStart = data[pos] != 0;
      pos++;
      SimpleList gameplayers = new SimpleList(data, pos);

      return new NetworkManager.ServerMessage { type = ServerMessages.StartingTheGame, gamePlayersList = gameplayers, message = gamename, num = (canStart ? 1 : 0) };
    } catch (IOException e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "IOException: " + e.Message, playerList = null }; ;
    } catch (SocketException e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "SocketException: " + e.Message, playerList = null }; ;
    } catch (Exception e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "Exception: " + e.Message, playerList = null }; ;
    }
  }

  Game multiplayerGame = null;
  int gameRandomSeed = 0;


  private void HandlePlayerDeath(byte[] data) {
    ulong id = BitConverter.ToUInt64(data, 0);
    string gamename = Encoding.UTF8.GetString(data, 9, data[8]);

    GD.DebugLog("Received playerdeath for " + id + " on " + gamename, GD.LT.Log);

    OnGame?.Invoke(this, new NetworkManager.GameMessage { type = GameMsgType.PlayerDeath, id = id, text = gamename });
  }

  private void HandleGameProgressUpdate(byte[] data) {
    ulong id = BitConverter.ToUInt64(data, 0);

    GD.DebugLog("Received GameProgressUpdate for " + id, GD.LT.Log);

    OnGame?.Invoke(this, new NetworkManager.GameMessage { type = GameMsgType.GameProgressUpdate, id = id });
  }

  private void HandleGameTurnUpdate(byte[] data) {
    string name = Encoding.UTF8.GetString(data, 3, data[2]);

    GD.DebugLog("Received GameTurn for game " + name, GD.LT.Log);
    if (TheGame.Name != name) {
      OnGame?.Invoke(this, new NetworkManager.GameMessage { type = GameMsgType.Error, text = "Wrong game received: " + name });
      OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "Wrong game received: " + name });
      return;
    }

    GameEngineValues gev = new GameEngineValues(data);
    OnGame?.Invoke(this, new NetworkManager.GameMessage { type = GameMsgType.GameTurn, engineValues = gev });
  }

  private void DecodeReceivedMultiplayerGame(byte[] data) {
    gameRandomSeed = BitConverter.ToInt32(data, 0);
    Game g = new Game(data, 4);
    multiplayerGame = g;
  }

  public string ReceiveMultiplayerGame(string gamename, out Game g, out int rndSeed) {
    // Wait for the data to arrive...
    while (multiplayerGame == null)
      Thread.Sleep(250);

    if (multiplayerGame.Name != gamename) {
      g = null;
      rndSeed = 0;
      return "!Wrong game received! " + multiplayerGame.Name;
    }

    g = multiplayerGame;
    rndSeed = gameRandomSeed;
    return null;
  }


  private NetworkManager.ChatMessage ReceiveChat(byte[] data) {
    /* Chat format
     * 16 bytes for chatid
     * 1 byte for type
     * 2 bytes msg len
     * n bytes msg
     * 8 bytes sender ID
     * 1 byte sender avatar
     * 1 byte num destinations
     * { serialization of each participant }
     */
    NetworkManager.ChatMessage e = new NetworkManager.ChatMessage();
    e.FromBytes(data);
    return e;
  }
}


public class SimplePlayer {
  public ulong id; // for AIs it goes from 0 to the max number of defined AIs
  public string name; // Empty in case of AI
  public byte avatar;
  public bool ai;
  public StatusOfPlayer status;

  public override string ToString() {
    if (id == 0 && string.IsNullOrEmpty(name)) return "<No-one>";
    return id + " (" + (name??"").Length + ") " + name + " [" + avatar + "] " + ai + " " + status.ToString();
  }

  public byte[] Serialize() {
    int len = 8 + 1 + Encoding.UTF8.GetByteCount(name ?? "") + 1 + 1 + 1;
    byte[] res = new byte[len];
    byte[] idd = BitConverter.GetBytes(id);
    for (int i = 0; i < 8; i++)
      res[i] = idd[i];
    byte[] nd = Encoding.UTF8.GetBytes(name ?? "");
    res[8] = (byte)nd.Length;
    for (int i = 0; i < nd.Length; i++)
      res[i + 8 + 1] = nd[i];
    res[8 + 1 + nd.Length] = avatar;
    res[8 + 1 + nd.Length + 1] = (byte)(ai ? 1 : 0);
    res[8 + 1 + nd.Length + 1 + 1] = (byte)status;
    return res;
  }

  public SimplePlayer(ulong id, string name, byte avatar) {
    this.id = id;
    this.name = name;
    this.avatar = avatar;
    ai = false;
    status = StatusOfPlayer.Waiting;
  }

  public SimplePlayer(Player player) {
    id = player.ID;
    name = player.Name;
    avatar = (byte)player.Avatar;
    ai = false;
    status = player.Status;
  }

  public SimplePlayer(int index) {
    id = (ulong)(index + 1);
    name = GD.GetNameForAI(index);
    avatar = (byte)GD.GetAvatarForAI(name);
    ai = true;
    status = StatusOfPlayer.Playing;
  }

  public SimplePlayer() {
    id = 0;
    name = null;
    avatar = 0;
    ai = false;
    status = StatusOfPlayer.Waiting;
  }

  public SimplePlayer(byte[] data, int pos) {
    id = BitConverter.ToUInt64(data, pos);
    name = Encoding.UTF8.GetString(data, 9 + pos, data[8 + pos]);
    avatar = data[pos + 8 + 1 + data[8 + pos]];
    ai = data[pos + 8 + 1 + data[8 + pos] + 1] != 0;
    status = (StatusOfPlayer)data[pos + 8 + 1 + data[8 + pos] + 1 + 1];
  }

  public SimplePlayer(byte[] data, int pos, out int len) {
    id = BitConverter.ToUInt64(data, pos);
    name = Encoding.UTF8.GetString(data, 9 + pos, data[8 + pos]);
    avatar = data[pos + 8 + 1 + data[8 + pos]];
    ai = data[pos + 8 + 1 + data[8 + pos] + 1] != 0;
    status = (StatusOfPlayer)data[pos + 8 + 1 + data[8 + pos] + 1 + 1];
    len = 8 + 1 + data[8 + pos] + 1 + 1 + 1;
  }

}

public class SimpleList {
  private readonly SimplePlayer[] sps;
  public int Count;

  public SimpleList() {
    sps = new SimplePlayer[6];
    for (int i = 0; i < 6; i++)
      sps[i] = new SimplePlayer();
    Count = 0;
  }

  public SimpleList(SimplePlayer init) {
    sps = new SimplePlayer[6];
    sps[0] = init;
    Count = 1;
  }

  public void Add(SimplePlayer sp) {
    if (Count >= 6 || Contains(sp)) return;
    for (int i = 0; i < Count; i++)
      if (sps[i].id == sp.id) return; // Already in
    sps[Count] = sp;
    Count++;
  }

  public bool Contains(SimplePlayer sp) {
    for (int i = 0; i < Count; i++)
      if (sps[i].id == sp.id) return true;
    return false;
  }

  public bool Contains(ulong id) {
    for (int i = 0; i < Count; i++)
      if (sps[i].id == id) return true;
    return false;
  }

  public void Remove(SimplePlayer sp) {
    for (int i = 0; i < Count; i++)
      if (sps[i].id == sp.id) {
        for (int j = i; j < 5; j++)
          sps[j] = sps[j + 1];
        Count--;
        return;
      }
  }

  public bool Remove(ulong id) {
    for (int i = 0; i < Count; i++)
      if (sps[i].id == id) {
        for (int j = i; j < 5; j++)
          sps[j] = sps[j + 1];
        Count--;
        return true;
      }
    return false;
  }

  public void Clear() {
    Count = 0;
  }

  public SimplePlayer this[int i] {
    get {
      if (i >= 0 && i < Count) return sps[i];
      return null;
    }
    set {
      if (i < 0 || i > 5) return;
      sps[i] = value;
      if (i > Count - 1) Count = i + 1;
    }
  }

  public byte[] Serialize() {
    SimplePlayer empty = new SimplePlayer();

    byte[] d0 = (sps[0] ?? empty).Serialize();
    byte[] d1 = (sps[1] ?? empty).Serialize();
    byte[] d2 = (sps[2] ?? empty).Serialize();
    byte[] d3 = (sps[3] ?? empty).Serialize();
    byte[] d4 = (sps[4] ?? empty).Serialize();
    byte[] d5 = (sps[5] ?? empty).Serialize();
    byte[] res = new byte[1 + d0.Length + d1.Length + d2.Length + d3.Length + d4.Length + d5.Length];
    res[0] = (byte)Count;
    int pos = 1;
    for (int i = 0; i < d0.Length; i++)
      res[pos + i] = d0[i];
    pos += d0.Length;
    for (int i = 0; i < d1.Length; i++)
      res[pos + i] = d1[i];
    pos += d1.Length;
    for (int i = 0; i < d2.Length; i++)
      res[pos + i] = d2[i];
    pos += d2.Length;
    for (int i = 0; i < d3.Length; i++)
      res[pos + i] = d3[i];
    pos += d3.Length;
    for (int i = 0; i < d4.Length; i++)
      res[pos + i] = d4[i];
    pos += d4.Length;
    for (int i = 0; i < d5.Length; i++)
      res[pos + i] = d5[i];

    return res;
  }

  public SimpleList(byte[] data, int start) {
    sps = new SimplePlayer[6];
    Count = data[start];
    int pos = start + 1;
    sps[0] = new SimplePlayer(data, pos, out int len);
    pos += len;
    sps[1] = new SimplePlayer(data, pos, out len);
    pos += len;
    sps[2] = new SimplePlayer(data, pos, out len);
    pos += len;
    sps[3] = new SimplePlayer(data, pos, out len);
    pos += len;
    sps[4] = new SimplePlayer(data, pos, out len);
    pos += len;
    sps[5] = new SimplePlayer(data, pos, out _);
  }

  public SimplePlayer GetByID(ulong id) {
    for (int i = 0; i < Count; i++)
      if (sps[i].id == id) return sps[i];
    return null;
  }

  public void SetStatus(ulong id, StatusOfPlayer status) {
    for (int i = 0; i < Count; i++)
      if (sps[i].id == id) {
        sps[i].status = status;
        return;
      }
  }
}