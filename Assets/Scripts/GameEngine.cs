using System.Collections.Generic;
using UnityEngine;

public class GameEngine {
  // Used to run a game, on single it will handle the current game, on server mode we should line it to the game itself and handle it server side

  public GameEngine(Player player) {
    if (player != null) player.OnGame += OnMsgReceived;
    messages = new List<NetworkManager.GameMessage>();
  }

  internal int nump = 0;
  internal PlayerStatus[] players = new PlayerStatus[6];
  internal bool[] completed = new bool[6];
  public City[] cities = new City[3 * 3 * 6];
  public List<NetworkManager.GameMessage> messages;
  public PlayerStatus mySelf;
  public Game game;

  public void InitEnemiesSingleplayer(Game theGame, Transform selectedEnemies) { // This is for singleplayer
    game = theGame;
    GD.InitRandomGenerator(theGame.rndSeed);

    nump = 0;
    foreach (Transform p in selectedEnemies) {
      Enemy enemy = p.GetComponent<Enemy>();
      enemy.stats.avatar = GD.GetAvatarForAI(enemy.stats.name);
      enemy.stats.id = GD.GetIDForAI(enemy.stats.name);
      players[nump] = enemy.stats;
      players[nump].Init(enemy, nump);
      game.Players.Add(new SimplePlayer(enemy.stats.id, enemy.stats.name, (byte)enemy.stats.avatar));
      nump++;
    }
    Enemy player = GD.instance.PlayersBase[0].GetComponent<Enemy>();
    players[nump] = player.stats;
    player.stats.Init(player, nump);
    nump++;
    game.Players.Add(new SimplePlayer(player.stats.id, player.stats.name, (byte)player.stats.avatar));
    player.SetEnemy(GD.thePlayer.Name, GD.thePlayer.Avatar, GD.thePlayer.ID);

    // Randomize the positions
    for (int i = 0; i < 1000; i++) {
      int a = GD.GetRandom(0, nump);
      int b = GD.GetRandom(0, nump);
      PlayerStatus tmpe = players[a];
      players[a] = players[b];
      players[b] = tmpe;
      SimplePlayer tmpsp = game.Players[a];
      game.Players[a] = game.Players[b];
      game.Players[b] = tmpsp;
    }

    for (int i = 0; i < nump; i++)
      completed[i] = false;
  }

  public void InitEnemiesMultiplayer(Game theGame) { // This is for multiplayer
    game = theGame;
    GD.InitRandomGenerator(theGame.rndSeed);

    nump = 0;
    for (int i = 0; i < game.Players.Count; i++) {
      SimplePlayer sp = game.Players[i];
      players[nump] = new PlayerStatus(sp);
      nump++;
    }
    // Randomize the positions if single player
    if (!game.multiplayer) {
      for (int i = 0; i < 1000; i++) {
        int a = GD.GetRandom(0, nump);
        int b = GD.GetRandom(0, nump);
        PlayerStatus tmpe = players[a];
        players[a] = players[b];
        players[b] = tmpe;
        SimplePlayer tmpsp = game.Players[a];
        game.Players[a] = game.Players[b];
        game.Players[b] = tmpsp;
      }
    }

    for (int i = 0; i < nump; i++)
      completed[i] = false;
  }


  public void LeaveGame(string gamename, bool multiplayer) {
    if (multiplayer) {
      GD.instance.networkManager.LeaveGameClient(GD.thePlayer, gamename);
    }
  }

  public void EndTurn(bool multiplayer, Player player, GameAction gameAction) {
    if (GD.IsStatus(LoaderStatus.Server, LoaderStatus.ServerBackground)) { // We are on the server
      // This call comes from the client listner
      // We should have the action deserialized
      // Find the player in the player list
      for (int i = 0; i < nump; i++)
        if (players[i].id == player.ID) {
          completed[i] = true;
          players[i].gameAction = gameAction;
          break;
        }

      // Auto-complete AI and defeated
      for (int i = 0; i < nump; i++)
        if (players[i].isAI)
          completed[i] = true;
        else if (players[i].defeated) {
          completed[i] = true;
          players[i].gameAction = new GameAction();
        }

      // All human players completed?
      bool allCompleted = true;
      for (int i = 0; i < nump; i++) {
        if (!players[i].defeated && !players[i].isAI && !completed[i]) {
          allCompleted = false;
          break;
        }
      }
      if (allCompleted) {
        GameEngineValues values = CalculateNextTurn();
        // Send the result to all the clients
        GD.instance.networkManager.SendGameTurn(values);
        // And reset the completed
        for (int i = 0; i < nump; i++)
          completed[i] = false;
      }
    }
    else if (multiplayer) { // Multiplayer mode and we are the client
      // This call comes from the Renderer, Send the message to the server
      GD.instance.networkManager.SendGameAction(player, gameAction);
    }
    else { // Single player mode
      // This call comes from the Renderer, so we can process it right now: CalculateNextTurn
      for (int i = 0; i < nump; i++)
        if (players[i].id == player.ID) {
          completed[i] = true;
          players[i].gameAction = gameAction;
          break;
        }
      GameEngineValues values = CalculateNextTurn();
      GD.thePlayer.OnGame?.Invoke(this, new NetworkManager.GameMessage { type = GameMsgType.GameTurn, engineValues = values });
    }
  }

  public GameEngineValues CalculateNextTurn() {
    GameEngineValues values = new GameEngineValues(this);
    foreach (PlayerStatus p in players) {
      if (p == null || p.defeated) continue;
      CalculateResources(p, values);
      CalculatePopulation(p, values);
    }
    CalculateOrder(values);
    for (int i = 0; i < nump; i++) {
      if (values.order[i] == 255) continue;
      PlayerStatus p = players[values.order[i]];
      if (p == null || p.defeated) continue;
      CalculateActions(p, values); // Actions are computed after order is defined
    }

    // At this point we should actually execute the action and decide the outcome
    // The outcome should be serialized to be used in the renderer (after sending it, in case of multiplayer)




    /*
     
    Here we should calculate, for each player, the resources and population
    For the AIs (not defeated) we should calculate the next action
    Calculate the results of all actions (success, failures, etc.)

    Broadcast to all clients the results (multiplayer mode)
    Add a "message" with the results (single player mode)

     */
    return values;
  }


  private void OnMsgReceived(object sender, NetworkManager.GameMessage e) {
    // Add it to the list of messages to be processed by the GameRederer
    messages.Add(e);
  }


  public void CalculateResources(PlayerStatus p, GameEngineValues values) {
    // Find the place for the current player
    int pindex = -1;
    for (int i = 0; i < players.Length; i++) {
      if (players[i].id == p.id) {
        pindex = i;
        break;
      }
    }
    if (pindex==-1) {
      GD.DebugLog("CalculateResources: Cannot find the specified player id=" + p.id, GD.LT.Debug);
      return;
    }
    // Food ************************************************************************************************************************************************************
    // Food +1 per city, +10 per farm, city_production*2 per hydrofarm
    int produced = p.cities.Count;
    foreach (int cityIndex in p.cities) {
      if (cities[cityIndex].improvements[(int)ItemType.IFARM - (int)ProductionType.Improvement]) produced += 10;
      if (cities[cityIndex].improvements[(int)ItemType.IHFRM - (int)ProductionType.Improvement]) produced += 15;
    }
    values.players[pindex].Food[1] = produced;
    // Consume the food, 1 unit for each 10 millions population, but use floor to allow basic production
    int population = 0;
    foreach (int cityIndex in p.cities) {
      population += cities[cityIndex].population;
    }
    values.players[pindex].Food[2] = population / 10;
    values.players[pindex].Food[0] = p.Food[0] + values.players[pindex].Food[1] - values.players[pindex].Food[2];
    if (values.players[pindex].Food[0] < 0) values.players[pindex].Food[0] = 0; // FIXME add some problem for the enemy, do a message to the player to show there is not enough food


    // Iron ************************************************************************************************************************************************************
    // FIXME this is for debug
    values.players[pindex].Iron[0] = 11;
    values.players[pindex].Iron[1] = 22;
    values.players[pindex].Iron[2] = 33;

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

  public void CalculatePopulation(PlayerStatus p, GameEngineValues values) {
    // Find the place for the current player
    int pindex = -1;
    for (int i = 0; i < players.Length; i++) {
      if (players[i].id == p.id) {
        pindex = i;
        break;
      }
    }
    if (pindex == -1) {
      GD.DebugLog("CalculateResources: Cannot find the specified player id=" + p.id, GD.LT.Debug);
      return;
    }
    // FIXME
    // Increase the population in case there is enough food, in case the food is zero and the consumption is high, let people starve
    // The increase should be about *1.05 with at least 1 of increase

    if (values.players[pindex].Food[0] > 0 || values.players[pindex].Food[1] == values.players[pindex].Food[2]) {
      foreach (int cityIndex in p.cities) {
        City src = cities[cityIndex];
        CityVals dst = values.cities[cityIndex];
        if (dst == null) {
          dst = new CityVals(src, (byte)pindex);
          values.cities[cityIndex] = dst;
        }

        int increase = (int)(src.population * 0.05f);
        if (increase == 0) increase = 1;
        if (src.status == CityStatus.Radioactive) {
          increase = -1;
        }
        else if (src.population + increase > 25 && !src.improvements[(int)ItemType.IHOUS - (int)ProductionType.Improvement]) {
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
      if (players[i].defeated) continue;
      if (players[i].gameAction.action == ActionType.Defend || players[i].gameAction.action == ActionType.Diplomacy || players[i].gameAction.action == ActionType.Social)
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
      if (players[i].defeated) continue;
      if (players[i].gameAction.action != ActionType.Defend && players[i].gameAction.action != ActionType.Diplomacy && players[i].gameAction.action != ActionType.Social)
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

  private void CalculateActions(PlayerStatus p, GameEngineValues values) {
    // Depending on the action we may need to do something
    switch (p.gameAction.action) {
      case ActionType.Nothing: break;
      case ActionType.FindResources: // Double the production of resources and add some extra random resource based on the population size
        ExecuteFindResources(p, values);
        break;
      case ActionType.ResearchTechnology: break;
      case ActionType.BuildImprovement: break;
      case ActionType.Social: break;
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

  private void ExecuteFindResources(PlayerStatus p, GameEngineValues values) {
    values.players[p.index].Food[1] *= 2;
    values.players[p.index].Iron[1] *= 2;
    values.players[p.index].Aluminum[1] *= 2;
    values.players[p.index].Uranium[1] *= 2;
    values.players[p.index].Plutonium[1] *= 2;
    values.players[p.index].Hydrogen[1] *= 2;
    values.players[p.index].Plastic[1] *= 2;
    values.players[p.index].Electronics[1] *= 2;
    values.players[p.index].Composite[1] *= 2;
    values.players[p.index].FossilFuels[1] *= 2;

    int population = 0;
    foreach (int cityIndex in p.cities) {
      population += cities[cityIndex].population;
    }

    values.players[p.index].Food[1] += GD.GetRandom(0, population);
    values.players[p.index].Iron[1] += GD.GetRandom(0, 2 * population / 3);
    values.players[p.index].Aluminum[1] += GD.GetRandom(0, population / 3);
    values.players[p.index].Uranium[1] += GD.GetRandom(0, population / 4);
    values.players[p.index].Plutonium[1] += GD.GetRandom(0, population / 8);
    values.players[p.index].Hydrogen[1] += GD.GetRandom(0, population / 10);
    values.players[p.index].Plastic[1] += GD.GetRandom(0, population / 5);
    values.players[p.index].Electronics[1] += GD.GetRandom(0, population / 7);
    values.players[p.index].Composite[1] += GD.GetRandom(0, population / 20);
    values.players[p.index].FossilFuels[1] += GD.GetRandom(0, population / 9);
  }


  public void Destroy() {
    players = null;
    completed = null;
    cities = null;
    messages.Clear();
    mySelf = null;
    game = null;
  }


  public PlayerStatus GetPlayerStatusByIndex(byte index) {
    for (int i = 0; i < nump; i++)
      if (players[i].index == index) return players[i];
    return null;
  }
}



// *************************************** GameEngineValues *********************************************************************************************************************************
// Used to decode the values and then update the Engine

public class GameEngineValues {
  public readonly string gamename;
  public readonly byte[] order;
  public readonly CityVals[] cities;
  public readonly PlayerStatus[] players;

  public GameEngineValues(GameEngine engine) {
    gamename = engine.game.Name;
    order = new byte[6];
    cities = new CityVals[9 * 6];
    players = new PlayerStatus[6];
    for (int i = 0; i < 6; i++)
      players[i] = engine.players[i];
  }

  public byte[] Serialize() {
    byte[] gd = System.Text.Encoding.UTF8.GetBytes(gamename);
    int len = 2 + // Full length
              1 + gd.Length + // Game name
              6 + // Order for actions
              9 * 6 * 14 + // Status of each city
              6 * GD._PlayerStatusLen + // Status of each player including its action
              1; // Outcomes FIXME
    byte[] res = new byte[len];
    short len16 = (short)len;
    byte[] d = System.BitConverter.GetBytes(len16);
    res[0] = d[0];
    res[1] = d[1];

    res[2] = (byte)gd.Length;
    for (int i = 0; i < gd.Length; i++)
      res[3 + i] = gd[i];
    int pos = 3 + gd.Length;

    res[pos + 0] = order[0];
    res[pos + 1] = order[1];
    res[pos + 2] = order[2];
    res[pos + 3] = order[3];
    res[pos + 4] = order[4];
    res[pos + 5] = order[5];
    pos += 6;

    for (int i = 0; i < cities.Length; i++) {
      if (cities[i] == null) {
        for (int j = 0; j < 14; j++) res[pos + j] = 255;
        pos += 14;
        continue;
      }
      d = cities[i].Serialize();
      for (int j = 0; j < d.Length; j++)
        res[pos + j] = d[j];
      pos += d.Length;
    }
    for (int i = 0; i < 6; i++) {
      PlayerStatus ps = players[i];
      if (ps != null) {
        d = ps.Serialize();
        for (int j = 0; j < d.Length; j++)
          res[pos + j] = d[j];
        pos += d.Length;
      }
      else
        pos += GD._PlayerStatusLen;
    }

    // FIXME outcomes!!!

    return res;
  }


  public GameEngineValues(byte[] data) {
    int pos = data[2];
    gamename = System.Text.Encoding.UTF8.GetString(data, 3, pos);
    pos += 3;

    order = new byte[6];
    for (int i = 0; i < 6; i++)
      order[i] = data[pos + i];
    pos += 6;

    cities = new CityVals[9 * 6];
    for (int i = 0; i < 9 * 6; i++) {
      CityVals cv = new CityVals(data, pos);
      if (cv.pos != 255) 
        cities[i] = cv;
      pos += 14;
    }

    players = new PlayerStatus[6];
    for (int i = 0; i < 6; i++) {
      players[i] = new PlayerStatus(data, pos);
      pos += GD._PlayerStatusLen;
    }

    // FIXME outcomes
  }
}

public class CityVals {
  public byte pos;
  public long improvementsBF;
  public int population;
  public CityStatus status;
  public byte owner;

  public CityVals(City src, byte owner) {
    pos = (byte)src.pos;
    population = src.population;
    status = src.status;
    this.owner = owner;
    improvementsBF = 0;
    for (int i = 0; i < src.improvements.Length; i++)
      if (src.improvements[i]) improvementsBF = (improvementsBF) | ((long)(1 << i));
  }

  public byte[] Serialize() {
    /*
      1 // pos
      8 // bitfield improvements
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
}