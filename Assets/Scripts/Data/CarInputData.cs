using Fusion;

public enum CarInputButton
{
    Brake = 0,
    Handbrake = 1
}

public struct CarInputData : INetworkInput
{
    public float Throttle;
    public float Steering;
    public NetworkButtons Buttons;
}