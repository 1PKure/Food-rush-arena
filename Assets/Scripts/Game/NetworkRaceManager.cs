using Fusion;
using UnityEngine;
using System.Linq;

public class NetworkRaceManager : NetworkBehaviour
{
    public static NetworkRaceManager Instance { get; private set; }

    [Header("Race Settings")]
    [SerializeField] private int requiredPlayers = 2;
    [SerializeField] private float countdownDuration = 3f;
    [SerializeField] private float raceDuration = 15f;

    [Header("UI")]
    [SerializeField] private RaceUI raceUI;

    [Networked] public RaceState CurrentState { get; private set; }
    [Networked] public TickTimer CountdownTimer { get; private set; }
    [Networked] public TickTimer RaceTimer { get; private set; }
    [Networked] public int WinnerRawEncoded { get; private set; }
    [Networked] public int WinningScore { get; private set; }
    [Networked] public TickTimer GoMessageTimer { get; private set; }

    private RaceState lastVisualState;
    private int lastCountdownValue = -1;
    private int lastRaceTimeValue = -1;

    public bool CanPlayersMove => CurrentState == RaceState.Racing;

    private void Awake()
    {
        Instance = this;

        if (raceUI == null)
        {
            raceUI = FindFirstObjectByType<RaceUI>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public override void Spawned()
    {
        Debug.Log($"[RaceManager] Spawned. HasStateAuthority: {Object.HasStateAuthority} | HasInputAuthority: {Object.HasInputAuthority}");

        if (Object.HasStateAuthority)
        {
            CurrentState = RaceState.WaitingForPlayers;
            WinnerRawEncoded = -1;
            WinningScore = 0;

            Debug.Log("[RaceManager] State initialized by StateAuthority.");
        }

        UpdateVisuals(force: true);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        switch (CurrentState)
        {
            case RaceState.WaitingForPlayers:
                TickWaitingForPlayers();
                break;

            case RaceState.Countdown:
                TickCountdown();
                break;

            case RaceState.Racing:
                TickRacing();
                break;

            case RaceState.Finished:
                break;
        }
    }

    public override void Render()
    {
        UpdateVisuals(force: false);
    }

    private void TickWaitingForPlayers()
    {
        int playerCount = Runner.ActivePlayers.Count();

        if (playerCount >= requiredPlayers)
        {
            StartCountdown();
        }
    }

    private void StartCountdown()
    {
        if (CurrentState != RaceState.WaitingForPlayers)
        {
            return;
        }

        CurrentState = RaceState.Countdown;
        CountdownTimer = TickTimer.CreateFromSeconds(Runner, countdownDuration);

        Debug.Log("[RaceManager] Countdown started.");
    }

    private void TickCountdown()
    {
        if (CountdownTimer.Expired(Runner))
        {
            StartRace();
        }
    }

    private void StartRace()
    {
        if (CurrentState != RaceState.Countdown)
        {
            return;
        }

        CurrentState = RaceState.Racing;
        RaceTimer = TickTimer.CreateFromSeconds(Runner, raceDuration);
        GoMessageTimer = TickTimer.CreateFromSeconds(Runner, 1.25f);

        CloseCurrentSession();

        Debug.Log("[RaceManager] Race started.");
    }

    private void TickRacing()
    {
        if (RaceTimer.Expired(Runner))
        {
            FinishRaceByTime();
        }
    }

    private void CloseCurrentSession()
    {
        if (!Runner.IsServer)
        {
            return;
        }

        if (Runner.SessionInfo == null)
        {
            return;
        }

        Runner.SessionInfo.IsOpen = false;
        Runner.SessionInfo.IsVisible = false;

        Debug.Log("[RaceManager] Session closed after race start.");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyMatchFinished(int winnerRawEncoded, int winningScore)
    {
        Debug.Log($"[RaceManager] Match finished. Winner: Player {winnerRawEncoded} | Score: {winningScore}");

        RaceUI raceUI = FindFirstObjectByType<RaceUI>();

        if (raceUI != null)
        {
            raceUI.ShowTemporaryMessage($"Game Finished!");
        }
    }
    private void FinishRaceByTime()
    {
        if (CurrentState != RaceState.Racing)
        {
            return;
        }

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);

        PlayerMovement winner = null;
        int highestScore = int.MinValue;

        foreach (PlayerMovement player in players)
        {
            if (player == null || player.Object == null)
            {
                continue;
            }

            if (player.Score > highestScore)
            {
                highestScore = player.Score;
                winner = player;
            }
        }

        CurrentState = RaceState.Finished;

        if (winner != null)
        {
            WinnerRawEncoded = winner.Object.InputAuthority.RawEncoded;
            WinningScore = winner.Score;
        }
        else
        {
            WinnerRawEncoded = -1;
            WinningScore = 0;
        }

        CloseCurrentSession();
        RPC_NotifyMatchFinished(WinnerRawEncoded, WinningScore);
        Debug.Log($"[RaceManager] Race finished. Winner: Player {WinnerRawEncoded} | Score: {WinningScore}");
    }

    public void TryFinishRace(PlayerRef player)
    {
        if (!Object.HasStateAuthority)
        {
            RPC_RequestFinishRace(player);
            return;
        }

        FinishRace(player);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestFinishRace(PlayerRef player)
    {
        FinishRace(player);
    }

    private void FinishRace(PlayerRef player)
    {
        if (CurrentState != RaceState.Racing)
        {
            return;
        }

        CurrentState = RaceState.Finished;
        WinnerRawEncoded = player.RawEncoded;

        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);

        foreach (PlayerMovement playerMovement in players)
        {
            if (playerMovement == null || playerMovement.Object == null)
            {
                continue;
            }

            if (playerMovement.Object.InputAuthority == player)
            {
                WinningScore = playerMovement.Score;
                break;
            }
        }

        Debug.Log($"[RaceManager] Race finished manually. Winner: Player {WinnerRawEncoded} | Score: {WinningScore}");
    }

    public float GetRemainingRaceTime()
    {
        if (!RaceTimer.IsRunning)
        {
            return 0f;
        }

        return RaceTimer.RemainingTime(Runner) ?? 0f;
    }

    private void UpdateVisuals(bool force)
    {
        if (raceUI == null)
        {
            raceUI = FindFirstObjectByType<RaceUI>();
        }

        if (raceUI == null)
        {
            return;
        }

        if (force || lastVisualState != CurrentState)
        {
            Debug.Log($"[RaceManager UI] State changed to {CurrentState}. HasStateAuthority: {Object.HasStateAuthority}");

            lastVisualState = CurrentState;
            lastCountdownValue = -1;
            lastRaceTimeValue = -1;

            switch (CurrentState)
            {
                case RaceState.WaitingForPlayers:
                    raceUI.SetStatusText("Waiting for players...");
                    raceUI.SetCountdownText(string.Empty);
                    raceUI.SetTimerText(string.Empty);
                    break;

                case RaceState.Countdown:
                    raceUI.SetStatusText("Get ready...");
                    raceUI.SetTimerText(string.Empty);
                    break;

                case RaceState.Racing:
                    raceUI.SetStatusText("Collect food!");
                    raceUI.SetCountdownText("GO!");
                    break;

                case RaceState.Finished:
                    raceUI.SetStatusText($"Winner: Player {WinnerRawEncoded} | Score: {WinningScore}");
                    raceUI.SetCountdownText("FINISH!");
                    raceUI.SetTimerText("Time: 0");
                    break;
            }
        }

        if (CurrentState == RaceState.Countdown)
        {
            float remainingTime = CountdownTimer.RemainingTime(Runner) ?? 0f;
            int countdownValue = Mathf.CeilToInt(remainingTime);

            countdownValue = Mathf.Clamp(countdownValue, 1, Mathf.CeilToInt(countdownDuration));

            if (force || countdownValue != lastCountdownValue)
            {
                lastCountdownValue = countdownValue;
                raceUI.SetCountdownText(countdownValue.ToString());
            }
        }

        if (CurrentState == RaceState.Racing)
        {
            float remainingTime = RaceTimer.RemainingTime(Runner) ?? 0f;
            int raceTimeValue = Mathf.CeilToInt(remainingTime);

            if (raceTimeValue < 0)
            {
                raceTimeValue = 0;
            }

            if (force || raceTimeValue != lastRaceTimeValue)
            {
                lastRaceTimeValue = raceTimeValue;
                raceUI.SetTimerText($"Time: {raceTimeValue}");
            }

            if (GoMessageTimer.IsRunning && !GoMessageTimer.Expired(Runner))
            {
                raceUI.SetCountdownText("GO!");
            }
            else
            {
                raceUI.SetCountdownText(string.Empty);
            }
        }
    }
}