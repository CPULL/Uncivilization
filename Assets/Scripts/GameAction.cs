public class GameAction {
  public ActionType action;
  public int targetCity;
  public int val;
  public int outcome;

  public GameAction() {
    action = ActionType.Nothing;
    targetCity = -1;
    val = -1;
    outcome = -1;
  }

  public override string ToString() {
    return "\"" + action.ToString() + " city=" + targetCity + " v=" + val + " out=" + outcome + "\"";
  }

  public string Display() {
    switch(action) {
      case ActionType.Nothing: return "Nothing!";
      case ActionType.FindResources: return "Finding resources";
      case ActionType.ResearchTechnology: return "Reasearching <b>" + GD.GetTechName(val) + "</b>";
      case ActionType.BuildImprovement: return "Building <b>" + GD.GetImprovementName(val) + "</b> on city #" + targetCity;
      case ActionType.Propaganda: return "Propaganda against FIXME";
      case ActionType.Diplomacy: return "Diplomacy with FIXME";
      case ActionType.BuildWeapons: return "Building weapons";
      case ActionType.DeployWeapons: return "Deploying weapon FIXME";
      case ActionType.UseWeapons: return "Use weapon FIXME against FIXME";
      case ActionType.SettleCity: return "Settling new city in location #" + targetCity;
      case ActionType.Denucrlearize: return "Denuclearizing area/city #" + targetCity;
      default: return "Action undefined!";
    }
  }

  public byte[] Serialize() {
    byte[] res = new byte[4];
    res[0] = (byte)action;
    if (targetCity == -1)
      res[1] = 255;
    else
      res[1] = (byte)targetCity;
    if (val == -1)
      res[2] = 255;
    else
      res[2] = (byte)val;
    if (outcome == -1)
      res[3] = 255;
    else
      res[3] = (byte)outcome;
    return res; // FIXME complete with all other required parameters
  }

  public GameAction(byte[] data, int start) {
    action = (ActionType)data[start + 0];
    targetCity = data[start + 1];
    if (targetCity == 255) targetCity = -1;
    val = data[start + 2];
    if (val == 255) val = -1;
    outcome = data[start + 3];
    if (outcome == 255) outcome = -1;
  }

}

public enum ActionType {  Nothing=0, FindResources=1, ResearchTechnology=2, BuildImprovement=3, Social=4, Propaganda=5, Diplomacy=6, BuildWeapons=7, DeployWeapons=8, UseWeapons=9, Defend=10, SettleCity=11, Denucrlearize=12 };

