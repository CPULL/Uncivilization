using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class NetworkManager {
  private List<Game> games;
  private List<Player> players;
  private List<Game> runningGames;

  private IPAddress serverIP = null;
  private int serverPort = 55601;
  public string serverPassword = "";

  public bool alreadyStarted = false;
  public bool serverConnected = false;
  private Thread mainListeningThread;
  private readonly System.Random random = new System.Random();


  public void StartUpServer() {
    games = new List<Game>();
    players = new List<Player>();
    runningGames = new List<Game>();
    // Set our IP address and start the listener
    try {
      string myIP = new WebClient().DownloadString("http://ipinfo.io/ip");
      if (!IPAddress.TryParse(myIP.Trim(), out serverIP))
        serverIP = IPAddress.Parse("127.0.0.1");
    } catch (Exception) {
      serverIP = IPAddress.Parse("127.0.0.1");
    }
    GD.weAreAlive = true;
    mainListeningThread = new Thread(new ThreadStart(Listen));
    mainListeningThread.Start();
    alreadyStarted = true;
  }

  public void StartUpClient() {
    // Set our IP address
    try {
      string myIP = new WebClient().DownloadString("http://ipinfo.io/ip");
      if (!IPAddress.TryParse(myIP.Trim(), out serverIP))
        serverIP = IPAddress.Parse("127.0.0.1");
    } catch (Exception) {
      serverIP = IPAddress.Parse("127.0.0.1");
    }
    alreadyStarted = true;
  }

  public void StopServer() {
    GD.weAreAlive = false;
    GD.DebugLog("Stopping because stopping", GD.LT.Debug);
    Thread.Sleep(1000);
    if (mainListeningThread != null) {
      mainListeningThread.Interrupt();
      Thread.Sleep(100);
      mainListeningThread.Abort();
    }
    Thread.Sleep(1000);
  }

  public void SetPort(string port) {
    if (!int.TryParse(port, out serverPort))
      serverPort = 55601;
  }

  public string GetServerAddress() {
    while (serverIP == null)
      Thread.Sleep(500);
    return serverIP.ToString();
  }

  public string GetServerPort() {
    return serverPort.ToString();
  }

  #region Client ************************************************************************************************************************************************************************************

  public string PingServer(Player player) {
    if (player.tcpClient == null || !player.tcpClient.Connected) return "No connection to the server!";
    try {
      GD.DebugLog("Pinging server", GD.LT.Debug);
      byte[] tmp1 = System.Text.Encoding.UTF8.GetBytes(serverIP.ToString());
      byte[] tmp2 = System.Text.Encoding.UTF8.GetBytes(serverPort.ToString());
      byte[] cmd = GenerateCommand(NetworkCommand.Ping, tmp1.Length + tmp2.Length);
      for (int i = 0; i < tmp1.Length; i++)
        cmd[16 + i] = tmp1[i];
      for (int i = 0; i < tmp2.Length; i++)
        cmd[16 + i + tmp1.Length] = tmp2[i];

      player.stream.Write(cmd, 0, cmd.Length);
      return "";
    } catch (IOException e) {
      return "Communication error with " + player.ToString() + ": " + e.Message;
    } catch (SocketException e) {
      return "Communication error with " + player.ToString() + ": " + e.Message;
    }
  }

  public string ConnectToServer(Player player, string address, int port) {
    IPAddress.TryParse(address, out serverIP);
    serverPort = port;
    TcpClient tcpClient = new TcpClient {
      ReceiveBufferSize = 64*1024,
      ReceiveTimeout = 30000
    };
    try {
      GD.DebugLog("Connecting to server", GD.LT.Debug);
      tcpClient.Connect(address, port);
      byte[] playerData = player.Stringify();
      byte[] cmd = GenerateCommand(NetworkCommand.Connect, playerData.Length);
      for (int i = 0; i < playerData.Length; i++)
        cmd[16 + i] = playerData[i];
      NetworkStream stream = tcpClient.GetStream();
      stream.ReadTimeout = 30000;
      stream.Write(cmd, 0, cmd.Length);

      // Receive the answer
      byte[] data = new byte[256];
      int len = 0;
      try { 
        while (len < 16)
          len += stream.Read(data, len, 16 - len);
      }
      catch (Exception e) {
        return "!Cannot connect: " + e.Message;
      }
      NetworkCommand answercmd = GetCommand(data, out int paramLen);
      len = 0;
      try {
        while (len < paramLen)
          len += stream.Read(data, len, 256 - len);
      } catch (Exception e) {
        return "!Cannot connect: " + e.Message;
      }

      player.tcpClient = tcpClient;
      player.stream = stream;
      serverConnected = (answercmd == NetworkCommand.SuccessMsg);
      return (serverConnected ? "" : "!" ) + System.Text.Encoding.UTF8.GetString(data, 0, paramLen);
    } catch (IOException e) {
      player.stream = null;
      player.tcpClient = null;
      return "!Communication error with " + player.ToString() + ": " + e.Message;
    } catch (SocketException e) {
      player.stream = null;
      player.tcpClient = null;
      return "!Communication error with " + player.ToString() + ": " + e.Message;
    }
  }

  public string ConnectToRemoteServer(Player player, string password, string address, int port) {
    IPAddress.TryParse(address, out serverIP);
    serverPort = port;
    TcpClient tcpClient = new TcpClient {
      ReceiveBufferSize = 64 * 1024,
      ReceiveTimeout = 30000
    };
    try {
      GD.DebugLog("Connecting to remote server", GD.LT.Debug);
      tcpClient.Connect(address, port);
      byte[] playerData = player.Stringify();
      byte[] passwordBin = System.Text.Encoding.UTF8.GetBytes(password);
      byte[] cmd = GenerateCommand(NetworkCommand.ConnectRemote, playerData.Length + passwordBin.Length + 1);

      for (int i = 0; i < playerData.Length; i++)
        cmd[16 + i] = playerData[i];
      cmd[16 + playerData.Length] = (byte)passwordBin.Length;
      for (int i = 0; i < passwordBin.Length; i++)
        cmd[16 + playerData.Length + 1 + i] = passwordBin[i];

      NetworkStream stream = tcpClient.GetStream();
      stream.ReadTimeout = 30000;
      stream.Write(cmd, 0, cmd.Length);

      // Receive the answer
      byte[] data = new byte[256];
      int len = 0;
      try {
        while (len < 16)
          len += stream.Read(data, len, 16 - len);
      } catch (Exception e) {
        GD.DebugLog("Cannot connect: " + e.Message, GD.LT.Debug);
        return "!Cannot connect: " + e.Message;
      }
      NetworkCommand answercmd = GetCommand(data, out int paramLen);
      len = 0;
      try {
        while (len < paramLen)
          len += stream.Read(data, len, 256 - len);
      } catch (Exception e) {
        GD.DebugLog("Cannot connect: " + e.Message, GD.LT.Debug);
        return "!Cannot connect: " + e.Message;
      }

      player.tcpClient = tcpClient;
      player.stream = stream;
      serverConnected = (answercmd == NetworkCommand.SuccessMsg);
      return (serverConnected ? "" : "!") + System.Text.Encoding.UTF8.GetString(data, 0, paramLen);
    } catch (IOException e) {
      player.stream = null;
      player.tcpClient = null;
      GD.DebugLog("Communication error with " + player.ToString() + ": " + e.Message, GD.LT.Debug);
      return "!Communication error with " + player.ToString() + ": " + e.Message;
    } catch (SocketException e) {
      player.stream = null;
      player.tcpClient = null;
      GD.DebugLog("Communication error with " + player.ToString() + ": " + e.Message, GD.LT.Debug);
      return "!Communication error with " + player.ToString() + ": " + e.Message;
    }
  }

  public void DisconnectFromServer(Player player) {
    serverConnected = false;
    if (player.tcpClient != null && player.tcpClient.Connected) {
      GD.DebugLog("Disconnecting from server", GD.LT.Debug);
      byte[] playerData = player.Stringify();
      byte[] cmd = GenerateCommand(NetworkCommand.Goodbye, playerData.Length);
      for (int i = 0; i < playerData.Length; i++)
        cmd[16 + i] = playerData[i];
      player.stream.Write(cmd, 0, cmd.Length);
    }
    OnServerMessage?.Invoke(this, new ServerMessage { message = player.Name + " disconnected.", type = ServerMessages.Info });
    player.stream?.Close();
    player.tcpClient.Close();
    player.stream = null;
    player.tcpClient = null;
  }

  public void GetGameList(Player player) {
    if (player == null || player.tcpClient == null || !player.tcpClient.Connected) return;
    try {
      GD.DebugLog("Getting game list", GD.LT.Debug);
      byte[] cmd = GenerateCommand(NetworkCommand.GamesList, 0);
      player.stream.Write(cmd, 0, cmd.Length);
    } catch (Exception e) {
      GD.DebugLog("Error requesting game list: " + e.Message, GD.LT.Debug);
    }
  }

  public void UpdateLists(Player player, bool showingGames) {
    if (player == null || player.tcpClient == null || !player.tcpClient.Connected) return;
    try {
      if (showingGames) {
        GD.DebugLog("Requesting players list", GD.LT.Debug);
        byte[] cmd = GenerateCommand(NetworkCommand.PlayersList, 0);
        player.stream.Write(cmd, 0, cmd.Length);
        Thread.Sleep(200);
        cmd = GenerateCommand(NetworkCommand.GamesList, 0);
        player.stream.Write(cmd, 0, cmd.Length);
      }
      else {
        GD.DebugLog("Requesting games list", GD.LT.Debug);
        byte[] cmd = GenerateCommand(NetworkCommand.GamesList, 0);
        player.stream.Write(cmd, 0, cmd.Length);
        Thread.Sleep(200);
        cmd = GenerateCommand(NetworkCommand.PlayersList, 0);
        player.stream.Write(cmd, 0, cmd.Length);
      }
    } catch (Exception e) {
      GD.DebugLog("Error updating lists: " + e.Message, GD.LT.Debug);
    }
  }

  public string CreateGameClient(string name, Player player, int difficulty, int numplayers, int numais) {
    if (player.tcpClient == null || !player.tcpClient.Connected) return "You are not connected to a server!";
    try {
      GD.DebugLog("Creating game " + name, GD.LT.Debug);
      Game game = new Game(name, player, difficulty, numplayers + 2, numais);
      byte[] gameData = game.Serialize();
      byte[] cmd = GenerateCommand(NetworkCommand.CreateGame, gameData.Length);
      for (int i = 0; i < gameData.Length; i++)
        cmd[16 + i] = gameData[i];
      player.stream = player.tcpClient.GetStream();
      player.stream.Write(cmd, 0, cmd.Length);
      return null;
    } catch (IOException e) {
      GD.DebugLog("Error in creating the game: " + e.Message, GD.LT.Debug);
      return "Error: " + e.Message;
    } catch (SocketException e) {
      GD.DebugLog("Error in creating the game: " + e.Message, GD.LT.Debug);
      return "Error: " + e.Message;
    }
  }

  public string JoinGameClient(Player player, string gamename) {
    if (player.tcpClient == null || !player.tcpClient.Connected) return "!You are not connected to a server!";
    try {
      GD.DebugLog("Joining game " + gamename, GD.LT.Debug);
      byte[] gameData = System.Text.Encoding.UTF8.GetBytes(gamename.Trim());
      byte[] cmd = GenerateCommand(NetworkCommand.Join, gameData.Length);
      for (int i = 0; i < gameData.Length; i++)
        cmd[16 + i] = gameData[i];
      player.stream.Write(cmd, 0, cmd.Length);

      return "";
    } catch (IOException e) {
      GD.DebugLog("Error in joining a game: " + e.Message, GD.LT.Debug);
      return "!Error: " + e.Message;
    } catch (SocketException e) {
      GD.DebugLog("Error in joining a game: " + e.Message, GD.LT.Debug);
      return "!Error: " + e.Message;
    }
  }

  public string LeaveGameClient(Player player, string gamename) {
    if (string.IsNullOrWhiteSpace(gamename)) return "";
    if (player.tcpClient == null || !player.tcpClient.Connected) return "!You are not connected to a server!";
    try {
      GD.DebugLog("leaving game " + gamename, GD.LT.Debug);
      byte[] gameData = System.Text.Encoding.UTF8.GetBytes(gamename.Trim());
      byte[] cmd = GenerateCommand(NetworkCommand.Leave, gameData.Length);
      for (int i = 0; i < gameData.Length; i++)
        cmd[16 + i] = gameData[i];
      player.stream.Write(cmd, 0, cmd.Length);

      return "";
    } catch (IOException e) {
      GD.DebugLog("Error in leaving a game: " + e.Message, GD.LT.Debug);
      return "!Error: " + e.Message;
    } catch (SocketException e) {
      GD.DebugLog("Error in leaving a game: " + e.Message, GD.LT.Debug);
      return "!Error: " + e.Message;
    }

  }

  public string DeleteGameClient(Player player, string gamename) {
    if (player.tcpClient == null || !player.tcpClient.Connected) return "!You are not connected to a server!";
    try {
      GD.DebugLog("Deleting game gamename", GD.LT.Debug);
      byte[] gameData = System.Text.Encoding.UTF8.GetBytes(gamename.Trim());
      byte[] cmd = GenerateCommand(NetworkCommand.DeleteGame, gameData.Length);
      for (int i = 0; i < gameData.Length; i++)
        cmd[16 + i] = gameData[i];
      player.stream.Write(cmd, 0, cmd.Length);

      return "DELETING game \"" + gamename + "\"";
    } catch (IOException e) {
      GD.DebugLog("Error in deleting a game: " + e.Message, GD.LT.Debug);
      return "!Error: " + e.Message;
    } catch (SocketException e) {
      GD.DebugLog("Error in deleting a game: " + e.Message, GD.LT.Debug);
      return "!Error: " + e.Message;
    }
  }

  public string GetPlayersFromGameClient(Player player, string gamename) {
    if (player.tcpClient == null || !player.tcpClient.Connected) return "!You are not connected to a server!";
    try {
      GD.DebugLog("Getting players for game " + gamename, GD.LT.Debug);
      byte[] gameData = System.Text.Encoding.UTF8.GetBytes(gamename.Trim());
      byte[] cmd = GenerateCommand(NetworkCommand.GetPlayersFromGame, gameData.Length);
      for (int i = 0; i < gameData.Length; i++)
        cmd[16 + i] = gameData[i];
      player.stream.Write(cmd, 0, cmd.Length);
      return "";
    } catch (IOException e) {
      GD.DebugLog("Error in getting game players: " + e.Message, GD.LT.Debug);
      return "!Error: " + e.Message;
    } catch (SocketException e) {
      GD.DebugLog("Error in getting game players: " + e.Message, GD.LT.Debug);
      return "!Error: " + e.Message;
    }
  }

  public string IAmStartingTheGameClient(Player player) {
    if (player.tcpClient == null || !player.tcpClient.Connected) return "!You are not connected to a server!";
    try {
      GD.DebugLog("Confirming starting game", GD.LT.Debug);
      byte[] gameData = System.Text.Encoding.UTF8.GetBytes(player.CurrentGame.Trim());
      byte[] cmd = GenerateCommand(NetworkCommand.StartingTheGame, gameData.Length);
      for (int i = 0; i < gameData.Length; i++)
        cmd[16 + i] = gameData[i];
      player.stream.Write(cmd, 0, cmd.Length);
      return "";
    } catch (IOException e) {
      GD.DebugLog("Error in starting a game: " + e.Message, GD.LT.Debug);
      return "!Error: " + e.Message;
    } catch (SocketException e) {
      GD.DebugLog("Error in starting a game: " + e.Message, GD.LT.Debug);
      return "!Error: " + e.Message;
    }
  }

  public string GetTheGameFromTheServer(Player player, out Game game, out int seed) {
    game = null;
    seed = 0;
    if (player.tcpClient == null || !player.tcpClient.Connected) return "You are not connected to a server!";
    try {
      GD.DebugLog("Getting game definition from server", GD.LT.Debug);
      byte[] gameData = System.Text.Encoding.UTF8.GetBytes(player.CurrentGame);
      byte[] cmd = GenerateCommand(NetworkCommand.GetRunningGame, gameData.Length);
      for (int i = 0; i < gameData.Length; i++)
        cmd[16 + i] = gameData[i];
      player.stream.Write(cmd, 0, cmd.Length);
    } catch (Exception e) {
      GD.DebugLog("Error in getting the game from the server: " + e.Message, GD.LT.Debug);
      return "Error in getting the game from the server: " + e.Message;
    }

    // The answer will arrive from Player.Listener. We should wait here for a while
    ReceiveMultiplayerGameCallerDelegate caller = new ReceiveMultiplayerGameCallerDelegate(player.ReceiveMultiplayerGame);
    IAsyncResult result = caller.BeginInvoke(player.CurrentGame, out Game res, out int rndSeed, null, null);
    Thread.Sleep(0);
    result.AsyncWaitHandle.WaitOne();

    string returnValue = caller.EndInvoke(out res, out int resSeed, result);
    result.AsyncWaitHandle.Close();
    if (string.IsNullOrEmpty(returnValue)) {
      game = res;
      seed = resSeed;
      return null;
    }
    else { // Error
      GD.DebugLog(returnValue, GD.LT.Log);
      UnityEngine.Random.InitState(0);
      return returnValue;
    }
  }

  public delegate string ReceiveMultiplayerGameCallerDelegate(string gamename, out Game g, out int rndSeed);

  public string SendGameAction(Player player, GameAction gameAction) {
    if (player.tcpClient == null || !player.tcpClient.Connected) return "You are not connected to a server!";
    try {
      GD.DebugLog("Sending game action " + gameAction.ToString(), GD.LT.Debug);
      byte[] gameData = gameAction.Serialize();
      byte[] cmd = GenerateCommand(NetworkCommand.SendGameAction, gameData.Length);
      for (int i = 0; i < gameData.Length; i++)
        cmd[16 + i] = gameData[i];
      player.stream.Write(cmd, 0, cmd.Length);
      return null;
    } catch (IOException e) {
      GD.DebugLog("Error in sending the game action: " + e.Message, GD.LT.Debug);
      return "Error: " + e.Message;
    } catch (SocketException e) {
      GD.DebugLog("Error in sending the game action: " + e.Message, GD.LT.Debug);
      return "Error: " + e.Message;
    }
  }

  #endregion Client

  #region Server ************************************************************************************************************************************************************************************

  public void Listen() {
    GD.DebugLog("Server starting up...", GD.LT.Log);
    TcpListener serverTcpListener = null;
    byte[] cmdBuffer = new byte[16];
    byte[] paramsBuffer = new byte[65536];
    while (GD.weAreAlive) {
      try {
        serverTcpListener = new TcpListener(serverIP, serverPort);
        serverTcpListener.Start();
        GD.DebugLog("Listening on " + serverIP.ToString() + ":" + serverPort, GD.LT.Log);
      } catch (Exception) {
        GD.DebugLog("Cannot listen to a public IP port, trying localhost", GD.LT.Debug);
        // Try localhost
        try {
          serverIP = IPAddress.Parse("127.0.0.1");
          serverTcpListener = new TcpListener(serverIP, serverPort);
          serverTcpListener.Start();
          GD.DebugLog("Listening on " + serverIP.ToString() + ":" + serverPort, GD.LT.Log);
        } catch (Exception e) {
          GD.DebugLog("Cannot create a server TCP connection! " + e.Message, GD.LT.Warning);
          if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { message = "Cannot create a server TCP connection! " + e.Message, type = ServerMessages.Error });
          return;
        }
      }
      if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { message = "Server Connected", type = ServerMessages.ServerConnected });
      TcpClient client = null;
      NetworkStream stream = null;

      try {
        // Enter the listening loop.
        while (GD.weAreAlive) {
          GD.DebugLog("Waiting for some data...", GD.LT.Log);
          client = serverTcpListener.AcceptTcpClient();
          stream = client.GetStream();


          // We accept only "Connect " command here. All other commands should be sent on the client specific tcp connection
          int receivedLength = 0;
          int thisReceptionLength = 0;
          while (GD.weAreAlive && receivedLength < 16) {
            thisReceptionLength = stream.Read(cmdBuffer, receivedLength, cmdBuffer.Length - receivedLength);
            if (thisReceptionLength == 0) Thread.Sleep(100);
            receivedLength += thisReceptionLength;
            if (receivedLength < 16) continue;
          }

          // Check that we have a command in the first 16 bytes
          NetworkCommand command = GetCommand(cmdBuffer, out int paramsLength);
          if (command == NetworkCommand.NotValid) {
            string tmp = "";
            for (int i = 0; i < 16; i++)
              tmp += cmdBuffer[i].ToString("X") + " ";
            GD.DebugLog("Received garbage... " + tmp, GD.LT.Warning);
            if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { message = "Received garbage... " + tmp, type = ServerMessages.Error });
            receivedLength = 0;
            continue;
          }

          // We need to get the remaining "paramsLength" bytes and use them as parameter
          receivedLength = 0;
          while (GD.weAreAlive && receivedLength < paramsLength) {
            thisReceptionLength = stream.Read(paramsBuffer, receivedLength, paramsBuffer.Length - receivedLength);
            if (thisReceptionLength == 0) Thread.Sleep(100);
            receivedLength += thisReceptionLength;
            if (receivedLength < paramsLength) continue;
          }

          // Execute the actual command
          GD.DebugLog("Received command: |" + command.ToString() + ":" + paramsLength + "|", GD.LT.Log);
          switch (command) {
            case NetworkCommand.Connect:
              Connect(client, stream, paramsBuffer);
              break;
            case NetworkCommand.ConnectRemote:
              ConnectRemote(client, stream, paramsBuffer);
              break;
            case NetworkCommand.Ping:
              // Do it on the client only
              GD.DebugLog("We should not handle this command here! ||" + command.ToString() + "||", GD.LT.Warning);
              break;
            default:
              GD.DebugLog("Unknown command at root level! ||" + command.ToString() + "||", GD.LT.Warning);
              break;
          }
        }
      } catch (ThreadAbortException) {
        GD.weAreAlive = false;
        GD.DebugLog("Stopping because threadabort", GD.LT.DebugST);
      } catch (ThreadInterruptedException) {
        GD.DebugLog("Stopping because interrupt", GD.LT.DebugST);
        GD.weAreAlive = false;
      } catch (SocketException e) {
        GD.DebugLog("SocketException in Listening: " + e.Message, GD.LT.Warning);
        if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { message = "SocketException: " + e.Message, type = ServerMessages.Error });
      } catch (Exception e) {
        GD.DebugLog("Exception in Listening: " + e?.Message + "\n" + e.ToString(), GD.LT.Warning);
        GD.DebugLog(Environment.StackTrace, GD.LT.Error);
        if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { message = "Exception: " + e.Message, type = ServerMessages.Error });
      } finally {
        stream?.Close();
        client?.Close();
        serverTcpListener?.Stop();
      }

      if (GD.weAreAlive) {
        GD.DebugLog("Restarting server...", GD.LT.Log);
        OnServerMessage?.Invoke(this, new ServerMessage { message = "Restarting server...", type = ServerMessages.Info });
      }
    }
    if (serverTcpListener != null) serverTcpListener.Stop();
    if (GD.serverMode)
      GD.DebugLog("Server stopped!", GD.LT.Log);
    else
      OnServerMessage?.Invoke(this, new ServerMessage { message = "Server stopped!", type = ServerMessages.Info });
  }

  public async void ListenToClient(object playerObj) {
    Player player = (Player)playerObj;
    byte[] cmdBuffer = new byte[16];
    byte[] paramsBuffer = new byte[64 * 1024];
    bool localAlive = true;
    try {
      // Enter the listening loop.
      while (GD.weAreAlive && localAlive) {
        int receivedLength = 0;
        int thisReceptionLength = 0;
        while (GD.weAreAlive && receivedLength < 16) {
          thisReceptionLength = await player.stream.ReadAsync(cmdBuffer, receivedLength, cmdBuffer.Length - receivedLength);
          if (thisReceptionLength == 0) Thread.Sleep(2000); // We did not receive anything, just wait a couple of seconds
          receivedLength += thisReceptionLength;
          if (receivedLength < 16) continue;
        }

        NetworkCommand cmd = GetCommand(cmdBuffer, out int paramsLength);
        // We need to get the remaining "paramsLength" bytes and use them as parameter
        receivedLength = 0;
        while (GD.weAreAlive && receivedLength < paramsLength) {
          thisReceptionLength = await player.stream.ReadAsync(paramsBuffer, receivedLength, paramsBuffer.Length - receivedLength);
          if (thisReceptionLength == 0) Thread.Sleep(100);
          receivedLength += thisReceptionLength;
          if (receivedLength < paramsLength) continue;
        }

        // Process the command
        GD.DebugLog("Received command: |" + cmd.ToString() + ":" + paramsLength + "| from " + player.ToString(), GD.LT.Log);
        switch (cmd) {
          case NetworkCommand.Goodbye:
            Disconnect(player);
            localAlive = false; // No need to answer back, client will be disconnected
            break;
          case NetworkCommand.Ping:
            Answer(player, NetworkCommand.Pong, "Pong from " + serverIP + ":" + serverPort);
            break;
          case NetworkCommand.GamesList:
            player.LastAccess = DateTime.Now;
            Answer(player, NetworkCommand.GamesList, GenerateGameList(player.RemoteManager));
            break;
          case NetworkCommand.PlayersList:
            player.LastAccess = DateTime.Now;
            SendPlayerListToRemoteManagers(player);
            break;
          case NetworkCommand.CreateGame:
            CreateGame(player, paramsBuffer);
            break;
          case NetworkCommand.Join:
            JoinGame(player, paramsBuffer, paramsLength);
            break;
          case NetworkCommand.Leave:
            LeaveGame(player, paramsBuffer, paramsLength);
            break;
          case NetworkCommand.DeleteGame:
            DeleteGame(player, paramsBuffer, paramsLength);
            break;
          case NetworkCommand.SendChat:
            SendChatToClients(player, paramsBuffer);
            break;
          case NetworkCommand.GetPlayersFromGame:
            GetPlayersFromGame(player, paramsBuffer, paramsLength);
            break;
          case NetworkCommand.StartingTheGame:
            StartGame(player, paramsBuffer, paramsLength);
            break;
          case NetworkCommand.GetRunningGame:
            GetRunningGame(player, paramsBuffer, paramsLength);
            break;
          case NetworkCommand.SendGameAction:
            SetGameActionForClient(player, paramsBuffer);
            break;
          default:
            GD.DebugLog("Unknown command from " + player.Name + ": " + cmd.ToString() + " |" + System.Text.Encoding.Default.GetString(cmdBuffer) + "|", GD.LT.Log);
            break;
        }
      }
    } catch (ThreadAbortException) {
      localAlive = false;
      if (GD.serverMode)
        GD.DebugLog("Thread terminated.", GD.LT.Log);
      else
        OnServerMessage?.Invoke(this, new ServerMessage { message = player.Name + " Thread terminated", type = ServerMessages.Info });
    } catch (ThreadInterruptedException) {
      localAlive = false;
      if (GD.serverMode)
        GD.DebugLog("Thread terminated.", GD.LT.Log);
      else
        OnServerMessage?.Invoke(this, new ServerMessage { message = player.Name + " Thread terminated", type = ServerMessages.Info });
    } catch (SocketException e) {
      GD.DebugLog("SocketException when communicating with " + player.Name + ": " + e.Message + "\n" + e.ToString(), GD.LT.DebugST);
      foreach (Game game in games)
        game.RemovePlayer(player.ID);
      players.Remove(player);
      // Update the players list
      if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { playerList = players, message = "Player \"" + player.Name + "\" disconnected", type = ServerMessages.PlayersList });
      SendPlayerListToRemoteManagers();
      SendUpdateGameList();
      if (GD.serverMode)
        GD.DebugLog("SocketException when communicating with " + player.Name + ": " + e.Message + "\n" + e.ToString(), GD.LT.DebugST);
      else
        OnServerMessage?.Invoke(this, new ServerMessage { message = "SocketException when communicating with " + player.Name + ": " + e.Message + "\n" + e.ToString(), type = ServerMessages.Error });
    } catch (Exception e) {
      GD.DebugLog("Exception when communicating with " + player.Name + ": " + e.Message + "\n" + e.ToString(), GD.LT.DebugST);
      foreach (Game game in games)
        game.RemovePlayer(player.ID);
      players.Remove(player);
      if (!GD.serverMode)
        OnServerMessage?.Invoke(this, new ServerMessage { message = "Exception when communicating with " + player.Name + ": " + e.Message + "\n" + e.ToString(), type = ServerMessages.Error });
    }
    GD.DebugLog("Local listening thread for " + player.Name + " terminated!", GD.LT.Warning);
  }

  private void Connect(TcpClient client, NetworkStream stream, byte[] data) {
    Player player = new Player(data, 0);
    if (players.Count > 100) {
      player.tcpClient = client;
      player.stream = stream;
      Answer(player, NetworkCommand.Error, "Too many players on the server!");
      player.Kill();
      return;
    }
    Player foundPlayer = null;
    // Check if we have it
    foreach (Player p in players)
      if (p.ID == player.ID) {
        foundPlayer = p;
        p.LastAccess = DateTime.Now;
        break;
      }
    if (foundPlayer == null) { // Create
      player.tcpClient = client;
      player.stream = stream;
      players.Add(player);
      Answer(player, NetworkCommand.SuccessMsg, "Player created");
      Thread.Sleep(100);
      // Start a thread to wait for further client communications
      player.communicationThread = new Thread(new ParameterizedThreadStart(ListenToClient));
      player.communicationThread.Start(player);
    }
    else {
      // Remove the previous thread and Update the connection
      foundPlayer.communicationThread.Interrupt();
      Thread.Sleep(100);
      foundPlayer.communicationThread.Abort();
      Thread.Sleep(100);
      Answer(player, NetworkCommand.SuccessMsg, "Player connection updated");
      foundPlayer.communicationThread = new Thread(new ParameterizedThreadStart(ListenToClient));
      foundPlayer.communicationThread.Start(foundPlayer);
      foundPlayer.tcpClient = client;
      foundPlayer.stream = stream;
    }
    if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { message = "Player \"" + player.Name + " " + player.IP.ToString() + "\" connected.", type = ServerMessages.PlayersList, playerList = players });
    SendPlayerListToRemoteManagers();
    Answer(player, NetworkCommand.UpdateGames, "Update");
  }

  private void ConnectRemote(TcpClient client, NetworkStream stream, byte[] data) {
    Player player = new Player(data, 0) {
      tcpClient = client,
      stream = stream
    };
    Player foundPlayer = null;
    // Get the password
    int nameL = data[0];
    int ipL = data[nameL + 2];
    int pPos = nameL + ipL + 8 + 8 + 4;
    int pL = data[pPos];
    string password = System.Text.Encoding.UTF8.GetString(data, pPos + 1, pL);

    if (!string.IsNullOrWhiteSpace(serverPassword) && serverPassword != password) {
      Answer(player, NetworkCommand.Error, "Wrong password!");
      if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { message = "Wrong password from \"" + player.Name + " " + player.IP.ToString() + "\"", type = ServerMessages.Error });
      GD.DebugLog("Wrong password from \"" + player.Name + " " + player.IP.ToString() + "\"", GD.LT.Log);
      return;
    }

    // Check if we have it
    foreach (Player p in players)
      if (p.ID == player.ID) {
        foundPlayer = p;
        p.LastAccess = DateTime.Now;
        break;
      }
    if (foundPlayer == null) { // Create
      player.tcpClient = client;
      player.stream = stream;
      players.Add(player);
      Answer(player, NetworkCommand.SuccessMsg, player.ID.ToString());
      Thread.Sleep(100);
      // Start a thread to wait for further client communications
      player.communicationThread = new Thread(new ParameterizedThreadStart(ListenToClient));
      player.communicationThread.Start(player);
    }
    else {
      // Remove the previous thread and Update the connection
      foundPlayer.communicationThread.Interrupt();
      Thread.Sleep(100);
      foundPlayer.communicationThread.Abort();
      Thread.Sleep(100);
      Answer(player, NetworkCommand.SuccessMsg, player.ID.ToString());
      player.communicationThread = new Thread(new ParameterizedThreadStart(ListenToClient));
      player.communicationThread.Start(foundPlayer);
    }
    if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { message = "Remote server access \"" + player.Name + " " + player.IP.ToString() + "\" connected.", type = ServerMessages.PlayersList, playerList = players });
    SendPlayerListToRemoteManagers();
    Answer(player, NetworkCommand.UpdateGames, "Update");
  }

  private void Disconnect(Player player) {
    try {
      if (player.stream != null) player.stream.Close();
    } catch (Exception) { }
    try {
      if (player.tcpClient != null) player.tcpClient.Close();
    } catch (Exception) { }
    players.Remove(player);
    // Leave all the games we were in
    foreach (Game game in games) {
      if (game.Players.Remove(player.ID)) {
        game.NumJoined--;
        // Send a "cannot start" message to all the players
        for (int i = 0; i < game.Players.Count; i++) {
          Player p = GetPlayerByID(game.Players[i].id);
          if (p!=null) {
            if (runningGames.Contains(game))
              SendPlayerDeath(p, player.ID, game.Name); // Have the player to immediately die in the battle
            else
              SendGameCanStart(p, false, game.Name);
          }
        }
      }
    }

    Thread.Sleep(100);
    GD.DebugLog("Player \"" + player.Name + "\" disconnected", GD.LT.Log);
    if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { playerList = players, message = "Player \"" + player.Name + "\" disconnected", type = ServerMessages.PlayersList });
    SendPlayerListToRemoteManagers();
    SendUpdateGameList();
    player.Kill();
  }

  private byte[] GenerateGameList(bool fromRemoteServerManager) {
    /*
     * GAMELIST
     * 4 digits numgames
     * 4 digits numplayers
     * { Games stringified }
     */

    int numg = games.Count;
    int nump = players.Count; // In case it is not from a remote server we should filter out the remoteservermanagers
    if (!fromRemoteServerManager) {
      foreach (Player p in players)
        if (p.RemoteManager) nump--;
    }
    int total = 8; // numgames + numplayeres
    foreach (Game g in games) {
      byte[] gd = g.Serialize();
      total += gd.Length;
    }

    byte[] res = new byte[total];

    byte[] val = BitConverter.GetBytes(numg);
    for (int i = 0; i < 4; i++)
      res[i] = val[i];
    val = BitConverter.GetBytes(nump);
    for (int i = 0; i < 4; i++)
      res[i+4] = val[i];

    int pos = 8;
    foreach (Game g in games) {
      byte[] data = g.Serialize();
      for (int i = 0; i < data.Length; i++)
        res[pos + i] = data[i];
      pos += data.Length;
    }

    return res;
  }

  private void CreateGame(Player player, byte[] data) {
    player.LastAccess = DateTime.Now;
    if (games.Count > 50) {
      // Try to remove completed games
      for (int i = games.Count - 1; i >= 0; i--)
        if (games[i].Status == GameStatus.Completed)
          games.RemoveAt(i);
      if (games.Count > 50) {
        Answer(player, NetworkCommand.Error, "Too many games already defined!!");
        return;
      }
    }
    Game game = new Game(data, 0);

    // Generate the seed, it should be the same for everybody, but we want it pretty much random every time
    game.rndSeed = game.NumPlayers + game.NumAIs;
    byte[] nd = System.Text.Encoding.UTF8.GetBytes(game.Name);
    for (int i = 0; i < nd.Length; i++) {
      if (i == 0) game.rndSeed = game.rndSeed & 0x7fffff00 | ((game.rndSeed & 0x700000ff) ^ nd[i]);
      else if (i == 1) game.rndSeed = game.rndSeed & 0x7fff00ff | ((game.rndSeed & 0x7000ff00) ^ (nd[i] << 8));
      else if (i == 2) game.rndSeed = game.rndSeed & 0x7f00ffff | ((game.rndSeed & 0x70ff0000) ^ (nd[i] << 16));
      else if (i == 3) game.rndSeed = game.rndSeed & 0x00ffffff | ((game.rndSeed & 0x7f000000) ^ (nd[i] << 24));
    }
    game.rndSeed = (int)(game.rndSeed ^ DateTime.Now.Ticks);
    game.multiplayer = true;

    GD.DebugLog("Server: random seed = " + game.rndSeed, GD.LT.Log);

    for (int i = 0; i < game.Players.Count; i++) {
      if (GetPlayerByID(game.Players[i].id) == null)
        GD.DebugLog("Found an unknown player! " + game.Players[i].id + ": " + game.Players[i].name, GD.LT.Error);
    }

    // Is the game name unique?
    foreach (Game g in games)
      if (g.Name == game.Name) {
        Answer(player, NetworkCommand.Error, "A game called \"" + game.Name + "\" is already defined!");
        return;
      }

    // Do we need to remove this player from other games?
    foreach (Game g in games)
      g.RemovePlayer(player.ID);

    // Add it to the list
    games.Add(game);
    player.Status = StatusOfPlayer.ReadyToStart;
    player.CurrentGame = game.Name;
    if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { type = ServerMessages.GameList, gameList = games, num = players.Count, message = "Game \"" + game.Name + "\" created by " + player.Name });

    Answer(player, NetworkCommand.GameCreated, game.Name);
    GD.DebugLog("Game created by " + player.Name + ": " + game.Name, GD.LT.Log);
    SendUpdateGameList();
  }

  private void JoinGame(Player player, byte[] data, int dataLen) {
    player.LastAccess = DateTime.Now;
    string gamename = System.Text.Encoding.UTF8.GetString(data, 0, dataLen);
    if (string.IsNullOrEmpty(gamename)) {
      Answer(player, NetworkCommand.Error, "Game name is not specified!\n");
      return;
    }
    Game game = null;
    foreach(Game g in games)
      if (g.Name.Equals(gamename)) {
        game = g;
        break;
      }
    if (game == null) {
      Answer(player, NetworkCommand.Error, "Cannot find the game \"" + gamename + "\"!\n");
      return;
    }
    if (game.Status == GameStatus.Playing) {
      Answer(player, NetworkCommand.Error, "The game \"" + game.Name + "\" is already playing.\n");
      return;
    }
    if (game.Status == GameStatus.Completed) {
      Answer(player, NetworkCommand.Error, "The game \"" + game.Name + "\" is already terminated. Winner was: " + game.Winner + "\n");
      return;
    }
    if (game.NumPlayers <= game.NumJoined) {
      Answer(player, NetworkCommand.Error, "The game \"" + game.Name + "\" has already all players joined.\n");
      return;
    }

    // Leave any other game we where in
    foreach (Game g in games) {
      if (g == game) continue;
      g.RemovePlayer(player.ID);
      if (g.NumJoined == g.NumPlayers - 1) { // Here we need to send the "cannot start" message
        for (int i = 0; i < g.Players.Count; i++) {
          Player p = GetPlayerByID(g.Players[i].id);
          if (p != null) {
            g.Status = GameStatus.Waiting;
            SendGameCanStart(p, false, g.Name);
          }
        }
        SendGameCanStart(player, false, g.Name);
      }
    }
    game.NumJoined++;
    game.Players.Add(new SimplePlayer(player));
    player.Status = StatusOfPlayer.ReadyToStart;
    player.CurrentGame = game.Name;
    Answer(player, NetworkCommand.Joined, game.Name);
    GD.DebugLog("Player " + player.Name + " joined game: " + game.Name, GD.LT.Log);
    if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { type = ServerMessages.Info, message = "Player " + player.Name + " joined game: " + game.Name });

    if (game.NumJoined == game.NumPlayers) {
      game.Status = GameStatus.ReadyToStart;
      Thread.Sleep(100);
      // Send a "can start" message to all the players
      for (int i = 0; i < game.Players.Count; i++) {
        Player p = GetPlayerByID(game.Players[i].id);
        if (p != null) {
          SendGameCanStart(p, true, game.Name);
        }
      }
    }
    Thread.Sleep(100);
    if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { type = ServerMessages.GameList, gameList = games, num = players.Count });
    SendUpdateGameList();
    SendPlayerListToRemoteManagers();
  }

  private void LeaveGame(Player player, byte[] data, int dataLen) {
    player.LastAccess = DateTime.Now;
    string gamename = System.Text.Encoding.UTF8.GetString(data, 0, dataLen);
    if (string.IsNullOrEmpty(gamename)) {
      Answer(player, NetworkCommand.Error, "Game name is not specified!\n");
      return;
    }
    Game game = null;
    foreach (Game g in games)
      if (g.Name.Equals(gamename)) {
        game = g;
        break;
      }
    if (game == null) {
      Answer(player, NetworkCommand.Error, "Cannot find the game \"" + gamename + "\"!\n");
      return;
    }
    // Find the player now
    if (!game.Players.Contains(player.ID)) {
      Answer(player, NetworkCommand.Error, "You did not join the game \"" + game.Name + "\".\n");
      return;
    }
    if (game.Status == GameStatus.Playing) {
      // Have the player to immediately die in the battle
      if (runningGames.Contains(game)) {
        bool found = false;
        for (int i = 0; i < game.Players.Count; i++) {
          Player p = GetPlayerByID(game.Players[i].id);
          if (p != null && p.ID != player.ID) {
            SendPlayerDeath(p, player.ID, game.Name);
            found = true;
          }
        }
        if (!found) GD.DebugLog("The player with ID " + player.ID + " " + player.Name + " is not found in game " + gamename, GD.LT.Warning);
      }
    }
    game.NumJoined--;
    game.Players.Remove(player.ID);
    player.Status = StatusOfPlayer.Waiting;
    player.CurrentGame = "";
    player.TheGame = null;

    Answer(player, NetworkCommand.Left, game.Name);
    GD.DebugLog("Player " + player.Name + " left game: " + game.Name, GD.LT.Log);
    if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { type = ServerMessages.Info, message = "Player " + player.Name + " left game: " + game.Name });

    if (game.NumJoined < game.NumPlayers && game.Status != GameStatus.Playing) {
      game.Status = GameStatus.Waiting;
      Thread.Sleep(100);
      // Send a "cannot start" message to all the players
      for (int i = 0; i < game.Players.Count; i++) {
        Player p = GetPlayerByID(game.Players[i].id);
        if (p != null) {
          SendGameCanStart(p, false, game.Name);
        }
      }
    }
    Thread.Sleep(100);
    if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { type = ServerMessages.GameList, gameList = games, num = players.Count });
    SendUpdateGameList();
    SendPlayerListToRemoteManagers();
  }

  internal void DeleteGame(Player player, byte[] data, int dataLen) {
    if (player != null) player.LastAccess = DateTime.Now;
    string gamename = System.Text.Encoding.UTF8.GetString(data, 0, dataLen);

    if (string.IsNullOrEmpty(gamename)) {
      if (player != null) Answer(player, NetworkCommand.Error, "Game name is not specified!\n");
      return;
    }
    Game game = null;
    foreach (Game g in games)
      if (g.Name.Equals(gamename)) {
        game = g;
        break;
      }
    if (game == null) {
      if (player != null) Answer(player, NetworkCommand.Error, "Cannot find the game \"" + gamename + "\"!\n");
      return;
    }
    // Are we the owner of the game?
    if (player != null && player.ID != game.Creator.id && !player.RemoteManager) {
      Answer(player, NetworkCommand.Error, "You are not the owner of the game \"" + gamename + "\"!\n");
      return;
    }
    // Disconnect all players from the game and send an update
    if (player != null) {
      for (int i = 0; i < game.Players.Count; i++) {
        Player p = GetPlayerByID(game.Players[i].id);
        if (p != null && p.ID != player.ID) {
          Answer(p, NetworkCommand.GameDeleted, game.Name);
        }
      }
    }

    if (game.NumJoined == game.NumPlayers) {
      Thread.Sleep(100);
      // Send a "cannot start" message to all the players
      for (int i = 0; i < game.Players.Count; i++) {
        Player p = GetPlayerByID(game.Players[i].id);
        if (p != null) {
          SendGameCanStart(p, false, game.Name);
        }
      }
    }

    // Remove the game and send an update
    games.Remove(game);
    if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { type = ServerMessages.GameList, gameList = games, num = players.Count });
    if (player != null) {
      Answer(player, NetworkCommand.GameDeleted, game.Name);
      GD.DebugLog("Player " + player.Name + " deleted game: " + game.Name, GD.LT.Log);
      if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { type = ServerMessages.Info, message = "Player " + player.Name + " deleted game: " + game.Name });
    }
    Thread.Sleep(100);
    SendUpdateGameList();
  }

  private void SendUpdateGameList() {
    try {
      foreach (Player player in players)
        Answer(player, NetworkCommand.UpdateGames, "UpdateGlobal");
    } catch (Exception e) {
      GD.DebugLog("Error in SendUpdateGameList: " + e.Message, GD.LT.Debug);
    }
  }

  private void SendPlayerListToRemoteManagers(Player requester = null) {
    int len = 4;
    foreach (Player p in players)
      len += p.StringifyFullLen();
    /* 4 bytes numplayers
     * { full stringify of each player }
     */

    byte[] res = new byte[len];
    res[0] = (byte)(players.Count & 0xff);
    res[1] = (byte)((players.Count >> 8) & 0xff);
    res[2] = (byte)((players.Count >> 16) & 0xff);
    res[3] = (byte)((players.Count >> 24) & 0xff);

    int pos = 4;
    foreach (Player p in players) {
      byte[] pdata = p.StringifyFull();
      for (int i = 0; i < pdata.Length; i++)
        res[pos + i] = pdata[i];
      pos += pdata.Length;
    }
    if (requester != null && requester.RemoteManager) {
      Answer(requester, NetworkCommand.PlayersList, res);
    }
    else {
      foreach (Player p in players)
        if (p.RemoteManager) {
          Answer(p, NetworkCommand.PlayersList, res);
        }
    }
  }

  private Player GetPlayerByID(ulong id) {
    foreach (Player p in players)
      if (p.ID == id)
        return p;
    return null;
  }

  private void SendGameCanStart(Player player, bool canStart, string gamename) {
    byte[] gnd = System.Text.Encoding.UTF8.GetBytes(gamename);
    byte[] cmd = GenerateCommand(NetworkCommand.GameCanStart, 1 + 1 + gnd.Length);
    cmd[16] = (byte)(canStart ? 1 : 0);
    cmd[17] = (byte)gnd.Length;
    for (int i = 0; i < gnd.Length; i++)
      cmd[18 + i] = gnd[i];
    player.stream.Write(cmd, 0, cmd.Length);
  }

  public void GetPlayersFromGame(Player player, byte[] data, int dataLen) {
    string name = System.Text.Encoding.UTF8.GetString(data, 0, dataLen);
    // Do we have this game?
    Game game = null;
    foreach (Game g in games) {
      if (g.Name == name) {
        game = g;
        break;
      }
    }
    if (game == null) {
      Answer(player, NetworkCommand.Error, "Game \"" + name + "\" does not exists!");
      return;
    }

    byte[] gamename = System.Text.Encoding.UTF8.GetBytes(game.Name);
    byte[] sps = game.Players.Serialize();
    int len = 1 + gamename.Length + sps.Length;
    byte[] res = new byte[len];
    res[0] = (byte)gamename.Length;
    for (int i = 0; i < gamename.Length; i++)
      res[1 + i] = gamename[i];
    for (int i = 0; i < sps.Length; i++)
      res[1 + i + gamename.Length] = sps[i];

    Answer(player, NetworkCommand.SetPlayersFromGame, res);
  }


  public void StartGame(Player player, byte[] data, int dataLen) {
    string name = System.Text.Encoding.UTF8.GetString(data, 0, dataLen);
    // Do we have this game?
    Game game = null;
    foreach (Game g in games) {
      if (g.Name == name) {
        game = g;
        break;
      }
    }
    if (game == null) {
      Answer(player, NetworkCommand.Error, "Game \"" + name + "\" does not exists!");
      return;
    }

    // Set the current player as started
    SimplePlayer gsp = game.Players.GetByID(player.ID);
    if (gsp == null) {
      Answer(player, NetworkCommand.Error, "player \"" + player.Name + "\" is not inside the game \"" + name + "\"!");
      return;
    }
    player.Status = StatusOfPlayer.StartingGame;
    game.Players.SetStatus(player.ID, StatusOfPlayer.StartingGame);
    SendPlayerListToRemoteManagers();

    /* Return a command including all the players (id, name, and avatar) and if they are starting or not, add a last byte to tell if the game is actually ready to begin
     1 byte -> game name len
     n bytes -> game name
     1 byte -> can actually begin (0 = no, 1 = yes)
     simpleplayers serialized
     */
    byte[] gamename = System.Text.Encoding.UTF8.GetBytes(game.Name);
    byte[] sps = game.Players.Serialize();
    byte[] res = new byte[2 + gamename.Length + sps.Length];

    int pos = 0;
    res[pos] = (byte)gamename.Length;
    pos++;
    for (int i = 0; i < gamename.Length; i++)
      res[pos + i] = gamename[i];
    pos += gamename.Length;
    // Check if all players started
    bool canStart = true;
    for (int i = 0; i < game.Players.Count; i++) {
      SimplePlayer sp = game.Players[i];
      if (!sp.ai && sp.status != StatusOfPlayer.StartingGame) {
        canStart = false;
      }
    }
    res[pos] = (byte)(canStart ? 1 : 0);
    pos++;
    for (int i = 0; i < sps.Length; i++)
      res[pos + i] = sps[i];

    // Start the actual game server side if needed
    if (canStart && game.Status != GameStatus.Playing) {
      // Set the status and add it to the list of running games
      game.Status = GameStatus.Playing;
      runningGames.Add(game);

      // Fill the AIs
      int[] allAIs = new int[GD.enemies.Length];
      for (int i = 0; i < GD.enemies.Length; i++)
        allAIs[i] = i;
      for (int i = 0; i < 1000; i++) {
        int a = random.Next(0, GD.enemies.Length - 1);
        int b = random.Next(0, GD.enemies.Length - 1);
        int tmp = allAIs[a];
        allAIs[a] = allAIs[b];
        allAIs[b] = tmp;
      }

      // Get only the first n, where n is the number of expected AIs
      for (int i = 0; i < game.NumAIs; i++)
        game.Players.Add(new SimplePlayer(allAIs[i]));

      // Randomize the positions
      for (int i = 0; i < 1000; i++) {
        int a = random.Next(0, game.Players.Count);
        int b = random.Next(0, game.Players.Count);
        SimplePlayer tmp = game.Players[a];
        game.Players[a] = game.Players[b];
        game.Players[b] = tmp;
      }

      // Init the engine
      game.engine = new GameEngine(null);
      game.engine.InitEnemies(game);

      // Update the server and the remote servers
      if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { type = ServerMessages.GameList, gameList = games, num = players.Count, message = "Game \"" + game.Name + "\" created by " + player.Name });
      SendUpdateGameList();
    }

    // Set the game for the player and send the message to all players
    for (int i = 0; i < game.Players.Count; i++) {
      Player p = GetPlayerByID(game.Players[i].id);
      if (p != null) {
        p.TheGame = game;
        Answer(p, NetworkCommand.StartingTheGame, res);
      }
    }
  }

  public void GetRunningGame(Player player, byte[] data, int dataLen) {
    string name = System.Text.Encoding.UTF8.GetString(data, 0, dataLen);
    // Do we have this game?
    Game game = null;
    foreach (Game g in runningGames) {
      if (g.Name == name) {
        game = g;
        break;
      }
    }
    if (game == null) {
      Answer(player, NetworkCommand.Error, "Game \"" + name + "\" is not playing!");
      return;
    }

    // Set the current player as started
    player.Status = StatusOfPlayer.Playing;
    game.Players.SetStatus(player.ID, StatusOfPlayer.Playing);

    // Return the full definition of the game
    byte[] rnd = BitConverter.GetBytes(game.rndSeed);
    byte[] gd = game.Serialize();
    byte[] res = new byte[rnd.Length + gd.Length];
    for (int i = 0; i < 4; i++)
      res[i] = rnd[i];
    for (int i = 0; i < gd.Length; i++)
      res[i + 4] = gd[i];

    Answer(player, NetworkCommand.GetRunningGame, res);
  }

  private void SendPlayerDeath(Player player, ulong id, string gameName) {
    byte[] idd = BitConverter.GetBytes(id);
    byte[] gnd = System.Text.Encoding.UTF8.GetBytes(gameName);
    byte[] data = new byte[idd.Length + gnd.Length + 1];
    for (int i = 0; i < idd.Length; i++)
      data[i] = idd[i];
    data[idd.Length] = (byte)gnd.Length;
    for (int i = 0; i < gnd.Length; i++)
      data[1 + i + idd.Length] = gnd[i];

    GD.DebugLog("Sending playerdeath to " + player.Name, GD.LT.Log);
    Answer(player, NetworkCommand.PlayerDeath, data);
  }

  
  public void SetGameActionForClient(Player player, byte[] data) {
    GameAction gameAction = new GameAction(data, 0);

    if (player.TheGame == null) {
      Answer(player, NetworkCommand.Error, System.Text.Encoding.UTF8.GetBytes("!Missing game for player: " + player.Name));
      GD.DebugLog("Missing game for player: " + player.Name, GD.LT.Debug);
      return;
    }
    // Find the game
    bool found = false;
    foreach (Game g in games)
      if (g == player.TheGame) {
        found = true;
        break;
      }
    if (!found) {
      Answer(player, NetworkCommand.Error, System.Text.Encoding.UTF8.GetBytes("!Cannot find the game \"" + player.TheGame.Name + "\" played by the player: " + player.Name));
      GD.DebugLog("Cannot find the game \"" + player.TheGame.Name + "\" played by the player: " + player.Name, GD.LT.Debug);
      return;
    }
    if (player.TheGame.Status != GameStatus.Playing) {
      Answer(player, NetworkCommand.Error, System.Text.Encoding.UTF8.GetBytes("!Game \"" + player.TheGame.Name + "\" is not playing!"));
      return;
    }

    // Broadcast the progress message
    for (int i = 0; i < player.TheGame.Players.Count; i++) {
      SimplePlayer sp = player.TheGame.Players[i];
      if (sp.ai) continue;
      Player p = GetPlayerByID(player.TheGame.Players[i].id);
      if (p != null && p.tcpClient != null && p.tcpClient.Connected)
        Answer(p, NetworkCommand.GameProgressUpdate, BitConverter.GetBytes(player.ID));
      else
        GD.DebugLog("But " + sp.ToString() + " seems to be not existing!", GD.LT.Debug);
    }

    player.TheGame.engine.EndTurn(true, player, gameAction);
  }

  public void SendGameTurn(GameEngineValues game) {
    byte[] data = game.Serialize();

    for (int i = 0; i < 6; i++) {
      PlayerStatus sp = game.players[i];
      if (sp == null || sp.isAI) continue;
      Player p = GetPlayerByID(sp.id);
      if (p == null) continue;
      Answer(p, NetworkCommand.GameTurn, data);
    }
  }

  #endregion Server

  #region Events ************************************************************************************************************************************************************************************

  public void ShutDown() {
    GD.weAreAlive = false;
    GD.DebugLog("Stopping because shutdown", GD.LT.Debug);
    if (mainListeningThread != null) {
      GD.DebugLog("Stopping main listening thread...", GD.LT.Log);
      mainListeningThread.Abort();
    }
    if (players != null) {
      foreach (Player p in players) {
        try {
          if (p.stream != null) p.stream.Close();
        } catch (Exception) { }
        try {
          if (p.tcpClient != null) p.tcpClient.Close();
        } catch (Exception) { }
        try {
          GD.DebugLog("Stopping communicating thread for " + p.Name + "...", GD.LT.Log);
          if (p.communicationThread != null) {
            p.communicationThread.Abort();
          }
        } catch (Exception) { }
      }
    }
    // Player client shutdown
    if (GD.thePlayer != null) {
      try {
        if (GD.thePlayer.stream != null)
          GD.thePlayer.stream.Close();
      } catch (Exception e) {
        GD.DebugLog("TCP Exception shutting down: " + e.Message, GD.LT.Error);
      }
      try {
        if (GD.thePlayer.tcpClient != null)
          GD.thePlayer.tcpClient.Close();
      } catch (Exception e) {
        GD.DebugLog("Stream Exception shutting down: " + e.Message, GD.LT.Error);
      }
      Thread.Sleep(50);
    }
    GD.DebugLog("Goodbye!", GD.LT.Log);
  }

  public EventHandler<ServerMessage> OnServerMessage;
  public class ServerMessage : EventArgs {
    public ServerMessages type;
    public string message;
    public int num;
    public List<Game> gameList;
    public List<Player> playerList;
    public SimpleList gamePlayersList;
  }

  public class ChatMessage : EventArgs {
    public ChatID id;
    public ChatType type;
    public ulong senderid;
    public int senderavatar;
    public string message;
    public string chatname;
    public List<ChatParticipant> participants;

    public override string ToString() {
      return ">" + type.ToString() + " from " + senderid + ": " + message + "<";
    }

    public byte[] Stringify(Player player) {
      byte[] msg = System.Text.Encoding.UTF8.GetBytes(message);
      int msglen = (msg.Length > 32000) ? 32000 : msg.Length;
      int totalParticipantsLen = 1; // Size
      foreach (ChatParticipant p in participants)
        totalParticipantsLen += p.GetStringSize();

      int dataLen = 17 + 2 + msglen + 8 + 1 + 1 + totalParticipantsLen;
      byte[] data = new byte[dataLen];
      int pos = 0;
      byte[] chatidbytes = id.GetBytes();
      for (int i = 0; i < 16; i++)
        data[pos + i] = chatidbytes[i];
      data[16] = (byte)type;
      pos += 17;
      data[pos] = (byte)(msglen & 0xFF);
      data[pos + 1] = (byte)((msglen >> 8) & 0xFF);
      pos += 2;
      for (int i = 0; i < msglen; i++)
        data[pos + i] = msg[i];
      pos += msglen;
      byte[] idp = BitConverter.GetBytes(player.ID);
      for (int i = 0; i < 8; i++)
        data[pos + i] = idp[i];
      pos += 8;
      data[pos] = (byte)player.Avatar;
      pos++;
      data[pos] = (byte)participants.Count;
      pos++;
      foreach (ChatParticipant p in participants) {
        byte[] cp = p.Stringify();
        for (int i = 0; i < cp.Length; i++)
          data[pos + i] = cp[i];
        pos += cp.Length;
      }
      return data;
    }

    public byte[] Stringify(ulong pid, int avatar) {
      byte[] msg = System.Text.Encoding.UTF8.GetBytes(message);
      int msglen = (msg.Length > 32000) ? 32000 : msg.Length;
      int totalParticipantsLen = 1; // Size
      foreach (ChatParticipant p in participants)
        totalParticipantsLen += p.GetStringSize();

      int dataLen = 17 + 2 + msglen + 8 + 1 + 1 + totalParticipantsLen;
      byte[] data = new byte[dataLen];
      int pos = 0;
      byte[] chatidbytes = id.GetBytes();
      for (int i = 0; i < 16; i++)
        data[pos + i] = chatidbytes[i];
      data[16] = (byte)type;
      pos += 17;
      data[pos] = (byte)(msglen & 0xFF);
      data[pos + 1] = (byte)((msglen >> 8) & 0xFF);
      pos += 2;
      for (int i = 0; i < msglen; i++)
        data[pos + i] = msg[i];
      pos += msglen;
      byte[] idp = BitConverter.GetBytes(pid);
      for (int i = 0; i < 8; i++)
        data[pos + i] = idp[i];
      pos += 8;
      data[pos] = (byte)avatar;
      pos++;
      data[pos] = (byte)participants.Count;
      pos++;
      foreach (ChatParticipant p in participants) {
        byte[] cp = p.Stringify();
        for (int i = 0; i < cp.Length; i++)
          data[pos + i] = cp[i];
        pos += cp.Length;
      }
      return data;
    }
    public void FromBytes(byte[] data) {
      /* Chat format
       * 16 bytes for chatid
       * 1 byte for type
       * 2 (17) bytes msg len
       * n (19) bytes msg
       * 8 (19 + n) bytes sender ID
       * 1 (27 + n) byte sender avatar
       * 1 (28 + n) byte num destinations
       * { serialization of each participant } (29 + n)
       */

      ulong id1 = BitConverter.ToUInt64(data, 0);
      ulong id2 = BitConverter.ToUInt64(data, 8);
      id = new ChatID(id1, id2);
      type = (ChatType)data[16];

      int mlen = data[17] + (data[18] << 8);
      message = System.Text.Encoding.UTF8.GetString(data, 19, mlen);

      senderid = BitConverter.ToUInt64(data, 19 + mlen);
      senderavatar = data[27 + mlen];
      int nump = data[28 + mlen];
      int pos = 29 + mlen;
      participants = new List<ChatParticipant>();
      for (int i = 0; i < nump; i++) {
        ChatParticipant p = new ChatParticipant(data, pos);
        participants.Add(p);
        pos += data[pos];
      }
    }
  }

  public class GameMessage : EventArgs {
    public GameMsgType type;
    public string text;
    public int num;
    public ulong id;
    public GameEngineValues engineValues;
    // FIXME add all field we may need

    // status of each player
    // order of actions
    // actions of each player
    // techs and imps of each player
    // situation of all cities
    // Result of the actions


  }

  #endregion

  #region Communications ************************************************************************************************************************************************************************************

  internal byte[] GenerateCommand(NetworkCommand command, int paramsLenght) {
    byte[] res = new byte[16 + paramsLenght];
    switch (command) {
      case NetworkCommand.Connect:
        System.Text.Encoding.UTF8.GetBytes("Connect ", 0, 8, res, 0);
        break;
      case NetworkCommand.Ping:
        System.Text.Encoding.UTF8.GetBytes("Ping    ", 0, 8, res, 0);
        break;
      case NetworkCommand.ConnectRemote:
        System.Text.Encoding.UTF8.GetBytes("ConnectR", 0, 8, res, 0);
        break;
      case NetworkCommand.SuccessMsg:
        System.Text.Encoding.UTF8.GetBytes("SuccessM", 0, 8, res, 0);
        break;
      case NetworkCommand.Goodbye:
        System.Text.Encoding.UTF8.GetBytes("GoodBye ", 0, 8, res, 0);
        break;
      case NetworkCommand.Pong:
        System.Text.Encoding.UTF8.GetBytes("Pong    ", 0, 8, res, 0);
        break;
      case NetworkCommand.UpdateGames:
        System.Text.Encoding.UTF8.GetBytes("UpdateGs", 0, 8, res, 0);
        break;
      case NetworkCommand.GamesList:
        System.Text.Encoding.UTF8.GetBytes("GameList", 0, 8, res, 0);
        break;
      case NetworkCommand.PlayersList:
        System.Text.Encoding.UTF8.GetBytes("PlrsList", 0, 8, res, 0);
        break;
      case NetworkCommand.CreateGame:
        System.Text.Encoding.UTF8.GetBytes("CreateG ", 0, 8, res, 0);
        break;
      case NetworkCommand.GameCreated:
        System.Text.Encoding.UTF8.GetBytes("GameCrtd", 0, 8, res, 0);
        break;
      case NetworkCommand.Join:
        System.Text.Encoding.UTF8.GetBytes("JoinG   ", 0, 8, res, 0);
        break;
      case NetworkCommand.Joined:
        System.Text.Encoding.UTF8.GetBytes("GJoined ", 0, 8, res, 0);
        break;
      case NetworkCommand.Leave:
        System.Text.Encoding.UTF8.GetBytes("LeaveG  ", 0, 8, res, 0);
        break;
      case NetworkCommand.Left:
        System.Text.Encoding.UTF8.GetBytes("GLeft   ", 0, 8, res, 0);
        break;
      case NetworkCommand.DeleteGame:
        System.Text.Encoding.UTF8.GetBytes("DelGame ", 0, 8, res, 0);
        break;
      case NetworkCommand.GameDeleted:
        System.Text.Encoding.UTF8.GetBytes("GameDel ", 0, 8, res, 0);
        break;
      case NetworkCommand.GameCanStart:
        System.Text.Encoding.UTF8.GetBytes("GCanStar", 0, 8, res, 0);
        break;
      case NetworkCommand.Error:
        System.Text.Encoding.UTF8.GetBytes("Error   ", 0, 8, res, 0);
        break;

      case NetworkCommand.SendChat:
        System.Text.Encoding.UTF8.GetBytes("SendChat", 0, 8, res, 0);
        break;
      case NetworkCommand.ReceiveChat:
        System.Text.Encoding.UTF8.GetBytes("RecvChat", 0, 8, res, 0);
        break;
      case NetworkCommand.GetPlayersFromGame:
        System.Text.Encoding.UTF8.GetBytes("GetPlGam", 0, 8, res, 0);
        break;
      case NetworkCommand.SetPlayersFromGame:
        System.Text.Encoding.UTF8.GetBytes("SetPlGam", 0, 8, res, 0);
        break;
      case NetworkCommand.StartingTheGame:
        System.Text.Encoding.UTF8.GetBytes("StartGam", 0, 8, res, 0);
        break;
      case NetworkCommand.GetRunningGame:
        System.Text.Encoding.UTF8.GetBytes("GetRunGa", 0, 8, res, 0);
        break;

      case NetworkCommand.PlayerDeath:
        System.Text.Encoding.UTF8.GetBytes("PlayDeat", 0, 8, res, 0);
        break;
      case NetworkCommand.SendGameAction:
        System.Text.Encoding.UTF8.GetBytes("SndGameA", 0, 8, res, 0);
        break;
      case NetworkCommand.GameProgressUpdate:
        System.Text.Encoding.UTF8.GetBytes("GamePrUp", 0, 8, res, 0);
        break;
      case NetworkCommand.GameTurn:
        System.Text.Encoding.UTF8.GetBytes("GameTurn", 0, 8, res, 0);
        break;

      default:
        GD.DebugLog(">>>>>>>>> Unknown command: " + command.ToString(), GD.LT.Warning);
        break;
    }
    res[8] = (byte)(paramsLenght & 0xff);
    res[9] = (byte)((paramsLenght >> 8) & 0xff);
    res[10] = (byte)((paramsLenght >> 16) & 0xff);
    res[11] = (byte)((paramsLenght >> 24) & 0xff);
    res[12] = 0;
    res[13] = 0;
    res[14] = 0;
    res[15] = (byte)'\n';

    return res;
  }

  internal static NetworkCommand GetCommand(byte[] data, out int paramsLength) {
    string possibleCommand = System.Text.Encoding.UTF8.GetString(data, 0, 8);
    int len = (int)data[8] + ((int)data[9] << 8) + ((int)data[10] << 16) + ((int)data[11] << 24);
    if (data[12] != 0 || data[13] != 0 || data[14] != 0 || data[15] != (byte)'\n') {
      paramsLength = 0;
      return NetworkCommand.NotValid;
    }
    paramsLength = len;
    possibleCommand = possibleCommand.ToUpperInvariant().Trim();
    if (possibleCommand == "CONNECT") return NetworkCommand.Connect;
    if (possibleCommand == "PING") return NetworkCommand.Ping;
    if (possibleCommand == "CONNECTR") return NetworkCommand.ConnectRemote;
    if (possibleCommand == "SUCCESSM") return NetworkCommand.SuccessMsg;
    if (possibleCommand == "GOODBYE") return NetworkCommand.Goodbye;
    if (possibleCommand == "PONG") return NetworkCommand.Pong;
    if (possibleCommand == "UPDATEGS") return NetworkCommand.UpdateGames;
    if (possibleCommand == "GAMELIST") return NetworkCommand.GamesList;
    if (possibleCommand == "PLRSLIST") return NetworkCommand.PlayersList;
    if (possibleCommand == "CREATEG") return NetworkCommand.CreateGame;
    if (possibleCommand == "GAMECRTD") return NetworkCommand.GameCreated;
    if (possibleCommand == "JOING") return NetworkCommand.Join;
    if (possibleCommand == "GJOINED") return NetworkCommand.Joined;
    if (possibleCommand == "LEAVEG") return NetworkCommand.Leave;
    if (possibleCommand == "GLEFT") return NetworkCommand.Left;
    if (possibleCommand == "DELGAME") return NetworkCommand.DeleteGame;
    if (possibleCommand == "GAMEDEL") return NetworkCommand.GameDeleted;
    if (possibleCommand == "GCANSTAR") return NetworkCommand.GameCanStart;
    if (possibleCommand == "ERROR") return NetworkCommand.Error;
    if (possibleCommand == "SENDCHAT") return NetworkCommand.SendChat;
    if (possibleCommand == "RECVCHAT") return NetworkCommand.ReceiveChat;
    if (possibleCommand == "GETPLGAM") return NetworkCommand.GetPlayersFromGame;
    if (possibleCommand == "SETPLGAM") return NetworkCommand.SetPlayersFromGame;
    if (possibleCommand == "STARTGAM") return NetworkCommand.StartingTheGame;
    if (possibleCommand == "GETRUNGA") return NetworkCommand.GetRunningGame;
    if (possibleCommand == "PLAYDEAT") return NetworkCommand.PlayerDeath;
    if (possibleCommand == "SNDGAMEA") return NetworkCommand.SendGameAction;
    if (possibleCommand == "GAMEPRUP") return NetworkCommand.GameProgressUpdate;
    if (possibleCommand == "GAMETURN") return NetworkCommand.GameTurn;

    return NetworkCommand.NotValid;
  }

  private void Answer(Player player, NetworkCommand cmd, string data) {
    try {
      GD.DebugLog("Sending " + cmd.ToString() + " to " + player.ToString(), GD.LT.Debug);
      byte[] msg = System.Text.Encoding.UTF8.GetBytes(data);
      byte[] answer = GenerateCommand(cmd, msg.Length);
      player.stream.Write(answer, 0, 16);
      player.stream.Write(msg, 0, msg.Length);
    } catch (Exception e) {
      GD.DebugLog(">> Player " + player.ToString() + " communication error: " + e.Message, GD.LT.DebugST);
      players.Remove(player);
      player.Kill();
      if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { playerList = players, message = "", type = ServerMessages.PlayersList });
      if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { message = ">> Player " + player.ToString() + " communication error: " + e.Message, type = ServerMessages.Error });
      SendPlayerListToRemoteManagers();
    }
  }

  private void Answer(Player player, NetworkCommand cmd, byte[] data) {
    try {
      GD.DebugLog("Sending " + cmd.ToString() + " to " + player.ToString(), GD.LT.Log);
      byte[] answer = GenerateCommand(cmd, data.Length);
      player.stream.Write(answer, 0, 16);
      player.stream.Write(data, 0, data.Length);
    } catch (Exception e) {
      GD.DebugLog(">> Player " + player.ToString() + " communication error: " + e.Message, GD.LT.Warning);
      players.Remove(player);
      player.Kill();
      if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { playerList = players, message = "", type = ServerMessages.PlayersList });
      if (!GD.serverMode) OnServerMessage?.Invoke(this, new ServerMessage { message = ">> Player " + player.ToString() + " communication error: " + e.Message, type = ServerMessages.Error });
      SendPlayerListToRemoteManagers();
    }
  }

  #endregion Communications


  #region Chat ************************************************************************************************************************************************************************************

  public void SendChat(ChatID chatid, ChatType type, Player player, List<ChatParticipant> participants, string message) {

    string dbg = "";
    foreach (ChatParticipant cp in participants)
      dbg += cp.id + ", ";
    GD.DebugLog("Client send message from " + player + ": " + message + " -> " + dbg, GD.LT.Log);


    // Build the message and send it to all our party
    if (player.tcpClient == null || !player.tcpClient.Connected) {
      player.OnChat?.Invoke(this, new ChatMessage { id = chatid, type = ChatType.Error, message = "You are not connected to a server!" });
      return;
    }
    try {
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

      ChatMessage chat = new ChatMessage {
        id = chatid,
        type = type,
        senderid = player.ID,
        senderavatar = player.Avatar,
        message = message,
        chatname = "", // If empty it should not update
        participants = participants
      };

      byte[] chatbytes = chat.Stringify(player);
      byte[] data = GenerateCommand(NetworkCommand.SendChat, chatbytes.Length);
      for (int i = 0; i < chatbytes.Length; i++)
        data[16 + i] = chatbytes[i];
      player.stream.Write(data, 0, data.Length);
    } catch (IOException e) {
      player.OnChat?.Invoke(this, new ChatMessage { id = chatid, type = ChatType.Error, message = "Error: " + e.Message });
    } catch (SocketException e) {
      player.OnChat?.Invoke(this, new ChatMessage { id = chatid, type = ChatType.Error, message = "Error: " + e.Message });
    }
  }


  public void SendChatToClients(Player player, byte[] data) {
    // Get the message, pick all the participants, and send to all of them the message

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

    ChatMessage chat = new ChatMessage();
    chat.FromBytes(data);

    // Check if the participants are still here
    List<ChatParticipant> goners = new List<ChatParticipant>();
    foreach(ChatParticipant cp in chat.participants) {
      bool found = false;
      foreach (Player p in players)
        if (p.ID == cp.id) {
          found = true;
          break;
        }
      if (!found)
        goners.Add(cp);
    }

    foreach (ChatParticipant cp in goners)
      chat.participants.Remove(cp);
    ChatMessage gonechat = null;
    if (goners.Count > 0) {
      gonechat = new ChatMessage {
        id = chat.id,
        type = ChatType.ParticipantGone
      };
    }

    while (goners.Count > 0) { // Send a chat message telling that the person is gone
      ChatParticipant cp = goners[0];
      goners.RemoveAt(0);
      gonechat.message = "<b>Server Message</b>: The participant <i><sprite=" + cp.avatar + "> " + cp.name + "</i> is no more connected!";
      gonechat.senderid = cp.id;
      gonechat.senderavatar = cp.avatar;
      gonechat.participants = chat.participants;
      foreach (ChatParticipant participant in chat.participants) {
        Player receiver = GetPlayerByID(participant.id);
        if (receiver != null)
          Answer(receiver, NetworkCommand.ReceiveChat, gonechat.Stringify(cp.id, cp.avatar));
      }
    }

    foreach (ChatParticipant cp in chat.participants) {
      Player receiver = GetPlayerByID(cp.id);
      if (receiver != null)
        Answer(receiver, NetworkCommand.ReceiveChat, chat.Stringify(player));
    }
  }
  
  #endregion Chat
}


