using UnityEngine;

namespace NetSquare.Client
{
    /// <summary>
    /// Applies synchronized transforms and animation state to one remote Player.
    /// </summary>
    public sealed class NetsquareOtherPlayerController : MonoBehaviour
    {
        #region Fields
        [SerializeField]
        private Animator animator;
        private readonly PlayerStates states = new PlayerStates();
        #endregion

        #region Transform
        /// <summary>
        /// Applies a synchronized transform.
        /// </summary>
        /// <param name="position">World position.</param>
        /// <param name="rotation">World rotation.</param>
        public void SetTransform(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
        }
        #endregion

        #region State
        /// <summary>
        /// Applies one synchronized state change and updates only affected Animator parameters.
        /// </summary>
        /// <param name="state">State change.</param>
        public void SetState(NetSquareTransformState state)
        {
            switch (state)
            {
                case NetSquareTransformState.JumpEnabled:
                    states.IsJumping = true;
                    SetAnimatorBool("IsJumping", true);
                    break;
                case NetSquareTransformState.JumpDisabled:
                    states.IsJumping = false;
                    SetAnimatorBool("IsJumping", false);
                    break;
                case NetSquareTransformState.FallEnabled:
                    states.IsFalling = true;
                    SetAnimatorBool("IsFalling", true);
                    break;
                case NetSquareTransformState.FallDisabled:
                    states.IsFalling = false;
                    SetAnimatorBool("IsFalling", false);
                    break;
                case NetSquareTransformState.WalkEnabled:
                    states.IsWalking = true;
                    SetAnimatorBool("IsWalking", true);
                    break;
                case NetSquareTransformState.WalkDisabled:
                    states.IsWalking = false;
                    SetAnimatorBool("IsWalking", false);
                    break;
                case NetSquareTransformState.GroundedEnabled:
                    states.IsGrounded = true;
                    break;
                case NetSquareTransformState.GroundedDisabled:
                    states.IsGrounded = false;
                    break;
                case NetSquareTransformState.SprintEnabled:
                    states.IsSprinting = true;
                    break;
                case NetSquareTransformState.SprintDisabled:
                    states.IsSprinting = false;
                    break;
            }
        }

        /// <summary>
        /// Sets one Animator Boolean when an Animator is assigned.
        /// </summary>
        /// <param name="parameter">Animator parameter name.</param>
        /// <param name="value">Parameter value.</param>
        private void SetAnimatorBool(string parameter, bool value)
        {
            if (animator != null)
                animator.SetBool(parameter, value);
        }
        #endregion
    }
}
