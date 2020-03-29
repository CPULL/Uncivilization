using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Options : MonoBehaviour {
  public Slider VolumeSlider;
  public TextMeshProUGUI VolumeLabel;
  public TMP_Dropdown FullScreenDropdown;
  public Toggle TypingSoundToggle;
  public Toggle WriteLogsOnFileToggle;
  public TMP_InputField FilePathForLogs;
  public TMP_Dropdown LogsLevel;


  public void Init() {
    gameObject.SetActive(true);
    VolumeSlider.SetValueWithoutNotify(GD.mainVolume);
    VolumeLabel.text = "Volume: " + GD.mainVolume;
    FullScreenDropdown.SetValueWithoutNotify((int)GD.fullScreen);
    TypingSoundToggle.SetIsOnWithoutNotify(GD.useTypingSound);

    WriteLogsOnFileToggle.SetIsOnWithoutNotify(GD.writeLogsOnFile);
    FilePathForLogs.text = GD.filePathForLogs;
    LogsLevel.SetValueWithoutNotify(((int)GD.logsLevel) - 1);
    FilePathForLogs.enabled = GD.writeLogsOnFile;
    FilePathForLogs.interactable = GD.writeLogsOnFile;
    LogsLevel.enabled = GD.writeLogsOnFile;
    LogsLevel.interactable = GD.writeLogsOnFile;
  }

  public void AlterVolume() {
    GD.mainVolume = (int)VolumeSlider.value;
    VolumeLabel.text = "Volume: " + GD.mainVolume;
    AudioListener.volume = GD.mainVolume / 100f;
  }

  public void AlterFullScreen() {
    FullScreenMode value = (FullScreenMode)FullScreenDropdown.value;
    Screen.fullScreenMode = value;
    Screen.fullScreen = value != FullScreenMode.Windowed && value != FullScreenMode.MaximizedWindow;
    GD.fullScreen = value;
  }

  public void EnableTypingSound() {
    GD.writeLogsOnFile = TypingSoundToggle.isOn;
  }

  public void EnableLogs() {
    GD.writeLogsOnFile = WriteLogsOnFileToggle.isOn;
    FilePathForLogs.enabled = GD.writeLogsOnFile;
    FilePathForLogs.interactable = GD.writeLogsOnFile;
    LogsLevel.enabled = GD.writeLogsOnFile;
    LogsLevel.interactable = GD.writeLogsOnFile;
  }

  public void ChangeLogsLevel() {
    GD.logsLevel = (GD.LT)(LogsLevel.value + 1);
  }

  public void Close() {
    string path = FilePathForLogs.text.Trim();
    string old = GD.filePathForLogs;
    if (!path.Equals(old)) {
      GD.filePathForLogs = path;
      if (GD.InitLogFile()) {
        GD.filePathForLogs = old;
        GD.InitLogFile();
      }
    }
    gameObject.SetActive(false);
  }
}
