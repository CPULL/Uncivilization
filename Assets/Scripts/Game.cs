using System;


public class Game {
  public string Name;           // Serialized
  public int Difficulty;        // Serialized
  public int NumPlayers;        // Serialized
  public int NumJoined;         // Serialized
  public int NumAIs;            // Serialized
  public GameStatus Status;     // Serialized
  public SimplePlayer Creator;  // Serialized
  public SimplePlayer Winner;   // Serialized
  public DateTime CreationTime; // Serialized

  public int rndSeed;           // Serialized
  public bool multiplayer;      // Serialized
  public SimpleList Players;    // Serialized
  public GameEngine engine;
  public Player localPlayer;

  public byte[] Serialize() {
    byte[] nd = System.Text.Encoding.UTF8.GetBytes(Name); // 1 + len
    byte[] crd = Creator.Serialize(); // 1 + len
    byte[] wid = Winner.Serialize(); // 1 + len
    byte[] sps = Players.Serialize();

    int len = 2 + // full len of the data
              1 + nd.Length + // Name
              1 + // Difficulty
              1 + // Num players
              1 + // Num joined
              1 + // Num AIs
              1 + // Status
              1 + crd.Length + // Creator
              1 + wid.Length + // Winner
              8 + // Creation time
              4 + // Random seed
              1 + // Multiplayer
              sps.Length; // Players in the list

    byte[] res = new byte[len];
    short len16 = (short)len;
    byte[] ld = BitConverter.GetBytes(len16);
    res[0] = ld[0];
    res[1] = ld[1];
    res[2] = (byte)nd.Length;
    for (int i = 0; i < nd.Length; i++)
      res[3 + i] = nd[i];
    int pos = 3 + nd.Length;
    res[pos + 0] = (byte)Difficulty;
    res[pos + 1] = (byte)NumPlayers;
    res[pos + 2] = (byte)NumJoined;
    res[pos + 3] = (byte)NumAIs;
    res[pos + 4] = (byte)Status;
    pos += 5;
    res[pos] = (byte)crd.Length;
    for (int i = 0; i < crd.Length; i++)
      res[pos + 1 + i] = crd[i];
    pos += 1 + crd.Length;
    res[pos] = (byte)wid.Length;
    for (int i = 0; i < wid.Length; i++)
      res[pos + 1 + i] = wid[i];
    pos += 1 + wid.Length;

    long time = CreationTime.ToBinary();
    byte[] ctd = BitConverter.GetBytes(time);
    for (int i = 0; i < 8; i++)
      res[pos + i] = ctd[i];
    pos += 8;

    ctd = BitConverter.GetBytes(rndSeed);
    for (int i = 0; i < 4; i++)
      res[pos + i] = ctd[i];
    pos += 4;

    res[pos] = (byte)(multiplayer ? 1 : 0);
    pos++;

    for (int i = 0; i < sps.Length; i++)
      res[pos + i] = sps[i];

    return res;
  }


  public Game(byte[] data, int startindex) { // Deserialize
    int pos = startindex + 2;

    int len = data[pos];
    Name = System.Text.Encoding.UTF8.GetString(data, pos + 1, len);
    pos += len + 1;

    Difficulty = data[pos + 0];
    NumPlayers = data[pos + 1];
    NumJoined = data[pos + 2];
    NumAIs = data[pos + 3];
    Status = (GameStatus)data[pos + 4];
    pos += 5;

    Creator = new SimplePlayer(data, pos + 1);
    pos += data[pos] + 1;
    Winner = new SimplePlayer(data, pos + 1);
    pos += data[pos] + 1;

    long time = BitConverter.ToInt64(data, pos);
    CreationTime = DateTime.FromBinary(time);
    pos += 8;

    rndSeed = BitConverter.ToInt32(data, pos);
    pos += 4;

    multiplayer = data[pos] != 0;
    pos++;

    Players = new SimpleList(data, pos);
  }

  public Game(string name, Player player, int difficulty, int numplayers, int numais) {
    Name = name;
    Creator = new SimplePlayer(player.ID, player.Name, (byte)player.Avatar);
    Difficulty = difficulty;
    NumPlayers = numplayers;
    NumAIs = numais;
    NumJoined = 1;
    Winner = new SimplePlayer();
    Status = GameStatus.Waiting;
    CreationTime = DateTime.Now;
    Players = new SimpleList(Creator);
    while (NumPlayers + NumAIs > 6) NumAIs--;
  }

  public bool AmIIn(Player player) {
    return Players.Contains(player.ID);
  }

  public bool SpaceAvailable() {
    return NumPlayers > NumJoined;
  }

  public bool RemovePlayer(ulong playerID) {
    if (Players.Remove(playerID)) {
      NumJoined--;
      Status = GameStatus.Waiting;
      return true;
    }
    return false;
  }

  public void Destroy() {
    engine = null;
    localPlayer = null;
  }
}

