using System.Collections.Generic;
using UnityEngine;

public class GD : MonoBehaviour {
  // Global class to keep all static stateless functions and the parameters, the global enaums are defined in this file (but outsize the class)
  public const int _definedChars_ = 14;
  public const string Version = "V1.1 2020/04/10";

  public const int _GameActionLen = 4;
  public const int _PlayerStatusLen = 2 + // len
              8 + // id
              1 + // happiness
              8 + // techs availability as bitfield
              8 + // bitfiled for ownership of cities + defeated flag
              10 * 4 * 3 + // resources (10 types * 3 values * 4 bytes)
              _GameActionLen;



  public static GD instance = null;

  private static LoaderStatus loaderStatus = LoaderStatus.Startup;
  public static LoaderStatus GetStatus() {
    return loaderStatus;
  }
  public static void SetStatus(LoaderStatus status) {
    loaderStatus = status;
    DebugLog("Status changed: " + loaderStatus, LT.Debug);
  }
  public static bool IsStatus(LoaderStatus status) {
    return loaderStatus == status;
  }
  public static bool IsStatus(LoaderStatus status1, LoaderStatus status2) {
    return loaderStatus == status1 || loaderStatus == status2;
  }




  public GameLoader gameLoader;
  public NetworkManager networkManager;
  internal static FullScreenMode fullscreen = FullScreenMode.Windowed;
  public List<Sprite> AvatarsList;
  public List<GameObject> PlayersBase;
  public static bool weAreAlive = false;
  System.Random random = null;


  public static PlayerHandler thePlayer = null;
  public static byte localPlayerIndex = 255;
  public static int difficulty; // FIXME do we need it?
  public static Enemy[] enemies; // FIXME Are we using it?

  public const int CPUAvatarIndex = 74;

  #region *************************************************** Options ***************************************************
  public static FullScreenMode fullScreen;
  public static int mainVolume;
  public static bool useTypingSound;
  public static bool writeLogsOnFile;
  public static string filePathForLogs;
  public static LT logsLevel = LT.Log;
  public static bool serverMode = false;
  #endregion Options

  #region *************************************************** Items ***************************************************
  public Item[] Technologies;
  public Item[] Improvements;
  #endregion Items

  private void Awake() {
    if (instance == null)
      instance = this;
    networkManager = new NetworkManager();

    string[] args = System.Environment.GetCommandLineArgs();
    for (int i = 0; i < args.Length; i++) {
      if (args[i] == "-server") {
        loaderStatus = LoaderStatus.ServerBackground;
        DebugLog("Starting as server", GD.LT.Log);
        // ./UncivilizationLinux.x86_64 -server -batchmode -nographics -port 55601 -password olc
      }
      if (args[i] == "-port") {
        networkManager.SetPort(args[i + 1]);
        DebugLog("Listening port is: " + args[i + 1], GD.LT.Log);
      }
      if (args[i] == "-password") {
        networkManager.serverPassword = args[i + 1];
        DebugLog("Using password for server", GD.LT.Log);
      }
    }

    if (loaderStatus == LoaderStatus.ServerBackground) {
      networkManager.StartUpServer();
    }


    // Read the options
    fullScreen = (FullScreenMode)PlayerPrefs.GetInt("FullScreen", 3);
    mainVolume = PlayerPrefs.GetInt("MainVolume", 100);
    useTypingSound = PlayerPrefs.GetInt("UseTypingSound", 1) != 0;
    writeLogsOnFile = PlayerPrefs.GetInt("WriteLogsOnFile", 1) != 0;
    logsLevel = (LT)PlayerPrefs.GetInt("LogsLevel", (int)LT.Warning);
    filePathForLogs = PlayerPrefs.GetString("FilePathForLogs", Application.persistentDataPath);
    if (string.IsNullOrWhiteSpace(filePathForLogs)) filePathForLogs = "C:\\Users\\claud\\Unity\\Uncivilization\\Logs\\Server Logs\\";

    logsLevel = LT.DebugST; // FIXME remove after debugging
    writeLogsOnFile = true; // FIXME

    AudioListener.volume = mainVolume / 100f;
    Screen.fullScreenMode = fullScreen;
    Screen.fullScreen = fullScreen != FullScreenMode.Windowed && fullScreen != FullScreenMode.Windowed;


    Debug.Log(filePathForLogs);

    // Init the log file
    InitLogFile();

    enemies = gameLoader.GetEnemies();
    gameLoader.InitGame();
  }

  internal static bool InitLogFile() {
    if (!writeLogsOnFile) return false;
    try {
      if (debugFile != null) debugFile.Close();

      string filename = "Uncivilization debug log " + (System.DateTime.Now.Millisecond & 0x1FFFFFF).ToString("X") + ".log";
      filename = System.IO.Path.Combine(filePathForLogs, filename);
      debugFile = new System.IO.StreamWriter(filename, true, System.Text.Encoding.UTF8);
      debugFile.WriteLine("Uncivilization Debug Log created");
      return false;
    } catch (System.Exception e) {
      Debug.Log("Error opening log file: " + e.Message);
      return true;
    }
  }


  internal static Sprite Avatar(int num) {
    if (num < 0 || num >= instance.AvatarsList.Count) return instance.AvatarsList[0];
    return instance.AvatarsList[num];
  }

  void OnApplicationQuit() {
    if (instance != null && instance.networkManager != null) {
      if (thePlayer != null && thePlayer.tcpClient != null) instance.networkManager.DisconnectFromServer(thePlayer);
      instance.networkManager.ShutDown();
    }

    // Save the options
    PlayerPrefs.SetInt("FullScreen", (int)fullScreen);
    PlayerPrefs.SetInt("MainVolume", mainVolume);
    PlayerPrefs.SetInt("UseTypingSound", useTypingSound ? 1 : 0);
    PlayerPrefs.SetInt("WriteLogsOnFile", writeLogsOnFile ? 1 : 0);
    PlayerPrefs.SetString("FilePathForLogs", filePathForLogs);
    PlayerPrefs.SetInt("LogsLevel", (int)logsLevel);
  }

  public static void InitRandomGenerator(int seed) {
    instance.random = new System.Random(seed);
  }

  public static int GetRandom(int min, int max) {
    if (instance.random == null) {
      DebugLog("GetRandom called without initialization!", LT.DebugST);
      instance.random = new System.Random(System.DateTime.Now.Millisecond);
    }
    return instance.random.Next(min, max);
  }
  public static float GetRandom(float min, float max) {
    if (instance.random == null) {
      DebugLog("GetRandom called without initialization!", LT.DebugST);
      instance.random = new System.Random(System.DateTime.Now.Millisecond);
    }
    double val = instance.random.NextDouble();
    if (max > min)
      val *= (max - min);
    else
      val *= (min - max);
    val += min;
    return (float)val;
  }

  public static byte GetAvatarForAI(string name) {
    switch (name) {
      case "Allassan": return 83;
      case "Almadinejud": return 84;
      case "Borsonero": return 85;
      case "Caralam": return 86;
      case "Hisdognan": return 87;
      case "Joannesson": return 88;
      case "Kil Jonh-nun": return 89;
      case "Maccaron": return 90;
      case "Makkarel": return 91;
      case "Mungaweb": return 92;
      case "Puttan": return 93;
      case "Trashmp": return 94;
      case "Vikorban": return 95;
      case "XinJinPooh": return 96;
      default: return 0;
    };
  }

  public static byte GetAvatarForAI(int index) {
    return (byte)(index + 83);
  }

  public static ulong GetIDForAI(string name) {
    switch (name) {
      case "Allassan": return 0;
      case "Almadinejud": return 1;
      case "Borsonero": return 2;
      case "Caralam": return 3;
      case "Hisdognan": return 4;
      case "Joannesson": return 5;
      case "Kil Jonh-nun": return 6;
      case "Maccaron": return 7;
      case "Makkarel": return 8;
      case "Mungaweb": return 9;
      case "Puttan": return 10;
      case "Trashmp": return 11;
      case "Vikorban": return 12;
      case "XinJinPooh": return 13;
      default: return 0;
    };
  }

  public static string GetNameForAI(int index) {
    switch (index) {
      case 0: return "Allassan";
      case 1: return "Almadinejud";
      case 2: return "Borsonero";
      case 3: return "Caralam";
      case 4: return "Hisdognan";
      case 5: return "Joannesson";
      case 6: return "Kil Jonh-nun";
      case 7: return "Maccaron";
      case 8: return "Makkarel";
      case 9: return "Mungaweb";
      case 10: return "Puttan";
      case 11: return "Trashmp";
      case 12: return "Vikorban";
      case 13: return "XinJinPooh";
      default: return "Unknown";
    };
  }

  public static Enemy GetEnemy(int pos) {
    if (pos >= 0 && pos < enemies.Length) return enemies[pos];
    return null;
  }

  public static string GetTechName(int index) {
    switch (index) {
      case 0: return "Radio waves";
      case 1: return "Acustics";
      case 2: return "Optics";
      case 3: return "Chemicals";
      case 4: return "Explosives";
      case 5: return "Rocketry";
      case 6: return "Avionics";
      case 7: return "Jets";
      case 8: return "Hulls";
      case 9: return "Propellers";
      case 10: return "Refining";
      case 11: return "Assembly";
      case 12: return "Recycling";
      case 13: return "Applied Physics";
      case 14: return "Intelligence";
      case 15: return "Counter Intelligence";
      case 16: return "Electronics";
      case 17: return "Encryption / Decryption";
      case 18: return "Droned";
      case 19: return "Cybernetics";
      case 20: return "Artificial Intelligence";
      default: return "<color=red>Unknown Tech!</color>";
    };
  }

  public static string GetImprovementName(int index) {
    switch (index) {
      case 0: return "Houses";
      case 1: return "Parks";
      case 2: return "Theaters";
      case 3: return "Farms";
      case 4: return "Hydroponic Farms";
      case 5: return "Mines";
      case 6: return "Hospitals";
      case 7: return "Refineries";
      case 8: return "Factories";
      case 9: return "Heavy Factories";
      case 10: return "Offices";
      case 11: return "Electro Spinners";
      case 12: return "Power Plants";
      case 13: return "Advanced Nuclear Facility";
      case 14: return "Schools";
      case 15: return "Universities";
      case 16: return "Recycling Center";
      case 17: return "Aerospace Center";
      case 18: return "Intelligence Agency";
      default: return "<color=red>Unknown Improvement!</color>";
    };
  }

  public static void Restart() {
    instance.gameLoader.Restart();
  }

  static System.IO.StreamWriter debugFile = null;
  public enum LT { DebugST=1, Debug=2, Log=3, Warning=4, Error=5 };
  public static object logLock = new object();
  public static void DebugLog(string txt, LT type) {
    lock (logLock) {
      if (type == LT.DebugST && logsLevel == LT.Debug) type = LT.Debug;
      if ((int)type < (int)logsLevel)
        return;
      txt = txt.Trim();
      if (string.IsNullOrWhiteSpace(txt)) return;

      if (!writeLogsOnFile) {
        Debug.Log(txt);
        return;
      }
      if (debugFile == null) InitLogFile();
      if (debugFile == null) {
        if (type == LT.Log) Debug.Log(txt);
        if (type == LT.Warning) Debug.LogWarning(txt);
        if (type == LT.Error) Debug.LogError(txt);
        return;
      }
      if (thePlayer != null) txt = thePlayer.ToString() + ": " + txt;

      // FIXME
      foreach (char c in txt)
        if (c != '\n' && (char.IsControl(c) || c == '\0')) {
          System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(true);
          txt = "***" + txt + "***\nDEBUG >> DEBUG >> DEBUG >> DEBUG >> DEBUG >> DEBUG >> DEBUG >> DEBUG >> DEBUG >> DEBUG >> DEBUG >> DEBUG >> DEBUG >> \n";
          for (int i = 0; i < st.FrameCount && i < 8; i++) {
            System.Diagnostics.StackFrame frame = st.GetFrame(i);
            string file = frame.GetFileName();
            if (!string.IsNullOrWhiteSpace(file) && file.LastIndexOf("\\") != -1) file = file.Substring(file.LastIndexOf("\\") + 1);
            txt += frame.GetMethod() + " (" + frame.GetFileLineNumber() + ") " + file + "\n";
          }
          debugFile.WriteLine(txt);
          break;
        }

      if (type == LT.Log) debugFile.WriteLine(txt);
      else if (type == LT.Warning) debugFile.WriteLine("WARNING: " + txt);
      else if (type == LT.Error) debugFile.WriteLine("ERROR: " + txt);
      else if (type == LT.Debug) debugFile.WriteLine("DEBUG >> \"" + txt + "\"");
      else if (type == LT.DebugST) {
        System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(true);
        txt = "DEBUG >> \n\"" + txt + "\"\n";
        for (int i = 1; i < st.FrameCount && i < 4; i++) {
          System.Diagnostics.StackFrame frame = st.GetFrame(i);
          string file = frame.GetFileName();
          if (!string.IsNullOrWhiteSpace(file) && file.LastIndexOf("\\") != -1) file = file.Substring(file.LastIndexOf("\\") + 1);
          txt += frame.GetMethod() + " (" + frame.GetFileLineNumber() + ") " + file + "\n";
        }
        debugFile.WriteLine(txt);
      }
      debugFile.Flush();
      if (type == LT.Log) Debug.Log(txt);
      else if (type == LT.Warning) Debug.LogWarning(txt);
      else if (type == LT.Error) Debug.LogError(txt);
      else if (type == LT.Debug) Debug.Log(txt);
      else if (type == LT.DebugST) Debug.Log(txt);
    }
  }


  
  public static void DebugHex(byte[] data, int start, int len) {
    string msg = "";
    int min = len < data.Length ? len : data.Length;
    for(int i=0; i<min; i++) {
      msg += data[start + i].ToString("X") + " ";
    }
    writeLogsOnFile = true;
    DebugLog(msg, LT.Warning);
  }
}

public enum LoaderStatus {
  Startup, // Just starting, used to fade from black
  Started, // Waiting for the user to select the mode
  ServerBackground, // We are a server and we do not show the user interface
  Server, // We are a server (no game play here)

  RemoteServer, // We are managing a server from remote (no game play here)
  MultiPlayer, // We are in multiplayer mode (loader screen, not yet playing)
  SinglePlayer, // We are in single player mode (loader screen, not yet playing)

  StartGame, // The game is starting (not yet started, transition to game screen not yet completed)
  GameStarted, // Game actually playing
};


public enum GameStatus { Waiting = 1, ReadyToStart = 2, Playing = 3, Completed = 4 };
public enum PlayerGameStatus { Waiting = 1, ReadyToStart = 2, StartingGame = 3, Playing = 4 };

public enum ServerMessages { Nothing = 0, Info, Error, ServerConnected, PingAnswer, GameListUpdated, GameList, PlayersList, GameCreated, GameJoined, GameLeft, GameDeleted, GameCanStart, PlayersOfGame, StartingTheGame };

public enum BalloonDirection { TR=0, TL=1, BR=2, BL=3};

public enum NetworkCommand {
  NotValid,
  SuccessMsg, // Used to just send a message string to the client
  Connect, // Playerdata
  ConnectRemote, // string with ip
  Goodbye, // Client is leaving
  Ping, // Request of ping from the client
  Pong, // Answer to the ping from the server
  UpdateGames, // Server broadcast message telling that the games should be updated
  GamesList, // List of games
  PlayersList, // List of players (for remote server managers)
  CreateGame, // Client command to create a new game
  GameCreated, // Confirmation that a game is created (from server)
  Join, // Client command to join a game
  Joined, // Confirmation of joining a game (from server)
  Leave, // Client command to leave a game
  Left, // Confirmation of leaving a game (from server)
  DeleteGame, // Client command to delete a game
  GameDeleted, // Confirmation that a game was deleted (from server)
  GameCanStart, // Notification that a game can or cannot start
  Error, // Generic error from client and server

  SendChat, // Sending a Chat from client to server
  ReceiveChat, // Receiving a chat from server to clients
  GetPlayersFromGame, // Client asks for all ChatParticipants of a game
  SetPlayersFromGame, // Server sends back the list




  StartingTheGame, // Client tells that we are starting
  GetRunningGame, // Client asks for the full game definition (multiplayer)

  PlayerDeath, // The server send the ID of the player that left the game
  SendGameAction, // Client sends to the server its own game action
  GameProgressUpdate, // Sent by the server to all clients of a game to tell that a specific human player completed the turn
  GameTurn, // Full information about the calculated turn for the game (generated server side and sent to the participants)
}

public enum GameMsgType {
  Error, // To show errors
  PlayerDeath, // The player left the game
  GameProgressUpdate, // A player completed its turn
  GameTurn, // All players completed and this is the new situation of the game
}

public enum Mood {
  Normal = 0, Disgusted = 1, Tired = 2, Distrusting = 3, Sigh = 4, Dreaming = 5,
  Smug = 6, Gloat = 7, Tender = 8, Sly = 9, Content = 10, GoodDreaming = 11,
  Sad = 12, Angry = 13, Hurt = 14, Vengeful = 15, Grief = 16, BadDreaming = 17,
  Talking = 18, Scolding = 19, Sorry = 20, Puzzled = 21, Surprised = 22, Snoring = 23,
  Nasty = 24, Cruel = 25, Pissed = 26, Humiliating = 27, Victorious = 28, FeelingPain = 29
}

public enum CharType { Relaxed = 0, Vindicative = 2, Neutral = 1, Aggressive = 3, Crazy = 4 };

public enum WeaponType { WarheadT, WarheadC, WarheadU, WarheadP, WarheadH, DeliveryM, DeliveryA, DeliverySh, DeliverySu, DeliverySa, Defense, None };



public enum ProductionType {  Resource=0, Technology=10, Improvement=40, Warhead, Delivery, Defense };

public enum ResourceType {
  Food=0,
  Iron=1,
  Aluminum=2,
  Uranium=3,
  Plutonium=4,
  Hydrogen=5,
  Plastic=6,
  Electronics=7,
  Composite=8,
  FossilFuels=9,
};

public enum ItemType {
  // Resources - 0
  RFOOD, // Food     (IFARM | IHFRM)
  RIRON, // Iron     (IMINE)
  RALUM, // Aluminum     (IREFI)
  RURAN, // Uranium     (IMINE)
  RPLUT, // Plutonium     (IPOPL)
  RHYDR, // Hydrogen    TCHEM (IHFAC)
  RPLAS, // Plastic    TREFS (IFACT)
  RELEC, // Electronics    TELEC, TASBL (IOFFC)
  RCOMP, // Composite     (IELSP)
  RFOSS, // Fossil fuels     (IMINE, IREFI)

  // Technologies - 10
  TRWAV=10, // Radio waves
  TACST=11, // Acoustics
  TOPTC=12, // Optics
  TCHEM=13, // Chemicals
  TEXPL=14, // Explosives
  TRKRY=15, // Rocketry
  TAVIO=16, // Avionics
  TJETS=17, // Jets
  THULL=18, // Hulls
  TPROP=19, // Propellers
  TREFS=20, // Refining
  TASBL=21, // Assembly
  TRECY=22, // Recycling
  TAPHY=23, // Applied Physics
  TINTL=24, // Intelligence
  TCINT=25, // Counter Intelligence
  TELEC=26, // Electronics
  TENDE=27, // Encryption / Decryption
  TDRON=28, // Drones
  TCYBR=29, // Cybernetics
  TARIN=30, // Artificial Intelligence

  // Improvements - 40
  IHOUS=40, // Houses     ()
  IPARK=41, // Parks     ()
  ITHEA=42, // Theaters     (PARK)
  IFARM=43, // Farms     ()
  IHFRM=44, // Hydrophonic farms     (FARM)
  IMINE=45, // Mines     ()
  IHOSP=46, // Hospitals     (UNIV)
  IREFI=47, // Refineries    TREFS ()
  IFACT=48, // Factories    TASBL ()
  IHFAC=49, // Heavy factories     (FACT)
  IOFFC=50, // Offices     (UNIV)
  IELSP=51, // ElectroSpinners     (UNIV)
  IPOPL=52, // Power plants     (UNIV)
  IANUF=53, // Advanced Nuclear Facility     (POPL)
  ISCHO=54, // Schools     ()
  IUNIV=55, // Universities     (SCHO)
  IRECC=56, // Recycling center    TRECY (SCHO)
  IAECR=57, // Aerospace center    TRKRY, TAVIO (UNIV)
  IINAG=58, // Intelligence Agency    TINTL (UNIV)

  // Warheads - 60
  WTN=60, // TNT     ()
  WC4=61, // C4    TEXPL ()RFOSS
  WUR=62, // Uranium    TAPHY (IFACT)RURAN
  WPL=63, // Plutonium    TAPHY (IHFAC, IPOPL)RPLUT
  WHY=64, // Hydrogen    TCHEM, TEXPL, TAPHY ()RHYDR

  // Deliver - 70
  DTROP=70, // Troups     ()
  DMISS=71, // Missile    TRKRY ()
  DAIRP=72, // Airplane    TRWAV, TRKRY, TAVIO, TJETS (IFACT)
  DSHIP=73, // Ship    TPROP, THULL (IFACT)
  DSUBM=74, // Submarine    TPROP, THULL, TOPTC, TACST (IHFAC)
  DDRNE=75, // Drone    TAVIO, TOPTC, TELEC, TDRON, TAPHY (IOFFC, ISCHO)
  DSATE=76, // Satellite    TRKRY, TAVIO, TOPTC, TJETS, TELEC, TAPHY (IAECR, IUNIV)
  DTERM=77, // Terminators    TELEC, TARIN, TCYBR (IELSP, IUNIV, IINAG)RCOMP

  // Defemses - 80
  PRTRK=80, // Radio Tracking    TRWAV ()
  PANTE=81, // Antennas    TINTL, TACST (IFACT)
  PSAMB=82, // SAM battery    TRKRY, TEXPL (IFACT)
  PEMPS=83, // EMP    TELEC, TAPHY (IHFAC)
  PHKDV=84, // Hacking devices    TENDE, TCINT (IOFFC, IINAG)
};



