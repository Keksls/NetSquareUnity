using UnityEngine;

namespace NetSquare.Client
{
    /// <summary>
    /// This class is used to control the other player's movement and animation.
    /// </summary>
    public class NetsquareOtherPlayerController : MonoBehaviour
    {
        #region Variables
        [SerializeField]
        private Animator animator;
        [SerializeField]
        private PlayerStates states = new PlayerStates();
        #endregion

        /// <summary>
        /// Set the transform of the other player.
        /// </summary>
        /// <param name="position"> The position of the other player. </param>
        /// <param name="rotation"> The rotation of the other player. </param>
        public void SetTransform(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;
        }

        /// <summary>
        /// Set the animation of the other player.
        /// </summary>
        public void SetAnimation()
        {
            // If the animator is null, return.
            if (animator == null)
            {
                return;
            }
            // Set the animation parameters.
            animator.SetBool("IsJumping", states.IsJumping);
            animator.SetBool("IsFalling", states.IsFalling);
            animator.SetBool("IsWalking", states.IsWalking);
        }

        /// <summary>
        /// Update is called once per frame.
        /// </summary>
        private void Update()
        {
            // Set the animation.
            SetAnimation();
        }

        /// <summary>
        /// Set the state of the other player.
        /// </summary>
        /// <param name="state"> The state of the other player. </param>
        public void SetState(NetSqauareTransformState state)
        {
            switch (state)
            {
                case NetSqauareTransformState.Jump_True:
                    states.IsJumping = true;
                    break;
                case NetSqauareTransformState.Jump_False:
                    states.IsJumping = false;
                    break;
                case NetSqauareTransformState.Fall_True:
                    states.IsFalling = true;
                    break;
                case NetSqauareTransformState.Fall_False:
                    states.IsFalling = false;
                    break;
                case NetSqauareTransformState.Walk_True:
                    states.IsWalking = true;
                    break;
                case NetSqauareTransformState.Walk_False:
                    states.IsWalking = false;
                    break;
                case NetSqauareTransformState.Grounded_True:
                    states.IsGrounded = true;
                    break;
                case NetSqauareTransformState.Grounded_False:
                    states.IsGrounded = false;
                    break;
                case NetSqauareTransformState.Sprint_True:
                    states.IsSprinting = true;
                    break;
                case NetSqauareTransformState.Sprint_False:
                    states.IsSprinting = false;
                    break;

            }
        }
    }
}