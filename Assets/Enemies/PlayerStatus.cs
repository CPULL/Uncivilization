using System.Collections.Generic;

[System.Serializable]
public class PlayerStatus {
  public ulong id = 0;
  public string name;
  public int avatar = 0;
  public int index = 0;
  public int position = 0;
  public bool isAI;
  public CharType charType;
  public List<int> friends;
  public List<int> foes;
  public WeaponType preferredWeapon;
  public WeaponType preferredDelivery;
  public int social;
  public int tech;
  public int forget;
  public List<int> cities;
  public GameAction gameAction;
  public bool defeated = false; // FIXME make private
  public UnityEngine.Color color;

  public ItemInstance[] techs;
  public int[] Food;
  public int[] Iron;
  public int[] Aluminum;
  public int[] Uranium;
  public int[] Plutonium;
  public int[] Hydrogen;
  public int[] Plastic;
  public int[] Electronics;
  public int[] Composite;
  public int[] FossilFuels;

  public Enemy refEnemy;

  public void Init(Enemy enemy, int pos) {
    index = pos;
    position = pos;
    refEnemy = enemy;
    BasicInit();
  }

  public PlayerStatus(byte[] data, int start) {
    BasicInit();
    UpdateValues(data, start);
  }

  public PlayerStatus(SimplePlayer player) {
    BasicInit();
    name = player.name;
    avatar = player.avatar;
    id = player.id;
    isAI = player.ai;
    color = new UnityEngine.Color32(0, 0, 0, 255);
  }

  public override string ToString() {
    if (string.IsNullOrEmpty(name) && avatar == 0 && id == 0 && !isAI) return "\"VOID\"";
    return "\"" + name + "[" + avatar + "]" + id + (isAI) + "\"";
  }

  public void GetFrom(PlayerStatus src) {
    id = src.id;
    name = src.name;
    avatar = src.avatar;
    index = src.index;
    position = src.position;
    isAI = src.isAI;
  }

  private void BasicInit() {
    cities = new List<int>();
    gameAction = new GameAction();

    techs = new ItemInstance[21];
    for (int i = 0; i < 21; i++)
      techs[i] = new ItemInstance { available = false, item = GD.instance.Technologies[i] };

    Food = new int[3];
    Iron = new int[3];
    Aluminum = new int[3];
    Uranium = new int[3];
    Plutonium = new int[3];
    Hydrogen = new int[3];
    Plastic = new int[3];
    Electronics = new int[3];
    Composite = new int[3];
    FossilFuels = new int[3];
  }


  public void SetIndex(int pos) {
    index = pos;
    position = pos;
    switch (pos) {
      case 0: color = new UnityEngine.Color32(80, 80, 80, 255); break;
      case 1: color = new UnityEngine.Color32(126, 145, 235, 255); break;
      case 2: color = new UnityEngine.Color32(17, 241, 23, 255); break;
      case 3: color = new UnityEngine.Color32(242, 231, 19, 255); break;
      case 4: color = new UnityEngine.Color32(242, 18, 54, 255); break;
      case 5: color = new UnityEngine.Color32(110, 18, 241, 255); break;
    }
  }

  public byte[] Serialize() {
    int len = 2 + // len
              8 + // id
              8 + // techs availability as bitfield
              8 + // bitfiled for ownership of cities + defeated flag
              10 * 4 * 3 + // resources (10 types * 3 values * 4 bytes)
              GD._GameActionLen;

    byte[] res = new byte[len];
    int pos;
    short len16 = (short)len;
    byte[] d = System.BitConverter.GetBytes(len16);
    res[0] = d[0];
    res[1] = d[1];
    pos = 2;

    d = System.BitConverter.GetBytes(id);
    for (int i = 0; i < 8; i++)
      res[pos + i] = d[i];
    pos += 8;

    long bitfield = 0;
    for (int i = 0; i < techs.Length; i++)
      if (techs[i].available) bitfield = (bitfield) | ((long)(1 << i));
    d = System.BitConverter.GetBytes(bitfield);
    for (int i = 0; i < 8; i++)
      res[pos + i] = d[i];
    pos += 8;
    
    bitfield = (defeated ? 1 : 0);
    foreach (int ci in cities) {
      bitfield |= ((long)1 << (ci + 1));
    }
    d = System.BitConverter.GetBytes(bitfield);
    for (int i = 0; i < 8; i++)
      res[pos + i] = d[i];
    pos += 8;

    for (int i = 0; i < 3; i++) {
      d = System.BitConverter.GetBytes(Food[i]);
      for (int j = 0; j < 4; j++)
        res[pos + j] = d[j];
      pos += 4;
      d = System.BitConverter.GetBytes(Iron[i]);
      for (int j = 0; j < 4; j++)
        res[pos + j] = d[j];
      pos += 4;
      d = System.BitConverter.GetBytes(Aluminum[i]);
      for (int j = 0; j < 4; j++)
        res[pos + j] = d[j];
      pos += 4;
      d = System.BitConverter.GetBytes(Uranium[i]);
      for (int j = 0; j < 4; j++)
        res[pos + j] = d[j];
      pos += 4;
      d = System.BitConverter.GetBytes(Plutonium[i]);
      for (int j = 0; j < 4; j++)
        res[pos + j] = d[j];
      pos += 4;
      d = System.BitConverter.GetBytes(Hydrogen[i]);
      for (int j = 0; j < 4; j++)
        res[pos + j] = d[j];
      pos += 4;
      d = System.BitConverter.GetBytes(Plastic[i]);
      for (int j = 0; j < 4; j++)
        res[pos + j] = d[j];
      pos += 4;
      d = System.BitConverter.GetBytes(Electronics[i]);
      for (int j = 0; j < 4; j++)
        res[pos + j] = d[j];
      pos += 4;
      d = System.BitConverter.GetBytes(Composite[i]);
      for (int j = 0; j < 4; j++)
        res[pos + j] = d[j];
      pos += 4;
      d = System.BitConverter.GetBytes(FossilFuels[i]);
      for (int j = 0; j < 4; j++)
        res[pos + j] = d[j];
      pos += 4;
    }

    byte[] gad = gameAction.Serialize();
    for (int i = 0; i < gad.Length; i++)
      res[pos + i] = gad[i];

    return res;
  }

  public int UpdateValues(byte[] data, int start) {
    // len is skipped
    int pos = 2 + start;

    id = System.BitConverter.ToUInt64(data, pos);
    pos += 8;

    long bitfield = System.BitConverter.ToInt64(data, pos);
    for (int i = 0; i < techs.Length; i++)
      techs[i].available = (bitfield & (1 << i)) != 0;
    pos += 8;
    
    bitfield = System.BitConverter.ToInt64(data, pos);
    defeated = (bitfield & 1) == 1;
    cities.Clear();
    for (int i = 1; i < 60; i++)
      if ((bitfield & (1L << i)) != 0)
        cities.Add(i - 1);
    pos += 8;

    for (int i = 0; i < 3; i++) {
      Food[i] = System.BitConverter.ToInt32(data, pos);
      pos += 4;
      Iron[i] = System.BitConverter.ToInt32(data, pos);
      pos += 4;
      Aluminum[i] = System.BitConverter.ToInt32(data, pos);
      pos += 4;
      Uranium[i] = System.BitConverter.ToInt32(data, pos);
      pos += 4;
      Plutonium[i] = System.BitConverter.ToInt32(data, pos);
      pos += 4;
      Hydrogen[i] = System.BitConverter.ToInt32(data, pos);
      pos += 4;
      Plastic[i] = System.BitConverter.ToInt32(data, pos);
      pos += 4;
      Electronics[i] = System.BitConverter.ToInt32(data, pos);
      pos += 4;
      Composite[i] = System.BitConverter.ToInt32(data, pos);
      pos += 4;
      FossilFuels[i] = System.BitConverter.ToInt32(data, pos);
      pos += 4;
    }

    gameAction = new GameAction(data, pos);

    return System.BitConverter.ToInt16(data, 0);
  }

  public void UpdateResources(PlayerStatus src) {
    for (int i = 0; i < 3; i++) {
      Food[i] = src.Food[i];
      Iron[i] = src.Iron[i];
      Aluminum[i] = src.Aluminum[i];
      Uranium[i] = src.Uranium[i];
      Plutonium[i] = src.Plutonium[i];
      Hydrogen[i] = src.Hydrogen[i];
      Plastic[i] = src.Plastic[i];
      Electronics[i] = src.Electronics[i];
      Composite[i] = src.Composite[i];
      FossilFuels[i] = src.FossilFuels[i];
    }
  }
}