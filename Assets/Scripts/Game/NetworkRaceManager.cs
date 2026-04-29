using Fusion;
using TMPro;
using UnityEngine;
using System.Linq;

public class NetworkRaceManager : NetworkBehaviour
{
    public static NetworkRaceManager Instance { get; private set; }

    [Header("Race Settings")]
    [SerializeField] private int requiredPlayers = 2;
    [SerializeField] private float countdownDuration = 3f;

    [Header("UI")]
    [SerializeField] private RaceUI raceUI;

    [Networked] public RaceState CurrentState { get; private set; }
    [Networked] public TickTimer CountdownTimer { get; private set; }
    [Networked] public int WinnerRawEncoded { get; private set; }

    private RaceState lastVisualState;
    private int lastCountdownValue = -1;

    public bool CanPlayersMove => CurrentState == RaceState.Racing;

    private void Awake()
    {
        Instance = this;

        if (raceUI == null)
        {
            raceUI = FindFirstObjectByType<RaceUI>();
        }
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CurrentState = RaceState.WaitingForPlayers;
            WinnerRawEncoded = -1;
        }

        UpdateVisuals(force: true);
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
        {
            UpdateVisuals(force: false);
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
                break;

            case RaceState.Finished:
                break;
        }

        UpdateVisuals(force: false);
    }

    private void TickWaitingForPlayers()
    {
        if (Runner.ActivePlayers.Count() >= requiredPlayers)
        {
            StartCountdown();
        }
    }

    private void StartCountdown()
    {
        CurrentState = RaceState.Countdown;
        CountdownTimer = TickTimer.CreateFromSeconds(Runner, countdownDuration);

        Debug.Log("Countdown started.");
    }

    private void TickCountdown()
    {
        if (CountdownTimer.Expired(Runner))
        {
            CurrentState = RaceState.Racing;
            Debug.Log("Race started.");
        }
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

        Debug.Log($"Race finished. Winner: {player}");
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
            lastVisualState = CurrentState;

            switch (CurrentState)
            {
                case RaceState.WaitingForPlayers:
                    raceUI.SetStatusText("Waiting for players...");
                    raceUI.SetCountdownText(string.Empty);
                    break;

                case RaceState.Countdown:
                    raceUI.SetStatusText("Get ready...");
                    break;

                case RaceState.Racing:
                    raceUI.SetStatusText("Race started!");
                    raceUI.SetCountdownText("GO!");
                    break;

                case RaceState.Finished:
                    raceUI.SetStatusText($"Winner: Player {WinnerRawEncoded}");
                    raceUI.SetCountdownText("FINISH!");
                    break;
            }
        }

        if (CurrentState == RaceState.Countdown)
        {
            float remainingTime = CountdownTimer.RemainingTime(Runner) ?? 0f;
            int countdownValue = Mathf.CeilToInt(remainingTime);

            countdownValue = Mathf.Clamp(countdownValue, 1, 3);

            if (force || countdownValue != lastCountdownValue)
            {
                lastCountdownValue = countdownValue;
                raceUI.SetCountdownText(countdownValue.ToString());
            }
        }
    }
}