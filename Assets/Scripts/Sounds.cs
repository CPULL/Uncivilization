using UnityEngine;

public class Sounds : MonoBehaviour {
  private AudioListener listener;
  public Sounds instance = null;
  void Awake() {
    listener = Camera.main.GetComponent<AudioListener>();
    instance = this;
    
  }

  // FIXME add a way to play a specific sound and the music, all by static methods
  // The source should be the gameobject emitting the sound
}
