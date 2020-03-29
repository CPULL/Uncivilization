using UnityEngine;

public class World : MonoBehaviour {
  float t = 0;


  void Update() {
    if (GD.IsStatus(LoaderStatus.StartGame, LoaderStatus.GameStarted)) return;
    t += Time.deltaTime;
    transform.rotation = Quaternion.Euler(0, (90 * t) % 360, 0);
  }
}
