/*
    This class is an exemple of a state that can be used to sync the state of the transform over the network.
    It will be used to play the animation of the player on the other clients.

    Feel free to modify it to fit your needs.
    Remember to modify the NetsquareOtherPlayerController class to fit the new state.
    Also, remember to have parameters in the animator that match the state.
*/
namespace NetSquare.Client
{
    /// <summary>
    /// Represents the state of the NetSquareTransform.
    /// Will be used to sync the state of the transform over the network.
    /// Basically, it will be used to play the animation of the player on the other clients.
    /// </summary>
    public enum NetSqauareTransformState
    {
        /// <summary>
        /// No state.
        /// </summary>
        None = 0,
        /// <summary>
        /// The player is jumping.
        /// </summary>
        Jump_True = 1,
        /// <summary>
        /// The player is not jumping.
        /// </summary>
        Jump_False = 2,
        /// <summary>
        /// The player is falling.
        /// </summary>
        Fall_True = 3,
        /// <summary>
        /// The player is not falling.
        /// </summary>
        Fall_False = 4,
        /// <summary>
        /// The player is walking.
        /// </summary>
        Walk_True = 5,
        /// <summary>
        /// The player is not walking.
        /// </summary>
        Walk_False = 6,
        /// <summary>
        /// The player is grounded.
        /// </summary>
        Grounded_True = 7,
        /// <summary>
        /// The player is not grounded.
        /// </summary>
        Grounded_False = 8,
        /// <summary>
        /// The player is sprinting.
        /// </summary>
        Sprint_True = 9,
        /// <summary>
        /// The player is not sprinting.
        /// </summary>
        Sprint_False = 10,
    }
}