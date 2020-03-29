using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ChatManager : MonoBehaviour {
  private Player player;
  private Dictionary<ChatID, Chat> chats;
  public GameObject ChatTemplate;
  public GameObject Chats;
  public Transform ChatsGrid;
  private Queue<NetworkManager.ChatMessage> eventsQueue;

  public static ChatManager instance;

  public void Startup(Player player) {
    this.player = player;
    chats = new Dictionary<ChatID, Chat>();
    eventsQueue = new Queue<NetworkManager.ChatMessage>();
    player.OnChat += OnChatReceived;
    Chats.SetActive(false);
    if (instance == null) instance = this;
  }

  public void ChatEnded(ChatID id) {
    if (chats.ContainsKey(id))
      chats.Remove(id);
    if (chats.Count == 0)
      Chats.SetActive(false);
  }

  public void EndChats(Player thePlayer) {
    List<Chat> toBeRemoved = new List<Chat>();
    foreach(Chat chat in chats.Values)
      if (chat.IsPlayer(thePlayer))
        toBeRemoved.Add(chat);

    while (toBeRemoved.Count > 0) {
      Chat chat = toBeRemoved[0];
      GameObject.Destroy(chat.gameObject);
      if (chats.ContainsKey(chat.id))
        chats.Remove(chat.id);
      toBeRemoved.RemoveAt(0);
    }
    Chats.SetActive(false);
  }

  public void StartChatWith(ulong targetid, string targetname, int targetavatar) {
    // Generate the ID of the chat and the name
    string name = "Chat with <sprite=" + targetavatar + "> " + targetname;
    ChatID chatid = new ChatID(player.ID, targetid);

    if (!chats.ContainsKey(chatid)) {
      GameObject chat = Instantiate(ChatTemplate, ChatsGrid);
      Chat chatScript = chat.GetComponent<Chat>();
      NetworkManager.ChatMessage e = new NetworkManager.ChatMessage {
        id = chatid,
        type = ChatType.OneToOne,
        chatname = name,
        senderid = player.ID,
        senderavatar = player.Avatar,
        message = "",
        participants = new List<ChatParticipant> {
          new ChatParticipant (player.Name, player.Avatar, player.ID),
          new ChatParticipant (targetname, targetavatar, targetid)
        }
      };
      chatScript.Init(e, player, this);
      chats[chatid] = chatScript;
      // Send the chat init to the participant (FIXME we should do something similar for the game group chat)
      GD.instance.networkManager.SendChat(chatid, ChatType.OneToOne, player, e.participants, "");
    }
    Chats.SetActive(true);
  }

  public void StartChatWith(string gamename, List<ChatParticipant> targets) {
    // Generate the ID of the chat and the name
    string name = "Game Chat <b>" + gamename + "</b>";
    ChatID chatid = new ChatID(gamename);
    string introText = "<color=blue>Game chat:</color> ";
    int num = 1;
    foreach (ChatParticipant p in targets) {
      introText += " <sprite=" + p.avatar + "><b>" + p.name + "</b>";
      if (num < targets.Count) introText += ", ";
      num++;
    }
    if (!chats.ContainsKey(chatid)) {
      GameObject chat = Instantiate(ChatTemplate, ChatsGrid);
      Chat chatScript = chat.GetComponent<Chat>();
      NetworkManager.ChatMessage e = new NetworkManager.ChatMessage {
        id = chatid,
        type = ChatType.Game,
        chatname = name,
        senderid = player.ID,
        senderavatar = player.Avatar,
        message = "",
        participants = targets
      };
      chatScript.Init(e, player, this);
      chats[chatid] = chatScript;
      // Send the chat init to the participant
      GD.instance.networkManager.SendChat(chatid, ChatType.Game, player, e.participants, introText);
    }
    Chats.SetActive(true);
  }

  private void OnChatReceived(object sender, NetworkManager.ChatMessage e) {
    // We are not inside the MonoBehavior thread here, we need to store the event and process it inside the Update
    eventsQueue.Enqueue(e);
  }


  private void Update() {
    if (eventsQueue == null || eventsQueue.Count == 0) return;

    NetworkManager.ChatMessage e = eventsQueue.Dequeue();

    if (e.type == ChatType.Error && !chats.ContainsKey(e.id)) return;
    Chats.SetActive(true);

    // Pick the chat ID, if it does not exist just initialize it
    if (!chats.ContainsKey(e.id)) {
      GameObject chat = Instantiate(ChatTemplate, ChatsGrid);
      Chat chatScript = chat.GetComponent<Chat>();
      chatScript.Init(e, player, this);
      chats[e.id] = chatScript;
    }
    try {
      chats[e.id].ShowMessage(e);
    } catch (System.Exception ex) {
      GD.DebugLog("Problems in visualizing a chat: " + ex.Message + " --> " + e, GD.LT.Warning);
    }
  }

}

public class ChatParticipant {
  public string name;
  public int avatar;
  public ulong id;

  public override string ToString() {
    return name + id;
  }

  public ChatParticipant(string n, int a, ulong theid) {
    name = n;
    avatar = a;
    id = theid;
  }
  public ChatParticipant(byte[] data, int pos) {
    int namel = data[pos + 1];
    name = System.Text.Encoding.UTF8.GetString(data, pos + 2, namel);
    avatar = data[pos + 2 + namel];
    id = System.BitConverter.ToUInt64(data, pos + 3 + namel);
  }

  public byte[] Stringify() {
    byte[] nameb = System.Text.Encoding.UTF8.GetBytes(name);
    int len = 1 + 1 + nameb.Length + 1 + 8;
    byte[] res = new byte[len];

    /*
     * 1 byte full length
     * 1 byte name length
     * n bytes name
     * 1 byte avatar
     * 8 bytes ID
     */
    res[0] = (byte)len;
    res[1] = (byte)nameb.Length;
    for (int i = 0; i < nameb.Length; i++)
      res[2 + i] = nameb[i];
    res[2 + nameb.Length] = (byte)avatar;
    byte[] idb = System.BitConverter.GetBytes(id);
    for (int i = 0; i < 8; i++) 
      res[3 + nameb.Length + i] = idb[i];

    return res;
  }

  public int GetStringSize() {
    return 11 + System.Text.Encoding.UTF8.GetByteCount(name);
  }
}