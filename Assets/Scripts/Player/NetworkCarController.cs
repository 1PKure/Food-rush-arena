using Fusion;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class NetworkCarController : NetworkBehaviour
{
    [Header("Car Setup")]
    [Range(20, 190)]
    [SerializeField] private int maxSpeed = 90;

    [Range(10, 120)]
    [SerializeField] private int maxReverseSpeed = 45;

    [Range(1, 10)]
    [SerializeField] private int accelerationMultiplier = 2;

    [Range(10, 45)]
    [SerializeField] private int maxSteeringAngle = 27;

    [Range(0.1f, 1f)]
    [SerializeField] private float steeringSpeed = 0.5f;

    [Range(100, 600)]
    [SerializeField] private int brakeForce = 350;

    [Range(1, 10)]
    [SerializeField] private int decelerationMultiplier = 2;

    [Range(1, 10)]
    [SerializeField] private int handbrakeDriftMultiplier = 5;

    [SerializeField] private Vector3 bodyMassCenter;

    [Header("Wheels")]
    [SerializeField] private GameObject frontLeftMesh;
    [SerializeField] private WheelCollider frontLeftCollider;

    [SerializeField] private GameObject frontRightMesh;
    [SerializeField] private WheelCollider frontRightCollider;

    [SerializeField] private GameObject rearLeftMesh;
    [SerializeField] private WheelCollider rearLeftCollider;

    [SerializeField] private GameObject rearRightMesh;
    [SerializeField] private WheelCollider rearRightCollider;

    [Header("Effects")]
    [SerializeField] private bool useEffects = true;
    [SerializeField] private ParticleSystem rearLeftWheelParticleSystem;
    [SerializeField] private ParticleSystem rearRightWheelParticleSystem;
    [SerializeField] private TrailRenderer rearLeftTireSkid;
    [SerializeField] private TrailRenderer rearRightTireSkid;

    [Header("Debug")]
    [SerializeField] private bool useLocalInputWhenNotNetworked = false;

    [Networked] public float NetworkedSpeed { get; private set; }
    [Networked] public bool NetworkedIsDrifting { get; private set; }
    [Networked] public bool NetworkedIsHandbraking { get; private set; }

    public float CurrentSpeed => carSpeed;
    public bool IsDrifting => isDrifting;
    public bool IsHandbraking => isTractionLocked;

    private Rigidbody carRigidbody;

    private float carSpeed;
    private float steeringAxis;
    private float throttleAxis;
    private float driftingAxis;
    private float localVelocityZ;
    private float localVelocityX;

    private bool isDrifting;
    private bool isTractionLocked;

    private WheelFrictionCurve frontLeftWheelFriction;
    private WheelFrictionCurve frontRightWheelFriction;
    private WheelFrictionCurve rearLeftWheelFriction;
    private WheelFrictionCurve rearRightWheelFriction;

    private float frontLeftExtremumSlip;
    private float frontRightExtremumSlip;
    private float rearLeftExtremumSlip;
    private float rearRightExtremumSlip;

    private float DeltaTime => Runner != null ? Runner.DeltaTime : Time.fixedDeltaTime;

    private void Awake()
    {
        carRigidbody = GetComponent<Rigidbody>();
        carRigidbody.centerOfMass = bodyMassCenter;

        CacheWheelFriction();
        StopEffects();
    }

    public override void Spawned()
    {
        Debug.Log($"[NetworkCarController] Spawned | InputAuthority: {Object.InputAuthority} | HasInputAuthority: {Object.HasInputAuthority} | HasStateAuthority: {Object.HasStateAuthority}");

        if (Object.HasStateAuthority)
        {
            carRigidbody.isKinematic = false;
        }
        else
        {
            carRigidbody.isKinematic = true;
            StopCarForProxy();
        }

        Debug.Log($"[NetworkCarController] Rigidbody | IsKinematic: {carRigidbody.isKinematic} | Mass: {carRigidbody.mass} | Constraints: {carRigidbody.constraints}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
        {
            AnimateWheelMeshes();
            return;
        }

        UpdateCarData();

        if (!CanMove())
        {
            ApplyInput(default);
            SetBrakeTorque(brakeForce);
            AnimateWheelMeshes();
            return;
        }

        if (GetInput(out CarInputData input))
        {
            ApplyInput(input);
        }
        else
        {
            ApplyInput(default);
        }

        AnimateWheelMeshes();

        NetworkedSpeed = Mathf.Abs(carSpeed);
        NetworkedIsDrifting = isDrifting;
        NetworkedIsHandbraking = isTractionLocked;
    }
    private bool CanMove()
    {
        if (NetworkRaceManager.Instance == null)
        {
            return true;
        }

        return NetworkRaceManager.Instance.CanPlayersMove;
    }

    private void FixedUpdate()
    {
        if (Runner != null)
        {
            return;
        }

        if (!useLocalInputWhenNotNetworked)
        {
            return;
        }

        UpdateCarData();

        CarInputData input = ReadLocalDebugInput();
        ApplyInput(input);

        AnimateWheelMeshes();
    }

    private CarInputData ReadLocalDebugInput()
    {
        CarInputData input = new CarInputData();

        if (Input.GetKey(KeyCode.W))
        {
            input.Throttle += 1f;
        }

        if (Input.GetKey(KeyCode.S))
        {
            input.Throttle -= 1f;
        }

        if (Input.GetKey(KeyCode.A))
        {
            input.Steering -= 1f;
        }

        if (Input.GetKey(KeyCode.D))
        {
            input.Steering += 1f;
        }

        input.Buttons.Set((int)CarInputButton.Handbrake, Input.GetKey(KeyCode.Space));

        return input;
    }

    private void ApplyInput(CarInputData input)
    {
        float throttle = Mathf.Clamp(input.Throttle, -1f, 1f);
        float steering = Mathf.Clamp(input.Steering, -1f, 1f);
        bool handbrake = input.Buttons.IsSet((int)CarInputButton.Handbrake);

        if (throttle > 0.1f)
        {
            GoForward();
        }
        else if (throttle < -0.1f)
        {
            GoReverse();
        }
        else
        {
            ThrottleOff();
            DecelerateCar();
        }

        if (steering < -0.1f)
        {
            TurnLeft();
        }
        else if (steering > 0.1f)
        {
            TurnRight();
        }
        else
        {
            ResetSteeringAngle();
        }

        if (handbrake)
        {
            Handbrake();
        }
        else
        {
            RecoverTraction();
        }

        Debug.Log(
    $"[Car Input] Throttle: {input.Throttle} | Steering: {input.Steering} | " +
    $"MotorTorque: {frontLeftCollider.motorTorque} | BrakeTorque: {frontLeftCollider.brakeTorque} | " +
    $"Speed: {carSpeed} | Velocity: {carRigidbody.linearVelocity.magnitude}"
);
    }

    private void UpdateCarData()
    {
        if (frontLeftCollider != null)
        {
            carSpeed = (2f * Mathf.PI * frontLeftCollider.radius * frontLeftCollider.rpm * 60f) / 1000f;
        }

        Vector3 localVelocity = transform.InverseTransformDirection(carRigidbody.linearVelocity);
        localVelocityX = localVelocity.x;
        localVelocityZ = localVelocity.z;
    }

    private void CacheWheelFriction()
    {
        frontLeftWheelFriction = frontLeftCollider.sidewaysFriction;
        frontRightWheelFriction = frontRightCollider.sidewaysFriction;
        rearLeftWheelFriction = rearLeftCollider.sidewaysFriction;
        rearRightWheelFriction = rearRightCollider.sidewaysFriction;

        frontLeftExtremumSlip = frontLeftWheelFriction.extremumSlip;
        frontRightExtremumSlip = frontRightWheelFriction.extremumSlip;
        rearLeftExtremumSlip = rearLeftWheelFriction.extremumSlip;
        rearRightExtremumSlip = rearRightWheelFriction.extremumSlip;
    }

    private void TurnLeft()
    {
        steeringAxis -= DeltaTime * 10f * steeringSpeed;
        steeringAxis = Mathf.Clamp(steeringAxis, -1f, 1f);

        ApplySteering();
    }

    private void TurnRight()
    {
        steeringAxis += DeltaTime * 10f * steeringSpeed;
        steeringAxis = Mathf.Clamp(steeringAxis, -1f, 1f);

        ApplySteering();
    }

    private void ResetSteeringAngle()
    {
        if (steeringAxis < 0f)
        {
            steeringAxis += DeltaTime * 10f * steeringSpeed;
        }
        else if (steeringAxis > 0f)
        {
            steeringAxis -= DeltaTime * 10f * steeringSpeed;
        }

        if (Mathf.Abs(frontLeftCollider.steerAngle) < 1f)
        {
            steeringAxis = 0f;
        }

        ApplySteering();
    }

    private void ApplySteering()
    {
        float steeringAngle = steeringAxis * maxSteeringAngle;

        frontLeftCollider.steerAngle = Mathf.Lerp(frontLeftCollider.steerAngle, steeringAngle, steeringSpeed);
        frontRightCollider.steerAngle = Mathf.Lerp(frontRightCollider.steerAngle, steeringAngle, steeringSpeed);
    }

    private void GoForward()
    {
        UpdateDriftState();

        throttleAxis += DeltaTime * 3f;
        throttleAxis = Mathf.Clamp(throttleAxis, -1f, 1f);

        if (localVelocityZ < -1f)
        {
            Brakes();
            return;
        }

        if (Mathf.RoundToInt(carSpeed) < maxSpeed)
        {
            SetBrakeTorque(0f);
            SetMotorTorque((accelerationMultiplier * 50f) * throttleAxis);
        }
        else
        {
            SetMotorTorque(0f);
        }
    }

    private void GoReverse()
    {
        UpdateDriftState();

        throttleAxis -= DeltaTime * 3f;
        throttleAxis = Mathf.Clamp(throttleAxis, -1f, 1f);

        if (localVelocityZ > 1f)
        {
            Brakes();
            return;
        }

        if (Mathf.Abs(Mathf.RoundToInt(carSpeed)) < maxReverseSpeed)
        {
            SetBrakeTorque(0f);
            SetMotorTorque((accelerationMultiplier * 50f) * throttleAxis);
        }
        else
        {
            SetMotorTorque(0f);
        }
    }

    private void ThrottleOff()
    {
        SetMotorTorque(0f);
    }

    private void DecelerateCar()
    {
        UpdateDriftState();

        if (throttleAxis != 0f)
        {
            if (throttleAxis > 0f)
            {
                throttleAxis -= DeltaTime * 10f;
            }
            else if (throttleAxis < 0f)
            {
                throttleAxis += DeltaTime * 10f;
            }

            if (Mathf.Abs(throttleAxis) < 0.15f)
            {
                throttleAxis = 0f;
            }
        }

        carRigidbody.linearVelocity *= 1f / (1f + (0.025f * decelerationMultiplier));

        SetMotorTorque(0f);

        if (carRigidbody.linearVelocity.magnitude < 0.25f)
        {
            carRigidbody.linearVelocity = Vector3.zero;
        }
    }

    private void Brakes()
    {
        SetBrakeTorque(brakeForce);
    }

    private void Handbrake()
    {
        driftingAxis += DeltaTime;

        float secureStartingPoint = driftingAxis * frontLeftExtremumSlip * handbrakeDriftMultiplier;

        if (secureStartingPoint < frontLeftExtremumSlip)
        {
            driftingAxis = frontLeftExtremumSlip / (frontLeftExtremumSlip * handbrakeDriftMultiplier);
        }

        driftingAxis = Mathf.Clamp01(driftingAxis);

        isDrifting = Mathf.Abs(localVelocityX) > 2.5f;
        isTractionLocked = true;

        if (driftingAxis < 1f)
        {
            frontLeftWheelFriction.extremumSlip = frontLeftExtremumSlip * handbrakeDriftMultiplier * driftingAxis;
            frontRightWheelFriction.extremumSlip = frontRightExtremumSlip * handbrakeDriftMultiplier * driftingAxis;
            rearLeftWheelFriction.extremumSlip = rearLeftExtremumSlip * handbrakeDriftMultiplier * driftingAxis;
            rearRightWheelFriction.extremumSlip = rearRightExtremumSlip * handbrakeDriftMultiplier * driftingAxis;

            ApplySidewaysFriction();
        }

        UpdateEffects();
    }

    private void RecoverTraction()
    {
        isTractionLocked = false;

        driftingAxis -= DeltaTime / 1.5f;
        driftingAxis = Mathf.Clamp01(driftingAxis);

        if (frontLeftWheelFriction.extremumSlip > frontLeftExtremumSlip)
        {
            frontLeftWheelFriction.extremumSlip = frontLeftExtremumSlip * handbrakeDriftMultiplier * driftingAxis;
            frontRightWheelFriction.extremumSlip = frontRightExtremumSlip * handbrakeDriftMultiplier * driftingAxis;
            rearLeftWheelFriction.extremumSlip = rearLeftExtremumSlip * handbrakeDriftMultiplier * driftingAxis;
            rearRightWheelFriction.extremumSlip = rearRightExtremumSlip * handbrakeDriftMultiplier * driftingAxis;

            ApplySidewaysFriction();
        }

        if (frontLeftWheelFriction.extremumSlip < frontLeftExtremumSlip || driftingAxis <= 0f)
        {
            frontLeftWheelFriction.extremumSlip = frontLeftExtremumSlip;
            frontRightWheelFriction.extremumSlip = frontRightExtremumSlip;
            rearLeftWheelFriction.extremumSlip = rearLeftExtremumSlip;
            rearRightWheelFriction.extremumSlip = rearRightExtremumSlip;

            driftingAxis = 0f;
            ApplySidewaysFriction();
        }

        UpdateEffects();
    }

    private void UpdateDriftState()
    {
        isDrifting = Mathf.Abs(localVelocityX) > 2.5f;
        UpdateEffects();
    }

    private void ApplySidewaysFriction()
    {
        frontLeftCollider.sidewaysFriction = frontLeftWheelFriction;
        frontRightCollider.sidewaysFriction = frontRightWheelFriction;
        rearLeftCollider.sidewaysFriction = rearLeftWheelFriction;
        rearRightCollider.sidewaysFriction = rearRightWheelFriction;
    }

    private void SetMotorTorque(float torque)
    {
        frontLeftCollider.motorTorque = torque;
        frontRightCollider.motorTorque = torque;
        rearLeftCollider.motorTorque = torque;
        rearRightCollider.motorTorque = torque;
    }

    private void SetBrakeTorque(float torque)
    {
        frontLeftCollider.brakeTorque = torque;
        frontRightCollider.brakeTorque = torque;
        rearLeftCollider.brakeTorque = torque;
        rearRightCollider.brakeTorque = torque;
    }

    private void AnimateWheelMeshes()
    {
        UpdateWheelMesh(frontLeftCollider, frontLeftMesh);
        UpdateWheelMesh(frontRightCollider, frontRightMesh);
        UpdateWheelMesh(rearLeftCollider, rearLeftMesh);
        UpdateWheelMesh(rearRightCollider, rearRightMesh);
    }

    private void UpdateWheelMesh(WheelCollider wheelCollider, GameObject wheelMesh)
    {
        if (wheelCollider == null || wheelMesh == null)
        {
            return;
        }

        wheelCollider.GetWorldPose(out Vector3 position, out Quaternion rotation);

        wheelMesh.transform.position = position;
        wheelMesh.transform.rotation = rotation;
    }

    private void UpdateEffects()
    {
        if (!useEffects)
        {
            StopEffects();
            return;
        }

        bool shouldPlaySmoke = isDrifting;
        bool shouldShowSkid = (isTractionLocked || Mathf.Abs(localVelocityX) > 5f) && Mathf.Abs(carSpeed) > 12f;

        SetParticleState(rearLeftWheelParticleSystem, shouldPlaySmoke);
        SetParticleState(rearRightWheelParticleSystem, shouldPlaySmoke);

        if (rearLeftTireSkid != null)
        {
            rearLeftTireSkid.emitting = shouldShowSkid;
        }

        if (rearRightTireSkid != null)
        {
            rearRightTireSkid.emitting = shouldShowSkid;
        }
    }

    private void SetParticleState(ParticleSystem particleSystem, bool shouldPlay)
    {
        if (particleSystem == null)
        {
            return;
        }

        if (shouldPlay && !particleSystem.isPlaying)
        {
            particleSystem.Play();
        }
        else if (!shouldPlay && particleSystem.isPlaying)
        {
            particleSystem.Stop();
        }
    }

    private void StopEffects()
    {
        SetParticleState(rearLeftWheelParticleSystem, false);
        SetParticleState(rearRightWheelParticleSystem, false);

        if (rearLeftTireSkid != null)
        {
            rearLeftTireSkid.emitting = false;
        }

        if (rearRightTireSkid != null)
        {
            rearRightTireSkid.emitting = false;
        }
    }

    private void StopCarForProxy()
    {
        SetMotorTorque(0f);
        SetBrakeTorque(0f);
        StopEffects();
    }
}