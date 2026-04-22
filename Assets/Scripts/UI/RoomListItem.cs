using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

public class RoomListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private Button joinButton;

    private string roomName;
    private Action<string> onJoinPressed;

    public void Initialize(string newRoomName, Action<string> callback)
    {
        roomName = newRoomName;
        onJoinPressed = callback;
        roomNameText.text = roomName;

        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(HandleJoinPressed);
    }

    private void HandleJoinPressed()
    {
        onJoinPressed?.Invoke(roomName);
    }
}