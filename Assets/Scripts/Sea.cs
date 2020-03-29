using UnityEngine;

public class Sea : MonoBehaviour {
  float time = 0;
  Vector3 pos = new Vector3(0, 540, 0);
  public float sign = 1;
  public float scale = 1;

  private void Update() {
    time += Time.deltaTime;
    if (time > 10 * scale) time -= 10 * scale;
    float angle1 = Mathf.PI * .2f * time / scale;
    float angle2 = Mathf.PI * .2f * time / scale;
    float angle3 = Mathf.PI * .2f * time / scale;
    float angle4 = Mathf.PI * .2f * time / scale;
    float angle5 = Mathf.PI * .2f * time / scale;

    pos.x = -55 + sign * Mathf.Cos(angle1) * 25 + sign * Mathf.Cos(angle2) * 4 + sign * Mathf.Cos(angle3*2) * 8 + sign * Mathf.Sin(angle4*3) * 2 - sign * Mathf.Sin(angle5) * 3;
    pos.y = -55 + sign * Mathf.Sin(angle1) * 25 + sign * Mathf.Sin(-angle2) * 4 + sign * Mathf.Sin(angle3*2) * 2 + sign * Mathf.Cos(angle4*3) * 8 + sign * Mathf.Cos(angle5) * 3;
    transform.position = pos;
  }

}
