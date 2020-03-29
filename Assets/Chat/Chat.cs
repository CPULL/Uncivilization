using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ChatType { Error = 0, OneToOne = 1, Game = 2, ParticipantGone = 3 };

public struct ChatID {
  public ulong id1; // 0=>client1 id, 1=>gameid
  public ulong id2; // 0=>client2 id, 1=>gameid


  public ChatID(ulong id1, ulong id2) {
    if (id1 < id2) {
      this.id1 = id1;
      this.id2 = id2;
    }
    else {
      this.id1 = id2;
      this.id2 = id1;
    }
  }

  public ChatID(string name) {
    byte[] nd = System.Text.Encoding.UTF8.GetBytes(name);
    byte[] data = new byte[16];
    for (int i = 0; i < 16; i++) data[i] = 0;
    for (int i = 0; i < nd.Length; i++)
      data[i % 16] = (byte)(data[i % 16] ^ nd[i]);
    id1 = System.BitConverter.ToUInt64(data, 0);
    id2 = System.BitConverter.ToUInt64(data, 8);
    if (id1 > id2) {
      ulong tmp = id1;
      id1 = id2;
      id2 = tmp;
    }
  }

  public byte[] GetBytes() {
    byte[] res = new byte[17];
    byte[] idv = System.BitConverter.GetBytes(id1);
    for (int i = 0; i < 8; i++) res[i] = idv[i];
    idv = System.BitConverter.GetBytes(id2);
    for (int i = 0; i < 8; i++) res[8 + i] = idv[i];
    return res;
  }

  public override bool Equals(object obj) {
    if (!(obj is ChatID))
      return false;

    ChatID mys = (ChatID)obj;
    return id1 == mys.id1 && id2 == mys.id2;
  }

  public override int GetHashCode() {
    return id1.GetHashCode() ^ id2.GetHashCode();
  }
  public static bool operator ==(ChatID x, ChatID y) {
    return x.id1 == y.id1 && x.id2 == y.id2;
  }
  public static bool operator !=(ChatID x, ChatID y) {
    return x.id1 != y.id1 || x.id2 != y.id2;
  }
}

public class Chat : MonoBehaviour {
  public TextMeshProUGUI ChatTitle;
  public TextMeshProUGUI ChatTxt;
  public Image ChatAvatar;
  public TMP_InputField ChatInput;
  public Button CloseButton;
  public Button MinButton;
  public Button MaxButton;
  public GameObject ChatContainer;
  private List<ChatParticipant> participants;
  private Player player;
  private ChatManager manager;
  public ChatID id;
  public ChatType type;


  internal void Init(NetworkManager.ChatMessage e, Player player, ChatManager manager) {
    id = e.id;
    type = e.type;
    this.player = player;
    this.manager = manager;
    ChatAvatar.sprite = GD.Avatar(player.Avatar);
    participants = new List<ChatParticipant>();
    bool found = false;
    foreach (ChatParticipant p in e.participants) {
      participants.Add(p);
      if (p.id == player.ID) found = true;
    }
    // Add the current player if missing
    if (!found)
      participants.Add(new ChatParticipant(player.Name, player.Avatar, player.ID));
    ChatTitle.text = e.chatname;
    ChatTxt.text = "";
    gameObject.SetActive(true);
    MaxButton.gameObject.SetActive(false);
    ChatContainer.gameObject.SetActive(true);
    GetComponent<RectTransform>().sizeDelta = new Vector2(500, 360);
  }

  public void EndChat() {
    participants.Clear();
    manager.ChatEnded(id);
    GameObject.Destroy(gameObject);
  }

  public void MinimizeChat() {
    MinButton.gameObject.SetActive(false);
    MaxButton.gameObject.SetActive(true);
    ChatContainer.gameObject.SetActive(false);
    Vector2 now = GetComponent<RectTransform>().sizeDelta;
    GetComponent<RectTransform>().sizeDelta = new Vector2(500, 36);
  }
  public void MaximizeChat() {
    MinButton.gameObject.SetActive(true);
    MaxButton.gameObject.SetActive(false);
    ChatContainer.gameObject.SetActive(true);
    GetComponent<RectTransform>().sizeDelta = new Vector2(500, 360);
  }

  internal bool IsPlayer(Player thePlayer) {
    return player.ID == thePlayer.ID;
  }

  internal void ShowMessage(NetworkManager.ChatMessage e) {
    if (string.IsNullOrEmpty(e.message)) return;

    // Count the lines of the chat, trim if necessary
    int numlines = 1;
    foreach (char c in ChatTxt.text)
      if (c == '\n') numlines++;
    if (numlines > 11) {
      int pos = ChatTxt.text.Length - 1;
      numlines = 1;
      while (pos > 0 && numlines < 12) {
        if (ChatTxt.text[pos] == '\n') numlines++;
        pos--;
      }
      ChatTxt.text = ChatTxt.text.Substring(pos + 1);
    }

    string msg = "";
    if (e.type == ChatType.ParticipantGone) {
      ChatParticipant goner = null;
      foreach (ChatParticipant cp in participants)
        if (cp.id == e.senderid) {
          goner = cp;
          break;
        }
      if (goner != null) {
        participants.Remove(goner);
        msg = (ChatTxt.text.Length > 0 ? "\n" : "") + e.message;
      }
    }
    else if (e.type == ChatType.Error) msg = "<color=red>" + msg + "</color>";
    else {
      // Add the avatar and name of the sender if in the list
      ChatParticipant sender = null;
      foreach (ChatParticipant cp in participants)
        if (cp.id == e.senderid) {
          sender = cp;
          break;
        }

      if (sender != null) {
        msg = (ChatTxt.text.Length > 0 ? "\n" : "") + "<sprite=" + sender.avatar + "><b><color=#101000>" + sender.name + "</color></b> " + e.message;
      }
      else {
        msg = (ChatTxt.text.Length > 0 ? "\n" : "") + e.message;
      }
      // Merge the participants
      foreach (ChatParticipant pe in e.participants) {
        bool found = false;
        foreach (ChatParticipant pc in participants) {
          if (pe.id == pc.id) {
            found = true;
            break;
          }
        }
        if (!found)
          participants.Add(pe);
      }

      // Replace emojis
      msg = msg
        .Replace(":)", "<sprite=75>").Replace(":smile:", "<sprite=75>")
        .Replace(":D", "<sprite=76>").Replace(":happy:", "<sprite=76>")
        .Replace(":love:", "<sprite=77>")
        .Replace("8)", "<sprite=78>").Replace(":glasses:", "<sprite=78>")
        .Replace(":lol:", "<sprite=79>").Replace(":joy:", "<sprite=79>")
        .Replace(":p", "<sprite=80>").Replace(":P", "<sprite=80>").Replace(":tongue:", "<sprite=80>")
        .Replace(":cry:", "<sprite=81>").Replace(">(", "<sprite=81>").Replace(":sad:", "<sprite=81>")
        .Replace(":(", "<sprite=82>");
    }

    // Add the message
    ChatTxt.text = ChatTxt.text + msg;
  }

  public void SendMessage() {
    if (string.IsNullOrWhiteSpace(ChatInput.text)) return;

    List<ChatParticipant> dests = new List<ChatParticipant>();
    foreach (ChatParticipant p in participants)
      dests.Add(p);
    GD.instance.networkManager.SendChat(id, type, player, dests, ChatInput.text.Trim());
    ChatInput.text = "";
    ChatInput.Select();
    ChatInput.ActivateInputField();
  }

}

