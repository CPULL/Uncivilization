using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;


public class Character : MonoBehaviour { // FIXME remove the whole class, after recycling the parts
  public Mood mood = Mood.Normal;
  public string charName;

  public GameObject eyesBase;
  public GameObject mouthBase;
  public GameObject crossEyesBaseL;
  public GameObject crossEyesBaseR;

  public SpriteAtlas saparts;
  public bool defeated = false;

  // AI
  public CharType charType;
  public List<int> friends = new List<int>();
  public List<int> foes = new List<int>();
  public WeaponType preferredWeapon = WeaponType.None;
  public WeaponType preferredDelivery = WeaponType.None;
  public int social = 0;
  public int tech = 0;
  public int forget = 0;




}

// 0 face
// 1 mouth flat (0)
// 2 mouth sad  (1)
// 3 mouth happy(2)
// 4 mouth open (3)
// 5 mouth grind (4)
// 6 eyes normal  (0)
// 7 eyes up      (1)
// 8 eyes down    (2)
// 9 eyes semiclosed (3)
// 10 eyes closed  (4)
// 11 eyes closed down (5)
