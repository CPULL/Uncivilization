public class GameValues {
  public string gamename;
  public byte[] order;
  public CityValues[] cities;

  public GameValues(string name) {
    gamename = name;
    order = new byte[6];
    cities = new CityValues[6 * 96];
  }

  public byte[] Serialize() {
    int len = 1 + System.Text.Encoding.UTF8.GetByteCount(gamename) +
            6 +
            9 * 6 * CityValues.Len;
    byte[] res = new byte[len];
    byte[] d = System.Text.Encoding.UTF8.GetBytes(gamename);
    int pos = 0;
    res[pos++] = (byte)System.Text.Encoding.UTF8.GetByteCount(gamename);
    for (int i = 0; i < d.Length; i++)
      res[pos++] = d[i];
    for (int i = 0; i < 6; i++)
      res[pos++] = order[i];

    for (int i = 0; i < 6 * 9; i++) {
      if (cities[i] == null) {
        for (int j = 0; j < CityValues.Len; j++)
          res[pos++] = 0;
        continue;
      }
      d = cities[i].Serialize();
      for (int j = 0; j < CityValues.Len; j++)
        res[pos++] = d[j];
    }
    return res;
  }

}

/* What do we need?
 * 
 * list of player: name, avatar, index, id, defeated, ai, techs, happiness, resources, action
 * list of cities: status, population, radio, owner, improvements
 * 
 * We need to keep the current status, to show the visual when the turn starts, update the values with the actions
 * 
 * 
 */

public class PlayerValues {
  public byte index;
  public byte avatar;
  public ulong id;
  public Status status;
  public ActionType action;
  public byte targetCity;
  public int val;
  public byte happiness;
  public bool[] techs;
  public short[,] Resources;

  public enum Status { Human=1, AI=2, Defeated=3, Empty=0 };

  public const int Len =  1 + // index
                          1 + // avatar
                          8 + // id
                          1 + // status
                          1 + // action
                          1 + // targetCity
                          4 + // val
                          1 + // happiness
                          4 + // techs (as bitfield)
                          2 * 3 + // Food
                          2 * 3 + // Iron
                          2 * 3 + // Aluminum
                          2 * 3 + // Uranium
                          2 * 3 + // Plutonium
                          2 * 3 + // Hydrogen
                          2 * 3 + // Plastic
                          2 * 3 + // Electronics
                          2 * 3 + // Composite
                          2 * 3;  // FossilFuels

  public PlayerValues(byte[] data, int pos) {
    index = data[pos++];
    avatar = data[pos++];
    id = System.BitConverter.ToUInt64(data, pos);
    pos += 8;
    status = (Status)data[pos++];
    action = (ActionType)data[pos++];
    targetCity = data[pos++];
    val = System.BitConverter.ToInt32(data, pos);
    happiness = data[pos++];
    techs = new bool[21];
    int bf = System.BitConverter.ToInt32(data, pos);
    for (int i = 0; i < 21; i++)
      techs[i] = (bf & (1 << i)) != 0;
    pos += 4;

    for (int i = (int)ResourceType.Food; i <= (int)ResourceType.FossilFuels; i++)
      for (int j = 0; j < 3; j++) {
        Resources[i, j] = System.BitConverter.ToInt16(data, pos);
        pos += 2;
      }
  }

  public PlayerValues(PlayerValues src) {
    index = src.index;
    avatar = src.avatar;
    id = src.id;
    status = src.status;
    action = src.action;
    targetCity = src.targetCity;
    val = src.val;
    happiness = src.happiness;
    techs = new bool[21];
    for (int i = 0; i < 21; i++)
      techs[i] = src.techs[i];
    Resources = new short[10, 3];
    for (int i = 0; i < 10; i++)
      for (int j = 0; j < 3; j++)
        Resources[i, j] = src.Resources[i, j];
  }

public byte[] Serialize() {
    byte[] res = new byte[Len];
    int pos = 0;
    res[pos++] = index;
    res[pos++] = avatar;
    byte[] d = System.BitConverter.GetBytes(id);
    for (int i = 0; i < 8; i++)
      res[pos++] = d[i];
    res[pos++] = (byte)status;
    res[pos++] = (byte)action;
    res[pos++] = targetCity;
    d = System.BitConverter.GetBytes(val);
    for (int i = 0; i < 4; i++)
      res[pos++] = d[i];
    res[pos++] = happiness;
    int bf = 0;
    for (int i = 0; i < techs.Length; i++)
      bf |= techs[i] ? (1 << i) : 0;
    d = System.BitConverter.GetBytes(bf);
    for (int i = 0; i < 4; i++)
      res[pos++] = d[i];
    for (int i = (int)ResourceType.Food ; i <= (int)ResourceType.FossilFuels; i++)
      for (int j = 0; j < 3; j++) {
        d = System.BitConverter.GetBytes(Resources[i, j]);
        for (int k = 0; k < 2; k++)
          res[pos++] = d[k];
      }

    return res;
  }
}

public class CityValues {
  public byte index;
  public long improvements;
  public short population;
  public short populationVar;
  public bool radioactive;
  public Status status;
  public byte owner;

  public enum Status {
    Empty = 0, // Spot is not used
    Owned = 1, // The city is owned by somebody and not destroyed
    Destroyed = 2, // Crater, still owned, but can be reconquered
  };

  public const int Len =  1 + // index
                          8 + // improvements
                          2 + // population
                          2 + // populationVar
                          1 + // status + radioactive
                          1;  // owner

  public CityValues(byte[] data, int pos) {
    index = data[pos++];
    improvements = System.BitConverter.ToInt64(data, pos);
    pos += 8;
    population = System.BitConverter.ToInt16(data, pos);
    pos += 2;
    populationVar = System.BitConverter.ToInt16(data, pos);
    pos += 2;
    radioactive = (data[pos] & 128) == 128;
    status = (Status)(data[pos] & 127);
    pos++;
    owner = data[pos];
  }

  public byte[] Serialize() {
    byte[] res = new byte[Len];
    int pos = 0;
    res[pos++] = index;
    byte[] d = System.BitConverter.GetBytes(improvements);
    for (int i = 0; i < 8; i++)
      res[pos++] = d[i];
    d = System.BitConverter.GetBytes(population);
    for (int i = 0; i < 2; i++)
      res[pos++] = d[i];
    d = System.BitConverter.GetBytes(populationVar);
    for (int i = 0; i < 2; i++)
      res[pos++] = d[i];
    byte s = (byte)status;
    if (radioactive) s |= 128;
    res[pos++] = s;
    res[pos++] = owner;
    return res;
  }

}



