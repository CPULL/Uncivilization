﻿using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.U2D;
using UnityEngine.UI;

public enum CityStatus { 
  Empty = 0, // Spot is not used
  Owned = 1, // The city is owned by somebody and not destroyed
  Radioactive = 2, // Owned by somebody and radioactive
  Destroyed = 3, // Crater, still owned, but can be reconquered
  RadioWaste = 4, // Destroyed and radioactive
};

public enum CityHighlight { 
  Owned, // Full color and selectable if the player is the owner
  Settling, // Full color and selectable only on the areas where is possible to settle
  Denucrearise, // Full color and selectable only on the areas where is possible to denuclearize
  Blasting, // Full color and selectable only on the areas that is pobbile to blast (maybe increase for the range of weapons)
  None 
};

// MIX and match these two. If needed add other values


public class City : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, IPointerExitHandler {
  public Image city;
  public Image radiation;
  public TextMeshProUGUI popText;
  public TextMeshProUGUI varText;
  public SpriteAtlas items;
  public Image[] Improvements;

  public CityVals vals;

  public bool[] improvements;
  public CityHighlight highlight;
  private bool areWeOver = false;

  private Color transparency = new Color32(0, 0, 0, 0);
  private Color fullVisible = new Color32(255, 255, 255, 255);
  private Color halfVisible = new Color32(100, 100, 100, 200);

  public Action<object, EventArgs> OnClick { get; internal set; }

  public City Init(CityVals src) {
    radiation.gameObject.SetActive(false);
    city.gameObject.SetActive(true); // FIXME set to false, but use it to debug the positions
    popText.text = "";
    varText.text = "";
    improvements = new bool[19];
    for (int i = 0; i < 19; i++) {
      improvements[i] = false;
      Improvements[i].enabled = false;
    }

    vals = src;
    int px = src.pos % 9;
    int py = src.pos / 9;
    posx = 80 + px * 150 + 20 * (px / 3);
    posy = 735 - py * 115 - (py > 2 ? 55 : 0);
    transform.position = new Vector3(posx, posy, 0);

    SetSprite();
    return this;
  }

  internal void Set() {
    popText.text = vals.population + "M";
    vals.status = CityStatus.Owned;
    SetSprite();
  }

  private void SetSprite() {
    // Empty -> never shown unless is Settling
    if (vals.status == CityStatus.Empty) {
      if (highlight == CityHighlight.Settling) {
        city.sprite = areWeOver ?  items.GetSprite("Items_128") : items.GetSprite("Items_127");
        city.color = fullVisible;
      }
      else {
        city.sprite = null;
        city.color = transparency;
      }
      radiation.gameObject.SetActive(false);
      popText.text = "";
      varText.text = "";
      for (int i = 0; i < 19; i++)
        Improvements[i].enabled = false;
      return;
    }

    // Owned and not radioactive, but the border will depend on the highlightstatus
    if (vals.status == CityStatus.Owned || vals.status == CityStatus.Radioactive) {
      radiation.gameObject.SetActive(vals.status == CityStatus.Radioactive);
      switch (highlight) {
        case CityHighlight.Owned: // Selectable if the player is the owner, half color if not the owner
          if (vals.owner == GD.localPlayerIndex) {
            city.color = fullVisible;
            for (int i = 0; i < 19; i++)
              Improvements[i].enabled = improvements[i];
            city.sprite = items.GetSprite("Items_" + CalculateSpriteOnPopulation());
          }
          else {
            city.color = halfVisible;
            for (int i = 0; i < 19; i++)
              Improvements[i].enabled = improvements[i] && false; // FIXME understand if we have the technology for intellicence
            city.sprite = items.GetSprite("Items_" + CalculateSpriteOnPopulation());
          }
          break;

        case CityHighlight.Settling: // FIXME
          break;
        case CityHighlight.Denucrearise: // FIXME
          break;
        case CityHighlight.Blasting: // FIXME
          break;
        case CityHighlight.None: // FIXME
          city.color = fullVisible;
          for (int i = 0; i < 19; i++)
            Improvements[i].enabled = improvements[i] && true; // FIXME understand if we have the technology for intellicence
          city.sprite = items.GetSprite("Items_" + CalculateSpriteOnPopulation());
          break;
      }
    }

    if (vals.status == CityStatus.Destroyed || vals.status == CityStatus.RadioWaste) {
      radiation.gameObject.SetActive(vals.status == CityStatus.RadioWaste);
      if (highlight == CityHighlight.Denucrearise) {
        if (areWeOver)
          city.sprite = items.GetSprite("Items_105"); // Flag over crater
        else
          city.sprite = items.GetSprite("Items_" + 9 + 16 * vals.owner); // Flag over crater with owner color
      }
      else
        city.sprite = items.GetSprite("Items_" + CalculateSpriteOnPopulation());
    }
  }

  private int CalculateSpriteOnPopulation() {
    int s;
    if (vals.population == 0) s = 0;
    else if (vals.population < 5) s = 1;
    else if (vals.population < 10) s = 2;
    else if (vals.population < 25) s = 3;
    else if (vals.population < 50) s = 4;
    else if (vals.population < 100) s = 5;
    else if (vals.population < 250) s = 6;
    else if (vals.population < 500) s = 7;
    else s = 8;
    if (areWeOver) s += 96;
    else s += 16 * vals.owner;
    return s;
  }

  public void OnPointerEnter(PointerEventData eventData) {
    // Depending on the action we may want to highlight the city or not
    PlayerAction action = GameRenderer.GetAction();
    if (action == PlayerAction.SettleNewCity) { // Highlight only empty areas that are close to a city of ours
      // FIXME
    }
    else if (action == PlayerAction.Denuclearize) { // Highlight only if city not empty and readioactive and ours, or any empty radioactive spot
      // FIXME
    }
    else if (action == PlayerAction.BuildCityImprovements) { // Highlight only if the city is ours and the improvement (if selected) is not yet in the city. On click select the city for the improvement
      if ((vals.status != CityStatus.Owned && vals.status != CityStatus.Radioactive) || vals.owner != GD.localPlayerIndex) return;
      areWeOver = true;
      SetSprite();
    }
    else { // Action not selected: highlight, on click base details. More info only if we have intelligence
      areWeOver = true;
      SetSprite();
    }
  }

  public void OnPointerClick(PointerEventData eventData) {
    if (!areWeOver || vals.status == CityStatus.Empty) return;
    areWeOver = false;
    SetSprite();

    // Send a message to the Renderer to handle what to do
    OnClick?.Invoke(this, null);
    areWeOver = true;
    SetSprite();
  }

  public void OnPointerExit(PointerEventData eventData) {
    areWeOver = false;
    SetSprite();
  }


  public void SetValues(CityVals cv) {
    vals.status = cv.status;
    vals.owner = cv.owner;
    if (vals.population != cv.population) AddPopulation(cv.population - vals.population);
    vals.improvementsBF |= cv.improvementsBF;
    for (int i = 0; i < improvements.Length; i++)
      improvements[i] = (cv.improvementsBF & (1 << i)) != 0;
    SetSprite();
  }


  public void Highlight(CityHighlight hlg) {
    highlight = hlg;
    SetSprite();
  }

  // ****************************************************************************************************************************************************************
  // ****************************************************************************************************************************************************************
  // *************************************************       RECYCLE                   ******************************************************************************
  // ****************************************************************************************************************************************************************
  // ****************************************************************************************************************************************************************


  public float posx, posy;
  private IEnumerator anim = null;

  public string GetSpriteName() {
    int s;
    if (vals.population == 0) s = 0;
    else if (vals.population < 5) s = 1;
    else if (vals.population < 10) s = 2;
    else if (vals.population < 20) s = 3;
    else if (vals.population < 40) s = 4;
    else if (vals.population < 60) s = 5;
    else if (vals.population < 80) s = 6;
    else if (vals.population < 100) s = 7;
    else s = 8;
    s += 16 * vals.owner;
    return "Items_" + s;
  }

  internal int CalculateDistance(City c) {
    return (int)(3 * Mathf.Sqrt(1.5f * (posx - c.posx) * (posx - c.posx) + 2 * (posy - c.posy) * (posy - c.posy)) / 22);
  }

  public bool IsDestroyed() {
    return vals.status == CityStatus.Destroyed || vals.status == CityStatus.RadioWaste;
  }

  public int GetPopulation() {
    return vals.population;
  }

  public void SetPopulation(int p) {
    vals.population = p;
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
    vals.population += amount;
    popText.text = vals.population + "M";
    if (amount != 0) {
      if (anim != null) StopCoroutine(anim);
      anim = ChangeAnim(amount);
      StartCoroutine(anim);
    }
    SetSprite();
  }



  public bool KillPopulation(float dead, bool radio) {
    if (vals.population - (int)dead < 0) dead = vals.population;
    if (dead != 0) {
      if (anim != null) StopCoroutine(anim);
      anim = ChangeAnim((int)-dead);
      StartCoroutine(anim);
    }
    vals.population -= (int)dead;
    if (vals.population <= 0) {
      radiation.gameObject.SetActive(radio);
      vals.status = radio ? CityStatus.RadioWaste : CityStatus.Destroyed;
      vals.population = 0;
      popText.text = "";
      SetSprite();
      return true;
    }
    else
      popText.text = vals.population + "M";
    SetSprite();
    return false;
  }

  public void Obliterate(bool radioactive) {
    if (vals.population != 0) {
      if (anim != null) StopCoroutine(anim);
      anim = ChangeAnim(-vals.population);
      StartCoroutine(anim);
    }
    vals.population = 0;
    popText.text = "";
    vals.status = radioactive ? CityStatus.RadioWaste : CityStatus.Destroyed;
    SetSprite();
    radiation.gameObject.SetActive(radioactive);
  }

  internal bool IsInhabitedMoreThanOne(byte owner) {
    return (vals.owner == owner && vals.population > 1 && vals.status != CityStatus.Radioactive);
  }

  internal bool IsInhabited(byte owner) {
    return (vals.owner == owner && vals.population > 0 && vals.status != CityStatus.Radioactive);
  }

  internal void MakeAbitable(int pop, byte owner) {
    if (pop == 0) return;
    if (pop < 0) pop = -pop;
    vals.owner = owner;
    vals.status = CityStatus.Owned;
    SetSprite();
    AddPopulation(pop);
  }


  internal void Assign(byte owner) {
    vals.status = CityStatus.Owned;
    vals.owner = owner;
    radiation.gameObject.SetActive(false);
    SetSprite();
  }

  internal void ResetCity(byte owner) {
    vals.status = CityStatus.Owned;
    vals.owner = owner;
    if (vals.population < 0) vals.population = 0;
    radiation.gameObject.SetActive(false);
    SetSprite();
  }

  internal void SetFontSize(int size) {
    popText.fontSize = size;
    varText.fontSize = size;
  }

  internal bool IsValid(byte owner) {
    return vals.owner == owner && vals.status == CityStatus.Owned && vals.population > 0;
  }

  internal int Owned(byte owner) {
    if (vals.owner == owner && vals.status == CityStatus.Owned && vals.population > 0) return 1;
    return 0;
  }
  internal byte NotMeAsOwner(byte owner) {
    if (vals.owner != owner && vals.status == CityStatus.Owned && vals.population > 0) return owner;
    return 0;
  }

}
