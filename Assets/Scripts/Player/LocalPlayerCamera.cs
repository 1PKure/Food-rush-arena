using Fusion;
using UnityEngine;

public class LocalPlayerCamera : NetworkBehaviour
{
    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 8f, -7f);
    [SerializeField] private Vector3 cameraRotation = new Vector3(50f, 0f, 0f);
    [SerializeField] private float followSmoothness = 12f;

    private bool isCameraActive;

    public override void Spawned()
    {
        if (!Object.HasInputAuthority)
        {
            DisableCamera();
            return;
        }

        EnableCamera();
    }

    public override void Render()
    {
        if (!isCameraActive || playerCamera == null)
        {
            return;
        }

        FollowPlayer();
    }

    private void EnableCamera()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        if (playerCamera == null)
        {
            Debug.LogError("Player camera not found.");
            return;
        }

        playerCamera.transform.SetParent(null);

        playerCamera.gameObject.SetActive(true);
        playerCamera.transform.position = transform.position + cameraOffset;
        playerCamera.transform.rotation = Quaternion.Euler(cameraRotation);

        isCameraActive = true;
    }

    private void DisableCamera()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }

        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(false);
        }

        isCameraActive = false;
    }

    private void FollowPlayer()
    {
        Vector3 targetPosition = transform.position + cameraOffset;

        playerCamera.transform.position = Vector3.Lerp(
            playerCamera.transform.position,
            targetPosition,
            followSmoothness * Time.deltaTime
        );

        playerCamera.transform.rotation = Quaternion.Euler(cameraRotation);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (playerCamera != null)
        {
            Destroy(playerCamera.gameObject);
        }
    }
}