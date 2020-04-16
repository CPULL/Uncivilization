public class GameAction {
  public ActionType action;
  public byte val1;
  public byte val2;

  public GameAction() {
    action = ActionType.Nothing;
    val1 = 255;
    val2 = 255;
  }

  public override string ToString() {
    return "\"" + action.ToString() + " v1=" + val1 + " v2=" + val2 + "\"";
  }

  public string Display() {
    switch(action) {
      case ActionType.Nothing: return "Nothing!";
      case ActionType.FindResources: return "Finding resources";
      case ActionType.ResearchTechnology: return "Reasearching <b>" + GD.GetTechName(val1) + "</b>";
      case ActionType.BuildImprovement: return "Building <b>" + GD.GetImprovementName(val2) + "</b> on city #" + val1;
      case ActionType.Propaganda: return "Propaganda against FIXME";
      case ActionType.Diplomacy: return "Diplomacy with FIXME";
      case ActionType.BuildWeapons: return "Building weapons";
      case ActionType.DeployWeapons: return "Deploying weapon FIXME";
      case ActionType.UseWeapons: return "Use weapon FIXME against FIXME";
      case ActionType.SettleCity: return "Settling new city in location #" + val1;
      case ActionType.Denucrlearize: return "Denuclearizing area/city #" + val1;
      default: return "Action undefined!";
    }
  }

  public byte[] Serialize() {
    byte[] res = new byte[3];
    res[0] = (byte)action;
    res[1] = val1;
    res[2] = val2;
    return res;
  }

  public GameAction(byte[] data, int start) {
    action = (ActionType)data[start + 0];
    val1 = data[start + 1];
    val2 = data[start + 2];
  }

}

public enum ActionType {  Nothing=0, FindResources=1, ResearchTechnology=2, BuildImprovement=3, Social=4, Propaganda=5, Diplomacy=6, BuildWeapons=7, DeployWeapons=8, UseWeapons=9, Defend=10, SettleCity=11, Denucrlearize=12 };

