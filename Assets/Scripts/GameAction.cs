public class GameAction {
  public ActionType action;
  public int targetCity;
  public int tech; // FIXME mix together tech and imp, we never need both
  public int imp;

  public GameAction() {
    action = ActionType.Nothing;
    targetCity = -1;
    tech = -1;
    imp = -1;
  }

  public override string ToString() {
    return "\"" + action.ToString() + " tc=" + targetCity + " t=" + tech + " i=" + imp + "\"";
  }

  public string Display() {
    switch(action) {
      case ActionType.Nothing: return "Nothing!";
      case ActionType.FindResources: return "Finding resources";
      case ActionType.ResearchTechnology: return "Reasearching <b>" + GD.GetTechName(tech) + "</b>";
      case ActionType.BuildImprovement: return "Building <b>" + GD.GetImprovementName(imp) + "</b> on city #" + targetCity;
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
    res[1] = (byte)targetCity;
    res[2] = (byte)tech;
    res[3] = (byte)imp;

    return res; // FIXME complete with all other required parameters
  }

  public GameAction(byte[] data, int start) {
    action = (ActionType)data[start + 0];
    targetCity = data[start + 1];
    tech = data[start + 2];
    imp = data[start + 3];
  }

}

public enum ActionType {  Nothing=0, FindResources=1, ResearchTechnology=2, BuildImprovement=3, Social=4, Propaganda=5, Diplomacy=6, BuildWeapons=7, DeployWeapons=8, UseWeapons=9, Defend=10, SettleCity=11, Denucrlearize=12 };

