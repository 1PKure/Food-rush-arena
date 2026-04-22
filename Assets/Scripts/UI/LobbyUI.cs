using System.Collections.Generic;
using Fusion;
using TMPro;
using UnityEngine;

public class LobbyUI : MonoBehaviour
{
    [Header("Create Room")]
    [SerializeField] private TMP_InputField roomNameInput;
    [SerializeField] private GameObject lobbyRoot;
    [SerializeField] private GameObject inRoomRoot;

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
        FusionLauncher.Instance.CreateRoom(roomName);
    }

    public void RefreshRoomList(List<SessionInfo> sessions)
    {
        ClearRoomList();

        foreach (SessionInfo session in sessions)
        {
            RoomListItem item = Instantiate(roomListItemPrefab, roomListContainer);
            item.Initialize(session.Name, OnJoinRoomPressed);
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
        inRoomRoot.SetActive(true);
        currentRoomText.text = $"Room: {roomName}";
    }

    public void ShowLobbyState()
    {
        lobbyRoot.SetActive(true);
        inRoomRoot.SetActive(false);
        currentRoomText.text = string.Empty;
    }

    public void OnLeaveRoomButtonPressed()
    {
        FusionLauncher.Instance.LeaveRoom();
    }
}