using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkCharacterController))]
public class NetworkCarMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float maxForwardSpeed = 12f;
    [SerializeField] private float maxReverseSpeed = 5f;
    [SerializeField] private float acceleration = 18f;
    [SerializeField] private float braking = 24f;
    [SerializeField] private float naturalDeceleration = 10f;
    [SerializeField] private float steeringSpeed = 120f;
    [SerializeField] private float minimumSpeedToSteer = 0.5f;

    [Header("Visual Wheels")]
    [SerializeField] private Transform frontLeftWheel;
    [SerializeField] private Transform frontRightWheel;
    [SerializeField] private Transform rearLeftWheel;
    [SerializeField] private Transform rearRightWheel;
    [SerializeField] private float wheelRotationSpeed = 720f;
    [SerializeField] private float frontWheelSteeringAngle = 30f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs;

    [Header("Steering")]
    [SerializeField] private bool invertSteering = true;
    [Networked] public int Score { get; private set; }
    [Networked] public float CurrentSpeed { get; private set; }
    [Networked] public bool IsAccelerating { get; private set; }
    [Networked] public bool IsBraking { get; private set; }

    public PlayerRef Owner => Object.InputAuthority;

    private NetworkCharacterController networkCharacterController;
    private float localSteeringVisualAngle;

    private void Awake()
    {
        networkCharacterController = GetComponent<NetworkCharacterController>();
    }

    public override void Spawned()
    {
        if (showDebugLogs)
        {
            Debug.Log(
                $"[NetworkCarMovement] Spawned | " +
                $"Object: {gameObject.name} | " +
                $"InputAuthority: {Object.InputAuthority} | " +
                $"HasInputAuthority: {Object.HasInputAuthority} | " +
                $"HasStateAuthority: {Object.HasStateAuthority}"
            );
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!CanMove())
        {
            ApplyStop();
            return;
        }

        if (GetInput(out CarInputData input))
        {
            ApplyCarInput(input);
        }
        else
        {
            ApplyCarInput(default);
        }
    }

    public override void Render()
    {
        AnimateWheels();
    }

    public void AddScore(int amount)
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

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

    public int GetScore()
    {
        return Score;
    }

    private bool CanMove()
    {
        if (NetworkRaceManager.Instance == null)
        {
            return true;
        }

        return NetworkRaceManager.Instance.CanPlayersMove;
    }

    private void ApplyCarInput(CarInputData input)
    {
        float throttle = Mathf.Clamp01(input.Throttle);
        float steering = Mathf.Clamp(input.Steering, -1f, 1f);

        bool brake = input.Buttons.IsSet((int)CarInputButton.Brake);
        bool handbrake = input.Buttons.IsSet((int)CarInputButton.Handbrake);

        UpdateSpeed(throttle, brake, handbrake);
        RotateCar(steering, handbrake);
        MoveCar();

        IsAccelerating = throttle > 0.1f;
        IsBraking = brake || handbrake;

        localSteeringVisualAngle = Mathf.Lerp(
            localSteeringVisualAngle,
            steering * frontWheelSteeringAngle,
            Runner.DeltaTime * 10f
        );
    }

    private void UpdateSpeed(float throttle, bool brake, bool handbrake)
    {
        float deltaTime = Runner.DeltaTime;

        if (throttle > 0.1f)
        {
            CurrentSpeed += acceleration * throttle * deltaTime;
        }
        else
        {
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, naturalDeceleration * deltaTime);
        }

        if (brake)
        {
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, braking * deltaTime);
        }

        if (handbrake)
        {
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, braking * 0.75f * deltaTime);
        }

        CurrentSpeed = Mathf.Clamp(CurrentSpeed, 0f, maxForwardSpeed);
    }

    private void RotateCar(float steering, bool handbrake)
    {
        if (CurrentSpeed < minimumSpeedToSteer)
        {
            return;
        }

        float finalSteering = invertSteering ? -steering : steering;

        float speedFactor = Mathf.InverseLerp(0f, maxForwardSpeed, CurrentSpeed);
        speedFactor = Mathf.Clamp(speedFactor, 0.25f, 0.75f);

        float handbrakeMultiplier = handbrake ? 1.1f : 1f;

        float rotationAmount =
            finalSteering *
            steeringSpeed *
            speedFactor *
            handbrakeMultiplier *
            Runner.DeltaTime;

        rotationAmount = Mathf.Clamp(rotationAmount, -2f, 2f);

        transform.Rotate(0f, rotationAmount, 0f);
    }
    private void MoveCar()
    {
        Vector3 movement = transform.forward * CurrentSpeed * Runner.DeltaTime;
        networkCharacterController.Move(movement);
    }

    private void ApplyStop()
    {
        CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, 0f, braking * Runner.DeltaTime);
        Vector3 movement = transform.forward * CurrentSpeed * Runner.DeltaTime;
        networkCharacterController.Move(movement);
    }

    private void AnimateWheels()
    {
        float rotationAmount = CurrentSpeed * wheelRotationSpeed * Time.deltaTime;

        RotateWheel(frontLeftWheel, rotationAmount, true);
        RotateWheel(frontRightWheel, rotationAmount, true);
        RotateWheel(rearLeftWheel, rotationAmount, false);
        RotateWheel(rearRightWheel, rotationAmount, false);
    }

    private void RotateWheel(Transform wheel, float rotationAmount, bool canSteer)
    {
        if (wheel == null)
        {
            return;
        }

        wheel.Rotate(Vector3.right, rotationAmount, Space.Self);

        if (canSteer)
        {
            Vector3 localEulerAngles = wheel.localEulerAngles;
            localEulerAngles.y = localSteeringVisualAngle;
            wheel.localEulerAngles = localEulerAngles;
        }
    }
}