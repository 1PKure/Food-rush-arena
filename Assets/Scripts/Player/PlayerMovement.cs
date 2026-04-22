using Fusion;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out PlayerInputData inputData))
        {
            Vector3 direction = new Vector3(inputData.Move.x, 0f, inputData.Move.y);
            transform.position += direction * moveSpeed * Runner.DeltaTime;
        }
    }
}