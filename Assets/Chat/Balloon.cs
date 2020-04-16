using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Balloon : MonoBehaviour {
  private GameObject tl;
  private GameObject tr;
  private GameObject bl;
  private GameObject br;

  private Image ContentTL;
  private RectTransform ContentTLrt;
  private Image ContentTR;
  private RectTransform ContentTRrt;
  private Image ContentBL;
  private RectTransform ContentBLrt;
  private Image ContentBR;
  private RectTransform ContentBRrt;

  private TextMeshProUGUI WordsTL;
  private RectTransform WordsTLrt;
  private TextMeshProUGUI WordsTR;
  private RectTransform WordsTRrt;
  private TextMeshProUGUI WordsBL;
  private RectTransform WordsBLrt;
  private TextMeshProUGUI WordsBR;
  private RectTransform WordsBRrt;

  float toWait = 0;
  private string textToShow = "";


  private void Awake() {
    Transform tmp = transform.Find("TL");
    tl = tmp.gameObject;
    ContentTL = tmp.Find("Content").GetComponent<Image>();
    ContentTLrt = ContentTL.GetComponent<RectTransform>();
    WordsTL = tmp.Find("Words").GetComponent<TextMeshProUGUI>();
    WordsTLrt = WordsTL.GetComponent<RectTransform>();
    WordsTL.text = "";

    tmp = transform.Find("TR");
    tr = tmp.gameObject;
    ContentTR = tmp.Find("Content").GetComponent<Image>();
    ContentTRrt = ContentTR.GetComponent<RectTransform>();
    WordsTR = tmp.Find("Words").GetComponent<TextMeshProUGUI>();
    WordsTRrt = WordsTR.GetComponent<RectTransform>();
    WordsTR.text = "";

    tmp = transform.Find("BL");
    bl = tmp.gameObject;
    ContentBL = tmp.Find("Content").GetComponent<Image>();
    ContentBLrt = ContentBL.GetComponent<RectTransform>();
    WordsBL = tmp.Find("Words").GetComponent<TextMeshProUGUI>();
    WordsBLrt = WordsBL.GetComponent<RectTransform>();
    WordsBL.text = "";

    tmp = transform.Find("BR");
    br = tmp.gameObject;
    ContentBR = tmp.Find("Content").GetComponent<Image>();
    ContentBRrt = ContentBR.GetComponent<RectTransform>();
    WordsBR = tmp.Find("Words").GetComponent<TextMeshProUGUI>();
    WordsBRrt = WordsBR.GetComponent<RectTransform>();
    WordsBR.text = "";

    // 210,80 min for content
    ContentTLrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 210);
    ContentTLrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80);
    ContentBLrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 210);
    ContentBLrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80);
    ContentTRrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 210);
    ContentTRrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80);
    ContentBRrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 210);
    ContentBRrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 80);
    // 170,40 min for text
    WordsTLrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 170);
    WordsTLrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 40);
    WordsBLrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 170);
    WordsBLrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 40);
    WordsTRrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 170);
    WordsTRrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 40);
    WordsBRrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 170);
    WordsBRrt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 40);

    tl.gameObject.SetActive(false);
    tr.gameObject.SetActive(false);
    bl.gameObject.SetActive(false);
    br.gameObject.SetActive(false);
  }

  internal void Say(string text, Transform speaker, Vector3 position, BalloonDirection dir) {
    StartCoroutine(WaitCompletion(text, speaker, position, dir));
  }

  internal void Hide() {
    StopAllCoroutines();
    tl.gameObject.SetActive(false);
    tr.gameObject.SetActive(false);
    bl.gameObject.SetActive(false);
    br.gameObject.SetActive(false);
    typing = false;
    typingCoroutine = null;
  }

  bool typing = false;
  Coroutine typingCoroutine = null;
  IEnumerator WaitCompletion(string text, Transform speaker, Vector3 position, BalloonDirection dir) {
    while (typing) {
      yield return new WaitForSeconds(1.5f);
    }
    tl.gameObject.SetActive(dir == BalloonDirection.TL);
    tr.gameObject.SetActive(dir == BalloonDirection.TR);
    bl.gameObject.SetActive(dir == BalloonDirection.BL);
    br.gameObject.SetActive(dir == BalloonDirection.BR);

    int numLines = 1;
    foreach (char c in text)
      if (c == '\n')
        numLines++;

    RectTransform ContentRT = null;
    TextMeshProUGUI Words = null;
    RectTransform WordsRT = null;
    switch (dir) {
      case BalloonDirection.TL:
        ContentRT = ContentTLrt;
        Words = WordsTL;
        WordsRT = WordsTLrt;
        break;

      case BalloonDirection.TR:
        ContentRT = ContentTRrt;
        Words = WordsTR;
        WordsRT = WordsTRrt;
        break;

      case BalloonDirection.BL:
        ContentRT = ContentBLrt;
        Words = WordsBL;
        WordsRT = WordsBLrt;
        break;

      case BalloonDirection.BR:
        ContentRT = ContentBRrt;
        Words = WordsBR;
        WordsRT = WordsBRrt;
        break;
    }
    transform.position = position;
    previousPosition = speaker.position;

    if (typingCoroutine != null) StopCoroutine(typingCoroutine);
    typingCoroutine = StartCoroutine(TypeText(text, Words, WordsRT, ContentRT, speaker));
  }

  private Vector3 previousPosition = Vector3.zero;
  IEnumerator TypeText(string msg, TextMeshProUGUI Words, RectTransform WordsRT, RectTransform ContentRT, Transform speaker) {
    typing = true;
    textToShow = msg;
    Words.text = "";
    Vector2 size = Words.GetPreferredValues(msg);
    if (size.x < 170) size.x = 170;
    if (size.y < 40) size.y = 40;
    WordsRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
    WordsRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
    ContentRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x + 40);
    ContentRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y + 40);
    toWait = msg.Length * .01f + 2f;

    while (!string.IsNullOrWhiteSpace(textToShow)) {
      yield return new WaitForSeconds(.005f);
      Words.text += textToShow[0];
      textToShow = textToShow.Substring(1);
      // Re-align if the speaker position changed
      if (previousPosition != speaker.position) {
        transform.position += (speaker.position - previousPosition);
        previousPosition = speaker.position;
      }
    }
    // Wait the time to wait, but check the position of the speaker
    float waitTime = toWait;
    while (waitTime > 0) {
      if (previousPosition != speaker.position) {
        transform.position += (speaker.position - previousPosition);
        previousPosition = speaker.position;
      }
      waitTime -= 0.25f;
      yield return new WaitForSeconds(.25f);
    }

    // Remove the balloon (only if we do not have other messages to print, or the starting of the coroutine will fail)
    tl.gameObject.SetActive(false);
    tr.gameObject.SetActive(false);
    bl.gameObject.SetActive(false);
    br.gameObject.SetActive(false);
    typing = false;
  }
}
