using Fusion;
using UnityEngine;

public class ScorePickup : NetworkBehaviour
{
    [Header("Pickup")]
    [SerializeField] private string pickupName = "Apple";

    [Header("Score")]
    [SerializeField] private int scoreAmount = 1;

    [Header("Respawn")]
    [SerializeField] private float respawnDelay = 5f;

    [Header("References")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Collider pickupCollider;

    [Networked] private bool IsAvailable { get; set; }
    [Networked] private TickTimer RespawnTimer { get; set; }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            IsAvailable = true;
        }

        UpdateVisualState();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (IsAvailable)
        {
            return;
        }

        if (RespawnTimer.Expired(Runner))
        {
            IsAvailable = true;
        }
    }

    public override void Render()
    {
        UpdateVisualState();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsAvailable)
        {
            return;
        }

        PlayerMovement player = other.GetComponentInParent<PlayerMovement>();

        if (player == null || player.Object == null)
        {
            return;
        }

        if (Object.HasStateAuthority)
        {
            Collect(player.Object.InputAuthority);
            return;
        }

        RPC_RequestCollect(player.Object.InputAuthority);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestCollect(PlayerRef playerRef)
    {
        Collect(playerRef);
    }

    private void Collect(PlayerRef playerRef)
    {
        if (!IsAvailable)
        {
            return;
        }

        PlayerMovement player = FindPlayerByRef(playerRef);

        if (player == null)
        {
            return;
        }

        player.AddScore(scoreAmount);

        IsAvailable = false;
        RespawnTimer = TickTimer.CreateFromSeconds(Runner, respawnDelay);

        RPC_NotifyPickupCollected(playerRef.RawEncoded, pickupName, scoreAmount);

        Debug.Log($"Player {playerRef.RawEncoded} collected {pickupName}. Score {FormatScore(scoreAmount)}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyPickupCollected(int playerRawEncoded, string collectedPickupName, int collectedScoreAmount)
    {
        string formattedScore = FormatScore(collectedScoreAmount);

        Debug.Log($"[Pickup] Player {playerRawEncoded} collected {collectedPickupName}. Score {formattedScore}");

        RaceUI raceUI = FindFirstObjectByType<RaceUI>();

        if (raceUI != null)
        {
            raceUI.ShowTemporaryMessage($"Player {playerRawEncoded} collected {collectedPickupName} {formattedScore}");
        }
    }

    private PlayerMovement FindPlayerByRef(PlayerRef playerRef)
    {
        PlayerMovement[] players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None);

        foreach (PlayerMovement player in players)
        {
            if (player == null || player.Object == null)
            {
                continue;
            }

            if (player.Object.InputAuthority == playerRef)
            {
                return player;
            }
        }

        return null;
    }

    private string FormatScore(int amount)
    {
        if (amount >= 0)
        {
            return $"+{amount}";
        }

        return amount.ToString();
    }

    private void UpdateVisualState()
    {
        bool shouldBeVisible = IsAvailable;

        if (visualRoot != null)
        {
            visualRoot.SetActive(shouldBeVisible);
        }

        if (pickupCollider != null)
        {
            pickupCollider.enabled = shouldBeVisible;
        }
    }
}