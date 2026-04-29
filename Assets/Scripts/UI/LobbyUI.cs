using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;

public class LobbyUI : MonoBehaviour
{
    [Header("Create Room")]
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private GameObject lobbyRoot;


    [Header("Room List")]
    [SerializeField] private Transform roomListContainer;
    [SerializeField] private RoomListItem roomListItemPrefab;

    [Header("Room Info")]
    [SerializeField] private TMP_Text currentRoomText;

    private readonly List<RoomListItem> spawnedItems = new();

    private void Awake()
    {
        ShowLobbyState();
    }

    public void OnCreateRoomButtonPressed()
    {
        string roomName = roomNameInput.text.Trim();

        if (string.IsNullOrWhiteSpace(roomName))
        {
            Debug.LogWarning("Room name input is empty.");
            return;
        }

        FusionLauncher.Instance.CreateRoom(roomName);
    }

    public void RefreshRoomList(List<SessionInfo> sessions)
    {
        ClearRoomList();

        foreach (SessionInfo session in sessions)
        {
            if (!session.IsVisible)
            {
                continue;
            }

            RoomListItem item = Instantiate(roomListItemPrefab, roomListContainer);
            item.transform.localScale = Vector3.one;

            item.Initialize(
                session.Name,
                session.PlayerCount,
                session.MaxPlayers,
                session.IsOpen,
                OnJoinRoomPressed
            );

            spawnedItems.Add(item);
        }
    }

    private void ClearRoomList()
    {
        for (int i = 0; i < spawnedItems.Count; i++)
        {
            if (spawnedItems[i] != null)
            {
                Destroy(spawnedItems[i].gameObject);
            }
        }

        spawnedItems.Clear();
    }

    private void OnJoinRoomPressed(string roomName)
    {
        FusionLauncher.Instance.JoinRoom(roomName);
    }

    public void ShowInRoomState(string roomName)
    {
        lobbyRoot.SetActive(false);

        if (currentRoomText != null)
        {
            currentRoomText.text = $"Room: {roomName}";
        }
    }

    public void ShowLobbyState()
    {
        lobbyRoot.SetActive(true);

        if (currentRoomText != null)
        {
            currentRoomText.text = string.Empty;
        }
    }

    public void OnLeaveRoomButtonPressed()
    {
        FusionLauncher.Instance.LeaveRoom();
    }

        public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}