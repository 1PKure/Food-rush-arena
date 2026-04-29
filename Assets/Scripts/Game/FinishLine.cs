using Fusion;
using UnityEngine;

public class FinishLine : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PlayerMovement playerMovement = other.GetComponentInParent<PlayerMovement>();

        if (playerMovement == null)
        {
            return;
        }

        NetworkObject networkObject = playerMovement.Object;

        if (networkObject == null)
        {
            return;
        }

        if (NetworkRaceManager.Instance == null)
        {
            return;
        }

        NetworkRaceManager.Instance.TryFinishRace(networkObject.InputAuthority);
    }
}