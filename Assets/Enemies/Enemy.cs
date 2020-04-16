using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

public class Enemy : MonoBehaviour {

  // Split properties used to run the game from the ones required to represent the gameobject

  [SerializeField]
  public Player player;

    // ********************** gobject **********************  
  public SpriteAtlas sprites;
  private Balloon balloon;
  public GameObject BalloonTemplate;
  private Sprite[] Eyes;
  private Sprite[] Mouths;
  private Image DeadEyeL;
  private Image DeadEyeR;
  private Image Mouth;
  private Image Eye;
  private TextMeshProUGUI NameTxt;
  private Mood mood = Mood.Normal;
  private int eyes = 0;
  private int mouth = 0;
  private float blink = 0;
  private float timeOut;
  private float moodTimeout;

  public System.EventHandler<AlterPlayerEvent> OnAlterPlayer;
  public class AlterPlayerEvent : System.EventArgs {
    public Enemy enemy;
  }

  public void Destroy() {
    if (balloon != null) GameObject.Destroy(balloon.gameObject);
    GameObject.Destroy(gameObject);
  }

  public void SetEnemyAsPlayer(string name, byte avatar, ulong id) { // FIXME this may be no more necessary
    if (player == null) {
      player = new Player(name, avatar);
      player.def.id = id;
    }
    NameTxt = transform.Find("Name").GetComponent<TextMeshProUGUI>();
    NameTxt.text = name;
    transform.Find("Face").GetComponent<Image>().sprite = GD.instance.AvatarsList[avatar];
    transform.Find("DeadEyeL").GetComponent<Image>().enabled = false;
    transform.Find("DeadEyeR").GetComponent<Image>().enabled = false;
    transform.Find("Mouth").GetComponent<Image>().enabled = false;
    transform.Find("Eyes").GetComponent<Image>().enabled = false;
    player.def.type = PlayerDef.Type.Human;
    player.color = new Color32(0, 0, 0, 255);
  }

  private void Awake() {
    if (player.def.type == PlayerDef.Type.AI) {
      NameTxt = transform.Find("Name").GetComponent<TextMeshProUGUI>();
      NameTxt.text = player.def.name;
      Mouths = new Sprite[5];
      for (int i = 1; i < 6; i++)
        Mouths[i - 1] = sprites.GetSprite(player.def.name + "_" + i);
      Eyes = new Sprite[6];
      for (int i = 6; i < 12; i++)
        Eyes[i - 6] = sprites.GetSprite(player.def.name + "_" + i);
      DeadEyeL = transform.Find("DeadEyeL").GetComponent<Image>();
      DeadEyeR = transform.Find("DeadEyeR").GetComponent<Image>();
      Mouth = transform.Find("Mouth").GetComponent<Image>();
      Eye = transform.Find("Eyes").GetComponent<Image>();
      transform.Find("Face").GetComponent<Image>().sprite = sprites.GetSprite(player.def.name + "_0");
      DeadEyeL.enabled = false;
      DeadEyeR.enabled = false;
      SetMood(Mood.Normal, true);
    }
  }

  private void Update() {
    if (player.def.type == PlayerDef.Type.Defeated || player.def.type != PlayerDef.Type.AI) return;
    if (mood == Mood.Normal) {
      if (Random.Range(0, 1000) == 0) {
        Mouth.sprite = Mouths[Random.Range(0, 3)];
      }
      if (blink == 0) {
        if (Random.Range(0, 250) == 0)
          blink += Time.deltaTime;
      }
      else {
        blink += Time.deltaTime;
        if (blink > 0.40f) {
          Eye.sprite = Eyes[0];
          blink = 0;
        }
        else if (blink > 0.30f)
          Eye.sprite = Eyes[3];
        else if (blink > 0.20f)
          Eye.sprite = Eyes[4];
        else if (blink > 0.10f)
          Eye.sprite = Eyes[3];
      }
    }
    else {
      timeOut += Time.deltaTime;
      if (timeOut > moodTimeout) {
        SetMood(Mood.Normal, true);
      }
    }
  }

  public void SetMood(Mood m, bool silent = false, BalloonDirection dir = BalloonDirection.TL) {
    if (player.def.type == PlayerDef.Type.Defeated || player.def.type != PlayerDef.Type.AI) return;
    mood = m;
    eyes = ((int)m) % 6;
    mouth = ((int)m) / 6;
    SetExpression(eyes, mouth);

    timeOut = 0;
    moodTimeout = Random.Range(5, 10);

    if (!silent) {
      Say(GetIntroMessage(), dir);
    }
  }

  public void SetExpression(int e, int m) {
    if (player.def.type == PlayerDef.Type.Defeated || player.def.type != PlayerDef.Type.AI) return;
    eyes = e;
    mouth = m;
    Eye.sprite = Eyes[eyes];
    Mouth.sprite = Mouths[mouth];
  }

  public void Defeat() {
    if (player.def.type == PlayerDef.Type.AI) {
      SetMood(Mood.FeelingPain, true);
      DeadEyeL.enabled = true;
      DeadEyeR.enabled = true;
    }
    else {
      // FIXME do something to show a defeated human player
      DeadEyeL.enabled = true;
      DeadEyeR.enabled = false;
    }
    player.def.type = PlayerDef.Type.Defeated;
  }


  public void OnClick() {
    if (player.def.type != PlayerDef.Type.AI && ChatManager.instance != null && GD.thePlayer != null && GD.thePlayer.ID != player.def.id)
      ChatManager.instance.StartChatWith(player.def.id, player.def.name, player.def.avatar);
    OnAlterPlayer?.Invoke(this, new AlterPlayerEvent { enemy = this });
  }

  Coroutine sayLaterCoroutine = null;

  public void SayLater(int pos) {
    if (balloon == null) { // Check if we have a balloon, if not instantiate one
      balloon = Instantiate(BalloonTemplate, BalloonTemplate.transform.parent).GetComponent<Balloon>();
    }

    if (sayLaterCoroutine != null) StopCoroutine(sayLaterCoroutine);
    sayLaterCoroutine = StartCoroutine(SayLater(pos, GetIntroMessage(), pos % 2 == 0 ? BalloonDirection.TR : BalloonDirection.BR));
  }

  IEnumerator SayLater(int pos, string text, BalloonDirection dir) {
    yield return new WaitForSeconds(pos + Random.Range(.5f, 1.5f));
    Say(text, dir);
    sayLaterCoroutine = null;
    yield return null;
  }


  public void Say(string text, BalloonDirection dir) {
    if (balloon == null) { // Check if we have a balloon, if not instantiate one
      balloon = Instantiate(BalloonTemplate, BalloonTemplate.transform.parent).GetComponent<Balloon>();
    }

    Vector3[] corners = new Vector3[4];
    gameObject.GetComponent<RectTransform>().GetWorldCorners(corners);
    Vector3 center = Vector3.zero;
    float minx = corners[0].x;
    if (corners[0].x > corners[1].x) minx = corners[1].x;
    if (corners[1].x > corners[2].x) minx = corners[2].x;
    float maxx = corners[0].x;
    if (corners[0].x < corners[1].x) maxx = corners[1].x;
    if (corners[1].x < corners[2].x) maxx = corners[2].x;
    float miny = corners[0].y;
    if (corners[0].y > corners[1].y) miny = corners[1].y;
    if (corners[1].y > corners[2].y) miny = corners[2].y;
    float maxy = corners[0].y;
    if (corners[0].y < corners[1].y) maxy = corners[1].y;
    if (corners[1].y < corners[2].y) maxy = corners[2].y;

    center.x = (maxx + minx) / 2;
    center.y = (maxy + miny) / 2;
    balloon.Say(text, transform, center, dir);
  }

  public void HideBalloon() {
    if (balloon == null) return;
    if (sayLaterCoroutine != null) StopCoroutine(sayLaterCoroutine);
    sayLaterCoroutine = null;
    balloon.Hide();
  }

  internal static string GetIntroMessage() {
    return introMsgs[Random.Range(0, introMsgs.Length)];
  }

  private static readonly string[] introMsgs = {
    "I will kill you",
    "You have no chances\nagainst me",
    "The world is mine",
    "I am the\nsupreme leader",
    "Expect my nukes soon",
    "I will turn you\nto ashes",
    "I am hungry",
    "I am angry",
    "Wait for my attack",
    "I am beautiful",
    "Nobody can compare\nwith myself",
    "You will be sorry",
    "My arsenal is huge",
    "Make war, not love",
    "I will be in your dreams,\nand they will be nightmares",
    "I will ate all\nremaining Pandas",
    "You will be soon\nobliterated",
    "Resistance is futile!",
    "I will make my\ncountry great again!"
  };

  public static readonly string[] reactionMsgs = {
    "That hurts",
    "You will pay the consequences of your insolence!",
    "Expect my harsh reaction...",
    "This was the city where I was born!",
    "I am already preparing my counter-attack",
    "You have no pity, and I have less than you for your kind",
    "This was just like fireworks. Is it holiday in your contry?",
    "You barely scratched my contry",
    "You are dead!",
    "My revenge will be served cold, warm, and room temperature...",
    "My wrath is upon you!!!"
  };

  public static readonly string[] happyMsgs = {
    "I told you",
    "My country is way better than yours",
    "People live better in my country!",
    "A failed propaganda attempt?",
    "Your lies are uncovered",
    "I have many homes for these people",
    "Are you sad?"
  };

  public static readonly string[] notgoodMsgs = {
    "Next time it will work better!",
    "How you tricked me?",
    "This is inadmissible!",
    "What went wrong?",
    "I was not expecting that",
    "Was just bad luck"
  };

  public static readonly string[] defenseMsgs = {
    "You can do nothing against me!",
    "My defenses are impenetrable!",
    "Have you paid enough your technicians?",
    "You should use better materials...",
    "Do not buy cheap components next time",
    "You really think you can hit me?"
  };

  public static readonly string[] expandMsgs = {
    "My empire is expanding!",
    "A new home is built!",
    "Home sweet home",
    "I will build a casino and a resort in this new location",
    "Prepare to be attacked from this new area",
    "I know that the food here will be the best food in the world"
  };

}
