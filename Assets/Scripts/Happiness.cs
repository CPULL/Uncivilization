using TMPro;
using UnityEngine;

public class Happiness : MonoBehaviour {
  TextMeshProUGUI Text;
  Transform Handle;

  void Start() {
    Text = transform.Find("Text").GetComponent<TextMeshProUGUI>();
    Handle = transform.Find("Handle");
  }

  public void SetValue(bool visible, int val) {
    if (Text == null) Text = transform.Find("Text").GetComponent<TextMeshProUGUI>();
    if (Handle == null) Handle = transform.Find("Handle");
    gameObject.SetActive(visible);
    Handle.localPosition = new Vector3(250 * (val - 50f) / 50f, 0, 0);
    Text.text = "Happiness: " + val + "%";
  }
}
