using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum PlayerAction { Nothing,
  SettleNewCity,
  Denuclearize,
  BuildCityImprovements,
  DevelopTech
};

public enum WhatTheFuckStatus {  GetLost, PlayReceivedActions };

public class GameRenderer : MonoBehaviour {
  // Used just to renderer things. We should always ask the GameEngine for all operations

  private PlayerAction playerAction = PlayerAction.Nothing;
  private WhatTheFuckStatus wtf = WhatTheFuckStatus.GetLost;
  public static GameRenderer instance = null;
  private Game game;
  private GameEngine engine;

  [Header("Debug")]
  public TextMeshProUGUI DDDebug;

  private Color HighlightColor = new Color32(128, 128, 128, 255);
  private Color ColorInvisible = new Color32(0, 0, 0, 0);
  private Color ColorVisible = new Color32(255, 255, 255, 255);
  private readonly GameAction gameAction = new GameAction();
  private GameEngineValues turn = null;
  private float timeToWait = 0;
  private const float waitTime = 15f;
  int lastWidth = 1920;
  int lastHeight = 1080;
  float lastResize = 1;


  [Header("Players")]
  public Transform Players;
  public Transform EnemiesStorage;
  public Transform SelectedEnemies;
  public GameObject PlayerTemplate;
  public GameObject BalloonTemplate;
  public Transform Balloons;
  public Enemy[] enemyGOs;
  public GameObject[] playersGOs;
  public GameObject Highlighter;

  [Header("UI")]
  public GameObject MultiPlayerStarting;
  public Image[] Lands;
  public Sprite[] PossibleLands;
  public ChatManager chat;
  public Toggle ChatButton;
  public Options Options;
  public Image DisableButtons;
  public Button SkipButton;
  public Sprite GoodSprite;
  public Sprite BadSprite;
  public GameObject MessageBox;

  [Header("Status")]
  public Image StatusImage;
  public TextMeshProUGUI StatusText;
  public Happiness happiness;
  public Button EndTurnButton;

  [Header("Cities")]
  public GameObject CityTemplate;
  public Transform CitiesHolder;
  public City[] cities;
  public GameObject PersonPrefab;

/*
  [Header("Social")]
  [Header("Building")]
  [Header("Deploying")]
  [Header("Weapons")]
  [Header("Defenses")]
  */



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

    for (int i = 0; i < engine.nump; i++) {
      Player player = engine.players[i];
      GameObject enemy = Instantiate(GetEnemyGameObject(player), Players);

      player.refEnemy = enemy.GetComponent<Enemy>();
      // FIXME Initialize the stats (name, avatar, id)?

      playersGOs[i] = enemy;
      enemy.transform.Find("Button").GetComponent<Image>().enabled = true;
      ColorBlock cb = enemy.transform.Find("Button").GetComponent<Button>().colors;
      cb.colorMultiplier = 1;
      cb.highlightedColor = HighlightColor;
      enemy.transform.Find("Button").GetComponent<Button>().colors = cb;
      enemy.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = player.def.name;
      Transform tc = enemy.transform.Find("TurnCompleted");
      if (tc != null) tc.GetComponent<Image>().color = ColorInvisible;
      enemyGOs[i] = player.refEnemy;
      enemy.transform.Find("Button").GetComponent<Image>().color = player.color;
      if (player.def.type == PlayerDef.Type.AI)
        player.def.avatar = GD.GetAvatarForAI(player.def.name); // Find the AI avatar (based on name)
      else {
        enemy.transform.Find("Face").GetComponent<Image>().sprite = GD.Avatar(player.def.avatar);
        enemy.transform.Find("DeadEyeL").GetComponent<Image>().enabled = false;
      }

      if (player.def.id == GD.thePlayer.ID) {
        engine.mySelf = player; // Pick ourself as Enemy (based on ID), and initialize the stats
        GD.localPlayerIndex = player.index;
      }
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
      Lands[i].sprite = PossibleLands[ls[i]];

    // Create the cities
    if (cities != null) {
      for (int i = 0; i < cities.Length; i++)
        if (cities[i] != null)
          GameObject.Destroy(cities[i].gameObject);
    }

    cities = new City[3 * 3 * 6];
    for (int i = 0; i < 3 * 3 * 6; i++) {
      GameObject city = Instantiate(CityTemplate, CitiesHolder);
      cities[i] = city.GetComponent<City>();
      cities[i].Init(engine.cityVals[i]);
    }

    // Set the event for each city
    for (int i = 0; i < cities.Length; i++) {
      cities[i].OnClick += ClickOnCity;
      if (engine.cityVals[i].status == CityStatus.Owned) {
        cities[i].Set();
      }
    }

    // Set the initial resources based on difficulty
    // FIXME

    HideAllViews();
    DisableButtons.gameObject.SetActive(false);
  }

  private GameObject GetEnemyGameObject(Player p) {
    if (p.def.type == PlayerDef.Type.AI) {
      foreach (Transform t in SelectedEnemies) {
        Enemy e = t.GetComponent<Enemy>();
        if (e != null && e.player.def.name == p.def.name)
          return t.gameObject;
      }
      foreach (Transform t in EnemiesStorage) {
        Enemy e = t.GetComponent<Enemy>();
        if (e != null && e.player.def.name == p.def.name)
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
      if (en.player.def.type == PlayerDef.Type.AI && en.player.index != 2 && en.player.index != 4) {
        en.Say(Enemy.GetIntroMessage(), BalloonDirection.TL);
        yield return new WaitForSeconds(.5f);
      }
    }
    yield return new WaitForSeconds(1.5f);
    foreach (Transform t in Players) {
      Enemy en = t.GetComponent<Enemy>();
      // 2, 4, 5 -> TR
      if (en.player.def.type == PlayerDef.Type.AI && (en.player.index == 2 || en.player.index == 4)) {
        en.Say(Enemy.GetIntroMessage(), BalloonDirection.TR);
        yield return new WaitForSeconds(.5f);
      }
    }
    yield break;
  }

  public void ClickOnPlayer(object sender, Enemy.AlterPlayerEvent e) {
    if (e.enemy.player.def.type == PlayerDef.Type.AI) {
      e.enemy.SetMood((Mood)UnityEngine.Random.Range(0, 30), false, e.enemy.player.index < 3 ? BalloonDirection.TL : BalloonDirection.TR);
    }

    // Show some basic statistics (advanced in case Intelligence is done FIXME)
    int pop = 0;
    int num = 0;
    Player own = engine.GetPlayerByID(e.enemy.player.def.id);
    foreach (int idx in own.cities) {
      pop += engine.cityVals[idx].population;
      num++;
    }
    ShowMessage("<sprite=" + own.def.avatar + "> </b>" + own.def.name + "</b>\n\nPopulation: " + pop + "\nNumber of cities: " + num);
    // FIXME show the happiness bar
    happiness.SetValue(true, e.enemy.player.happiness);
  }

  public void ClickOnCity(object sender, System.EventArgs e) {
    City city = (City)sender;

    if (playerAction == PlayerAction.SettleNewCity) { // If possible to settle set the location to settle
      if (city.highlight == CityHighlight.Settling) {
        ShowMessage("A new city will be settled in position #" + city.vals.pos);
        gameAction.val1 = city.vals.pos;
        gameAction.action = ActionType.SettleCity;
      }
    }
    else if (playerAction == PlayerAction.Denuclearize) { // if possible to denuclearize set the location as denuclearize
      // FIXME
    }
    else if (playerAction == PlayerAction.BuildCityImprovements) { 
      // Check that the city is ours and has not yet the specified improvement
      if (city.vals.status != CityStatus.Owned || city.vals.owner != GD.localPlayerIndex) return;
      ViewImps(city.vals.pos);
      ShowMessage("Select the improvement to build on city #" + city.vals.pos);
      gameAction.val1 = city.vals.pos;
    }
    else { // Action not selected: highlight, on click base details. More info only if we have intelligence
      gameAction.val1 = city.vals.pos;
      Player own = engine.GetPlayerByIndex(city.vals.owner);
      string msg = "<b>City</b>\nOwner: <sprite=" + own.def.avatar + "> " + own.def.name + "\nPopulation: " + city.vals.population;
      if (city.vals.status == CityStatus.Radioactive) msg += "\n<i>Radioactive!</i>";
      // List city improvements
      string imps = "";
      for (int i = 0; i < city.improvements.Length; i++) {
        if (city.improvements[i]) {
          if (imps != "") imps += ", ";
          imps += GD.GetImprovementName(i);
        }
      }
      if (imps != "") imps = "\n" + imps;
      UpdateStatusMessage(msg + imps, city.city.sprite);

      // Have the owner (if AI) to say something
      if (own.def.type == PlayerDef.Type.AI) {
        if (city.vals.status == CityStatus.Destroyed) {
          if (own.refEnemy != null) own.refEnemy.Say("This city was mine!", BalloonDirection.TL);
        }
        else {
          if (own.refEnemy != null) own.refEnemy.Say("This city is mine!", BalloonDirection.TL);
        }
      }
    }
  }


  public static void UpdateStatusMessage(string text, Sprite icon) {
    instance.StatusText.text = text;
    instance.StatusImage.sprite = icon;
  }

  public static PlayerAction GetAction() {
    return instance != null ? instance.playerAction : PlayerAction.Nothing;
  }
  public static int GetSelectedCity() {
    return instance != null ? instance.gameAction.val1 : -1;
  }
  public static int GetImprovement() {
    return instance != null ? instance.gameAction.val1 : -1;
  }

  private Vector3 GetCenterSector(Player player) {
    // Find the sector with most cities
    int[] sectors = new int[6];
    foreach (int city in player.cities) {
      int sector = (city % 9) / 3 + (city > 26 ? 3 : 0);
      sectors[sector]++;
    }
    int max = 0;
    int posS = -1;
    for (int i = 0; i < 6; i++)
      if (sectors[i] > max) {
        max = sectors[i];
        posS = i;
      }
    // Find where is the biggest city. In case of top row or first column, move down/right, on all other cases place it on top-left
    int[] disp = new int[] { 0, 1, 2, 9, 10, 11, 18, 19, 20 };
    int posC = 0;
    max = 0;
    int sectorStart = 0;
    if (posS == 1) sectorStart = 3;
    else if (posS == 2) sectorStart = 6;
    else if (posS == 3) sectorStart = 28;
    else if (posS == 4) sectorStart = 31;
    else if (posS == 5) sectorStart = 34;
    for (int i = 0; i < 9; i++) {
      CityVals cv = engine.cityVals[sectorStart + disp[i]];
      if (cv.status == CityStatus.Owned && cv.owner == player.index) {
        if (max < cv.population) {
          max = cv.population;
          posC = sectorStart + disp[i];
        }
      }
    }

    // We have the biggest city, fix the displacement depending if we are on first column or first row
    bool moveRight = (posC % 3 == 0);
    bool moveDown = (posC < 9 || (posC > 26 && posC < 36));

    return cities[posC].transform.position + 250 * (Vector3.right * (moveRight ? .5f : -.25f) - Vector3.up * (moveDown ? .5f : -.25f));
  }

  private Vector3 GetCenterCity(int city) {
    bool moveRight = (city % 3 == 0);
    bool moveDown = (city < 9 || (city > 26 && city < 36));
    return cities[city].transform.position + 250 * (Vector3.right * (moveRight ? .5f : -.25f) - Vector3.up * (moveDown ? .5f : -.25f));
  }


  private void Update() {
    DDDebug.text = "plaAct = " + playerAction.ToString() + "\nwtf = " + wtf.ToString();

    if (GD.fullScreen == FullScreenMode.Windowed && (Screen.width != lastWidth || Screen.height != lastHeight)) {
      lastResize -= Time.deltaTime;
      if (lastResize < 0) {
        lastResize = 1f;
        lastWidth = Screen.width;
        lastHeight = (int)(lastWidth * 1080f / 1920f);
        Screen.SetResolution(lastWidth, lastHeight, GD.fullscreen);
      }
    }

    SkipButton.gameObject.SetActive(timeToWait > 0 && timeToWait < waitTime - 1f);
    if (timeToWait > 0) {
      timeToWait -= Time.deltaTime;
      return;
    }

    if (wtf == WhatTheFuckStatus.PlayReceivedActions) {
      DisableButtons.gameObject.SetActive(true);
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
          if (engine.players[i].def.id == msg.id) {
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
          if (pt.GetComponent<Enemy>().player.def.id == msg.id)
            pt.Find("TurnCompleted").GetComponent<Image>().color = ColorVisible;
        break;

      case GameMsgType.Error:
        ShowMessage(msg.text, true);
        break;

      case GameMsgType.GameTurn:
        turn = msg.engineValues;
        PlayEndTurn();
        break;

    }
  }

  #region Resources ************************************************************************************************************************************************************
  [Header("Resources")]

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
    if (wtf != WhatTheFuckStatus.GetLost) return;
    HideAllViews();
    Resources.SetActive(true);

    Player s = enemy == -1 ? engine.mySelf : engine.players[enemy];

    FoodQ.text = s.Resources[(int)ResourceType.Food, 0].ToString();
    FoodI.text = s.Resources[(int)ResourceType.Food, 1] != 0 ? s.Resources[(int)ResourceType.Food, 1].ToString() : "";
    FoodD.text = s.Resources[(int)ResourceType.Food, 2] != 0 ? s.Resources[(int)ResourceType.Food, 2].ToString() : "";
    IronQ.text = s.Resources[(int)ResourceType.Iron, 0].ToString();
    IronI.text = s.Resources[(int)ResourceType.Iron, 1] != 0 ? s.Resources[(int)ResourceType.Iron, 1].ToString() : "";
    IronD.text = s.Resources[(int)ResourceType.Iron, 2] != 0 ? s.Resources[(int)ResourceType.Iron, 2].ToString() : "";
    AluminumQ.text = s.Resources[(int)ResourceType.Aluminum, 0].ToString();
    AluminumI.text = s.Resources[(int)ResourceType.Aluminum, 1] != 0 ? s.Resources[(int)ResourceType.Aluminum, 1].ToString() : "";
    AluminumD.text = s.Resources[(int)ResourceType.Aluminum, 2] != 0 ? s.Resources[(int)ResourceType.Aluminum, 2].ToString() : "";
    UraniumQ.text = s.Resources[(int)ResourceType.Uranium, 0].ToString();
    UraniumI.text = s.Resources[(int)ResourceType.Uranium, 1] != 0 ? s.Resources[(int)ResourceType.Uranium, 1].ToString() : "";
    UraniumD.text = s.Resources[(int)ResourceType.Uranium, 2] != 0 ? s.Resources[(int)ResourceType.Uranium, 2].ToString() : "";
    PlutoniumQ.text = s.Resources[(int)ResourceType.Plutonium, 0].ToString();
    PlutoniumI.text = s.Resources[(int)ResourceType.Plutonium, 1] != 0 ? s.Resources[(int)ResourceType.Plutonium, 1].ToString() : "";
    PlutoniumD.text = s.Resources[(int)ResourceType.Plutonium, 2] != 0 ? s.Resources[(int)ResourceType.Plutonium, 2].ToString() : "";
    HydrogenQ.text = s.Resources[(int)ResourceType.Hydrogen, 0].ToString();
    HydrogenI.text = s.Resources[(int)ResourceType.Hydrogen, 1] != 0 ? s.Resources[(int)ResourceType.Hydrogen, 1].ToString() : "";
    HydrogenD.text = s.Resources[(int)ResourceType.Hydrogen, 2] != 0 ? s.Resources[(int)ResourceType.Hydrogen, 2].ToString() : "";
    PlasticQ.text = s.Resources[(int)ResourceType.Plastic, 0].ToString();
    PlasticI.text = s.Resources[(int)ResourceType.Plastic, 1] != 0 ? s.Resources[(int)ResourceType.Plastic, 1].ToString() : "";
    PlasticD.text = s.Resources[(int)ResourceType.Plastic, 2] != 0 ? s.Resources[(int)ResourceType.Plastic, 2].ToString() : "";
    ElectronicQ.text = s.Resources[(int)ResourceType.Electronics, 0].ToString();
    ElectronicI.text = s.Resources[(int)ResourceType.Electronics, 1] != 0 ? s.Resources[(int)ResourceType.Electronics, 1].ToString() : "";
    ElectronicD.text = s.Resources[(int)ResourceType.Electronics, 2] != 0 ? s.Resources[(int)ResourceType.Electronics, 2].ToString() : "";
    CompositeQ.text = s.Resources[(int)ResourceType.Composite, 0].ToString();
    CompositeI.text = s.Resources[(int)ResourceType.Composite, 1] != 0 ? s.Resources[(int)ResourceType.Composite, 1].ToString() : "";
    CompositeD.text = s.Resources[(int)ResourceType.Composite, 2] != 0 ? s.Resources[(int)ResourceType.Composite, 2].ToString() : "";
    FossilQ.text = s.Resources[(int)ResourceType.FossilFuels, 0].ToString();
    FossilI.text = s.Resources[(int)ResourceType.FossilFuels, 1] != 0 ? s.Resources[(int)ResourceType.FossilFuels, 1].ToString() : "";
    FossilD.text = s.Resources[(int)ResourceType.FossilFuels, 2] != 0 ? s.Resources[(int)ResourceType.FossilFuels, 2].ToString() : "";
    // FIXME show the avatar of the enemy
  }

  #endregion

  #region Technologies ************************************************************************************************************************************************************
  [Header("Technologies")]
  public GameObject TechPrefab;

  public GameObject Technologies;
  public Toggle[] TechButtons;

  public Image TechIcon;
  public TextMeshProUGUI TechTitle;
  public TextMeshProUGUI TechDesription;
  public Transform TechDepsGrid;
  public GameObject TechDepTemplate;

  /*
   FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME 

    I may want to see the current status of the techs and click the to check what they do
    I may decide to research a tech, in this case I need to select one


   FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME FIXME 
     

    If click on view buttons, the action should be nothing
    If clicking on actions buttons we set the action.

     
     */



  public void ViewTechs() {
    if (wtf != WhatTheFuckStatus.GetLost) return;
    HideAllViews();
    Technologies.SetActive(true);
    long allImprovements = 0;
    foreach (int idx in engine.mySelf.cities) {
      CityVals c = engine.cityVals[idx];
      if (c.status == CityStatus.Owned || c.status == CityStatus.Radioactive)
        allImprovements |= c.improvementsBF;
    }

    for (int i = 0; i < TechButtons.Length; i++) {
      TechButtons[i].SetIsOnWithoutNotify(false);
      if (engine.mySelf.techs[i]) {
        TechButtons[i].GetComponent<Image>().color = AvailableTechColor;
      }
      else {
        // Are prerequisites met? (techs and imprs)
        if (IsItemValid(GD.instance.Technologies[i], allImprovements))
          TechButtons[i].GetComponent<Image>().color = PossibleTechColor;
        else
          TechButtons[i].GetComponent<Image>().color = NotgoodTechColor;
      }
    }

    // Cleanup
    TechIcon.sprite = null;
    TechTitle.text = "";
    TechDesription.text = "";
    foreach (Transform t in TechDepsGrid.transform)
      GameObject.Destroy(t.gameObject);
  }

  public void ClickOnTech(int techIndex) {
    if (wtf != WhatTheFuckStatus.GetLost) return;
    // Start by unchecking all buttons
    for (int i = 0; i < TechButtons.Length; i++)
      TechButtons[i].SetIsOnWithoutNotify(false);
    // Are we deciding which tech to develop?
    // FIXME??? How we can understand if we are in viewing or building a tech? Let's use a specific variable and avoid the status
    if (playerAction == PlayerAction.DevelopTech) {
      // Yes, Can we develop this?
      if (TechButtons[techIndex].GetComponent<Image>().color == PossibleTechColor) {
        //   Yes, set it as action
        TechButtons[techIndex].SetIsOnWithoutNotify(true);
        gameAction.action = ActionType.ResearchTechnology;
        gameAction.val1 = (byte)techIndex;
        ShowMessage("You will research <b>" + GD.GetTechName(techIndex) + "</b>");
      }
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
      GameObject dependency = Instantiate(TechDepTemplate, TechDepsGrid);
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
  [Header("Improvements")]
  public GameObject BuildPrefab;

  public GameObject Improvements;
  public Toggle[] ImpButtons;
  public Image ImpIcon;
  public TextMeshProUGUI ImpTitle;
  public TextMeshProUGUI ImpDesription;
  public Transform ImpDepsGrid;
  public GameObject ImpDepTemplate;

  public void ViewImps(int cityIndex) {
    if (wtf != WhatTheFuckStatus.GetLost) return;
    HideAllViews();
    Improvements.SetActive(true);
    CityVals city = engine.cityVals[cityIndex];
    for (int i = 0; i < ImpButtons.Length; i++) {
      ImpButtons[i].SetIsOnWithoutNotify(false);
      // If the improvement on the city is already done we just make it green and not selectable
      if ((city.improvementsBF & (1 << i)) != 0)
        ImpButtons[i].GetComponent<Image>().color = AvailableTechColor;
      else {
        // Can we have it?
        // Are prerequisites met? (techs and imprs)
        if (IsItemValid(GD.instance.Improvements[i], city.improvementsBF))
          ImpButtons[i].GetComponent<Image>().color = PossibleTechColor;
        else
          ImpButtons[i].GetComponent<Image>().color = NotgoodTechColor;
      }
    }

    // Cleanup
    ImpIcon.sprite = null;
    ImpTitle.text = "";
    ImpDesription.text = "";
    foreach (Transform t in ImpDepsGrid.transform)
      GameObject.Destroy(t.gameObject);
  }

  public void ClickOnImp(int impIndex) {
    if (wtf != WhatTheFuckStatus.GetLost) return;
    // Start by unchecking all buttons
    for (int i = 0; i < ImpButtons.Length; i++)
      ImpButtons[i].SetIsOnWithoutNotify(false);
    // Can we build this?
    if (ImpButtons[impIndex].GetComponent<Image>().color == PossibleTechColor) {
      ImpButtons[impIndex].SetIsOnWithoutNotify(true);
      if (playerAction == PlayerAction.BuildCityImprovements) {
        gameAction.val1 = (byte)impIndex;
        ShowMessage("Improvement <b>" + GD.GetImprovementName(gameAction.val2) + "</b> will be built on city #" + gameAction.val1);
      }
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


  #region Social ************************************************************************************************************************************************************
  [Header("Improvements")]
  public GameObject SocialPrefab;

  private void ExecuteSocialActivities(Player player) {
    List<CityVals> cits = new List<CityVals>();
    foreach (int idx in player.cities)
      cits.Add(engine.cityVals[idx]);
    cits.Sort((a, b) => a.population - b.population);
    for (int i = 0; i < cits.Count; i++) {
      int src = cits[i].pos;
      int dst = cits[cits.Count - i - 1].pos;
      Person person;
      if (src == dst) {
        person = Instantiate(PersonPrefab, PersonPrefab.transform.parent).GetComponent<Person>();
        person.WalkAround(cities[src].transform.position, player.color);
      }
      else {
        person = Instantiate(PersonPrefab, PersonPrefab.transform.parent).GetComponent<Person>();
        person.WalkTo(
          cities[src].transform.position,
          cities[dst].transform.position,
          player.color, player.color,
          cities[src].vals.population / 2, dst,
          CompleteSocialWalk
          );
        cities[src].AddPopulation(-cities[src].vals.population / 2);
      }
    }
    GameObject visualBuild = Instantiate(SocialPrefab, CityTemplate.transform.parent);
    visualBuild.transform.position = GetCenterSector(player);
  }

  public void CompleteSocialWalk(int pop, int city) {
    cities[city].AddPopulation(pop);
  }


  #endregion Social

  #region Items support functions *************************************************************************************************************************************************

  private bool IsItemValid(Item item, long improvements) {
    foreach (Dependency it in item.prerequisites) {
      if (it.type == ProductionType.Technology && !engine.mySelf.techs[(int)it.index - (int)it.type]) {
        return false;
      }
      else if (it.type == ProductionType.Improvement && (improvements & (1 << ((int)it.index - (int)it.type))) == 0) {
        return false;
      }
    }
    return true;
  }

  private bool IsItemAvailable(Item item) {
    if (item.type == ProductionType.Technology) return engine.mySelf.techs[(int)item.index];
    if (item.type == ProductionType.Improvement) {
      foreach (int idx in engine.mySelf.cities) {
        CityVals c = engine.cityVals[idx];
        if (c.status == CityStatus.Owned || c.status == CityStatus.Radioactive) {
          if ((c.improvementsBF & (1 << (int)item.index)) != 0) return true;
        }
      }
    }
    return false; // FIXME check the resources or other stuff
  }

  #endregion Items support functions

  #region Actions *********************************************************************************************************************************************************

  public void FindResources() {
    gameAction.action = ActionType.FindResources;
    playerAction = PlayerAction.Nothing;
    ShowMessage("You will find resources.");
  }

  public void ResearchTechnologies() {
    // Show the tech view and wait for a tech to be selected
    gameAction.val1 = 255;
    gameAction.val2 = 255;
    playerAction = PlayerAction.DevelopTech;
    ViewTechs();
  }

  public void BuildImprovements() {
    HideAllViews();
    // Show the improvement view and wait for a tech to be selected
    gameAction.action = ActionType.BuildImprovement;
    gameAction.val1 = 255;
    gameAction.val2 = 255;
    playerAction = PlayerAction.BuildCityImprovements;
    // Show only our cities
    for (int i = 0; i < cities.Length; i++) {
      cities[i].Highlight(CityHighlight.Owned);
    }
    ShowMessage("Select one of your cities to build the improvement...");
  }

  public void SocialActivities() {
    gameAction.action = ActionType.Social;
    playerAction = PlayerAction.Nothing;
    ShowMessage("You will promote social activities to increase population happiness.");
  }

  public void SettleNewCity() {
    // Food should be at least 25
    if (engine.mySelf.Resources[(int)ResourceType.Food, 0] < 25) {
      ShowMessage("Not enough food to settle a new city!", true);
      return;
    }

    for (int i = 0; i < engine.cityVals.Length; i++) {
      // Are we empty and do we have a city of ours (pop>1) around?
      CityVals c = engine.cityVals[i];
      if (c.status == CityStatus.Owned || c.status == CityStatus.Radioactive) continue; // Not empty
      int x = i % 9;
      int y = i / 9;
      if (x > 0 && engine.cityVals[i - 1].owner == engine.mySelf.index) cities[i].Highlight(CityHighlight.Settling);
      else if (x < 8 && engine.cityVals[i + 1].owner == engine.mySelf.index) cities[i].Highlight(CityHighlight.Settling);
      else if (y > 0 && engine.cityVals[i - 9].owner == engine.mySelf.index) cities[i].Highlight(CityHighlight.Settling);
      else if (y < 5 && engine.cityVals[i + 9].owner == engine.mySelf.index) cities[i].Highlight(CityHighlight.Settling);
      else if (x > 0 && y > 0 && engine.cityVals[i - 1 - 9].owner == engine.mySelf.index) cities[i].Highlight(CityHighlight.Settling);
      else if (x > 0 && y < 5 && engine.cityVals[i - 1 + 9].owner == engine.mySelf.index) cities[i].Highlight(CityHighlight.Settling);
      else if (x < 8 && y > 0 && engine.cityVals[i + 1 - 9].owner == engine.mySelf.index) cities[i].Highlight(CityHighlight.Settling);
      else if (x < 8 && y < 5 && engine.cityVals[i + 1 + 9].owner == engine.mySelf.index) cities[i].Highlight(CityHighlight.Settling);
    }
    ShowMessage("Select where to settle a new city...");
  }


  public void EndTurn() {
    if (gameAction.action == ActionType.Nothing) {
      ShowMessage("<color=red>You did not select an action for the turn!</color>", true);
      return;
    }
    else if (gameAction.action == ActionType.ResearchTechnology && gameAction.val1 == 255) {
      ShowMessage("<color=red>You did not select the technology to research!</color>", true);
      return;
    }
    else if (gameAction.action == ActionType.BuildImprovement) {
      if (gameAction.val2 == 255) {
        ShowMessage("<color=red>You did not select the improvement to build!</color>", true);
        return;
      }
      if (gameAction.val1 == 255) {
        ShowMessage("<color=red>You did not select the city where to build the improvement!</color>", true);
        return;
      }
    }

    engine.EndTurn(game.multiplayer, GD.thePlayer.ID, gameAction);
    // Disable endturn, and hide all views
    EndTurnButton.enabled = false;
    HideAllViews();
    playerAction = PlayerAction.Nothing;
    wtf = WhatTheFuckStatus.GetLost;
    // Show the short version of the selected action
    ShowMessage("Action:\n" + gameAction.Display());
  }

  #endregion Actions

  #region Global commands *************************************************************************************************************************************************

  public void StartGameChat() {
    if (!game.multiplayer) return;
    List<ChatParticipant> participants = new List<ChatParticipant>();
    for(int i = 0; i < engine.nump; i++) {
      Player en = engine.players[i];
      if (en.def.type != PlayerDef.Type.AI)
        participants.Add(new ChatParticipant(en.def.name, en.def.avatar, en.def.id));
    }
    chat.StartChatWith(game.Name, participants);
  }

  public void LeaveGame() {
    MessageBox.SetActive(true);
  }

  public void YesLeave() {
    MessageBox.SetActive(false);
    engine.LeaveGame(game.id, game.multiplayer);
    gameObject.SetActive(false);
    foreach (Transform t in Players) {
      Enemy en = t.GetComponent<Enemy>();
      if (en != null)
        en.Destroy();
    }
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
    for (int i = 0; i < cities.Length; i++)
      cities[i].Highlight(CityHighlight.None);
    happiness.SetValue(false, 0);
  }

  private void ShowMessage(string msg, bool warning = false) {
    StatusText.text = (warning ? "<color=red>" : "") + msg + (warning ? "</color>" : "");
    StatusImage.enabled = false;
    GD.DebugLog(msg, GD.LT.Debug);
  }

  public void Skip() {
    timeToWait = 0;
    SkipButton.gameObject.SetActive(false);
  }

  private Color AvailableTechColor = new Color32(20, 255, 20, 255);
  private Color PossibleTechColor = new Color32(87, 142, 209, 255);
  private Color NotgoodTechColor = new Color32(255, 20, 20, 255);
  #endregion Global commands

  #region Main visual actions *************************************************************************************************************************************************

  private void PlayEndTurn() {
    // We call this function only if we receive a message from the engine telling that the turn is ended.
    // We just play all the actions that are happening, until completed. Then we go back to the normal user interaction status
    HideAllViews();
    ShowMessage("Results of the turn...");
    GD.DebugLog("Processing next turn", GD.LT.Debug);

    // First reset the messages and the completed and endturn
    foreach (Transform t in Players) {
      Enemy en = t.GetComponent<Enemy>();
      if (en == null) continue;
      Transform completed = t.Find("TurnCompleted");
      if (completed != null) completed.GetComponent<Image>().color = ColorInvisible;
      en.HideBalloon();
    }

    // Next city increase/decrease
    for (int i = 0; i < turn.cities.Length; i++) {
      if (turn.cities[i] == null) continue;
      CityVals cv = turn.cities[i];
      if (cv.pos == 255 || (cv.status != CityStatus.Owned && cv.status != CityStatus.Radioactive)) continue;
      cities[i].SetValues(cv);
    }


    // FIXME at this point we cannot do everything here. We may need to use the Update() and play all actions
    wtf = WhatTheFuckStatus.PlayReceivedActions;

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
      wtf = WhatTheFuckStatus.GetLost;
      DisableButtons.gameObject.SetActive(false);
      EndTurnButton.enabled = true;
      HideAllViews();
      engine.mySelf.action = ActionType.Nothing;
      GD.DebugLog("Next turn completed", GD.LT.Debug);
      Highlighter.SetActive(false);
      return;
    }

    // Set the resources for the player
    Player player = engine.players[index];
    // FIXME engine.UpdateResources(player); <-- we may need to get the new resources from the net
    Highlighter.SetActive(true);
    Highlighter.transform.localPosition = new Vector3(206 * index, 0, 0);

    // Show something depending on the action, we may have the result of the action inside the turn FIXME it is not yet available
    switch (player.action) {
      case ActionType.Nothing:
        ShowMessage(player.def.name + " does nothing");
        break;

      case ActionType.FindResources:
        // We cannot really see the extra resources
        ShowMessage(player.def.name + " is finding resources.\nFound extra FIXME"); // Show stuff for other players only if we have counterintelligence
        break;

      case ActionType.ResearchTechnology:
        // FIXME show details only if we have intelligence
        ShowMessage(player.def.name + " researched technology " + GD.GetTechName(player.val1));
        engine.players[index].techs[player.val1] = true;
        GameObject visualTech = Instantiate(TechPrefab, CityTemplate.transform.parent);
        visualTech.transform.position = GetCenterSector(player);
        break;

      case ActionType.BuildImprovement:
        // FIXME show details only if we have intelligence
        // Do we still have this city?
        if (!player.cities.Contains(player.val1))
          ShowMessage(player.def.name + " tried to build " + GD.GetImprovementName(player.val2) + " on city #" + player.val1 + " but the city does not belong anymore to " + player.def.name);
        else if (engine.cityVals[player.val1].status == CityStatus.Destroyed)
          ShowMessage(player.def.name + " tried to build " + GD.GetImprovementName(player.val2) + " on city #" + player.val1 + " but the city is destroyed.");
        else {
          ShowMessage(player.def.name + " is building " + GD.GetImprovementName(player.val2) + " on city #" + player.val1);
          engine.cityVals[player.val1].SetImprovement(player.val2);
        }
        GameObject visualBuild = Instantiate(BuildPrefab, CityTemplate.transform.parent);
        visualBuild.transform.position = GetCenterCity(player.val1);
        break;


      case ActionType.Social:
        ShowMessage(player.def.name + " is promoting social activities");
        // List all cities, sorted by population size. Have a person go from the first to the last. When completed exchange population.
        ExecuteSocialActivities(player);
        break;

      default:
        ShowMessage(player.def.name + " unhandled action " + player.action.ToString(), true);
        break;
    }
    timeToWait = waitTime;
    turn.order[index] = 255;
  }

  #endregion Main visual actions

}

