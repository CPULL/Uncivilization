using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

[System.Serializable]
public class PlayerDef {
  public enum Type { Human = 1, AI = 2, Defeated = 3, Empty = 0 };

  public string name;
  public ulong id;
  public byte avatar;
  public Type type;
  public PlayerGameStatus Status = PlayerGameStatus.Waiting;

  public PlayerDef() {
    name = null;
    id = 0;
    avatar = 0;
    type = Type.Empty;
  }

  public PlayerDef(string name, byte avatar) {
    this.name = name;
    this.avatar = avatar;
    type = Type.Human;
    id = GenerateID();
  }

  public PlayerDef(ulong id, string name, byte avatar) {
    this.id = id;
    this.name = name;
    this.avatar = avatar;
    type = Type.Human;
  }

  public PlayerDef(byte[] data, int pos) {
    name = System.Text.Encoding.UTF8.GetString(data, pos + 2, data[pos + 1]);
    id = System.BitConverter.ToUInt64(data, pos + 2 + data[pos + 1]);
    avatar = data[pos + 2 + data[pos + 2] + 8];
    type = (Type)data[pos + 2 + data[pos + 2] + 8 + 1];
  }

  public override string ToString() {
    return "\"" + name + "("+avatar+")" + type.ToString() + "\"";
  }

  public int GetDataLength() {
    return System.Text.Encoding.UTF8.GetByteCount(name) + 12;
  }

  public byte[] Serialize() {
    byte[] n = System.Text.Encoding.UTF8.GetBytes(name);
    int len = 1 + // length of the serialization
              1 + // size of the name
              n.Length + // name
              8 + // id
              1 + // avatar
              1; // status
    byte[] res = new byte[len];
    int pos = 0;
    res[pos++] = (byte)len;
    res[pos++] = (byte)n.Length;
    for (int i = 0; i < n.Length; i++)
      res[pos + i] = n[i];
    pos += n.Length;
    n = System.BitConverter.GetBytes(id);
    for (int i = 0; i < 8; i++)
      res[pos + i] = n[i];
    pos += 8;
    res[pos++] = (byte)avatar;
    res[pos++] = (byte)type;
    return res;
  }

  private ulong GenerateID() {
    int pos = 0;
    byte[] res = new byte[8];
    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(name);
    for (int i = 0; i < bytes.Length; i++) {
      res[pos] = (byte)(res[pos] ^ (bytes[i] + i));
      pos++;
      if (pos == 7) pos = 0;
    }
    res[0] = (byte)(res[0] ^ (bytes.Length & 0xff));
    res[1] = (byte)(res[1] ^ (avatar & 0xff));
    return BitConverter.ToUInt64(res, 0);
  }

}

[System.Serializable]
public class Player {
  public PlayerDef def;
  public UnityEngine.Color color; // Not serialized
  public byte index; // Used to specify which color to use for background and cities. The index value is the actual owner of the city
  public ActionType action;
  public byte val1; // First parameter of the action
  public byte val2; // Second parameter of the action
  public byte happiness;
  public bool[] techs;
  public short[,] Resources;
  public List<byte> cities;
  public Enemy refEnemy;

  public override string ToString() {
    return def == null ? "NullPLayer" : def.ToString();
  }

  private void BaseInit() {
    techs = new bool[21];
    Resources = new short[10, 3];
    cities = new List<byte>();
  }

  public Player(string name, byte avatar) {
    def = new PlayerDef(name, avatar);
    BaseInit();
  }

  public Player(PlayerDef playerDef) {
    def = playerDef;
    BaseInit();
  }

  public Player(Enemy enemy) {
    refEnemy = enemy;
    def = enemy.player.def;
    def.type = PlayerDef.Type.AI;
    BaseInit();
  }

  internal void Init(Enemy enemy, Player p) {
    refEnemy = enemy;
    def = new PlayerDef(p.def.id, p.def.name, p.def.avatar);
    BaseInit();
  }

  public byte[] Serialize() {
    // All fields
    return null;
  }

}

public class PlayerHandler {
  public IPAddress IP;
  public DateTime LastAccess;
  public TcpClient tcpClient; // Used in client mode to communicate with the server
  public NetworkStream stream; // Used in both client and server to exchange data
  public Thread communicationThread;
  public EventHandler<NetworkManager.ServerMessage> OnServerMessage;
  public EventHandler<NetworkManager.ChatMessage> OnChat;
  public EventHandler<NetworkManager.GameMessage> OnGame;
  public bool RemoteManager = false;
  private bool localListenerAlive = true;
  public Player player;
  public ulong currentGameID;

  Game multiplayerGame = null;
  int gameRandomSeed = 0;

  public ulong ID { get { return player.def.id; } }
  public string Name { get { return player.def.name; } }
  public byte Avatar { get { return player.def.avatar; } }

  public override string ToString() {
    return player == null ? "NullPLayer" : player.ToString();
  }

  public PlayerHandler(string name, byte avatar = 0) {
    try {
      string pip = new WebClient().DownloadString("http://ipinfo.io/ip");
      if (!IPAddress.TryParse(pip.Trim(), out IP))
        IP = IPAddress.Parse("127.0.0.1");
    } catch (Exception) {
      IP = IPAddress.Parse("127.0.0.1");
    }
    LastAccess = DateTime.Now;
    name = name.Trim();
    if (name.Length > 64) name = name.Substring(0, 64);
    player = new Player(name, avatar);
  }

  public PlayerHandler(byte[] data) {
    try {
      string pip = new WebClient().DownloadString("http://ipinfo.io/ip");
      if (!IPAddress.TryParse(pip.Trim(), out IP))
        IP = IPAddress.Parse("127.0.0.1");
    } catch (Exception) {
      IP = IPAddress.Parse("127.0.0.1");
    }
    LastAccess = DateTime.Now;
    player = new Player(new PlayerDef(data, 0));
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

  public void StartClientThread() {
    GD.weAreAlive = true;
    localListenerAlive = true;
    communicationThread = new Thread(new ThreadStart(ClientListener));
    communicationThread.Start();
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
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.PingAnswer, message = System.Text.Encoding.UTF8.GetString(dataBuffer, 0, paramLen) });
              break;

            case NetworkCommand.GamesList:
              OnServerMessage?.Invoke(this, ReceiveGameList(dataBuffer, paramLen));
              break;

            case NetworkCommand.PlayersList:
              OnServerMessage?.Invoke(this, ReceivePlayersList(dataBuffer, paramLen));
              break;

            case NetworkCommand.GameCreated:
              currentGameID = BitConverter.ToUInt64(dataBuffer, 0);
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.GameCreated, message = "Game \"" + currentGameID + "\" created and joined" });
              break;

            case NetworkCommand.Joined:
              currentGameID = BitConverter.ToUInt64(dataBuffer, 0);
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.GameJoined, message = currentGameID.ToString() });
              break;

            case NetworkCommand.Left:
              currentGameID = 0;
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.GameLeft, message = System.Text.Encoding.UTF8.GetString(dataBuffer, 0, paramLen) });
              break;

            case NetworkCommand.GameDeleted:
              ulong theDeletedGame = BitConverter.ToUInt64(dataBuffer, 0);
              if (currentGameID == theDeletedGame)
                currentGameID = 0;
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.GameDeleted, message = theDeletedGame.ToString() });
              break;

            case NetworkCommand.GameCanStart:
              currentGameID = BitConverter.ToUInt64(dataBuffer, 1);
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.GameCanStart, message = (dataBuffer[0] == 1 ? "Y" : "N") + currentGameID });
              break;

            case NetworkCommand.Error:
              OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.Error, message = System.Text.Encoding.UTF8.GetString(dataBuffer, 0, paramLen) });
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
        OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { message = this.ToString() + " Thread terminated", type = ServerMessages.Info });
      } catch (ThreadInterruptedException) {
        localListenerAlive = false;
        OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { message = this.ToString() + " Thread terminated", type = ServerMessages.Info });
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
        GD.DebugLog(this.ToString() + ": exiting for consecutive errors. Probably the server connection is gone.", GD.LT.Warning);
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
      int num = BitConverter.ToInt32(data, 0);

      List<PlayerDef> res = new List<PlayerDef>();
      int pos = 4;
      for (int i = 0; i < num; i++) {
        PlayerDef p = new PlayerDef(data, pos);
        res.Add(p);
        pos += p.GetDataLength();
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
       * 8 bytes game id
       * serialized playerlist
       */
      ulong gameid = BitConverter.ToUInt64(data, 0);
      List<PlayerDef> pdef = new List<PlayerDef>();
      int pos = 0;
      for (int i = 0; i < 6; i++) {
        pdef.Add(new PlayerDef(data, pos));
        pos += pdef[i].GetDataLength();
      }
      return new NetworkManager.ServerMessage { type = ServerMessages.PlayersOfGame, playerList = pdef, message = gameid.ToString() };

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
      List<PlayerDef> pdef = new List<PlayerDef>();
      for (int i = 0; i < 6; i++) {
        pdef.Add(new PlayerDef(data, pos));
        pos += pdef[i].GetDataLength();
      }

      return new NetworkManager.ServerMessage { type = ServerMessages.StartingTheGame, playerList = pdef, message = gamename, num = (canStart ? 1 : 0) };
    } catch (IOException e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "IOException: " + e.Message, playerList = null }; ;
    } catch (SocketException e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "SocketException: " + e.Message, playerList = null }; ;
    } catch (Exception e) {
      return new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "Exception: " + e.Message, playerList = null }; ;
    }
  }



  private void HandlePlayerDeath(byte[] data) {
    ulong playerid = BitConverter.ToUInt64(data, 0);
    ulong gameid = BitConverter.ToUInt64(data, 9);
    GD.DebugLog("Received playerdeath for " + playerid + " on " + gameid, GD.LT.Log);
    OnGame?.Invoke(this, new NetworkManager.GameMessage { type = GameMsgType.PlayerDeath, id = playerid });
  }

  private void HandleGameProgressUpdate(byte[] data) {
    ulong id = BitConverter.ToUInt64(data, 0);
    GD.DebugLog("Received GameProgressUpdate for " + id, GD.LT.Log);
    OnGame?.Invoke(this, new NetworkManager.GameMessage { type = GameMsgType.GameProgressUpdate, id = id });
  }

  private void HandleGameTurnUpdate(byte[] data) {
    ulong gameid = BitConverter.ToUInt64(data, 0);

    GD.DebugLog("Received GameTurn for game " + gameid, GD.LT.Log);
    if (currentGameID != gameid) {
      OnGame?.Invoke(this, new NetworkManager.GameMessage { type = GameMsgType.Error, text = "Wrong game received: " + gameid });
      OnServerMessage?.Invoke(this, new NetworkManager.ServerMessage { type = ServerMessages.Error, message = "Wrong game received: " + gameid });
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


  public string ReceiveMultiplayerGame(ulong gameid, out Game g, out int rndSeed) {
    // Wait for the data to arrive...
    while (multiplayerGame == null)
      Thread.Sleep(250);

    if (multiplayerGame.id != gameid) {
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

public class PlayerAI {
  public CharType charType;
  public WeaponType preferredWeapon;
  public WeaponType preferredDelivery;
  public int social;
  public int tech;
  public int forget;
}

