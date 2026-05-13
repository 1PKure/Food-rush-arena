using Fusion;

public enum CarInputButton
{
    Handbrake = 0
}

public struct CarInputData : INetworkInput
{
    public float Throttle;
    public float Steering;
    public NetworkButtons Buttons;
}