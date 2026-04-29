using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomListItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text playersText;
    [SerializeField] private Button joinButton;

    private string roomName;
    private Action<string> onJoinPressed;

    public void Initialize(
        string newRoomName,
        int currentPlayers,
        int maxPlayers,
        bool isOpen,
        Action<string> callback)
    {
        roomName = newRoomName;
        onJoinPressed = callback;

        if (roomNameText != null)
        {
            roomNameText.text = roomName;
        }

        if (playersText != null)
        {
            playersText.text = $"{currentPlayers}/{maxPlayers}";
        }

        bool canJoin = isOpen && currentPlayers < maxPlayers;

        if (joinButton != null)
        {
            joinButton.interactable = canJoin;
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(HandleJoinPressed);
        }
    }

    private void HandleJoinPressed()
    {
        onJoinPressed?.Invoke(roomName);
    }
}