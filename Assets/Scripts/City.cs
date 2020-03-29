using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;
using UnityEngine.UI;

public class City : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler {
  public enum Status { Empty=0, Destroyed=1, Owned=2 };
  public Image city;
  public Image radiation;
  public TextMeshProUGUI popText;
  public TextMeshProUGUI varText;
  public SpriteAtlas items;
  public Image[] Improvements;

  public int pos; // Number from 0 to 54 for all possible cities, 3x3 for each sector
  public PlayerStatus owner;
  public int population;
  public bool[] improvements;
  public int sector;
  public Status status;
  public bool radioactive;
  public int px, py;
  private bool areWeOver = false;

  private Color transparency = new Color32(0, 0, 0, 0);
  private Color fullVisible = new Color32(255, 255, 255, 255);

  public City Init(int position) {
    radiation.gameObject.SetActive(false);
    city.gameObject.SetActive(true); // FIXME set to false, but use it to debug the positions
    popText.text = "";
    varText.text = "";
    status = Status.Empty;
    improvements = new bool[19];
    for (int i = 0; i < 19; i++) {
      improvements[i] = false;
      Improvements[i].enabled = false;
    }

    pos = position;
    owner = null; // unassigned
    population = 0;
    sector = (pos % 9) / 3 + (pos > 26 ? 3 : 0);
    px = pos % 9;
    py = pos / 9;

    posx = 80 + px * 150 + 20 * (px / 3);
    posy = 735 - py * 115 - (py > 2 ? 55 : 0);
    transform.position = new Vector3(posx, posy, 0);

    SetSprite();
    return this;
  }

  internal void Set(int pop, PlayerStatus own) {
    population = pop;
    popText.text = pop + "M";
    owner = own;
    status = Status.Owned;
    SetSprite();
  }

  private void SetSprite() {
    if (status == Status.Empty) {
      city.sprite = null;
      city.color = transparency;
      radiation.sprite = null;
      radiation.color = transparency;
      popText.text = "";
      varText.text = "";
      for (int i = 0; i < 19; i++) 
        Improvements[i].enabled = false;
      return;
    }
    if (owner == null) {
      GD.DebugLog("Empty owner for an non-empty city!", GD.LT.Error); // FIXME
      return;
    }
    city.color = fullVisible;
    if (settling) {
      if (areWeOver)
        city.sprite = items.GetSprite("Items_128");
      else
        city.sprite = items.GetSprite("Items_127");
      return;
    }
    if (forDenuclearization) {
      if (areWeOver)
        city.sprite = items.GetSprite("Items_105");
      else
        city.sprite = items.GetSprite("Items_" + 9 + 16 * owner.index);
      return;
    }

    // FIXME the improvements should be visible only if the city is ours, or a friend, or we have intelligence perk
    for (int i = 0; i < 19; i++)
      Improvements[i].enabled = improvements[i];

    int s;
    if (population == 0) s = 0;
    else if (population < 5) s = 1;
    else if (population < 10) s = 2;
    else if (population < 25) s = 3;
    else if (population < 50) s = 4;
    else if (population < 100) s = 5;
    else if (population < 250) s = 6;
    else if (population < 500) s = 7;
    else s = 8;
    if (areWeOver) s += 96;
    else s += 16 * owner.index;
    city.sprite = items.GetSprite("Items_" + s);
  }

  public void OnPointerEnter(PointerEventData eventData) {
    areWeOver = true;
    SetSprite();
  }

  public void OnPointerClick(PointerEventData eventData) {
    if (!areWeOver || status == Status.Empty) return;
    areWeOver = false;
    SetSprite();
    // Update the UI with a description of the city (size, owner, radiation)
    string msg = "<b>City</b>\nOwner: <sprite=" + owner.avatar +"> "  + owner.name + "\nPopulation: " + population;
    if (radioactive) msg += "\n<i>Radioactive!</i>";
    // FIXME list city improvements
    GameRenderer.UpdateStatusMessage(msg, city.sprite);
    areWeOver = true;
    SetSprite();
    // We may need to set the target city for the GameRenderer
    GD.selectedCity = pos;

    // Have the owner (if AI) to say something
    if (owner.isAI && !owner.defeated) {
      if (status == Status.Destroyed)
        if (owner.refEnemy != null) owner.refEnemy.Say("This city was mine!", BalloonDirection.TL);
      else
        if (owner.refEnemy != null) owner.refEnemy.Say("This city is mine!", BalloonDirection.TL);
    }
  }

  public void OnPointerExit(PointerEventData eventData) {
    areWeOver = false;
    SetSprite();
  }


  public void Decode(byte[] data, int start, GameEngine engine) {
    pos = data[start];
    long bitfield = System.BitConverter.ToInt64(data, start + 1);
    for (int i = 0; i < improvements.Length; i++)
      improvements[i] = (bitfield & (~(1 << i))) == 1;
    int var = System.BitConverter.ToInt32(data, start + 9);
    AddPopulation(var - population);
    status = (Status)(data[start + 9 + 4] & 3);
    SetRadioactive((data[start + 9 + 4] & 4) == 4);
    int idx = (data[start + 9 + 4] & 248) >> 3;
    owner = engine.players[idx];
  }

  public void SetValues(CityVals cv, PlayerStatus ownerval) {
    status = cv.status;
    owner = ownerval;
    if (population != cv.population) AddPopulation(cv.population - population);
    SetRadioactive(cv.radioactive);
    for (int i = 0; i < improvements.Length; i++)
      improvements[i] = (cv.improvementsBF & (~(1 << i))) == 1;
    SetSprite();
  }

  // ****************************************************************************************************************************************************************
  // ****************************************************************************************************************************************************************
  // *************************************************       RECYCLE                   ******************************************************************************
  // ****************************************************************************************************************************************************************
  // ****************************************************************************************************************************************************************


  public float posx, posy;
  private IEnumerator anim = null;
  public bool settling = false;
  public bool forDenuclearization = false;




  public string GetSpriteName() {
    int s;
    if (population == 0) s = 0;
    else if (population < 5) s = 1;
    else if (population < 10) s = 2;
    else if (population < 20) s = 3;
    else if (population < 40) s = 4;
    else if (population < 60) s = 5;
    else if (population < 80) s = 6;
    else if (population < 100) s = 7;
    else s = 8;
    s += 16 * owner.index;
    return "Items_" + s;
  }

  internal int CalculateDistance(City c) {
    return (int)(3 * Mathf.Sqrt(1.5f * (posx - c.posx) * (posx - c.posx) + 2 * (posy - c.posy) * (posy - c.posy)) / 22);
  }

  public bool IsDestroyed() {
    return status == Status.Destroyed;
  }

  public int GetPopulation() {
    return population;
  }

  public void SetPopulation(int p) {
    population = p;
    popText.text = p + "M";
    SetSprite();
  }

  internal void IncreasePopulation(int increase) {
    AddPopulation(increase);
    if (increase != 0) {
      if (anim != null) StopCoroutine(anim);
      anim = ChangeAnim(increase);
      StartCoroutine(anim);
    }
  }

  IEnumerator ChangeAnim(int val) {
    varText.transform.position = popText.transform.position;
    Vector3 pos = varText.transform.position;
    if (val > 0) {
      varText.text = "<color=#2ED634>+" + val + "</color>";
    }
    else {
      varText.text = "<color=#D6342E>" + val.ToString() + "</color>"; ;
    }
    for (int i = 0; i < 200; i++) {
      pos.y += 0.2f;
      varText.transform.position = pos;
      yield return new WaitForEndOfFrame();
    }
    varText.text = "";
    varText.transform.position = varText.transform.position;
    yield return null;
  }

  internal void AddPopulation(int amount) {
    population += amount;
    popText.text = population + "M";
    if (amount != 0) {
      if (anim != null) StopCoroutine(anim);
      anim = ChangeAnim(amount);
      StartCoroutine(anim);
    }
    SetSprite();
  }



  public bool KillPopulation(float dead, bool radio) {
    if (population - (int)dead < 0) dead = population;
    if (dead != 0) {
      if (anim != null) StopCoroutine(anim);
      anim = ChangeAnim((int)-dead);
      StartCoroutine(anim);
    }
    population -= (int)dead;
    if (population <= 0) {
      radioactive = radio;
      radiation.gameObject.SetActive(radio);
      status = Status.Destroyed;
      population = 0;
      popText.text = "";
      SetSprite();
      return true;
    }
    else
      popText.text = population + "M";
    SetSprite();
    return false;
  }

  public void SetRadioactive(bool rad) {
    radioactive = rad;
    radiation.gameObject.SetActive(rad);
    SetSprite();
  }

  public void Obliterate(bool radioactive) {
    if (population != 0) {
      if (anim != null) StopCoroutine(anim);
      anim = ChangeAnim(-population);
      StartCoroutine(anim);
    }
    population = 0;
    popText.text = "";
    status = Status.Destroyed;
    SetSprite();
    this.radioactive = this.radioactive || radioactive;
    radiation.gameObject.SetActive(radioactive);
  }

  internal Vector3 GetCityPosition() {
    return transform.position;
  }

  internal bool IsInhabitedMoreThanOne(PlayerStatus owner) {
    return (this.owner == owner && population > 1 && !radioactive);
  }

  internal bool IsInhabited(PlayerStatus owner) {
    return (this.owner == owner && population > 0 && !radioactive);
  }

  internal City PossibleSettling(bool set) {
    settling = set;
    SetSprite();
    return this;
  }

  internal void MakeAbitable(int pop, PlayerStatus own) {
    if (pop == 0) return;
    if (pop < 0) pop = -pop;
    owner = own;
    status = Status.Owned;
    radioactive = false;
    settling = false;
    SetSprite();
    AddPopulation(pop);
  }

  internal void SetDenuclearization(bool v) {
    forDenuclearization = v;
    SetSprite();
  }

  internal void Assign(PlayerStatus owner) {
    radioactive = false;
    this.owner = owner;
    radiation.gameObject.SetActive(false);
    forDenuclearization = false;
    SetSprite();
  }

  internal void ResetCity(PlayerStatus owner) {
    radioactive = false;
    this.owner = owner;
    if (population < 0) population = 0;
    radiation.gameObject.SetActive(false);
    forDenuclearization = false;
    SetSprite();
  }

  internal void SetFontSize(int size) {
    popText.fontSize = size;
    varText.fontSize = size;
  }

  internal bool IsValid(int player) {
    return owner.index == player && status == Status.Owned && population > 0;
  }

  internal int Owned(int player) {
    if (owner.index == player && status == Status.Owned && population > 0) return 1;
    return 0;
  }
  internal int NotMeAsOwner(int player) {
    if (owner.index != player && owner.index != -1 && status == Status.Owned && population > 0) return owner.index;
    return -1;
  }
}
