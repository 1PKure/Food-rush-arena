using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkCharacterController))]
public class NetworkCarMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float maxForwardSpeed = 6f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float braking = 100f;
    [SerializeField] private float naturalDeceleration = 35f;

    [Header("Steering")]
    [SerializeField] private float steeringSpeed = 70f;
    [SerializeField] private float steeringSmoothness = 12f;
    [SerializeField] private float minimumSpeedToSteer = 0.5f;
    [SerializeField] private bool invertSteering = false;

    [Header("Grounding")]
    [SerializeField] private bool keepCarGrounded = true;
    [SerializeField] private float groundStickForce = -2f;

    [Header("Visual Wheels")]
    [SerializeField] private Transform frontLeftWheel;
    [SerializeField] private Transform frontRightWheel;
    [SerializeField] private Transform rearLeftWheel;
    [SerializeField] private Transform rearRightWheel;
    [SerializeField] private float wheelRotationSpeed = 720f;
    [SerializeField] private float frontWheelSteeringAngle = 30f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs;

    [Networked] public int Score { get; private set; }
    [Networked] public float CurrentSpeed { get; private set; }
    [Networked] public float CurrentSteering { get; private set; }
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
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log(
            $"[NetworkCarMovement] Spawned | " +
            $"Object: {gameObject.name} | " +
            $"InputAuthority: {Object.InputAuthority} | " +
            $"HasInputAuthority: {Object.HasInputAuthority} | " +
            $"HasStateAuthority: {Object.HasStateAuthority}"
        );
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
        float rawThrottle = Mathf.Clamp(input.Throttle, -1f, 1f);
        float throttle = Mathf.Clamp01(rawThrottle);
        float targetSteering = Mathf.Clamp(input.Steering, -1f, 1f);

        if (invertSteering)
        {
            targetSteering *= -1f;
        }

        bool brake = input.Buttons.IsSet((int)CarInputButton.Brake) || rawThrottle < -0.1f;
        bool handbrake = input.Buttons.IsSet((int)CarInputButton.Handbrake);

        if (brake)
        {
            CurrentSpeed = 0f;
            CurrentSteering = 0f;

            IsAccelerating = false;
            IsBraking = true;

            MoveCar(Vector3.zero);
            return;
        }

        UpdateSpeed(throttle, handbrake);
        UpdateSteering(targetSteering);
        RotateCar(handbrake);

        Vector3 movement = transform.forward * CurrentSpeed;

        if (keepCarGrounded)
        {
            movement.y = groundStickForce;
        }

        MoveCar(movement);

        IsAccelerating = throttle > 0.1f;
        IsBraking = handbrake;

        localSteeringVisualAngle = Mathf.Lerp(
            localSteeringVisualAngle,
            CurrentSteering * frontWheelSteeringAngle,
            Runner.DeltaTime * 10f
        );
    }

    private void UpdateSpeed(float throttle, bool handbrake)
    {
        float deltaTime = Runner.DeltaTime;

        if (throttle > 0.1f)
        {
            CurrentSpeed += acceleration * throttle * deltaTime;
        }
        else
        {
            CurrentSpeed = Mathf.MoveTowards(
                CurrentSpeed,
                0f,
                naturalDeceleration * deltaTime
            );
        }

        if (handbrake)
        {
            CurrentSpeed = Mathf.MoveTowards(
                CurrentSpeed,
                0f,
                braking * deltaTime
            );
        }

        if (CurrentSpeed < 0.05f)
        {
            CurrentSpeed = 0f;
        }

        CurrentSpeed = Mathf.Clamp(CurrentSpeed, 0f, maxForwardSpeed);
    }

    private void UpdateSteering(float targetSteering)
    {
        if (CurrentSpeed < minimumSpeedToSteer)
        {
            targetSteering = 0f;
        }

        CurrentSteering = Mathf.Lerp(
            CurrentSteering,
            targetSteering,
            Runner.DeltaTime * steeringSmoothness
        );

        if (Mathf.Abs(CurrentSteering) < 0.01f)
        {
            CurrentSteering = 0f;
        }
    }

    private void RotateCar(bool handbrake)
    {
        if (CurrentSpeed < minimumSpeedToSteer)
        {
            return;
        }

        float speedFactor = Mathf.InverseLerp(0f, maxForwardSpeed, CurrentSpeed);
        speedFactor = Mathf.Clamp(speedFactor, 0.35f, 1f);

        float handbrakeMultiplier = handbrake ? 1.1f : 1f;

        float rotationAmount =
            CurrentSteering *
            steeringSpeed *
            speedFactor *
            handbrakeMultiplier *
            Runner.DeltaTime;

        transform.Rotate(0f, rotationAmount, 0f, Space.World);
    }

    private void MoveCar(Vector3 movement)
    {
        networkCharacterController.Move(movement);
    }

    private void ApplyStop()
    {
        CurrentSpeed = 0f;
        CurrentSteering = 0f;

        Vector3 movement = Vector3.zero;

        if (keepCarGrounded)
        {
            movement.y = groundStickForce;
        }

        MoveCar(movement);

        IsAccelerating = false;
        IsBraking = true;
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

        if (!canSteer)
        {
            return;
        }

        Vector3 localEulerAngles = wheel.localEulerAngles;
        localEulerAngles.y = localSteeringVisualAngle;
        wheel.localEulerAngles = localEulerAngles;
    }
}