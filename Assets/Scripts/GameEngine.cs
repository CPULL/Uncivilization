using System;
using System.Collections.Generic;
using UnityEngine;

public class GameEngine {
  // Used to run a game, on single it will handle the current game, on server mode we should line it to the game itself and handle it server side

  public GameEngine(PlayerHandler player) {
    if (player != null) player.OnGame += OnMsgReceived;
    messages = new List<NetworkManager.GameMessage>();
  }

  internal int nump = 0;
  internal bool[] completed = new bool[6];
  GameValues currentVals;
  GameValues nextVals;


  internal Player[] players = new Player[6];
  public CityVals[] cityVals = new CityVals[3 * 3 * 6];
  public List<NetworkManager.GameMessage> messages;
  public Player mySelf;
  public Game game;

  public void InitEnemiesSingleplayer(Game theGame, Transform selectedEnemies) { // This is for singleplayer
    game = theGame;
    GD.InitRandomGenerator(theGame.rndSeed);
    currentVals = new GameValues(theGame.Name);

    nump = 0;
    Enemy enemy;
    foreach (Transform p in selectedEnemies) {
      enemy = p.GetComponent<Enemy>();
      enemy.player.def.id = GD.GetIDForAI(enemy.player.def.name);
      enemy.player.def.avatar = GD.GetAvatarForAI(enemy.player.def.name);
      players[nump] = new Player(enemy);
      game.AddPlayer(players[nump].def);
      nump++;
    }
    enemy = GD.instance.PlayersBase[0].GetComponent<Enemy>();
    enemy.player = new Player(GD.thePlayer.player.def);
    players[nump] = new Player(enemy);
    game.AddPlayer(players[nump].def);
    nump++;
    enemy.SetEnemyAsPlayer(GD.thePlayer.Name, GD.thePlayer.Avatar, GD.thePlayer.ID);

    // Randomize the positions
    for (int i = 0; i < 1000; i++) {
      int a = GD.GetRandom(0, nump);
      int b = GD.GetRandom(0, nump);
      Player tmpe = players[a];
      players[a] = players[b];
      players[b] = tmpe;
      PlayerDef tmpsp = game.Players[a];
      game.Players[a] = game.Players[b];
      game.Players[b] = tmpsp;
    }

    SharedInit();
  }

  public void InitEnemiesMultiplayer(Game theGame) { // This is for multiplayer
    game = theGame;
    GD.InitRandomGenerator(theGame.rndSeed);

    nump = 0;
    for (int i = 0; i < game.nump; i++) {
      players[nump] = new Player(game.Players[i]);
      nump++;
    }

    SharedInit();
  }

  private void SharedInit() {
    for (byte i = 0; i < nump; i++) {
      if (players[i] == null) {
        Debug.LogError("Empty player found! SharedInit"); // FIXME
      }
    }

    byte[] sectors = new byte[6];
    for (byte i = 0; i < 6; i++) sectors[i] = i;
    for (int i = 0; i < 1000; i++) {
      int a = GD.GetRandom(0, 6);
      int b = GD.GetRandom(0, 6);
      byte tmp = sectors[a];
      sectors[a] = sectors[b];
      sectors[b] = tmp;
    }

    // Assign the colors
    for (byte i = 0; i < nump; i++) {
      players[i].index = sectors[i];
      players[i].color = GetPlayerColor(sectors[i]);
    }

    // Init the cities
    cityVals = new CityVals[9 * 6];
    for (byte i = 0; i < cityVals.Length; i++)
      cityVals[i] = new CityVals(i);

    // Assign a single city (population proportional to the difficulty) to each player
    for (int i = 0; i < nump; i++) {
      Player ps = players[i];
      int pos = 1 + 9 + (sectors[i] < 3 ? sectors[i] * 3 : sectors[i] * 3 + 9 * 2);
      if (ps.def.type == PlayerDef.Type.AI)
        cityVals[pos].Set(5 + 5 * GD.difficulty, ps.index);
      else
        cityVals[pos].Set(10 - GD.difficulty, ps.index);
      ps.cities.Add((byte)pos);
    }

    // FIXME add some extra cities to debug social activities
    for (int i = 0; i < 15; i++) {
      int a = UnityEngine.Random.Range(0, 9 * 6);
      if (cityVals[a].status == CityStatus.Empty)
        cityVals[a].Set(UnityEngine.Random.Range(1, 50), players[i % 2].index);
      players[i % 2].cities.Add((byte)a);
    }

    for (int i = 0; i < nump; i++)
      completed[i] = false;
  }

  private Color32 GetPlayerColor(int pos) {
    switch (pos) {
      case 0: return new Color32(80, 80, 80, 255);
      case 1: return new Color32(126, 145, 235, 255);
      case 2: return new Color32(17, 241, 23, 255);
      case 3: return new Color32(242, 231, 19, 255);
      case 4: return new Color32(242, 18, 54, 255);
      case 5: return new Color32(110, 18, 241, 255);
    }
    return Color.black;
  }
  public void LeaveGame(ulong gameid, bool multiplayer) {
    if (multiplayer) {
      GD.instance.networkManager.LeaveGameClient(GD.thePlayer, gameid);
    }
  }

  public void EndTurn(bool multiplayer, ulong playerID, GameAction gameAction) {
    if (GD.IsStatus(LoaderStatus.Server, LoaderStatus.ServerBackground)) { // We are on the server
      // This call comes from the client listner
      // Find the player in the player list
      for (int i = 0; i < nump; i++)
        if (players[i].def.id == playerID) {
          completed[i] = true;
          players[i].action = gameAction.action;
          players[i].val1 = gameAction.val1;
          players[i].val2 = gameAction.val2;
          break;
        }

      // Auto-complete AI and defeated
      for (int i = 0; i < nump; i++)
        if (players[i].def.type == PlayerDef.Type.AI)
          completed[i] = true;
        else if (players[i].def.type == PlayerDef.Type.Defeated) {
          completed[i] = true;
          players[i].action = ActionType.Nothing;
        }

      // All human players completed?
      bool allCompleted = true;
      for (int i = 0; i < nump; i++) {
        if (players[i].def.type != PlayerDef.Type.Defeated && players[i].def.type != PlayerDef.Type.AI && !completed[i]) {
          allCompleted = false;
          break;
        }
      }
      if (allCompleted) {
        GameEngineValues values = CalculateNextTurn();
        // Send the result to all the clients
        GD.instance.networkManager.SendGameTurn(players, values);
        // And reset the completed
        for (int i = 0; i < nump; i++)
          completed[i] = false;
      }
    }
    else if (multiplayer) { // Multiplayer mode and we are the client
      // This call comes from the Renderer, Send the message to the server
      GD.instance.networkManager.SendGameAction(GD.thePlayer, gameAction);
    }
    else { // Single player mode
      // This call comes from the Renderer, so we can process it right now: CalculateNextTurn
      for (int i = 0; i < nump; i++)
        if (players[i].def.id == playerID) {
          completed[i] = true;
          players[i].action = gameAction.action;
          players[i].val1 = gameAction.val1;
          players[i].val2 = gameAction.val2;
          break;
        }
      GameEngineValues values = CalculateNextTurn();
      GD.thePlayer.OnGame?.Invoke(this, new NetworkManager.GameMessage { type = GameMsgType.GameTurn, engineValues = values });
    }
  }

  public GameEngineValues CalculateNextTurn() {
    GameEngineValues values = new GameEngineValues(this);
    foreach (Player p in players) {
      if (p == null || p.def.type == PlayerDef.Type.Defeated) continue;
      CalculateResources(p, values);
      CalculatePopulation(p, values);
    }
    CalculateOrder(values);
    for (int i = 0; i < nump; i++) {
      if (values.order[i] == 255) continue;
      Player p = players[values.order[i]];
      if (p == null || p.def.type == PlayerDef.Type.Defeated) continue;
      CalculateActions(p, values); // Actions are computed after order is defined
    }

    return values;
  }


  private void OnMsgReceived(object sender, NetworkManager.GameMessage e) {
    // Add it to the list of messages to be processed by the GameRederer
    messages.Add(e);
  }


  public void CalculateResources(Player p, GameEngineValues values) {
    // Find the place for the current player
    int pindex = -1;
    for (int i = 0; i < players.Length; i++) {
      if (players[i].def.id == p.def.id) {
        pindex = i;
        break;
      }
    }
    if (pindex==-1) {
      GD.DebugLog("CalculateResources: Cannot find the specified player id=" + p, GD.LT.Debug);
      return;
    }
    // Food ************************************************************************************************************************************************************
    // Food +1 per city, +10 per farm, city_production*2 per hydrofarm
    short produced = (short)p.cities.Count;
    foreach (int cityIndex in p.cities) {
      if (cityVals[cityIndex].HasImprovement((int)ItemType.IFARM - (int)ProductionType.Improvement)) produced += 10;
      if (cityVals[cityIndex].HasImprovement((int)ItemType.IHFRM - (int)ProductionType.Improvement)) produced += 15;
    }
    players[pindex].Resources[(int)ResourceType.Food, 1] = produced;
    // Consume the food, 1 unit for each 10 millions population, but use floor to allow basic production
    int population = 0;
    foreach (int cityIndex in p.cities) {
      population += cityVals[cityIndex].population;
    }
    // FIXME here we want to set the vals to a new spot
    players[pindex].Resources[(int)ResourceType.Food, 2] = (short)(population / 10);
    players[pindex].Resources[(int)ResourceType.Food, 0] = (short)(players[pindex].Resources[(int)ResourceType.Food, 0] + players[pindex].Resources[(int)ResourceType.Food, 1] - players[pindex].Resources[(int)ResourceType.Food, 2]);
    if (players[pindex].Resources[(int)ResourceType.Food, 0] < 0) players[pindex].Resources[(int)ResourceType.Food, 0] = 0; // FIXME add some problem for the enemy, do a message to the player to show there is not enough food


    // Iron ************************************************************************************************************************************************************

    // Aluminum ************************************************************************************************************************************************************
    // Uranium ************************************************************************************************************************************************************
    // Plutonium ************************************************************************************************************************************************************
    // Hydrogen ************************************************************************************************************************************************************
    // Plastic ************************************************************************************************************************************************************
    // Electronic ************************************************************************************************************************************************************
    // Composite ************************************************************************************************************************************************************
    // FossilFuel ************************************************************************************************************************************************************

    // FIXME
  }

  internal Player GetPlayerByIndex(byte index) {
    for (int i = 0; i < nump; i++)
      if (players[i].index == index) {
        return players[i];
      }
    return null;
  }

  internal Player GetPlayerByID(ulong id) {
    for (int i = 0; i < nump; i++)
      if (players[i].def.id == id) {
        return players[i];
      }
    return null;
  }

  public void CalculatePopulation(Player p, GameEngineValues values) {
    // Find the place for the current player
    int pindex = -1;
    for (int i = 0; i < players.Length; i++) {
      if (players[i].def.id == p.def.id) {
        pindex = i;
        break;
      }
    }
    if (pindex == -1) {
      GD.DebugLog("CalculateResources: Cannot find the specified player id=" + p, GD.LT.Debug);
      return;
    }
    // FIXME
    // Increase the population in case there is enough food, in case the food is zero and the consumption is high, let people starve
    // The increase should be about *1.05 with at least 1 of increase

    if (players[pindex].Resources[(int)ResourceType.Food, 0] > 0 || players[pindex].Resources[(int)ResourceType.Food, 1] == players[pindex].Resources[(int)ResourceType.Food, 2]) {
      foreach (int cityIndex in p.cities) {
        CityVals src = cityVals[cityIndex];
        CityVals dst = values.cities[cityIndex];
        if (dst == null) {
          dst = new CityVals(src);
          values.cities[cityIndex] = dst;
        }

        int increase = (int)(src.population * 0.05f);
        if (increase == 0) increase = 1;
        if (src.status == CityStatus.Radioactive) {
          increase = -1;
        }
        else if (src.population + increase > 25 && !src.HasImprovement((int)ItemType.IHOUS - (int)ProductionType.Improvement)) {
          increase = 25 - src.population;
          if (increase < 0) increase = 0;
        }
        if (src.population != 0 && increase != 0)
          dst.population += increase;
        if (dst.population <= 0) {
          dst.population = 0;
          dst.status = src.status == CityStatus.Radioactive ? CityStatus.RadioWaste : CityStatus.Destroyed;
        }
      }
    }
  }

  private void CalculateOrder(GameEngineValues values) {
    // The order is a byte[6] with the index of each player in order of action
    byte[] order = new byte[6];
    for (int i = 0; i < 6; i++) {
      order[i] = 255;
      values.order[i] = 255;
    }

    // Pick all players that are defending or doing social, randomize them and place them at the begin
    // Pick all other players, randomize, and place after the first ones
    List<int> list = new List<int>();
    for (int i = 0; i < nump; i++) {
      if (players[i].def.type == PlayerDef.Type.Defeated) continue;
      if (players[i].action == ActionType.Defend || players[i].action == ActionType.Diplomacy || players[i].action == ActionType.Social)
        list.Add(i);
    }
    int pos = list.Count;
    if (pos > 0) {
      for (int i = 0; i < 100; i++) {
        int a = GD.GetRandom(0, pos);
        int b = GD.GetRandom(0, pos);
        int tmp = list[a];
        list[a] = list[b];
        list[b] = tmp;
      }
    }
    for (int i = 0; i < pos; i++)
      order[i] =(byte) list[i];

    list.Clear();
    for (int i = 0; i < nump; i++) {
      if (players[i].def.type == PlayerDef.Type.Defeated) continue;
      if (players[i].action != ActionType.Defend && players[i].action != ActionType.Diplomacy && players[i].action != ActionType.Social)
        list.Add(i);
    }
    if (list.Count > 0) {
      for (int i = 0; i < 100; i++) {
        int a = GD.GetRandom(0, list.Count);
        int b = GD.GetRandom(0, list.Count);
        int tmp = list[a];
        list[a] = list[b];
        list[b] = tmp;
      }
    }
    for (int i = 0; i < list.Count; i++)
      order[i + pos] = (byte)list[i];

    // Now use the oder array to define the order of the players
    byte min;
    int index = 0;
    pos = 0;
    while (index != -1) {
      index = -1;
      min = 255;
      for (int i = 0; i < 6; i++) {
        if (min > order[i]) {
          min = order[i];
          index = i;
        }
      }
      if (index != -1) {
        values.order[pos] = (byte)index;
        order[index] = 255;
        pos++;
      }
    }
  }

  private void CalculateActions(Player p, GameEngineValues values) {
    // Depending on the action we may need to do something
    switch (p.action) {
      case ActionType.Nothing: break;
      case ActionType.FindResources: // Double the production of resources and add some extra random resource based on the population size
        ExecuteFindResources(p, values);
        break;
      case ActionType.ResearchTechnology:
        ExecuteResearchTechnology(p);
        break;
      case ActionType.BuildImprovement:
        ExecuteBuildImprovement(p, values);
        break;
      case ActionType.Social:
        ExecuteSocialActivities(p);
        break;
      case ActionType.BuildWeapons: break;
      case ActionType.DeployWeapons: break;
      case ActionType.UseWeapons: break;
      case ActionType.Propaganda: break;
      case ActionType.Diplomacy: break;
      case ActionType.SettleCity: break;
      case ActionType.Denucrlearize: break;
      case ActionType.Defend: break;
    }
  }

  private void ExecuteFindResources(Player p, GameEngineValues values) {
    p.Resources[(int)ResourceType.Food, 1] *= 2;
    p.Resources[(int)ResourceType.Iron, 1] *= 2;
    p.Resources[(int)ResourceType.Aluminum, 1] *= 2;
    p.Resources[(int)ResourceType.Uranium, 1] *= 2;
    p.Resources[(int)ResourceType.Plutonium, 1] *= 2;
    p.Resources[(int)ResourceType.Hydrogen, 1] *= 2;
    p.Resources[(int)ResourceType.Plastic, 1] *= 2;
    p.Resources[(int)ResourceType.Electronics, 1] *= 2;
    p.Resources[(int)ResourceType.Composite, 1] *= 2;
    p.Resources[(int)ResourceType.FossilFuels, 1] *= 2;

    int population = 0;
    foreach (int cityIndex in p.cities) {
      population += cityVals[cityIndex].population;
    }

    p.Resources[(int)ResourceType.Food, 1] += (short)GD.GetRandom(0, population);
    p.Resources[(int)ResourceType.Iron, 1] += (short)GD.GetRandom(0, 2 * population / 3);
    p.Resources[(int)ResourceType.Aluminum, 1] += (short)GD.GetRandom(0, population / 3);
    p.Resources[(int)ResourceType.Uranium, 1] += (short)GD.GetRandom(0, population / 4);
    p.Resources[(int)ResourceType.Plutonium, 1] += (short)GD.GetRandom(0, population / 8);
    p.Resources[(int)ResourceType.Hydrogen, 1] += (short)GD.GetRandom(0, population / 10);
    p.Resources[(int)ResourceType.Plastic, 1] += (short)GD.GetRandom(0, population / 5);
    p.Resources[(int)ResourceType.Electronics, 1] += (short)GD.GetRandom(0, population / 7);
    p.Resources[(int)ResourceType.Composite, 1] += (short)GD.GetRandom(0, population / 20);
    p.Resources[(int)ResourceType.FossilFuels, 1] += (short)GD.GetRandom(0, population / 9);
  }


  private void ExecuteResearchTechnology(Player p) {
    p.techs[p.val1] = true;
  }

  private void ExecuteBuildImprovement(Player p, GameEngineValues values) {
    long imp = cityVals[p.val1].improvementsBF;
    imp |= values.cities[p.val1].improvementsBF;
    imp = (imp) | ((long)(1 << p.val2));
    values.cities[p.val1].improvementsBF = imp;
  }

  private void ExecuteSocialActivities(Player p) {
    // Alter only the happiness here
    p.happiness += 5;
    if (p.happiness > 100) p.happiness = 100;
  }

  private void ExecuteSettleCity(Player p, GameEngineValues values) {
    // Pick the closest city with max population, halve the population of the src city and send people to the target city
    // Do we need to do it here? I suppose the action will be the same on all clients. The problem is that we need to run the actions if we are server side. Or we will not be able to process the AI
  }


  public void Destroy() {
    players = null;
    completed = null;
    cityVals = null;
    messages.Clear();
    mySelf = null;
    game = null;
  }

}



// *************************************** GameEngineValues *********************************************************************************************************************************
// Used to keep the status of the game




public class GameEngineValues {
  public ulong gameid;
  public byte[] order; // Players execution order
  public GameAction[] actions; // Players actions
  public readonly CityVals[] cities;


  public GameEngineValues(GameEngine engine) {
    gameid = engine.game.id;
    order = new byte[6];
    actions = new GameAction[6];
    cities = new CityVals[9 * 6];
    for (int i = 0; i < 9 * 6; i++)
      if (engine.cityVals[i].status != CityStatus.Empty)
        cities[i] = new CityVals(engine.cityVals[i]);
  }

  public byte[] Serialize() {
    int len = 8 + // Game ID
              6 + // Order for actions
              6 * 3 + // Game actions
              9 * 6 * 14; // Status of each city
    byte[] res = new byte[len];
    byte[] d = System.BitConverter.GetBytes(gameid);
    int pos = 0;
    for (int i = 0; i < 8; i++)
      res[pos++] = d[i];
    for (int i = 0; i < 6; i++)
      res[pos++] = order[i];
    for (int i = 0; i < 6; i++) {
      d = actions[i].Serialize();
      for (int j = 0; j < 3; j++) 
        res[pos++] = d[i];
    }

    for (int i = 0; i < cities.Length; i++) {
      if (cities[i] == null) {
        for (int j = 0; j < 14; j++) res[pos++] = 255;
      }
      else {
        d = cities[i].Serialize();
        for (int j = 0; j < d.Length; j++)
          res[pos++] = d[j];
      }
    }

    return res;
  }


  public GameEngineValues(byte[] data) {
    gameid = System.BitConverter.ToUInt64(data, 0);
    int pos = 8;
    order = new byte[6];
    for (int i = 0; i < 6; i++)
      order[i] = data[pos++];
    for (int i = 0; i < 6; i++) {
      actions[i].action = (ActionType)data[pos++];
      actions[i].val1 = data[pos++];
      actions[i].val2 = data[pos++];
    }

    cities = new CityVals[9 * 6];
    for (int i = 0; i < 9 * 6; i++) {
      CityVals cv = new CityVals(data, pos);
      if (cv.pos != 255) 
        cities[i] = cv;
      pos += 14;
    }
  }
}

public class CityVals {
  public byte pos;
  public long improvementsBF;
  public int population;
  public CityStatus status;
  public byte owner;

  public CityVals(CityVals src) {
    pos = src.pos;
    population = src.population;
    status = src.status;
    owner = src.owner;
    improvementsBF = src.improvementsBF;
  }

  public CityVals(byte position) {
    pos = position;
    improvementsBF = 0;
    population = 0;
    status = CityStatus.Empty;
    owner = 255;
  }

  public void Set(int pop, byte index) {
    population = pop;
    owner = index;
    status = pop > 0 ? CityStatus.Owned : CityStatus.Empty;
  }

  public byte[] Serialize() {
    /*
      1 // pos
      8 // bitfield improvements
      4 // population
      1 // owner + status
    */

    byte[] res = new byte[14];
    res[0] = (byte)pos;

    byte[] d = System.BitConverter.GetBytes(improvementsBF);
    for (int i = 0; i < 8; i++)
      res[1 + i] = d[i];

    d = System.BitConverter.GetBytes(population);
    for (int i = 0; i < 4; i++)
      res[9 + i] = d[i];

    byte stat = (byte)(owner + 8 * (byte)status);
    res[9 + 4] = stat;

    return res;
  }

  public CityVals(byte[] data, int start) {
    pos = data[start];
    improvementsBF = System.BitConverter.ToInt64(data, start + 1);
    population = System.BitConverter.ToInt32(data, start + 9);
    owner = (byte)(data[start + 9 + 4] & 3);
    status = (CityStatus)((data[start + 9 + 4] & 248) >> 3);
  }

  public bool HasImprovement(int imp) {
    return (improvementsBF & (1 << imp)) != 0;
  }

  internal void SetImprovement(int val) {
    improvementsBF |= (long)(1 << val);
  }
}