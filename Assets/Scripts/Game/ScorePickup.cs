using Fusion;
using UnityEngine;

public class ScorePickup : NetworkBehaviour
{
    [Header("Score")]
    [SerializeField] private int scoreAmount = 1;

    [Header("Visual")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Collider pickupCollider;

    [Networked] private bool IsCollected { get; set; }

    public override void Spawned()
    {
        UpdateVisualState();
    }

    public override void Render()
    {
        UpdateVisualState();
    }

    private void OnTriggerEnter(Collider other)
    {
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
        if (IsCollected)
        {
            return;
        }

        PlayerMovement player = FindPlayerByRef(playerRef);

        if (player == null)
        {
            return;
        }

        IsCollected = true;

        player.AddScore(scoreAmount);

        UpdateVisualState();

        Debug.Log($"Player {playerRef.RawEncoded} collected pickup. +{scoreAmount} points.");
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

    private void UpdateVisualState()
    {
        bool shouldBeVisible = !IsCollected;

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