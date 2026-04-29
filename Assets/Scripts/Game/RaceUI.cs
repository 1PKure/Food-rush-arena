using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RaceUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button backToLobbyButton;

    private void Awake()
    {
        if (backToLobbyButton != null)
        {
            backToLobbyButton.onClick.RemoveAllListeners();
            backToLobbyButton.onClick.AddListener(BackToLobby);
        }

        SetCountdownText(string.Empty);
        SetStatusText("Waiting for players...");
    }

    public void SetCountdownText(string value)
    {
        if (countdownText != null)
        {
            countdownText.text = value;
        }
    }

    public void SetStatusText(string value)
    {
        if (statusText != null)
        {
            statusText.text = value;
        }
    }

    private void BackToLobby()
    {
        if (FusionLauncher.Instance == null)
        {
            Debug.LogError("FusionLauncher instance not found.");
            return;
        }

        FusionLauncher.Instance.LeaveRoom();
    }
}