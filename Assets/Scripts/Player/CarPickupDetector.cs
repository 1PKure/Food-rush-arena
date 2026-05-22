using Fusion;
using UnityEngine;

public class CarPickupDetector : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkCarMovement carMovement;

    [Header("Detection")]
    [SerializeField] private LayerMask pickupLayerMask = ~0;
    [SerializeField] private bool showDebugLogs = false;

    private Transform rootTransform;

    private void Awake()
    {
        if (carMovement == null)
        {
            carMovement = GetComponentInParent<NetworkCarMovement>();
        }

        if (carMovement != null)
        {
            rootTransform = carMovement.transform;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (carMovement == null || carMovement.Object == null)
        {
            return;
        }

        if (IsOwnCollider(other))
        {
            return;
        }

        if (!IsInPickupLayer(other.gameObject.layer))
        {
            return;
        }

        ScorePickup scorePickup = other.GetComponentInParent<ScorePickup>();

        if (scorePickup == null)
        {
            return;
        }

        scorePickup.TryCollect(carMovement);

        if (showDebugLogs)
        {
            Debug.Log($"[CarPickupDetector] Collected pickup: {scorePickup.name}");
        }
    }

    private bool IsOwnCollider(Collider other)
    {
        if (rootTransform == null)
        {
            return false;
        }

        return other.transform == rootTransform || other.transform.IsChildOf(rootTransform);
    }

    private bool IsInPickupLayer(int layer)
    {
        return (pickupLayerMask.value & (1 << layer)) != 0;
    }
}