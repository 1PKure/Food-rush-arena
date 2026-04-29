using Fusion;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Networked] private float CurrentMoveSpeed { get; set; }

    public override void Spawned()
    {
        CurrentMoveSpeed = moveSpeed;
    }

    public override void FixedUpdateNetwork()
    {
        if (!CanMove())
        {
            return;
        }

        if (GetInput(out PlayerInputData inputData))
        {
            Vector3 direction = new Vector3(inputData.Move.x, 0f, inputData.Move.y);

            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            transform.position += direction * CurrentMoveSpeed * Runner.DeltaTime;
        }
    }

    private bool CanMove()
    {
        if (!Object.HasInputAuthority)
        {
            return false;
        }

        if (NetworkRaceManager.Instance == null)
        {
            return false;
        }

        return NetworkRaceManager.Instance.CanPlayersMove;
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