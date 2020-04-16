using System;


public class Game {
  public ulong id;
  public string Name;
  public int Difficulty;
  public int NumPlayers;
  public int NumJoined;
  public int NumAIs;
  public GameStatus Status;
  public PlayerDef Creator;
  public PlayerDef Winner;
  public DateTime CreationTime;

  public int rndSeed;
  public bool multiplayer;
  public int nump;
  public PlayerDef[] Players; // Needed to communicate with the clients
  public GameEngine engine;

  public byte[] Serialize() {
    byte[] nd = System.Text.Encoding.UTF8.GetBytes(Name); // 1 + len
    byte[] crd = Creator.Serialize(); // 1 + len
    byte[] wid = Winner.Serialize(); // 1 + len

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
              1;  // Multiplayer

    byte[] res = new byte[len];
    short len16 = (short)len;
    byte[] ld = BitConverter.GetBytes(len16);
    res[0] = ld[0];
    res[1] = ld[1];
    res[2] = (byte)nd.Length;
    for (int i = 0; i < nd.Length; i++)
      res[3 + i] = nd[i];
    int pos = 3 + nd.Length;
    res[pos++] = (byte)Difficulty;
    res[pos++] = (byte)NumPlayers;
    res[pos++] = (byte)NumJoined;
    res[pos++] = (byte)NumAIs;
    res[pos++] = (byte)Status;
    res[pos++] = (byte)crd.Length;
    for (int i = 0; i < crd.Length; i++)
      res[pos + i] = crd[i];
    pos += crd.Length;
    res[pos++] = (byte)wid.Length;
    for (int i = 0; i < wid.Length; i++)
      res[pos + i] = wid[i];
    pos += wid.Length;

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

    Creator = new PlayerDef(data, pos + 1);
    pos += data[pos] + 1;
    Winner = new PlayerDef(data, pos + 1);
    pos += data[pos] + 1;

    long time = BitConverter.ToInt64(data, pos);
    CreationTime = DateTime.FromBinary(time);
    pos += 8;

    rndSeed = BitConverter.ToInt32(data, pos);
    pos += 4;

    multiplayer = data[pos] != 0;

    nump = 0;
    Players = new PlayerDef[6];
  }

  public Game(string name, Player player, int difficulty, int numplayers, int numais) {
    Name = name;
    Creator = player.def;
    Difficulty = difficulty;
    NumPlayers = numplayers;
    NumAIs = numais;
    NumJoined = 1;
    Winner = new PlayerDef();
    Status = GameStatus.Waiting;
    CreationTime = DateTime.Now;
    nump = 1;
    Players = new PlayerDef[6];
    Players[0] = player.def;
    while (NumPlayers + NumAIs > 6) NumAIs--;

    byte[] ID = new byte[8];
    byte[] d = System.Text.Encoding.UTF8.GetBytes(Name);
    for (int i = 0; i < d.Length; i++)
      ID[i % 8] = (byte)(ID[i % 8] ^ d[i]);
    ID[0] = (byte)(ID[0] ^ NumPlayers);
    ID[1] = (byte)(ID[1] ^ NumAIs);
    ID[2] = (byte)(ID[2] ^ Difficulty);

    d = BitConverter.GetBytes(CreationTime.Ticks);
    for (int i = 0; i < d.Length; i++)
      ID[i % 8] = (byte)(ID[i % 8] ^ d[i]);

    id = BitConverter.ToUInt64(ID, 0);
  }

  public void AddPlayer(ulong id, string name, byte avatar) {
    Players[nump] = new PlayerDef(id, name, avatar);
    nump++;
  }

  public PlayerDef GetPlayer(ulong id) {
    for (int i = 0; i < nump; i++)
      if (Players[i].id == id) return Players[i];
    return null;
  }

  public void AddPlayer(PlayerHandler player) {
    Players[nump] = player.player.def;
    nump++;
  }

  public void AddPlayer(PlayerDef player) {
    Players[nump] = player;
    nump++;
  }

  public bool AmIIn(ulong id) {
    for (int i = 0; i < nump; i++)
      if (Players[i].id == id) return true;
    return false;
  }

  public bool SpaceAvailable() {
    return NumPlayers > NumJoined;
  }

  public bool RemovePlayer(ulong id) {
    if (!AmIIn(id)) return false;

    for (int i = 5; i >= 0; i--)
      if (Players[i].id == id) {
        for (int j = i; j < 5; j++)
          Players[j] = Players[j + 1];
        Players[5] = null;
        nump--;
        break;
      }
    NumJoined--;
    Status = GameStatus.Waiting;
    return true;
  }

  public void Destroy() {
    engine = null;
  }

  internal void SetPlayerStatus(ulong id, PlayerGameStatus status) {
    for (int i = 0; i < 6; i++)
      if (Players[i].id == id) {
        Players[i].Status = status;
        return;
      }
  }
}

