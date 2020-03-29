using System.Collections.Generic;

[System.Serializable]
public class Item {
  public ProductionType type;
  public string name;
  public string description;
  public UnityEngine.Sprite icon;
  public List<Dependency> prerequisites;
  public int index;
  // FIXME items to produce for each level

}


public class ItemInstance {
  public Item item;
  public bool available;
}

[System.Serializable]
public struct Dependency {
  public ProductionType type;
  public ItemType index;
}

