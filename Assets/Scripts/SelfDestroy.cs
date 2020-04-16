using UnityEngine;

public class SelfDestroy : MonoBehaviour {
  float time = 3f;
  AudioSource self;

  private void Start() {
    self = GetComponent<AudioSource>();
  }

  void Update() {
    time -= Time.deltaTime;
    if ((time < 0 && self == null) || (self != null && !self.isPlaying)) {
      GameObject.Destroy(gameObject);
      time = 1000;
    }
  }
}
