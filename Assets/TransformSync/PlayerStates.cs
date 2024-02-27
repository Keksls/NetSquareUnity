namespace NetSquare.Client
{
    /// <summary>
    /// A class to hold the player states
    /// </summary>
    public class PlayerStates
    {
        /// <summary>
        /// Whatever the player is walking
        /// </summary>
        public bool IsWalking;
        /// <summary>
        /// Whatever the player is Jumping
        /// </summary>
        public bool IsJumping;
        /// <summary>
        /// Whatever the player is Grounded
        /// </summary>
        public bool IsGrounded;
        /// <summary>
        /// Whatever the player is Falling
        /// </summary>
        public bool IsFalling;
        /// <summary>
        /// Whatever the player is Sprinting
        /// </summary>
        public bool IsSprinting;
    }
}