using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameLoader : MonoBehaviour {
  #region Public References
  float goBlackTime = 1f;
  float worldRotation = 1f;
  public World world;
  public GameObject title;
  public CanvasGroup loaderCanvas;
  public CanvasGroup gameCanvas;
  public GameObject GameContainer;
  public Button singlePlayerButton;
  public Button multiplePlayersButton;
  public Button serverButton;
  public Button remoteServerButton;
  public Button creditsButton;
  public Button optionsButton;
  public Button StartGameButton;
  public TextMeshProUGUI StartGameText;
  public TextMeshProUGUI InfoTxt;
  public TextMeshProUGUI GameTxt;
  public GameObject CreateGameLine;
  public Button CreateGameButton;
  public Button RefreshGameListButton;
  public TextMeshProUGUI RefreshGameListButtonText;
  public TMP_InputField PlayerName;
  public Button ChangeAvatarButton;
  public GameObject Avatars;
  public Image Avatar;
  public int playerAvatar = 0;
  public TMP_InputField GameName;
  public TMP_Dropdown DifficultyMP;
  public TMP_Dropdown NumPlayers;
  public TMP_Dropdown NumAIs;
  public Button ConnectPlayerButton;
  public Button DisconnectPlayerButton;
  public Button StartServerButton;
  public Button ConnectServerButton;
  public TMP_InputField ServerAddress;
  public TMP_InputField ServerPort;
  public TMP_InputField ServerPassword;
  public GameObject Player;
  public GameObject GameModeInfo;
  public TextMeshProUGUI GameModeInfoText;
  public GameObject GamePlayersSwitch;
  public TextMeshProUGUI GamePlayersSwitchText;
  private bool showGames = true;
  public GameObject PlayersList;
  public Transform PlayersGrid;
  public GameObject PlayerLineTemplate;
  public GameObject GamesList;
  public Transform GamesGrid;
  public GameObject GameLineTemplate;
  public TextMeshProUGUI TotalNumberOfPlayers;
  private ServerMessage[] serverMessages;
  private int serverMessagesNum = 0;
  private Camera cam;
  public Vector3 cameraRot = Vector3.zero;
  private float cameraSize = 2.5f;
  private bool enableCameraMovements = true;
  private string textToUpdate = null;
  private bool isAnError = false;
  public Material SkyboxMaterial;
  public ChatManager chat;
  public GameObject NewGameSinglePlayer;
  public TMP_Dropdown NumberOfEnemiesDropDown;
  public TMP_Dropdown DifficultySP;
  public Transform SelectedEnemies;
  public Transform EnemiesStorage;
  public GameObject Game;
  public GameRenderer gameRenderer;
  public AudioSource introMusic;
  public Options Options;
  #endregion Public References


  public void InitGame() {
    // Make the screen to go black, show the title and the options to start a single player game or a multiplayer game
    GD.SetStatus(LoaderStatus.Startup);
    GD.instance.networkManager.OnServerMessage += HandleEventServerMessages;
    serverMessages = new ServerMessage[16];
    for (int i = 0; i < 16; i++) {
      serverMessages[i] = new ServerMessage {
        type = ServerMessages.Nothing
      };
    }
    serverMessagesNum = 0;
    cam = Camera.main;
  }

  public void Restart() {
    GD.SetStatus(LoaderStatus.Startup);
    gameObject.SetActive(true);
    world.gameObject.SetActive(true);
    title.SetActive(true);
    RenderSettings.skybox = SkyboxMaterial;
    loaderCanvas.alpha = 1;
    Back();
    if (GD.thePlayer.tcpClient != null && GD.thePlayer.tcpClient.Connected)
      MultiplePlayers();
    else
      SinglePlayer();
  }

  public Enemy[] GetEnemies() {
    int num = 0;
    foreach(Transform t in EnemiesStorage)
      if (t.GetComponent<Enemy>() != null) num++;

    Enemy[] res = new Enemy[num];
    num = 0;
    foreach (Transform t in EnemiesStorage) {
      res[num] = t.GetComponent<Enemy>();
      if (res[num] != null)
        num++;
    }
    // The enemies should be sorted by name, so the index will be the same on all clients and on the server
    System.Array.Sort(res, (a, b) => a.stats.name.CompareTo(b.stats.name));

    return res;
  }

  Vector2 srcCamRot = Vector2.zero;
  Vector2 dstCamRot = Vector2.zero;
  Vector2 curCamRot = Vector2.zero;
  float srcCamSize = 2.5f;
  float dstCamSize = 2.5f;
  float movingTime = 0;
  float timeToMove = 3f;
  bool updateQuicklyCam = true;
  Quaternion currentCamTransfRot = Quaternion.identity;
  float currentCamTransfSize = 2.5f;

  private void Update() {
    if (GD.IsStatus(LoaderStatus.ServerBackground)) return;

    // Transition from black
    if (GD.IsStatus(LoaderStatus.Startup)) {
      GameModeInfo.SetActive(false);
      PlayersList.SetActive(false);
      GamesList.SetActive(false);
      Player.SetActive(false);
      ConnectPlayerButton.gameObject.SetActive(false);
      DisconnectPlayerButton.gameObject.SetActive(false);
      StartServerButton.gameObject.SetActive(false);
      ConnectServerButton.gameObject.SetActive(false);
      ServerAddress.gameObject.SetActive(false);
      ServerPort.gameObject.SetActive(false);
      ServerPassword.gameObject.SetActive(false);
      CreateGameButton.gameObject.SetActive(false);
      RefreshGameListButton.gameObject.SetActive(false);
      GamePlayersSwitch.gameObject.SetActive(false);
      NewGameSinglePlayer.SetActive(false);
      StartGameButton.gameObject.SetActive(false);
      LeaveGameButton.gameObject.SetActive(false);
      MultiPlayerStarting.SetActive(false);
      cam.backgroundColor = new Color32((byte)(49 * goBlackTime), (byte)(77 * goBlackTime), (byte)(121 * goBlackTime), 255);
      goBlackTime -= Time.deltaTime;
      loaderCanvas.alpha = 1f - goBlackTime;
      if (goBlackTime <= 0) {
        GD.SetStatus(LoaderStatus.Started);
        enableCameraMovements = true;
        // Show all required buttons
        singlePlayerButton.gameObject.SetActive(true);
        multiplePlayersButton.gameObject.SetActive(true);
        serverButton.gameObject.SetActive(true);
        remoteServerButton.gameObject.SetActive(true);
        creditsButton.gameObject.SetActive(true);
        optionsButton.gameObject.SetActive(true);
      }
      return;
    }

    // World and camera movement
    if (enableCameraMovements) {
      world.gameObject.SetActive(true);
      if (worldRotation > 0) {
        worldRotation -= Time.deltaTime;
        title.transform.position = new Vector3(8.14f - (1 - worldRotation) * 7.5f, 1.9f, -5);
        return;
      }

      movingTime += Time.deltaTime;
      if (updateQuicklyCam) {
        if (dstCamRot != Vector2.zero && GD.IsStatus(LoaderStatus.StartGame)) {
          srcCamRot = dstCamRot;
          srcCamSize = dstCamSize;
          dstCamRot = Vector2.zero;
          cameraSize = 2.5f;
          timeToMove = .5f;
          movingTime = 0;
          updateQuicklyCam = false;
          currentCamTransfRot = cam.transform.rotation;
          currentCamTransfSize = cam.orthographicSize;
        }
        else {
          srcCamRot = curCamRot;
          movingTime = timeToMove;
          updateQuicklyCam = false;
        }
      }
      else if (movingTime < timeToMove) { // Rotate and scale
        if (!GD.IsStatus(LoaderStatus.StartGame)) {
          float perc = movingTime / timeToMove;
          perc = perc * 1.5f / (perc + .5f);
          float rx = perc * dstCamRot.x + (1 - perc) * srcCamRot.x;
          float ry = perc * dstCamRot.y + (1 - perc) * srcCamRot.y;
          if (curCamRot.x > 180) curCamRot.x -= 360;
          if (curCamRot.y > 180) curCamRot.y -= 360;
          if (rx > 180) rx -= 360;
          if (ry > 180) ry -= 360;
          rx *= Time.deltaTime;
          ry *= Time.deltaTime;
          curCamRot.x += rx;
          curCamRot.y += ry;
          cam.transform.Rotate(rx, ry, 0);
          Vector3 r = cam.transform.rotation.eulerAngles;
          r.z = 0;
          cam.transform.rotation = Quaternion.Euler(r);
          cam.orthographicSize = perc * dstCamSize + (1 - perc) * srcCamSize;
        }
        else {
          float perc = movingTime / timeToMove;
          perc = perc * 1.5f / (perc + .5f);
          cam.transform.rotation = Quaternion.Lerp(currentCamTransfRot, Quaternion.identity, perc);
          cam.orthographicSize = perc * 2.5f + (1 - perc) * currentCamTransfSize;
        }
      }
      else {
        if (!GD.IsStatus(LoaderStatus.StartGame)) { // Calculate the new values and restart, but only if we are not starting the game
          srcCamRot = dstCamRot;
          srcCamSize = dstCamSize;
          if (curCamRot.x > 2)
            dstCamRot.x = Random.Range(-10, -1) * (cameraSize / 10);
          else if (curCamRot.x < -2)
            dstCamRot.x = Random.Range(1, 10) * (cameraSize / 10);
          else
            dstCamRot.x = Random.Range(-5, 5) * (cameraSize / 10);
          if (curCamRot.y > 2)
            dstCamRot.y = Random.Range(-10, -1) * (cameraSize / 10);
          else if (curCamRot.y < -2)
            dstCamRot.y = Random.Range(1, 10) * (cameraSize / 10);
          else
            dstCamRot.y = Random.Range(-5, 5) * (cameraSize / 10);
          dstCamSize = cameraSize + Random.Range(-1f, 1f);
          timeToMove = Random.Range(2f, 4f);
          movingTime = 0;
        }
        else {
          cam.transform.rotation = Quaternion.identity;
          cam.orthographicSize = 2.5f;
          curCamRot = Vector2.zero;
          dstCamSize = 2.5f;
        }
      }
    }

    // Transition to the statuses
    if (GD.IsStatus(LoaderStatus.Started)) {
      singlePlayerButton.gameObject.SetActive(true);
      multiplePlayersButton.gameObject.SetActive(true);
      serverButton.gameObject.SetActive(true);
      remoteServerButton.gameObject.SetActive(true);
      if (hideButton > 0) {
        singlePlayerButton.GetComponent<CanvasGroup>().alpha = (.5f - hideButton) * 2;
        multiplePlayersButton.GetComponent<CanvasGroup>().alpha = (.5f - hideButton) * 2;
        serverButton.GetComponent<CanvasGroup>().alpha = (.5f - hideButton) * 2;
        remoteServerButton.GetComponent<CanvasGroup>().alpha = (.5f - hideButton) * 2;
        hideButton -= Time.deltaTime;
        if (hideButton <= 0) {
          singlePlayerButton.GetComponent<CanvasGroup>().alpha = 1;
          multiplePlayersButton.GetComponent<CanvasGroup>().alpha = 1;
          serverButton.GetComponent<CanvasGroup>().alpha = 1;
          ConnectPlayerButton.gameObject.SetActive(false);
          DisconnectPlayerButton.gameObject.SetActive(false);
          StartServerButton.gameObject.SetActive(false);
          ConnectServerButton.gameObject.SetActive(false);
          ServerAddress.gameObject.SetActive(false);
          ServerPort.gameObject.SetActive(false);
          ServerPassword.gameObject.SetActive(false);
          GamePlayersSwitch.SetActive(false);
          Player.gameObject.SetActive(false);
          CreateGameLine.SetActive(false);
          GameModeInfoText.text = "Select Game Mode";
        }
      }
    }
    else if (GD.IsStatus(LoaderStatus.SinglePlayer)) {
      if (hideButton > 0) {
        singlePlayerButton.GetComponent<CanvasGroup>().alpha = hideButton;
        multiplePlayersButton.GetComponent<CanvasGroup>().alpha = hideButton;
        serverButton.GetComponent<CanvasGroup>().alpha = hideButton;
        remoteServerButton.GetComponent<CanvasGroup>().alpha = hideButton;
        hideButton -= Time.deltaTime;
        if (hideButton <= 0) {
          GameModeInfo.SetActive(true);
          GameModeInfoText.text = "Single Player";
          singlePlayerButton.gameObject.SetActive(false);
          multiplePlayersButton.gameObject.SetActive(false);
          serverButton.gameObject.SetActive(false);
          remoteServerButton.gameObject.SetActive(false);
        }
      }
    }
    else if (GD.IsStatus(LoaderStatus.MultiPlayer)) {
      if (hideButton > 0) {
        singlePlayerButton.GetComponent<CanvasGroup>().alpha = hideButton;
        multiplePlayersButton.GetComponent<CanvasGroup>().alpha = hideButton;
        serverButton.GetComponent<CanvasGroup>().alpha = hideButton;
        remoteServerButton.GetComponent<CanvasGroup>().alpha = hideButton;
        hideButton -= Time.deltaTime;
        if (hideButton <= 0) {
          GameModeInfo.SetActive(true);
          if (GD.IsStatus(LoaderStatus.Server)) {
            GameModeInfoText.text = "Multiplayer Server";
            ServerAddress.enabled = false;
          }
          else {
            GameModeInfoText.text = "Multiplayer";
            ServerAddress.enabled = true;
          }
          singlePlayerButton.gameObject.SetActive(false);
          multiplePlayersButton.gameObject.SetActive(false);
          serverButton.gameObject.SetActive(false);
          remoteServerButton.gameObject.SetActive(false);
        }
      }
    }


    // Update the info line in case we have something to show
    if (textToUpdate != null) {
      SetInfo(textToUpdate, isAnError);
      textToUpdate = null;
      isAnError = false;
    }

    // Fix the aspect ration of the window/screen
    if (GD.fullScreen == FullScreenMode.Windowed && (Screen.width != lastWidth || Screen.height != lastHeight)) {
      lastResize -= Time.deltaTime;
      if (lastResize < 0) {
        lastResize = 1f;
        lastWidth = Screen.width;
        lastHeight = (int)(lastWidth * 1080f / 1920f);
        Screen.SetResolution(lastWidth, lastHeight, GD.fullscreen);
      }
    }

    // Handling of messages
    ServerMessage message = GetFirstMessage();
    if (message.type != ServerMessages.Nothing) {
      switch (message.type) {
        case ServerMessages.Info:
          SetInfo(message.message);
          break;
        case ServerMessages.Error:
          SetInfo(message.message, true);
          break;
        case ServerMessages.ServerConnected:
          SetInfo(message.message);
          GameModeInfoText.text = "Local Server";
          GameModeInfo.GetComponent<Button>().enabled = false;
          GamePlayersSwitch.gameObject.SetActive(true);
          GD.SetStatus(LoaderStatus.Server);
          ConnectPlayerButton.gameObject.SetActive(false);
          DisconnectPlayerButton.gameObject.SetActive(false);
          StartServerButton.gameObject.SetActive(false);
          ConnectServerButton.gameObject.SetActive(false);
          ServerAddress.gameObject.SetActive(true);
          ServerAddress.enabled = false;
          ServerPort.gameObject.SetActive(true);
          ServerPort.enabled = false;
          ServerPassword.gameObject.SetActive(true);
          ServerPassword.enabled = false;
          ServerAddress.text = GD.instance.networkManager.GetServerAddress();
          ServerPort.text = GD.instance.networkManager.GetServerPort();
          creditsButton.gameObject.SetActive(false);
          optionsButton.gameObject.SetActive(false);
          enableCameraMovements = false;
          world.gameObject.SetActive(false);
          PlayersList.gameObject.SetActive(true);
          break;

        case ServerMessages.PingAnswer:
          SetInfo(message.message);
          break;
        case ServerMessages.GameListUpdated:
          RefreshGames();
          break;
        case ServerMessages.GameList:
          UpdateGameList(message.gameList, message.num);
          break;

        case ServerMessages.PlayersList:
          SetInfo(message.message);
          UpdatePlayersList(message.playersList);
          break;
        case ServerMessages.GameCreated:
          SetInfo(message.message);
          GameTxt.text = "Game joined: " + GameName.text;
          GD.thePlayer.CurrentGame = GameName.text;
          CreateGameLine.SetActive(false);
          break;
        case ServerMessages.GameJoined:
          SetInfo("Joined game \"" + message.message + "\"");
          GameTxt.text = "Game joined: " + message.message;
          GD.thePlayer.CurrentGame = message.message;
          break;
        case ServerMessages.GameLeft:
          SetInfo("Left game \"" + message.message + "\"");
          GameTxt.text = "";
          GD.thePlayer.CurrentGame = null;
          break;
        case ServerMessages.GameDeleted:
          SetInfo("The game \"" + message.message + "\" was deleted");
          if (GD.thePlayer.CurrentGame == message.message) {
            GD.thePlayer.CurrentGame = null;
            GameTxt.text = "";
          }
          break;
        case ServerMessages.GameCanStart:
          if (message.message[0] == 'Y') {
            StartGameButton.gameObject.SetActive(true);
            StartGameText.text = "Start Game\n" + message.message.Substring(1);
            SetInfo("The game \"" + message.message.Substring(1) + "\" can start!");
          }
          else {
            MultiPlayerStarting.SetActive(false);
            LeaveGameButton.gameObject.SetActive(false);
            StartGameButton.gameObject.SetActive(false);
            SetInfo("The game \"" + message.message.Substring(1) + "\" has not enough players");
          }
          break;

        case ServerMessages.PlayersOfGame:
          if (message.gameplayers != null) {
            List<ChatParticipant> participants = new List<ChatParticipant>();
            for (int i = 0; i > message.gameplayers.Count; i++) {
              participants.Add(new ChatParticipant(message.gameplayers[i].name, message.gameplayers[i].avatar, message.gameplayers[i].id));
            }
            chat.StartChatWith(message.message, participants);
          }
          break;

        case ServerMessages.StartingTheGame:
          if (message.num == 2) { // Single player. Direct start
            GD.DebugLog("Starting the game Single Player", GD.LT.Log);
            ActualGameStart(false); // Single player
          }
          else if (message.num == 0) { // Multiplayer. Not all players started
            GD.DebugLog("Waiting for players to start the game: " + message.message + " with " + message.gameplayers.Count + " players", GD.LT.Log);
            // Just update the user interface to wait for people to join/start
            foreach (Transform t in MultiPlayersGrid)
              GameObject.Destroy(t.gameObject);
            for (int i = 0; i < message.gameplayers.Count; i++) {
              SimplePlayer sp = message.gameplayers[i];
              GameObject pl = Instantiate(PlayerStartingTemplate, MultiPlayersGrid);
              pl.SetActive(true);
              pl.transform.Find("Player").GetComponent<TextMeshProUGUI>().text = "<sprite=" + sp.avatar + "> " + sp.name;
              pl.transform.Find("Status").GetComponent<TextMeshProUGUI>().text = sp.status == StatusOfPlayer.StartingGame ? "Ready!" : "Waiting for player to start...";
              pl.transform.Find("Chat").gameObject.SetActive(sp.id != GD.thePlayer.ID);
              pl.transform.Find("Chat").GetComponent<Button>().onClick.AddListener(delegate { StartChatWith(message.message); });
            }
          }
          else if (message.num == 1) { // Multiplayer. Actual start
            GD.DebugLog("Starting the game \"" + message.message +"\" MultiPlayer", GD.LT.Log);
            ActualGameStart(true); // Multiplayer
          }
          break;
      }
    }
  }

  float hideButton = 0;
  int lastWidth = 1920;
  int lastHeight = 1080;
  float lastResize = 1;

  private void ActualGameStart(bool multiplayer) { // FIXME
    GD.DebugLog("The game is actually starting!", GD.LT.Log);

    // **************************************************************************************************************************************************************************
    // The "Game" instance can arrive from the server or we may need to create one in case of single player

    // Hiding of what is in the user interface
    foreach (Transform t in SelectedEnemies)
      t.GetComponent<Enemy>().HideBalloon();
    world.gameObject.SetActive(false);
    title.SetActive(false);
    gameObject.SetActive(false);
    RenderSettings.skybox = null;

    Game game;
    if (multiplayer) { // Server mode
      string answer = GD.instance.networkManager.GetTheGameFromTheServer(GD.thePlayer, out game, out int seed);
      if (answer != null) {
        SetInfo(answer, true);
        return;
      }
      game.rndSeed = seed;
      GD.InitRandomGenerator(seed);
      game.multiplayer = true;
      game.engine = new GameEngine(GD.thePlayer);
      game.engine.InitEnemiesMultiplayer(game);
      GD.thePlayer.TheGame = game;
    }
    else { // Single player
      GD.difficulty = DifficultySP.value;
      game = new Game("Single Player", GD.thePlayer, GD.difficulty, 1, NumberOfEnemiesDropDown.value + 1) {
        engine = new GameEngine(GD.thePlayer)
      };
      game.multiplayer = false;
      game.rndSeed = Random.Range(-75000, 75000); // We will not receive a random seed from the server, so just create a local one, we are in SinglePlayer
      // Pick the actual AIs and initialize the Enemies of the engine
      game.engine.InitEnemiesSingleplayer(game, SelectedEnemies);
      GD.thePlayer.TheGame = game;
    }

    // Start the actual game
    game.localPlayer = GD.thePlayer;
    gameRenderer.Init(game);
    world.gameObject.SetActive(false);
    title.SetActive(false);
    RenderSettings.skybox = null;
    gameObject.SetActive(false);
    introMusic.Stop();
  }


  public void DEBUGSinglePlayer() {
    /*
    Player pl = new Player("This is a name", 87);
    GameEngine en = new GameEngine(pl);
    en.cities[1] = new City {
      improvements = new bool[19],
      population = 321,
      pos = 1
    };
    en.cities[1].improvements[1] = true;
    en.cities[1].improvements[2] = true;
    en.cities[1].improvements[3] = true;
    en.game = new Game("Test game", pl, 1, 2, 3);

    GameEngineValues g1 = new GameEngineValues(en);
    g1.order[0] = 2;
    g1.order[1] = 4;
    g1.order[2] = 8;
    g1.order[3] = 16;
    g1.order[4] = 32;
    g1.cities[1] = new CityVals(en.cities[1], 1);
    byte[] data = g1.Serialize();

    GameEngineValues g2 = new GameEngineValues(data);
    */
    Debug.Log("Test ");
  }

  public void SinglePlayer() {
    System.Console.Title = "The title has changed!";

    hideButton = .5f;
    cameraSize = 3.5f;
    updateQuicklyCam = true;
    GD.SetStatus(LoaderStatus.SinglePlayer);

    Player.SetActive(true);
    ConnectPlayerButton.gameObject.SetActive(false);
    DisconnectPlayerButton.gameObject.SetActive(false);
    StartServerButton.gameObject.SetActive(false);
    ConnectServerButton.gameObject.SetActive(false);
    ServerAddress.gameObject.SetActive(false);
    ServerPort.gameObject.SetActive(false);
    ServerPassword.gameObject.SetActive(false);
    NewGameSinglePlayer.SetActive(true);

    // Find the value of the number of enemies
    int enemiesInDropDown = NumberOfEnemiesDropDown.value + 1;
    int enemiesSelected = SelectedEnemies.childCount;

    if (enemiesSelected < enemiesInDropDown) { // If greater add some random enemies
      for (int i = 0; i < enemiesInDropDown - enemiesSelected; i++) {
        Transform enemy = EnemiesStorage.GetChild(Random.Range(0, EnemiesStorage.childCount));
        enemy.SetParent(SelectedEnemies);
        Enemy e = enemy.GetComponent<Enemy>();
        e.OnAlterPlayer += AlterPlayer;
        e.SayLater(i);
      }
    }
    else if (enemiesSelected > enemiesInDropDown) { // If less than the number of enemies we have, remove some enemies
      for (int i = 0; i < enemiesSelected - enemiesInDropDown; i++) {
        Transform enemy = SelectedEnemies.GetChild(enemiesSelected - i - 1);
        enemy.SetParent(EnemiesStorage);
        Enemy e = enemy.GetComponent<Enemy>();
        e.OnAlterPlayer -= AlterPlayer;
      }
    }
  }

  public void ChangeNumEnemiesSinglePlayer() {
    int enemiesInDropDown = NumberOfEnemiesDropDown.value + 1;
    int enemiesSelected = SelectedEnemies.childCount;

    if (enemiesSelected < enemiesInDropDown) { // If greater add some random enemies
      for (int i = 0; i < enemiesInDropDown - enemiesSelected; i++) {
        Transform enemy = EnemiesStorage.GetChild(Random.Range(0, EnemiesStorage.childCount));
        enemy.SetParent(SelectedEnemies);
        Enemy e = enemy.GetComponent<Enemy>();
        e.OnAlterPlayer += AlterPlayer;
        e.SayLater(i);
      }
    }
    else if (enemiesSelected > enemiesInDropDown) { // If less than the number of enemies we have, remove some enemies
      for (int i = 0; i < enemiesSelected - enemiesInDropDown; i++) {
        Transform enemy = SelectedEnemies.GetChild(enemiesSelected - i - 1);
        enemy.SetParent(EnemiesStorage);
        Enemy e = enemy.GetComponent<Enemy>();
        e.OnAlterPlayer -= AlterPlayer;
      }
    }
  }

  public void AlterPlayer(object sender, Enemy.AlterPlayerEvent e) {
    // Stop the previous message
    e.enemy.HideBalloon();
    // Pick a new one
    Transform newEnemy = EnemiesStorage.GetChild(Random.Range(0, EnemiesStorage.childCount));
    // Find the enemy inside the SelectedEnemies (position is important)
    foreach(Transform et in SelectedEnemies) {
      if (et.GetComponent<Enemy>() == e.enemy) {
        // Remove it and put it back to the EnemyStorage
        et.SetParent(EnemiesStorage);
        et.GetComponent<Enemy>().OnAlterPlayer -= AlterPlayer;
        newEnemy.SetParent(SelectedEnemies);
        Enemy ne = newEnemy.GetComponent<Enemy>();
        ne.OnAlterPlayer += AlterPlayer;
        ne.SayLater(0);
      }
    }
  }

  public void MultiplePlayers() {
    hideButton = .5f;
    GD.SetStatus(LoaderStatus.MultiPlayer);
    cameraSize = 12.5f;
    updateQuicklyCam = true;

    Player.SetActive(true);
    ConnectPlayerButton.gameObject.SetActive(true);
    DisconnectPlayerButton.gameObject.SetActive(false);
    StartServerButton.gameObject.SetActive(false);
    ConnectServerButton.gameObject.SetActive(false);
    ServerAddress.gameObject.SetActive(true);
    ServerAddress.enabled = true;
    ServerPort.gameObject.SetActive(true);
    ServerPassword.gameObject.SetActive(false);
    PlayerName.enabled = true;
    ChangeAvatarButton.enabled = true;
  }

  public void LocalServer() {
    hideButton = .5f;
    cameraSize = 12.5f;
    GD.SetStatus(LoaderStatus.Server);

    Player.SetActive(false);
    ConnectPlayerButton.gameObject.SetActive(false);
    DisconnectPlayerButton.gameObject.SetActive(false);
    StartServerButton.gameObject.SetActive(true);
    ConnectServerButton.gameObject.SetActive(false);
    ServerAddress.gameObject.SetActive(true);
    ServerAddress.enabled = false;
    ServerPort.gameObject.SetActive(true);
    ServerPassword.gameObject.SetActive(true);
  }

  public void RemoteServer() {
    hideButton = .5f;
    cameraSize = 12.5f;
    GD.SetStatus(LoaderStatus.RemoteServer);

    Player.SetActive(false);
    ConnectPlayerButton.gameObject.SetActive(false);
    DisconnectPlayerButton.gameObject.SetActive(false);
    StartServerButton.gameObject.SetActive(false);
    ConnectServerButton.gameObject.SetActive(true);
    ServerAddress.gameObject.SetActive(true);
    ServerAddress.enabled = true;
    ServerPort.gameObject.SetActive(true);
    ServerPassword.gameObject.SetActive(true);
  }

  public void Back() {
    GD.SetStatus(LoaderStatus.Startup);
    hideButton = .5f;
    cameraSize = 2.5f;
    updateQuicklyCam = true;
    GameModeInfoText.text = "Select Game Mode";
    GameModeInfo.SetActive(false);
    PlayersList.SetActive(false);
    GamesList.SetActive(false);
    enableCameraMovements = true;
    world.gameObject.SetActive(true);
    RenderSettings.skybox = SkyboxMaterial;
    Player.SetActive(false);
    ConnectPlayerButton.gameObject.SetActive(false);
    DisconnectPlayerButton.gameObject.SetActive(false);
    StartServerButton.gameObject.SetActive(false);
    ConnectServerButton.gameObject.SetActive(false);
    ServerAddress.gameObject.SetActive(false);
    ServerPort.gameObject.SetActive(false);
    ServerPassword.gameObject.SetActive(false);
    CreateGameButton.gameObject.SetActive(false);
    RefreshGameListButton.gameObject.SetActive(false);
    GamePlayersSwitch.gameObject.SetActive(false);
    creditsButton.gameObject.SetActive(true);
    optionsButton.gameObject.SetActive(true);
    NewGameSinglePlayer.SetActive(false);
    StartGameButton.gameObject.SetActive(false);
    LeaveGameButton.gameObject.SetActive(false);
    MultiPlayerStarting.SetActive(false);
    foreach (Transform t in SelectedEnemies)
      t.GetComponent<Enemy>().HideBalloon();
  }

  public void Credits() { // FIXME
    // Move the camera to the default position
    // Scroll the Text in a good position
    // Add version number, my avatar, and a few lines of description using a scrollbar
  }

  public void ShowOptions() {
    Options.Init();
  }

  public void StartGameSinglePlayer() {
    updateQuicklyCam = true;
    if (GD.thePlayer == null) {
      if (string.IsNullOrWhiteSpace(PlayerName.text))
        GD.thePlayer = new Player("Player", playerAvatar);
      else
        GD.thePlayer = new Player(PlayerName.text, playerAvatar);
    }

    // Add a stub message to actually start the game
    serverMessages[serverMessagesNum].type = ServerMessages.StartingTheGame;
    serverMessages[serverMessagesNum].message = GD.thePlayer.CurrentGame;
    serverMessages[serverMessagesNum].gameList = null;
    serverMessages[serverMessagesNum].playersList = null;
    serverMessages[serverMessagesNum].num = 2;
    serverMessagesNum++;
  }

  public Button LeaveGameButton;
  public GameObject MultiPlayerStarting;
  public Transform MultiPlayersGrid;
  public GameObject PlayerStartingTemplate;
  public void StartGameMultiPlayer() {
    // Enable the Multiplayer starting and Leave button
    MultiPlayerStarting.SetActive(true);
    LeaveGameButton.gameObject.SetActive(true);
    StartGameButton.gameObject.SetActive(false);
    // Tell the server that we are in, it will answer with the status of the players that we will use to fill the grid
    GD.instance.networkManager.IAmStartingTheGameClient(GD.thePlayer);
  }

  public void GamesPlayersSwitch() {
    showGames = !showGames;
    PlayersList.SetActive(!showGames);
    GamesList.SetActive(showGames);
    if (showGames)
      GamePlayersSwitchText.text = "Show Players";
    else
      GamePlayersSwitchText.text = "Show Games";
  }

  public void ConnectAsMultiplayer() { // A client will connect to a server (NOT used in remove server connection to monitor a server)
    if (string.IsNullOrEmpty(PlayerName.text.Trim())) {
      SetInfo("You need to define your name and avatar to connect to a server!", true);
      return;
    }
    SetInfo("Connecting...");
    GD.DebugLog("Connecting...", GD.LT.Log);
    if (!GD.instance.networkManager.alreadyStarted) GD.instance.networkManager.StartUpClient();
    int.TryParse(ServerPort.text, out int ip);
    if (GD.thePlayer == null) {
      GD.thePlayer = new Player(PlayerName.text, playerAvatar);
    }
    else if (!GD.thePlayer.Name.Equals(PlayerName.text) || GD.thePlayer.Avatar != playerAvatar) {
      GD.instance.networkManager.DisconnectFromServer(GD.thePlayer);
      chat.EndChats(GD.thePlayer);
      GD.thePlayer = new Player(PlayerName.text, playerAvatar);
    }
    string res = GD.instance.networkManager.ConnectToServer(GD.thePlayer, ServerAddress.text, ip);
    if (res[0] != '!') {
      ConnectPlayerButton.gameObject.SetActive(false);
      DisconnectPlayerButton.gameObject.SetActive(true);
      GD.SetStatus(LoaderStatus.MultiPlayer);
      SetInfo(res);
      chat.Startup(GD.thePlayer);
      GameModeInfoText.text = "Client Mode";
      GD.thePlayer.OnServerMessage += HandleEventServerMessages;
      GD.thePlayer.StartClientThread();
      // Get the list of games, and enable create game button
      CreateGameButton.gameObject.SetActive(true);
      RefreshGameListButton.gameObject.SetActive(true);
      RefreshGameListButtonText.text = "Refresh Game List";
      PlayerName.enabled = false;
      ChangeAvatarButton.enabled = false;
      GamePlayersSwitch.SetActive(false);
    }
    else
      SetInfo(res.Substring(1), true);
  }

  public void DisconnectAsMultiplayer() {
    if (GD.thePlayer == null) {
      SetInfo("You are not connected to a server!", true);
      return;
    }
    SetInfo("Disconnecting...");
    GD.DebugLog("Disconnecting...", GD.LT.Log);
    if (!GD.instance.networkManager.alreadyStarted) GD.instance.networkManager.StartUpClient();
    if (GD.thePlayer != null) {
      GD.instance.networkManager.DisconnectFromServer(GD.thePlayer);
      chat.EndChats(GD.thePlayer);
      GD.thePlayer.Kill();
      GD.thePlayer = null;
    }
    GD.SetStatus(LoaderStatus.Startup);
    SetInfo("Disconnected");
    GD.DebugLog("Disconnecting...", GD.LT.Log);
    GameModeInfoText.text = "Select mode";
    CreateGameButton.gameObject.SetActive(false);
    RefreshGameListButton.gameObject.SetActive(false);
    GamePlayersSwitch.gameObject.SetActive(false);
    PlayerName.enabled = true;
    ChangeAvatarButton.enabled = true;
    Back();
  }

  public void StartLocalServer() {
    if (GD.instance.networkManager == null) GD.instance.networkManager = new NetworkManager();
    if (GD.instance.networkManager.alreadyStarted) GD.instance.networkManager.StopServer();
    GD.instance.networkManager.SetPort(ServerPort.text);
    GD.instance.networkManager.serverPassword = ServerPassword.text;
    GD.instance.networkManager.StartUpServer();
    GamePlayersSwitch.SetActive(true);
    GamePlayersSwitchText.text = "Show Games";
    showGames = false;
    introMusic.Stop();
  }

  public void ConnectToRemoteServer() {
    SetInfo("Connecting to remote server...");
    GD.DebugLog("Connecting to remote server...", GD.LT.Log);
    if (!GD.instance.networkManager.alreadyStarted) GD.instance.networkManager.StartUpClient();
    int.TryParse(ServerPort.text, out int ip);
    if (GD.thePlayer == null) {
      // Create a dummy player for remote connections
      GD.thePlayer = new Player("Server Manager");
    }
    else if (GD.thePlayer != null && GD.thePlayer.tcpClient != null && GD.thePlayer.tcpClient.Connected) {
      GD.instance.networkManager.DisconnectFromServer(GD.thePlayer);
      chat.EndChats(GD.thePlayer);
      // Create a dummy player for remote connections
      GD.thePlayer = new Player("Server Manager");
    }
    string res = GD.instance.networkManager.ConnectToRemoteServer(GD.thePlayer, ServerPassword.text, ServerAddress.text, ip);
    if (res[0] != '!') {
      ConnectServerButton.gameObject.SetActive(false);
      GamePlayersSwitch.SetActive(true);
      GamePlayersSwitchText.text = "Show Games";
      showGames = false;
      PlayersList.gameObject.SetActive(true);
      DisconnectPlayerButton.gameObject.SetActive(true);
      RefreshGameListButton.gameObject.SetActive(true);
      RefreshGameListButtonText.text = "Refresh Lists";
      GD.SetStatus(LoaderStatus.RemoteServer);
      SetInfo("Remote server access created");
      GD.DebugLog("Remote server access created...", GD.LT.Log);
      ulong.TryParse(res, out GD.thePlayer.ID);
      chat.Startup(GD.thePlayer);

      GameModeInfoText.text = "Remote Server";
      GD.thePlayer.OnServerMessage += HandleEventServerMessages;
      GD.thePlayer.StartClientThread();
      ServerAddress.gameObject.SetActive(true);
      ServerAddress.enabled = false;
      ServerPort.gameObject.SetActive(true);
      ServerPort.enabled = false;
      ServerPassword.gameObject.SetActive(true);
      ServerPassword.enabled = false;
      creditsButton.gameObject.SetActive(false);
      optionsButton.gameObject.SetActive(false);
      enableCameraMovements = false;
      world.gameObject.SetActive(false);
      introMusic.Stop();
    }
    else
      SetInfo(res.Substring(1), true);
  }

  public void PingServer() {
    if (GD.thePlayer == null) {
      SetInfo("Player is NOT defined!", true);
      return;
    }
    SetInfo(GD.instance.networkManager.PingServer(GD.thePlayer));
  }

  public void BeginCreateGame() {
    CreateGameLine.SetActive(true);
    CreateGameButton.gameObject.SetActive(false);
    RefreshGameListButton.gameObject.SetActive(false);
    GameName.text = "";
    DifficultyMP.SetValueWithoutNotify(0);
    NumPlayers.SetValueWithoutNotify(0);
    NumAIs.SetValueWithoutNotify(0);
    SetInfo("");
  }


  public void CreateGame() {
    SetInfo("");
    // player name
    if (string.IsNullOrEmpty(PlayerName.text)) {
      SetInfo("You must name your player!", true);
      return;
    }
    // game name
    if (string.IsNullOrWhiteSpace(GameName.text)) {
      SetInfo("You must name your game!", true);
      return;
    }
    GameName.text = GameName.text.Trim();
    // Create the actual game
    string res = GD.instance.networkManager.CreateGameClient(GameName.text, GD.thePlayer, DifficultyMP.value, NumPlayers.value, NumAIs.value);
    if (string.IsNullOrEmpty(res)) {
      CreateGameLine.SetActive(false);
      CreateGameButton.gameObject.SetActive(true);
      RefreshGameListButton.gameObject.SetActive(true);
      RefreshGameListButtonText.text = "Refresh Game List";
      RefreshGames();
      GD.thePlayer.CurrentGame = GameName.text;
    }
    else
      SetInfo(res, true);
  }

  public void JoinGame(string gamename) {
    string res = GD.instance.networkManager.JoinGameClient(GD.thePlayer, gamename);
    if (string.IsNullOrEmpty(res)) {
      SetInfo("");
      return;
    }
    if (res[0] != '!') {
      SetInfo(res);
      CreateGameButton.gameObject.SetActive(true);
      RefreshGameListButton.gameObject.SetActive(true);
      RefreshGameListButtonText.text = "Refresh Game List";
      GameTxt.text = "";
      GD.thePlayer.CurrentGame = gamename;
    }
    else
      SetInfo(res.Substring(1), true);
  }

  public void LeaveGame(string gamename) {
    string res = GD.instance.networkManager.LeaveGameClient(GD.thePlayer, gamename);
    if (string.IsNullOrEmpty(res)) {
      SetInfo("");
      return;
    }
    if (res[0] != '!') {
      SetInfo(res);
      CreateGameButton.gameObject.SetActive(true);
      RefreshGameListButton.gameObject.SetActive(true);
      RefreshGameListButtonText.text = "Refresh Game List";
      GameTxt.text = "";
      GD.thePlayer.CurrentGame = "";
      RefreshGames();
    }
    else
      SetInfo(res.Substring(1), true);
  }

  public void LeaveGameMultiplayer() {
    string res = GD.instance.networkManager.LeaveGameClient(GD.thePlayer, GD.thePlayer.CurrentGame);
    if (string.IsNullOrEmpty(res)) {
      SetInfo("");
      MultiPlayerStarting.SetActive(false);
      LeaveGameButton.gameObject.SetActive(false);
      StartGameButton.gameObject.SetActive(false);
      return;
    }
    if (res[0] != '!') {
      SetInfo(res);
      CreateGameButton.gameObject.SetActive(true);
      RefreshGameListButton.gameObject.SetActive(true);
      RefreshGameListButtonText.text = "Refresh Game List";
      GameTxt.text = "";
      GD.thePlayer.CurrentGame = "";
      RefreshGames();
    }
    else
      SetInfo(res.Substring(1), true);
  }


  public void DestroyGame(string gamename) {
    if (GD.thePlayer != null) {
      if (GD.IsStatus(LoaderStatus.MultiPlayer, LoaderStatus.RemoteServer)) {
        string res = GD.instance.networkManager.DeleteGameClient(GD.thePlayer, gamename);
        if (!string.IsNullOrEmpty(res)) {
          if (res[0] != '!') {
            SetInfo(res);
            RefreshGameListButton.gameObject.SetActive(true);
            if (GD.IsStatus(LoaderStatus.MultiPlayer)) {
              CreateGameButton.gameObject.SetActive(true);
              RefreshGameListButtonText.text = "Refresh Game List";
            }
            else if (GD.IsStatus(LoaderStatus.RemoteServer))
              RefreshGameListButtonText.text = "Refresh Lists";
            RefreshGames();
          }
          else
            SetInfo(res.Substring(1), true);
        }
      }
    }
    else {
      byte[] name = System.Text.Encoding.UTF8.GetBytes(gamename);
      GD.instance.networkManager.DeleteGame(null, name, name.Length);
    }
  }

  public void RefreshGames(bool verbose = false) {
    if (verbose) SetInfo("Refreshing game list...");
    if (GD.IsStatus(LoaderStatus.MultiPlayer))
      GD.instance.networkManager.GetGameList(GD.thePlayer);
    else
      GD.instance.networkManager.UpdateLists(GD.thePlayer, showGames);
    if (verbose) SetInfo("");
  }

  public void StartChatWith(ulong playerID, string pname, int pavatar) {
    // Open the chat window, set the recipent
    chat.StartChatWith(playerID, pname, pavatar);
  }
  public void StartChatWith(string gamename) {
    // Start a chat with all people in the game (we need to ask for the ids) and ourself
    string res = GD.instance.networkManager.GetPlayersFromGameClient(GD.thePlayer, gamename);
    if (!string.IsNullOrWhiteSpace(res) && res[0] == '!')
      SetInfo(res.Substring(1), true);
  }

  public void PickAvatar() {
    foreach (Transform t in SelectedEnemies)
      t.GetComponent<Enemy>().HideBalloon();
    Avatars.SetActive(true);
  }

  public void SelectAvatar(int num) {
    Avatars.SetActive(false);
    Avatar.sprite = GD.Avatar(num);
    playerAvatar = num;
  }

  public void HandleEventServerMessages(object sender, NetworkManager.ServerMessage msg) {
    if (serverMessagesNum >= serverMessages.Length) return;
    serverMessages[serverMessagesNum].type = msg.type;
    serverMessages[serverMessagesNum].message = msg.message;
    serverMessages[serverMessagesNum].gameList = msg.gameList;
    serverMessages[serverMessagesNum].playersList = msg.playerList;
    serverMessages[serverMessagesNum].gameplayers = msg.gamePlayersList;
    serverMessages[serverMessagesNum].num = msg.num;
    serverMessagesNum++;
  }

  public struct ServerMessage {
    public ServerMessages type;
    public string message;
    public List<Game> gameList;
    public List<Player> playersList;
    public SimpleList gameplayers;
    public int num; // In case the type is StartingTheGame then the value of num indicates what is going on. =2 if single player starting, =0 if multiplayer but not all players started, =1 if multiplayer and all players started

    public ServerMessage(ServerMessages type) {
      this.type = type;
      message = null;
      gameList = null;
      playersList = null;
      gameplayers = null;
      num = 0;
    }
  }

  ServerMessage EmptyMessage = new ServerMessage(ServerMessages.Nothing);

  private ServerMessage GetFirstMessage() {
    if (serverMessagesNum == 0) return EmptyMessage;
    ServerMessage res = serverMessages[serverMessagesNum - 1];
    serverMessages[serverMessagesNum - 1].type = ServerMessages.Nothing;
    serverMessages[serverMessagesNum - 1].message = null;
    serverMessages[serverMessagesNum - 1].gameList = null;
    serverMessages[serverMessagesNum - 1].playersList = null;
    serverMessages[serverMessagesNum - 1].gameplayers = null;
    serverMessages[serverMessagesNum - 1].num = 0;
    serverMessagesNum--;
    return res;
  }


  public void UpdateGameList(List<Game> games, int numPlayers) {
    if (games == null) return;
    PlayersList.SetActive(false);
    GamesList.SetActive(true);
    GamePlayersSwitchText.text = "Show Players";
    showGames = true;

    foreach (Transform c in GamesGrid)
      GameObject.Destroy(c.gameObject);

    TotalNumberOfPlayers.text = "Players on the server: " + numPlayers;
    if (games.Count == 0) {
      if (GD.IsStatus(LoaderStatus.Server))
        SetInfo("No games.");
      else if (InfoTxt.text == "")
        SetInfo("No games found. Create a new game!");
      return;
    }
    if (GD.IsStatus(LoaderStatus.Server))
      SetInfo(games.Count + " game" + (games.Count == 1 ? "" : "s") + " defined");

    int pos = 0;
    bool foundOurGame = false;
    foreach (Game g in games) {
      GameObject line = Instantiate(GameLineTemplate, GamesGrid);
      line.transform.localPosition = new Vector3(0, pos * -40f, 0);
      // Find the parts of the line and update the values
      line.transform.Find("GameName").GetComponent<TextMeshProUGUI>().text = g.Name;
      if (g.Name.Length > 30)
        line.transform.Find("GameName").GetComponent<TextMeshProUGUI>().fontSize = 18;
      if (g.Name.Length > 40)
        line.transform.Find("GameName").GetComponent<TextMeshProUGUI>().fontSize = 10;
      line.transform.Find("Creator").GetComponent<TextMeshProUGUI>().text = g.Creator.name;
      if (g.Creator.name.Length > 30)
        line.transform.Find("Creator").GetComponent<TextMeshProUGUI>().fontSize = 18;
      if (g.Creator.name.Length > 40)
        line.transform.Find("Creator").GetComponent<TextMeshProUGUI>().fontSize = 10;
      line.transform.Find("CreatorAvatar").GetComponent<Image>().sprite = GD.Avatar(g.Creator.avatar);
      line.transform.Find("Difficulty").GetComponent<TextMeshProUGUI>().text = (g.Difficulty + 1).ToString();
      line.transform.Find("NumPlayers").GetComponent<TextMeshProUGUI>().text = g.NumPlayers.ToString();
      line.transform.Find("NumAIs").GetComponent<TextMeshProUGUI>().text = g.NumAIs.ToString();
      line.transform.Find("Joined").GetComponent<TextMeshProUGUI>().text = g.NumJoined.ToString();
      line.transform.Find("Status").GetComponent<TextMeshProUGUI>().text = g.Status.ToString();

      string gameName = (string)g.Name.Clone();
      if (GD.IsStatus(LoaderStatus.Server, LoaderStatus.RemoteServer)) {
        line.transform.Find("Chat").gameObject.SetActive(true);
        line.transform.Find("Chat").GetComponent<Button>().onClick.AddListener(delegate { StartChatWith(gameName); });
        line.transform.Find("Join").gameObject.SetActive(false);
        line.transform.Find("Leave").gameObject.SetActive(false);
        line.transform.Find("Destroy").gameObject.SetActive(true); // Always possible to remove a game server side
        line.transform.Find("Destroy").GetComponent<Button>().onClick.AddListener(delegate { DestroyGame(gameName); });
      }
      else {
        line.transform.Find("Chat").gameObject.SetActive(true);
        line.transform.Find("Chat").GetComponent<Button>().onClick.AddListener(delegate { StartChatWith(gameName); });
        line.transform.Find("Join").gameObject.SetActive(!g.AmIIn(GD.thePlayer) && g.SpaceAvailable() && g.Status == GameStatus.Waiting);
        line.transform.Find("Join").GetComponent<Button>().onClick.AddListener(delegate { JoinGame(gameName); });
        line.transform.Find("Leave").gameObject.SetActive(g.AmIIn(GD.thePlayer));
        line.transform.Find("Leave").GetComponent<Button>().onClick.AddListener(delegate { LeaveGame(gameName); });
        line.transform.Find("Destroy").gameObject.SetActive(GD.thePlayer.ID.Equals(g.Creator.id)); // Only if I was the creator
        line.transform.Find("Destroy").GetComponent<Button>().onClick.AddListener(delegate { DestroyGame(gameName); });
      }
      pos++;

      if (GD.thePlayer != null && GD.thePlayer.CurrentGame == g.Name) {
        foundOurGame = true;
      }
    }
    if (!foundOurGame) {
      StartGameButton.gameObject.SetActive(false);
    }
  }

  public void UpdatePlayersList(List<Player> players) {
    if (players == null) return;
    PlayersList.SetActive(true);
    GamesList.SetActive(false);
    GamePlayersSwitchText.text = "Show Games";
    showGames = false;

    foreach (Transform c in PlayersGrid)
      GameObject.Destroy(c.gameObject);

    if (players.Count == 0) {
      SetInfo("No players.");
      PlayersList.SetActive(false);
      return;
    }
    SetInfo(players.Count + " player" + (players.Count == 1 ? "" : "s") + " registered");

    int pos = 0;
    foreach (Player p in players) {
      GameObject line = Instantiate(PlayerLineTemplate, PlayersGrid);
      line.transform.localPosition = new Vector3(0, pos * -40f, 0);
      pos++;
      // Find the parts of the line and update the values
      ulong playerID = p.ID;
      int avatar = p.Avatar;
      string pname = (string)p.Name.Clone();
      line.transform.Find("Avatar").GetComponent<Image>().sprite = GD.Avatar(p.Avatar);
      line.transform.Find("PlayerName").GetComponent<TextMeshProUGUI>().text = p.Name;
      if (GD.thePlayer != null && p.ID != GD.thePlayer.ID)
        line.transform.Find("Chat").GetComponent<Button>().onClick.AddListener(delegate { StartChatWith(playerID, pname, avatar); });
      else
        line.transform.Find("Chat").gameObject.SetActive(false);
      line.transform.Find("LastAccess").GetComponent<TextMeshProUGUI>().text = p.LastAccess.ToString();
      line.transform.Find("IP").GetComponent<TextMeshProUGUI>().text = p.IP.ToString();
      if (p.Status != StatusOfPlayer.Waiting)
        line.transform.Find("Status").GetComponent<TextMeshProUGUI>().text = p.Status.ToString() + " " + p.CurrentGame;
      else
        line.transform.Find("Status").GetComponent<TextMeshProUGUI>().text = p.Status.ToString();
    }
  }


  private void SetInfo(string msg, bool error = false) {
    if (msg == null) {
      InfoTxt.text = "EMPTY MESSAGE";
      GD.DebugLog(System.Environment.StackTrace, GD.LT.Warning);
      return;
    }
    if (msg.Length > 90)
      InfoTxt.fontSize = 24;
    else
      InfoTxt.fontSize = 36;
    if (error)
      InfoTxt.color = RedColor;
    else
      InfoTxt.color = YellowColor;
    InfoTxt.text = msg;
    if (msg.IndexOf("DEBUG") != -1 || error)
      GD.DebugLog(msg, GD.LT.Error);
  }

  Color RedColor = new Color32(255, 0, 0, 255);
  Color YellowColor = new Color32(255, 246, 0, 255);

}
