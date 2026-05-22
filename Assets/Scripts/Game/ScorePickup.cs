using Fusion;
using UnityEngine;

public class ScorePickup : NetworkBehaviour
{
    [Header("Pickup")]
    [SerializeField] private string pickupName = "Apple";

    [Header("Score")]
    [SerializeField] private int scoreAmount = 1;

    [Header("References")]
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Collider pickupCollider;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private NetworkMecanimAnimator networkMecanimAnimator;
    [SerializeField] private string collectTriggerName = "Collect";

    [Header("Despawn")]
    [SerializeField] private float despawnDelay = 0.65f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    [Networked] private NetworkBool IsCollected { get; set; }
    [Networked] private TickTimer DespawnTimer { get; set; }

    private NetworkPickupSpawner ownerSpawner;

    private Vector3 initialVisualLocalPosition;
    private Quaternion initialVisualLocalRotation;
    private Vector3 initialVisualLocalScale;

    private void Awake()
    {
        if (pickupCollider == null)
        {
            pickupCollider = GetComponent<Collider>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (networkMecanimAnimator == null)
        {
            networkMecanimAnimator = GetComponentInChildren<NetworkMecanimAnimator>();
        }

        CacheInitialVisualTransform();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            IsCollected = false;
        }

        ResetVisualState();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (!IsCollected)
        {
            return;
        }

        if (!DespawnTimer.Expired(Runner))
        {
            return;
        }

        if (ownerSpawner != null)
        {
            ownerSpawner.NotifyPickupDespawned(this);
        }

        Runner.Despawn(Object);
    }

    public void Initialize(NetworkPickupSpawner spawner)
    {
        ownerSpawner = spawner;
    }

    private void OnTriggerEnter(Collider other)
    {
        NetworkCarMovement player = other.GetComponentInParent<NetworkCarMovement>();

        if (player == null)
        {
            return;
        }

        TryCollect(player);
    }

    public void TryCollect(NetworkCarMovement player)
    {
        if (IsCollected)
        {
            return;
        }

        if (player == null || player.Object == null)
        {
            return;
        }

        PlayerRef playerRef = player.Object.InputAuthority;

        if (Object.HasStateAuthority)
        {
            Collect(playerRef);
            return;
        }

        RPC_RequestCollect(playerRef);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestCollect(PlayerRef playerRef)
    {
        Collect(playerRef);
    }

    private void Collect(PlayerRef playerRef)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (IsCollected)
        {
            return;
        }

        NetworkCarMovement player = FindPlayerByRef(playerRef);

        if (player == null)
        {
            return;
        }

        player.AddScore(scoreAmount);

        IsCollected = true;

        if (pickupCollider != null)
        {
            pickupCollider.enabled = false;
        }

        PlayCollectAnimation();

        DespawnTimer = TickTimer.CreateFromSeconds(Runner, despawnDelay);

        RPC_NotifyPickupCollected(playerRef.RawEncoded, pickupName, scoreAmount);

        if (showDebugLogs)
        {
            Debug.Log($"[ScorePickup] Player {playerRef.RawEncoded} collected {pickupName}. Score {FormatScore(scoreAmount)}");
        }
    }

    private void PlayCollectAnimation()
    {
        if (networkMecanimAnimator != null)
        {
            networkMecanimAnimator.SetTrigger(collectTriggerName);
            return;
        }

        if (animator != null)
        {
            animator.SetTrigger(collectTriggerName);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyPickupCollected(int playerRawEncoded, string collectedPickupName, int collectedScoreAmount)
    {
        string formattedScore = FormatScore(collectedScoreAmount);

        RaceUI raceUI = FindFirstObjectByType<RaceUI>();

        if (raceUI != null)
        {
            raceUI.ShowTemporaryMessage($"Player {playerRawEncoded} collected {collectedPickupName} {formattedScore}");
        }

        if (showDebugLogs)
        {
            Debug.Log($"[Pickup] Player {playerRawEncoded} collected {collectedPickupName}. Score {formattedScore}");
        }
    }

    private NetworkCarMovement FindPlayerByRef(PlayerRef playerRef)
    {
        NetworkCarMovement[] players = FindObjectsByType<NetworkCarMovement>(FindObjectsSortMode.None);

        foreach (NetworkCarMovement player in players)
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

    private void CacheInitialVisualTransform()
    {
        if (visualRoot == null)
        {
            return;
        }

        initialVisualLocalPosition = visualRoot.transform.localPosition;
        initialVisualLocalRotation = visualRoot.transform.localRotation;
        initialVisualLocalScale = visualRoot.transform.localScale;
    }

    private void ResetVisualState()
    {
        if (visualRoot != null)
        {
            visualRoot.SetActive(true);
            visualRoot.transform.localPosition = initialVisualLocalPosition;
            visualRoot.transform.localRotation = initialVisualLocalRotation;
            visualRoot.transform.localScale = initialVisualLocalScale;
        }

        if (pickupCollider != null)
        {
            pickupCollider.enabled = true;
        }

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.Play("Idle", 0, 0f);
        }
    }
}