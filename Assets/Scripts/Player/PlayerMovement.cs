using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkCharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    private NetworkCharacterController networkCharacterController;

    [Networked] private float CurrentMoveSpeed { get; set; }
    [Networked] public int Score { get; private set; }

    private void Awake()
    {
        networkCharacterController = GetComponent<NetworkCharacterController>();
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CurrentMoveSpeed = moveSpeed;
            Score = 0;
        }

        Debug.Log($"[PlayerMovement] Spawned | InputAuthority: {Object.InputAuthority} | HasInputAuthority: {Object.HasInputAuthority} | HasStateAuthority: {Object.HasStateAuthority}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!CanMove())
        {
            return;
        }

        if (!GetInput(out PlayerInputData inputData))
        {
            return;
        }

        Vector3 direction = new Vector3(inputData.Move.x, 0f, inputData.Move.y);

        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        networkCharacterController.Move(direction);
    }

    private bool CanMove()
    {
        if (NetworkRaceManager.Instance == null)
        {
            return false;
        }

        return NetworkRaceManager.Instance.CanPlayersMove;
    }

    public void AddScore(int amount)
    {
        if (Object.HasStateAuthority)
        {
            Score += amount;
            return;
        }

        RPC_RequestAddScore(amount);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestAddScore(int amount)
    {
        Score += amount;
    }

    public void ResetScore()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        Score = 0;
    }

    public void ApplySpeedBoost(float multiplier, float duration)
    {
        if (!Object.HasStateAuthority)
        {
            RPC_RequestSpeedBoost(multiplier, duration);
            return;
        }

        StartCoroutine(SpeedBoostRoutine(multiplier, duration));
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestSpeedBoost(float multiplier, float duration)
    {
        StartCoroutine(SpeedBoostRoutine(multiplier, duration));
    }

    private System.Collections.IEnumerator SpeedBoostRoutine(float multiplier, float duration)
    {
        CurrentMoveSpeed = moveSpeed * multiplier;

        yield return new WaitForSeconds(duration);

        CurrentMoveSpeed = moveSpeed;
    }
}