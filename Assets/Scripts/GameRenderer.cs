using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PlayerAction { Nothing, ViewResources, ViewTechs, DevelopTechs, ViewImps, Developimps };

public class GameRenderer : MonoBehaviour {
  // Used just to renderer things. We should always ask the GameManager for all operations
  // This script should handle the user inputs

  public static GameRenderer instance = null;
  public Transform Players;
  public Transform EnemiesStorage;
  public Transform SelectedEnemies;
  public GameObject PlayerTemplate;
  public GameObject BalloonTemplate;
  public Transform Balloons;
  private Color HighlightColor = new Color32(128, 128, 128, 255);
  public Image StatusImage;
  public TextMeshProUGUI StatusText;
  public Image[] Lands;
  public Sprite[] PossibleLands;
  public GameObject CityTemplate;
  public Transform CitiesHolder;
  public GameObject MultiPlayerStarting;
  public GameObject[] playersGOs;
  public Enemy[] enemyGOs;
  private Game game;
  private GameEngine engine;
  public ChatManager chat;
  private PlayerAction action = PlayerAction.Nothing;
  public Toggle ChatButton;
  public Options Options;
  private readonly GameAction gameAction;
  public Button EndTurnButton;
  private RunningGameStatus runningGameStatus = RunningGameStatus.WaitUserAction;
  private GameEngineValues turn = null;
  private float timeToWait = 0;

  public void Init(Game theGame) {
    game = theGame;
    engine = game.engine;
    playersGOs = new GameObject[6];
    enemyGOs = new Enemy[6];
    ChatButton.gameObject.SetActive(game.multiplayer);
    EndTurnButton.enabled = true;

    // Set all the graphics and the list of players
    foreach (Transform t in Players)
      GameObject.Destroy(t.gameObject);

    int pos = 0;
    for (int i = 0; i < engine.nump; i++) {
      PlayerStatus player = engine.players[i];
      GameObject enemy = Instantiate(GetEnemyGameObject(player), Players);

      player.refEnemy = enemy.GetComponent<Enemy>();
      if (player.isAI) {
        player.refEnemy.balloon = Instantiate(BalloonTemplate, Balloons).GetComponent<Balloon>();
      }
      else { // Initialize the stats (name, avatar, id)
        player.refEnemy.stats.GetFrom(player);
      }

      playersGOs[pos] = enemy;
      enemy.transform.Find("Button").GetComponent<Image>().enabled = true;
      ColorBlock cb = enemy.transform.Find("Button").GetComponent<Button>().colors;
      cb.colorMultiplier = 1;
      cb.highlightedColor = HighlightColor;
      enemy.transform.Find("Button").GetComponent<Button>().colors = cb;
      enemy.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = player.name;
      Transform tc = enemy.transform.Find("TurnCompleted");
      if (tc != null) tc.GetComponent<Image>().color = ColorInvisible;
      enemyGOs[pos] = player.refEnemy;
      player.SetIndex(pos);
      pos++;
      if (pos >= engine.nump) pos = 0;
      enemy.transform.Find("Button").GetComponent<Image>().color = player.color;
      if (player.isAI)
        player.avatar = GD.GetAvatarForAI(player.name); // Find the AI avatar (based on name)
      else {
        enemy.transform.Find("Face").GetComponent<Image>().sprite = GD.Avatar(player.avatar);
        enemy.transform.Find("DeadEyeL").GetComponent<Image>().enabled = false;
      }

      if (player.id == GD.thePlayer.ID)
        engine.mySelf = player; // Pick ourself as Enemy (based on ID), and initialize the stats
      else
        player.refEnemy.OnAlterPlayer += ClickOnPlayer;
    }

    
    // Say something, but keep in mind the balloon can be on the wrong Canvas
    gameObject.SetActive(true);
    MultiPlayerStarting.SetActive(false);
    StartCoroutine(IntroMessages());
    instance = this;

    // Get the lands as random
    int[] ls = new int[PossibleLands.Length];
    for (int i = 0; i < ls.Length; i++) ls[i] = i;
    for (int i = 0; i < ls.Length * ls.Length; i++) {
      int a = GD.GetRandom(0, ls.Length);
      int b = GD.GetRandom(0, ls.Length);
      int tmp = ls[a];
      ls[a] = ls[b];
      ls[b] = tmp;
    }
    for (int i = 0; i < 6; i++)
      Lands[i].sprite = PossibleLands[i];

    // Create the cities
    for (int i = 0; i < 3 * 3 * 6; i++)
      if (engine.cities[i] != null)
        GameObject.Destroy(engine.cities[i].gameObject);

    for (int i = 0; i < 3 * 3 * 6; i++) {
      GameObject city = Instantiate(CityTemplate, CitiesHolder);
      engine.cities[i] = city.GetComponent<City>();
      engine.cities[i].Init(i);
    }
    int[] sectors = new int[6];
    for (int i = 0; i < 6; i++) sectors[i] = i;
    for (int i = 0; i < 1000; i++) {
      int a = GD.GetRandom(0, 6);
      int b = GD.GetRandom(0, 6);
      int tmp = sectors[a];
      sectors[a] = sectors[b];
      sectors[b] = tmp;
    }

    // Assign a single city (population proportional to the difficulty) to each player
    for (int i = 0; i < engine.nump; i++) {
      PlayerStatus player = engine.players[i];
      pos = 1 + 9 + (sectors[i] < 3 ? sectors[i] * 3 : sectors[i] * 3 + 9 * 2);
      int cx = pos % 9;
      int cy = pos / 9;
      if (player.isAI)
        engine.cities[pos].Set(5 + 5 * GD.difficulty, engine.players[engine.players[i].index]);
      else
        engine.cities[pos].Set(10 - GD.difficulty, engine.players[engine.players[i].index]);
      player.cities.Add(pos);
    }

    // Set the initial resources based on difficulty
    // FIXME

    HideAllViews();
  }

  private GameObject GetEnemyGameObject(PlayerStatus p) {
    if (p.isAI) {
      foreach (Transform t in SelectedEnemies) {
        Enemy e = t.GetComponent<Enemy>();
        if (e != null && e.stats.name == p.name)
          return t.gameObject;
      }
      foreach (Transform t in EnemiesStorage) {
        Enemy e = t.GetComponent<Enemy>();
        if (e != null && e.stats.name == p.name)
          return t.gameObject;
      }
    }
    return PlayerTemplate;
  }

  public IEnumerator IntroMessages() {
    yield return new WaitForSeconds(.5f);
    foreach (Transform t in Players) {
      Enemy en = t.GetComponent<Enemy>();
      // 0, 1, 3 -> TL
      if (en.stats.isAI && en.stats.position != 2 && en.stats.position == 4) {
        en.Say(Enemy.GetIntroMessage(), BalloonDirection.TL);
        yield return new WaitForSeconds(.5f);
      }
    }
    yield return new WaitForSeconds(1.5f);
    foreach (Transform t in Players) {
      Enemy en = t.GetComponent<Enemy>();
      // 2, 4, 5 -> TR
      if (en.stats.isAI && (en.stats.position == 2 || en.stats.position == 4)) {
        en.Say(Enemy.GetIntroMessage(), BalloonDirection.TR);
        yield return new WaitForSeconds(.5f);
      }
    }
    yield break;
  }

  public void ClickOnPlayer(object sender, Enemy.AlterPlayerEvent e) {
    if (e.enemy.stats.isAI) {
      e.enemy.SetMood((Mood)Random.Range(0, 30), false, e.enemy.stats.index < 3 ? BalloonDirection.TL : BalloonDirection.TR);
    }
  }

  public static void UpdateStatusMessage(string text, Sprite icon) {
    instance.StatusText.text = text;
    instance.StatusImage.sprite = icon;
  }

  int lastWidth = 1920;
  int lastHeight = 1080;
  float lastResize = 1;

  private void Update() {
    if (GD.fullScreen == FullScreenMode.Windowed && (Screen.width != lastWidth || Screen.height != lastHeight)) {
      lastResize -= Time.deltaTime;
      if (lastResize < 0) {
        lastResize = 1f;
        lastWidth = Screen.width;
        lastHeight = (int)(lastWidth * 1080f / 1920f);
        Screen.SetResolution(lastWidth, lastHeight, GD.fullscreen);
      }
    }

    if (timeToWait > 0) {
      timeToWait -= Time.deltaTime;
      return;
    }

    if (runningGameStatus == RunningGameStatus.RunningActions) {
      ProcessTurnAction();
      return;
    }

    // Process the messages
    if (engine == null || engine.messages == null || engine.messages.Count == 0) return;
    NetworkManager.GameMessage msg = engine.messages[0];
    engine.messages.RemoveAt(0);

    switch(msg.type) {
      case GameMsgType.PlayerDeath: // Remove the player (And have all his cities to be destroyed, and use all remaining weapons
        ShowMessage("Received player death: " + msg.id);
        for (int i = 0; i < engine.nump; i++) {
          if (engine.players[i].id == msg.id) {
            playersGOs[i].transform.Find("DeadEyeL").GetComponent<Image>().enabled = true;
            return;

            // FIXME add a message
            // FIXME do some destruction
          }
        }
        break;

      case GameMsgType.GameProgressUpdate:
        // Find the player in the top bar, and show a green checkmark over it
        foreach (Transform pt in Players)
          if (pt.GetComponent<Enemy>().stats.id == msg.id)
            pt.Find("TurnCompleted").GetComponent<Image>().color = ColorVisible;
        break;

      case GameMsgType.Error:
        ShowMessage(msg.text, true);
        break;

      case GameMsgType.GameTurn:
        turn = msg.engineValues;
        PlayEndTurn();
        break; // FIXME A turn has ended, just play all the actions

    }
  }

  #region Resources ************************************************************************************************************************************************************
  public GameObject Resources;
  public Image ResourcesAvatar;
  public TextMeshProUGUI FoodQ;
  public TextMeshProUGUI FoodI;
  public TextMeshProUGUI FoodD;
  public TextMeshProUGUI IronQ;
  public TextMeshProUGUI IronI;
  public TextMeshProUGUI IronD;
  public TextMeshProUGUI AluminumQ;
  public TextMeshProUGUI AluminumI;
  public TextMeshProUGUI AluminumD;
  public TextMeshProUGUI UraniumQ;
  public TextMeshProUGUI UraniumI;
  public TextMeshProUGUI UraniumD;
  public TextMeshProUGUI PlutoniumQ;
  public TextMeshProUGUI PlutoniumI;
  public TextMeshProUGUI PlutoniumD;
  public TextMeshProUGUI HydrogenQ;
  public TextMeshProUGUI HydrogenI;
  public TextMeshProUGUI HydrogenD;
  public TextMeshProUGUI PlasticQ;
  public TextMeshProUGUI PlasticI;
  public TextMeshProUGUI PlasticD;
  public TextMeshProUGUI ElectronicQ;
  public TextMeshProUGUI ElectronicI;
  public TextMeshProUGUI ElectronicD;
  public TextMeshProUGUI CompositeQ;
  public TextMeshProUGUI CompositeI;
  public TextMeshProUGUI CompositeD;
  public TextMeshProUGUI FossilQ;
  public TextMeshProUGUI FossilI;
  public TextMeshProUGUI FossilD;

  public void ViewResources(int enemy = -1) {
    if (runningGameStatus != RunningGameStatus.WaitUserAction) return;
    HideAllViews();
    action = PlayerAction.ViewResources;
    Resources.SetActive(true);

    PlayerStatus s = enemy == -1 ? engine.mySelf : engine.players[enemy];

    FoodQ.text = s.Food[0].ToString();
    FoodI.text = s.Food[1] != 0 ? s.Food[1].ToString() : "";
    FoodD.text = s.Food[2] != 0 ? s.Food[2].ToString() : "";
    IronQ.text = s.Iron[0].ToString();
    IronI.text = s.Iron[1] != 0 ? s.Iron[1].ToString() : "";
    IronD.text = s.Iron[2] != 0 ? s.Iron[2].ToString() : "";
    AluminumQ.text = s.Aluminum[0].ToString();
    AluminumI.text = s.Aluminum[1] != 0 ? s.Aluminum[1].ToString() : "";
    AluminumD.text = s.Aluminum[2] != 0 ? s.Aluminum[2].ToString() : "";
    UraniumQ.text = s.Uranium[0].ToString();
    UraniumI.text = s.Uranium[1] != 0 ? s.Uranium[1].ToString() : "";
    UraniumD.text = s.Uranium[2] != 0 ? s.Uranium[2].ToString() : "";
    PlutoniumQ.text = s.Plutonium[0].ToString();
    PlutoniumI.text = s.Plutonium[1] != 0 ? s.Plutonium[1].ToString() : "";
    PlutoniumD.text = s.Plutonium[2] != 0 ? s.Plutonium[2].ToString() : "";
    HydrogenQ.text = s.Hydrogen[0].ToString();
    HydrogenI.text = s.Hydrogen[1] != 0 ? s.Hydrogen[1].ToString() : "";
    HydrogenD.text = s.Hydrogen[2] != 0 ? s.Hydrogen[2].ToString() : "";
    PlasticQ.text = s.Plastic[0].ToString();
    PlasticI.text = s.Plastic[1] != 0 ? s.Plastic[1].ToString() : "";
    PlasticD.text = s.Plastic[2] != 0 ? s.Plastic[2].ToString() : "";
    ElectronicQ.text = s.Electronics[0].ToString();
    ElectronicI.text = s.Electronics[1] != 0 ? s.Electronics[1].ToString() : "";
    ElectronicD.text = s.Electronics[2] != 0 ? s.Electronics[2].ToString() : "";
    CompositeQ.text = s.Composite[0].ToString();
    CompositeI.text = s.Composite[1] != 0 ? s.Composite[1].ToString() : "";
    CompositeD.text = s.Composite[2] != 0 ? s.Composite[2].ToString() : "";
    FossilQ.text = s.FossilFuels[0].ToString();
    FossilI.text = s.FossilFuels[1] != 0 ? s.FossilFuels[1].ToString() : "";
    FossilD.text = s.FossilFuels[2] != 0 ? s.FossilFuels[2].ToString() : "";
    // FIXME show the avatar of the enemy
  }

  #endregion
  
  #region Technologies ************************************************************************************************************************************************************

  public GameObject Technologies;
  public Toggle[] TechButtons;

  public Image TechIcon;
  public TextMeshProUGUI TechTitle;
  public TextMeshProUGUI TechDesription;
  public Transform TechDepsGrid;
  public GameObject TechDepTemplate;


  public void ViewTechs(bool build) {
    if (runningGameStatus != RunningGameStatus.WaitUserAction) return;
    HideAllViews();
    action = build ? PlayerAction.DevelopTechs : PlayerAction.ViewTechs;
    Technologies.SetActive(true);
    for (int i = 0; i < TechButtons.Length; i++) {
      TechButtons[i].SetIsOnWithoutNotify(false);
      if (engine.mySelf.techs[i].available) {
        TechButtons[i].GetComponent<Image>().color = AvailableTechColor;
      }
      else {
        // Are prerequisites met? (techs and imprs)
        if (IsItemValid(GD.instance.Technologies[i]))
          TechButtons[i].GetComponent<Image>().color = PossibleTechColor;
        else
          TechButtons[i].GetComponent<Image>().color = NotgoodTechColor;
      }
    }
  }

  public void ClickOnTech(int techIndex) {
    if (runningGameStatus != RunningGameStatus.WaitUserAction) return;
    // Start by unchecking all buttons
    for (int i = 0; i < TechButtons.Length; i++)
      TechButtons[i].SetIsOnWithoutNotify(false);
    // Are we deciding which tech to develop?
    if (action == PlayerAction.DevelopTechs) {
      // Yes, Can we develop this?
      if (TechButtons[techIndex].GetComponent<Image>().color == PossibleTechColor) {
        //   Yes, set it as action
        TechButtons[techIndex].SetIsOnWithoutNotify(true);
        gameAction.action = ActionType.ResearchTechnology;
        gameAction.tech = techIndex;
        ShowMessage("You will research <b>" + GD.GetTechName(techIndex) + "</b>");
      }
      //   No, do not check it
    }
    // Show the info
    Item tech = GD.instance.Technologies[techIndex];
    // Icon, Name, and Description
    TechIcon.sprite = tech.icon;
    TechTitle.text = tech.name;
    TechDesription.text = tech.description;

    // Prerequisites (need to create one for each dependency and place it in the layout container)
    foreach (Transform t in TechDepsGrid.transform)
      GameObject.Destroy(t.gameObject);
    foreach (Dependency dep in tech.prerequisites) {
      GameObject dependency = Instantiate(ImpDepTemplate, TechDepsGrid);
      dependency.SetActive(true);

      if (dep.type == ProductionType.Technology) {
        Item aTech = GD.instance.Technologies[(int)dep.index - (int)dep.type];
        dependency.transform.Find("GoodBad").GetComponent<Image>().sprite = IsItemAvailable(aTech) ? GoodSprite : BadSprite;
        dependency.transform.Find("Icon").GetComponent<Image>().sprite = aTech.icon;
        dependency.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = aTech.name;
      }
      else if (dep.type == ProductionType.Improvement) {
        Item anImp = GD.instance.Improvements[(int)dep.index - (int)dep.type];
        dependency.transform.Find("GoodBad").GetComponent<Image>().sprite = IsItemAvailable(anImp) ? GoodSprite : BadSprite;
        dependency.transform.Find("Icon").GetComponent<Image>().sprite = anImp.icon;
        dependency.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = anImp.name;
      }
    }


    // FIXME Allows
  }

  #endregion Technologies

  #region Improvements ************************************************************************************************************************************************************

  public GameObject Improvements;
  public Toggle[] ImpButtons;
  public Image ImpIcon;
  public TextMeshProUGUI ImpTitle;
  public TextMeshProUGUI ImpDesription;
  public Transform ImpDepsGrid;
  public GameObject ImpDepTemplate;

  public void ViewImps(bool build) {
    if (runningGameStatus != RunningGameStatus.WaitUserAction) return;
    HideAllViews();
    action = build ? PlayerAction.Developimps : PlayerAction.ViewImps;
    Improvements.SetActive(true);
    for (int i = 0; i < ImpButtons.Length; i++) {
      ImpButtons[i].SetIsOnWithoutNotify(false);
      if (engine.mySelf.improvements[i].available) {
        ImpButtons[i].GetComponent<Image>().color = AvailableTechColor;
      }
      else {
        // Are prerequisites met? (techs and imprs)
        if (IsItemValid(GD.instance.Improvements[i]))
          ImpButtons[i].GetComponent<Image>().color = PossibleTechColor;
        else
          ImpButtons[i].GetComponent<Image>().color = NotgoodTechColor;
      }
    }
  }

  public void ClickOnImp(int impIndex) {
    if (runningGameStatus != RunningGameStatus.WaitUserAction) return;
    // Start by unchecking all buttons
    for (int i = 0; i < ImpButtons.Length; i++)
      ImpButtons[i].SetIsOnWithoutNotify(false);
    // Are we deciding which tech to develop?
    if (action == PlayerAction.Developimps) {
      // Yes, Can we develop this?
      if (ImpButtons[impIndex].GetComponent<Image>().color == PossibleTechColor) {
        //   Yes, set it as action
        ImpButtons[impIndex].SetIsOnWithoutNotify(true);
        gameAction.action = ActionType.BuildImprovement;
        gameAction.imp = impIndex;
        if (GD.selectedCity == -1)
          ShowMessage("You will build <b>" + GD.GetImprovementName(impIndex) + "</b>\n<color=red><i><b>Select the city!</b></i></color>");
        else {
          ShowMessage("You will build <b>" + GD.GetImprovementName(impIndex) + "</b>");
          HideAllViews();
        }
      }
      //   No, do not check it
    }
    // Show the info
    Item improvement = GD.instance.Improvements[impIndex];
    // Icon, Name, and Description
    ImpIcon.sprite = improvement.icon;
    ImpTitle.text = improvement.name;
    ImpDesription.text = improvement.description;

    // Prerequisites (need to create one for each dependency and place it in the layout container)
    foreach (Transform t in ImpDepsGrid.transform)
      GameObject.Destroy(t.gameObject);
    foreach (Dependency dep in improvement.prerequisites) {
      GameObject dependency = Instantiate(ImpDepTemplate, ImpDepsGrid);
      dependency.SetActive(true);

      if (dep.type == ProductionType.Technology) {
        Item aTech = GD.instance.Technologies[(int)dep.index - (int)dep.type];
        dependency.transform.Find("GoodBad").GetComponent<Image>().sprite = IsItemAvailable(aTech) ? GoodSprite : BadSprite;
        dependency.transform.Find("Icon").GetComponent<Image>().sprite = aTech.icon;
        dependency.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = aTech.name;
      }
      else if (dep.type == ProductionType.Improvement) {
        Item anImp = GD.instance.Improvements[(int)dep.index - (int)dep.type];
        dependency.transform.Find("GoodBad").GetComponent<Image>().sprite = IsItemAvailable(anImp) ? GoodSprite : BadSprite;
        dependency.transform.Find("Icon").GetComponent<Image>().sprite = anImp.icon;
        dependency.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = anImp.name;
      }
    }


    // FIXME Consume
    // FIXME Produce
  }

  #endregion Improvements

  #region Items support functions *************************************************************************************************************************************************

  public Sprite GoodSprite;
  public Sprite BadSprite;

  private bool IsItemValid(Item item) {
    bool good = true;
    foreach (Dependency it in item.prerequisites) {
      if (it.type == ProductionType.Technology && !engine.mySelf.techs[(int)it.index - (int)it.type].available) {
        good = false;
        break;
      }
      else if (it.type == ProductionType.Improvement && !engine.mySelf.improvements[(int)it.index - (int)it.type].available) {
        good = false;
        break;
      }
    }
    return good;
  }

  private bool IsItemAvailable(Item item) {
    if (item.type == ProductionType.Technology) return engine.mySelf.techs[(int)item.index].available;
    if (item.type == ProductionType.Improvement) return engine.mySelf.improvements[(int)item.index].available;
    return false; // FIXME check the resources or other stuff
  }

  #endregion Items support functions

  #region Actions *********************************************************************************************************************************************************

  public void FindResources() {
    gameAction.action = ActionType.FindResources;
    ShowMessage("You will find resources.");
    // No need to specify anything else
  }

  public void ResearchTechnologies() {
    // Show the tech view and wait for a tech to be selected
    gameAction.tech = -1;
    ViewTechs(true);
  }

  public void BuildImprovements() {
    // Show the improvement view and wait for a tech to be selected
    gameAction.imp = -1;
    gameAction.targetCity = -1;
    GD.selectedCity = -1;
    ViewImps(true);
  }

  public void EndTurn() {
    if (gameAction.action == ActionType.Nothing) {
      ShowMessage("<color=red>You did not select an action for the turn!</color>", true);
      return;
    }
    else if (gameAction.action == ActionType.ResearchTechnology && gameAction.tech == -1) {
      ShowMessage("<color=red>You did not select the technology to research!</color>", true);
      return;
    }
    else if (gameAction.action == ActionType.BuildImprovement) {
      if (gameAction.imp == -1) {
        ShowMessage("<color=red>You did not select the improvement to build!</color>", true);
        return;
      }
      if (GD.selectedCity == -1) {
        ShowMessage("<color=red>You did not select the city where to build the improvement!</color>", true);
        return;
      }
      gameAction.targetCity = GD.selectedCity;
    }

    engine.EndTurn(game.multiplayer, GD.thePlayer, gameAction);
    // Disable endturn, and hide all views
    EndTurnButton.enabled = false;
    HideAllViews();
    // Show the short version of the selected action
    ShowMessage("Action:\n" + gameAction.Display());
  }

  #endregion Actions

  #region Global commands *************************************************************************************************************************************************

  public void StartGameChat() {
    if (!game.multiplayer) return;
    List<ChatParticipant> participants = new List<ChatParticipant>();
    for(int i = 0; i < engine.nump; i++) {
      PlayerStatus en = engine.players[i];
      if (!en.isAI)
        participants.Add(new ChatParticipant(en.name, en.avatar, en.id));
    }
    chat.StartChatWith(game.Name, participants);
  }

  public GameObject MessageBox;
  public void LeaveGame() {
    MessageBox.SetActive(true);
  }

  public void YesLeave() {
    MessageBox.SetActive(false);
    engine.LeaveGame(game.Name, game.multiplayer);
    gameObject.SetActive(false);
    foreach (Transform t in Players)
      GameObject.Destroy(t.gameObject);
    foreach (Transform t in Balloons)
      GameObject.Destroy(t.gameObject);
    game.engine.Destroy();
    game.Destroy();
    GD.Restart();
  }

  public void NoStay() {
    MessageBox.SetActive(false);
  }

  public void ShowOptions() {
    Options.Init();
  }


  public void HideAllViews() {
    Resources.SetActive(false);
    Technologies.SetActive(false);
    Improvements.SetActive(false);
  }

  private void ShowMessage(string msg, bool warning = false) {
    StatusText.text = (warning ? "<color=red>" : "") + msg + (warning ? "</color>" : "");
    StatusImage.enabled = false;
    GD.DebugLog(msg, GD.LT.Debug);
  }

  private Color AvailableTechColor = new Color32(20, 255, 20, 255);
  private Color PossibleTechColor = new Color32(87, 142, 209, 255);
  private Color NotgoodTechColor = new Color32(255, 20, 20, 255);
  #endregion Global commands

  #region Main visual actions *************************************************************************************************************************************************

  private void PlayEndTurn() {
    // We may need to set a status as "playing" and then get the actions one by one
    runningGameStatus = RunningGameStatus.RunningTurn;
    HideAllViews();
    ShowMessage("Results of the turn...");
    GD.DebugLog("Processing next turn", GD.LT.Debug);

    // First reset the messages and the completed and endturn
    foreach (Transform t in Players) {
      Enemy en = t.GetComponent<Enemy>();
      if (en == null) continue;
      Transform completed = t.Find("TurnCompleted");
      if (completed != null) completed.GetComponent<Image>().color = ColorInvisible;
      if (en.balloon != null) en.balloon.Hide();
    }

    // Next city increase/decrease
    for (int i = 0; i < turn.cities.Length; i++) {
      if (turn.cities[i] == null) continue;
      CityVals cv = turn.cities[i];
      if (cv.pos == 255 || cv.status == City.Status.Empty) continue;
      PlayerStatus ownerval = engine.GetPlayerStatusByIndex(cv.owner);
      engine.cities[i].SetValues(cv, ownerval);
    }


    // FIXME at this point we cannot do everything here. We may need to use the Update() and play all actions
    runningGameStatus = RunningGameStatus.RunningActions;

    // -> Techs and Improvements done
    // -> GameActions


    // Then go in order for the players and run the action of each one, showing what is going on on a message

    // Check for win/loss

    // FIXME move to the processing of the turn
  }

  private void ProcessTurnAction() {
    // How we can understand what to do? We may need to keep track if we are playing something

    // Pick the lower order, if we got only -1 then we are done
    int min = 10;
    int index = -1;
    for (int i = 0; i < turn.order.Length; i++) {
      if (turn.order[i] < min) {
        min = turn.order[i];
        index = i;
      }
    }
    if (index == -1) { // Nothing more to do, reset the status and wait for a new command
      ShowMessage("Turn completed, select your next action"); // FIMXE if defeated change the message and just set the action to none and no possible endturn
      runningGameStatus = RunningGameStatus.WaitUserAction;
      gameAction.action = ActionType.Nothing;
      EndTurnButton.enabled = true;
      GD.DebugLog("Next turn completed", GD.LT.Debug);
      return;
    }

    // Set the resources for the player
    PlayerStatus player = turn.players[index];
    engine.players[index].UpdateResources(player);

    // Show something depending on the action, we may have the result of the action inside the turn FIXME it is not yet available
    switch(player.gameAction.action) {
      case ActionType.Nothing:
        ShowMessage(engine.players[index].name + " does nothing", true);
        break;

      case ActionType.FindResources:
        ShowMessage(engine.players[index].name + " is finding resources.\nFound extra FIXME", true); // Show stuff for other players only if we have counterintelligence
        break;

      case ActionType.ResearchTechnology:
        ShowMessage(engine.players[index].name + " researched technology " + GD.GetTechName(player.gameAction.tech));
        break;

      case ActionType.BuildImprovement:
        ShowMessage(engine.players[index].name + " is building " + GD.GetImprovementName(player.gameAction.imp) + " on city #" + player.gameAction.targetCity);
        break;

      default:
        ShowMessage(engine.players[index].name + " unhandled action " + player.gameAction.action.ToString(), true);
        break;
    }
    timeToWait = 2f;
    turn.order[index] = 255;
  }

  #endregion Main visual actions

  private Color ColorInvisible = new Color32(0, 0, 0, 0);
  private Color ColorVisible = new Color32(255, 255, 255, 255);
}

public enum RunningGameStatus { WaitUserAction, RunningTurn, RunningActions, Won, Loss };