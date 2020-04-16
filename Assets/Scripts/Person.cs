using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Person : MonoBehaviour {
  bool isFemale;

  public Image Body;
  public Image Clothes;
  public Animator BodyAnim;
  public Animator ClothesAnim;

  private Vector3 srcPosition;
  private Vector3 dstPosition;
  private Color srcColor;
  private Color dstColor;
  private int val1;
  private int val2;
  private float dist;
  private float time;
  private System.Action<int, int> callBack;
  private Vector3 normalScale = new Vector3(1, 1, 1);
  private Vector3 flippedScale = new Vector3(-1, 1, 1);

  public void WalkTo(Vector3 start, Vector3 dest, Color startColor, Color endColor, int val1, int val2, System.Action<int, int> cb) {
    isFemale = Random.Range(0, 4) != 0;
    dist = Vector3.Distance(start, dest);
    time = 0;
    srcPosition = start;
    dstPosition = dest;
    srcColor = startColor;
    dstColor = endColor;
    this.val1 = val1;
    this.val2 = val2;
    callBack = cb;
    Clothes.color = srcColor;
    StartCoroutine(WalkTo());
  }

  // FIXME we still need to do the radial go out change color, and go back in
  // FIMXE alter the color in a way that they change on the middle


  public void WalkAround(Vector3 position, Color color) {
    isFemale = Random.Range(0, 4) != 0;
    time = 0;
    srcPosition = position;
    srcColor = color;
    Clothes.color = color;
    StartCoroutine(WalkAround());
  }

  IEnumerator WalkTo() {
    while (time < dist) {
      float perc = 1 - time / dist;
      Vector3 pos = srcPosition * perc + dstPosition * (1 - perc);
      Color color = new Color32(
        (byte)(255 * (srcColor.r * perc + dstColor.r * (1 - perc))),
        (byte)(255 * (srcColor.g * perc + dstColor.g * (1 - perc))),
        (byte)(255 * (srcColor.b * perc + dstColor.b * (1 - perc))),
        (byte)(255 * (srcColor.a * perc + dstColor.a * (1 - perc))));
      Clothes.color = color;
      SetDirection(transform.position, pos);
      transform.position = pos;
      time += Time.deltaTime * 125;
      yield return null;
    }
    callBack?.Invoke(val1, val2);
    GameObject.Destroy(gameObject);
  }

  IEnumerator WalkAround() {
    while (time < 2.5f) {
      Vector3 pos = srcPosition + 45 * (Vector3.up * Mathf.Cos(time * Mathf.PI * 2 / 2.5f) + Vector3.right * Mathf.Sin(time * Mathf.PI * 2 / 2.5f)) + Vector3.up * 15;
      SetDirection(transform.position, pos);
      transform.position = pos;
      time += Time.deltaTime;
      yield return null;
    }
    GameObject.Destroy(gameObject);
  }

  private enum Dir {  None, T, TL, L, BL, B, BR, R, TR };
  private Dir dir = Dir.None;

  private void SetDirection(Vector3 src, Vector3 dst) {
    float angle = Mathf.Atan2(src.y - dst.y, src.x - dst.x) * Mathf.Rad2Deg;

    // 360 / 8  = 45
    if (angle < -22.5f) angle += 360f;
    if (angle > 360f) angle -= 360f;

    Dir d = Dir.T;
    if (-22.5f < angle && angle <= 22.5f) { // L
      d = Dir.L;
    }
    else if (22.5f < angle && angle <= 45f + 22.5f) { // BL
      d = Dir.BL;
    }
    else if (45f + 22.5f < angle && angle <= 2 * 45f + 22.5f) { // B
      d = Dir.B;
    }
    else if (2 * 45f + 22.5f < angle && angle <= 3 * 45f + 22.5f) { // BR
      d = Dir.BR;
    }
    else if (3 * 45f + 22.5f < angle && angle <= 4 * 45f + 22.5f) { // R
      d = Dir.R;
    }
    else if (4 * 45f + 22.5f < angle && angle <= 5 * 45f + 22.5f) { // TR
      d = Dir.TR;
    }
    else if (5 * 45f + 22.5f < angle && angle <= 6 * 45f + 22.5f) { // T
      d = Dir.T;
    }
    else if (6 * 45f + 22.5f < angle && angle <= 7 * 45f + 22.5f) { // TL
      d = Dir.TL;
    }
    else if (7 * 45f + 22.5f < angle && angle <= 8 * 45f + 22.5f) { // L (but should not happen)
      d = Dir.L;
    }

    if (d == dir) return;
    dir = d;

    string sex = isFemale ? "Female" : "Male";

    if (d == Dir.T) { // TOP
      BodyAnim.Play(sex + "WalkT");
      ClothesAnim.Play(sex + "ClothesT");
      transform.localScale = normalScale;
    }
    else if (d == Dir.TR) { // TR
      BodyAnim.Play(sex + "WalkTL");
      ClothesAnim.Play(sex + "ClothesTL");
      transform.localScale = flippedScale;
    }
    else if (d == Dir.R) { // R
      BodyAnim.Play(sex + "WalkL");
      ClothesAnim.Play(sex + "ClothesL");
      transform.localScale = flippedScale;
    }
    else if (d == Dir.BR) { // BR
      BodyAnim.Play(sex + "WalkBL");
      ClothesAnim.Play(sex + "ClothesBL");
      transform.localScale = flippedScale;
    }
    else if (d == Dir.B) { // B
      BodyAnim.Play(sex + "WalkB");
      ClothesAnim.Play(sex + "ClothesB");
      transform.localScale = normalScale;
    }
    else if (d == Dir.BL) { // BL
      BodyAnim.Play(sex + "WalkBL");
      ClothesAnim.Play(sex + "ClothesBL");
      transform.localScale = normalScale;
    }
    else if (d == Dir.L) { // L
      BodyAnim.Play(sex + "WalkL");
      ClothesAnim.Play(sex + "ClothesL");
      transform.localScale = normalScale;
    }
    else if (d == Dir.TL) { // TL
      BodyAnim.Play(sex + "WalkTL");
      ClothesAnim.Play(sex + "ClothesTL");
      transform.localScale = normalScale;
    }
    else if (d == Dir.T) { // T (but should not happen)
      BodyAnim.Play(sex + "WalkT");
      ClothesAnim.Play(sex + "ClothesT");
      transform.localScale = normalScale;
    }
  }
}
