using UnityEngine;

namespace NetSquare.Client
{
    public class NetsquareOtherPlayerController : MonoBehaviour
    {
        public Animator Animator;
        public PlayerStates States = new PlayerStates();

        public void SetTransform(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;
        }

        public void SetAnimation()
        {
            if(Animator == null)
            {
                return;
            }
            Animator.SetBool("IsJumping", States.IsJumping);
            Animator.SetBool("IsFalling", States.IsFalling);
            Animator.SetBool("IsWalking", States.IsWalking);
        }

        private void Update()
        {
            SetAnimation();
        }

        public void SetState(TransformState state)
        {
            switch (state)
            {
                case TransformState.Jump_True:
                    States.IsJumping = true;
                    break;
                case TransformState.Jump_False:
                    States.IsJumping = false;
                    break;
                case TransformState.Fall_True:
                    States.IsFalling = true;
                    break;
                case TransformState.Fall_False:
                    States.IsFalling = false;
                    break;
                case TransformState.Walk_True:
                    States.IsWalking = true;
                    break;
                case TransformState.Walk_False:
                    States.IsWalking = false;
                    break;
                case TransformState.Grounded_True:
                    States.IsGrounded = true;
                    break;
                case TransformState.Grounded_False:
                    States.IsGrounded = false;
                    break;
                case TransformState.Sprint_True:
                    States.IsSprinting = true;
                    break;
                case TransformState.Sprint_False:
                    States.IsSprinting = false;
                    break;

            }
        }
    }
}