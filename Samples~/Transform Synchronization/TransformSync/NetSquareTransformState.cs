namespace NetSquare.Client
{
    /// <summary>
    /// Defines animation state changes transported by the transform synchronization sample.
    /// </summary>
    public enum NetSquareTransformState
    {
        None = 0,
        JumpEnabled = 1,
        JumpDisabled = 2,
        FallEnabled = 3,
        FallDisabled = 4,
        WalkEnabled = 5,
        WalkDisabled = 6,
        GroundedEnabled = 7,
        GroundedDisabled = 8,
        SprintEnabled = 9,
        SprintDisabled = 10
    }
}
