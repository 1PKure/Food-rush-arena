using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RaceUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Button backToLobbyButton;
    [SerializeField] private TMP_Text feedbackText;
    private PlayerMovement localPlayer;
    private bool isLeavingRoom;

    private void Awake()
    {
        if (backToLobbyButton != null)
        {
            backToLobbyButton.onClick.RemoveAllListeners();
            backToLobbyButton.onClick.AddListener(BackToLobby);
        }
        
        if (feedbackText != null)
        {
            feedbackText.text = string.Empty;
            feedbackText.gameObject.SetActive(false);
        }
        
        SetCountdownText(string.Empty);
        SetStatusText("Waiting for players...");
        SetTimerText(string.Empty);
        SetScoreText("Score: 0");
    }

    private void OnDisable()
    {
        localPlayer = null;
    }

    private void Update()
    {
        if (isLeavingRoom)
        {
            return;
        }

        if (FusionLauncher.Instance == null || FusionLauncher.Instance.Runner == null || !FusionLauncher.Instance.Runner.IsRunning)
        {
            return;
        }

        if (!IsLocalPlayerValid())
        {
            FindLocalPlayer();
        }

        UpdateLocalScore();
    }

    private bool IsLocalPlayerValid()
    {
        if (localPlayer == null)
        {
            return false;
        }

        if (localPlayer.Object == null)
        {
            return false;
        }

        return true;
    }

    private void FindLocalPlayer()
    {
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);

        foreach (PlayerMovement player in players)
        {
            if (player == null || player.Object == null)
            {
                continue;
            }

            if (player.Object.HasInputAuthority)
            {
                localPlayer = player;
                return;
            }
        }
    }

    private void UpdateLocalScore()
    {
        if (!IsLocalPlayerValid())
        {
            return;
        }

        try
        {
            SetScoreText($"Score: {localPlayer.Score}");
        }
        catch
        {
            localPlayer = null;
        }
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

    public void SetTimerText(string value)
    {
        if (timerText != null)
        {
            timerText.text = value;
        }
    }

    public void SetScoreText(string value)
    {
        if (scoreText != null)
        {
            scoreText.text = value;
        }
    }

    private void BackToLobby()
    {
        if (isLeavingRoom)
        {
            return;
        }

        isLeavingRoom = true;
        localPlayer = null;

        if (backToLobbyButton != null)
        {
            backToLobbyButton.interactable = false;
        }

        if (FusionLauncher.Instance == null)
        {
            Debug.LogError("FusionLauncher instance not found.");
            return;
        }

        FusionLauncher.Instance.LeaveRoom();
    }
    private Coroutine feedbackRoutine;

    public void ShowTemporaryMessage(string message)
    {
        if (feedbackText == null)
        {
            return;
        }

        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
        }

        feedbackRoutine = StartCoroutine(ShowTemporaryMessageRoutine(message));
    }

    private System.Collections.IEnumerator ShowTemporaryMessageRoutine(string message)
    {
        feedbackText.text = message;
        feedbackText.gameObject.SetActive(true);

        yield return new WaitForSeconds(1.5f);

        feedbackText.text = string.Empty;
        feedbackText.gameObject.SetActive(false);

        feedbackRoutine = null;
    }
}