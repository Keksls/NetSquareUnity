using System.Collections;
using UnityEngine;

namespace NetSquare.Client
{
    public class NetSquarePlayerController : MonoBehaviour
    {
        #region Variables
        [Header("Player")]
        [HideInInspector]
        public PlayerStates States;
        public Animator PlayerAnimator;
        public Transform CameraTransform;
        public bool IsLocalPlayer = true;
        [SerializeField]
        private bool debug;
        [Header("Movement")]
        public float Speed = 5;
        public float SprintSpeed = 10;
        public AnimationCurve AccelerationCurve;
        public float AccelerationDuration = 0.5f;
        private float accelerationTime = 0.5f;
        private float sprintAccelerationTime = 0.3f;
        public AnimationCurve DecelerationCurve;
        public float DecelerationDuration = 0.5f;
        private float decelerationTime = 0.1f;
        public float RotationSpeed = 5;
        [Header("Ground Checker")]
        public Transform GroundCheck;
        public LayerMask GroundLayer;
        public float GroundCheckRadius = 0.2f;
        public Vector3 PositionOffset;
        [Header("Gravity")]
        public float GravityForce = 9.81f;
        public AnimationCurve GravityAccelerationCurve;
        public float GravityAccelerationDuration = 0.5f;
        private float gravityTime = 0f;
        [Header("Jump")]
        public float JumpHeight = 3f;
        public float JumpTime = 0.5f;
        public float JumpSpeedMultiplier = 0.5f;
        public AnimationCurve JumpCurve;
        [Header("Network")]
        public float NetworkSendRate = 0.5f;
        public float TransformFramesStoreRate = 0.2f;
        public float TransformFramesStoreRateFast = 0.1f;
        public NetsquareTransformSender TransformSender;
        #endregion

        /// <summary>
        /// Awake the player
        /// </summary>
        private void Awake()
        {
            States = new PlayerStates();
            if (IsLocalPlayer)
            {
                if (NSClient.IsConnected)
                {
                    NSClient_OnConnected(NSClient.ClientID);
                }
                else
                {
                    NSClient.OnConnected += NSClient_OnConnected;
                }
            }
        }

        /// <summary>
        /// OnConnected event
        /// </summary>
        /// <param name="clientID"> The client ID </param>
        private void NSClient_OnConnected(uint clientID)
        {
            InitializeTransformSender();
            // Join a world
            TransformSender.JoinWorld(NSClient.Client, 1, transform);
        }

        /// <summary>
        /// Update the player
        /// </summary>
        void Update()
        {
            if (IsLocalPlayer)
            {
                float horizontal = Input.GetAxis("Horizontal");
                float vertical = Input.GetAxis("Vertical");
                if (horizontal > 0f)
                {
                    horizontal = 1f;
                }
                else if (horizontal < 0f)
                {
                    horizontal = -1f;
                }
                if (vertical > 0f)
                {
                    vertical = 1f;
                }
                else if (vertical < 0f)
                {
                    vertical = -1f;
                }

                // Handle the player inputs
                Move(horizontal, vertical, Input.GetKey(KeyCode.LeftShift));
                Rotate(Input.GetAxis("Mouse X"));
                Jump(Input.GetKeyDown(KeyCode.Space));
                UpdatePlayer();
                Sync(NSClient.Client);
            }
        }

        /// <summary>
        /// Initialize the transform sender using the given parameters
        /// </summary>
        public void InitializeTransformSender()
        {
            // Create a new transform sender
            TransformSender = new NetsquareTransformSender(NetworkSendRate, TransformFramesStoreRate, TransformFramesStoreRateFast);
        }

        /// <summary>
        /// Apply gravity, snap the player to the ground and update the animator
        /// Must be called AFTER the player inputs (Move, Rotate, Jump)
        /// </summary>
        public void UpdatePlayer()
        {
            // Apply gravity
            if (!States.IsGrounded && !States.IsJumping)
            {
                gravityTime += Time.deltaTime;
                float t = gravityTime / GravityAccelerationDuration;
                transform.position += Vector3.down * GravityForce * GravityAccelerationCurve.Evaluate(t) * Time.deltaTime;
                States.IsFalling = true;
            }
            else
            {
                gravityTime = 0;
                States.IsFalling = false;
            }

            // snap player to ground if needed
            RaycastHit hit;
            if (Physics.Raycast(transform.position + new Vector3(0f, 200f, 0f), Vector3.down, out hit, 1000f, GroundLayer))
            {
                // player is on top of the ground
                if (hit.point.y < transform.position.y - PositionOffset.y && !States.IsJumping && !States.IsFalling)
                {
                    transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z) + PositionOffset;
                }
                // player is under the ground
                else if (hit.point.y > transform.position.y - PositionOffset.y)
                {
                    transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z) + PositionOffset;
                }
            }

            // Check if the player is grounded
            States.IsGrounded = Physics.CheckSphere(GroundCheck.position, GroundCheckRadius, GroundLayer);

            // Update the animator
            if (PlayerAnimator != null && IsLocalPlayer)
            {
                PlayerAnimator.SetBool("IsFalling", States.IsFalling);
                PlayerAnimator.SetBool("IsJumping", States.IsJumping);
                PlayerAnimator.SetBool("IsWalking", States.IsWalking);
            }
        }

        /// <summary>
        /// Jump update
        /// Must be called each frame
        /// </summary>
        /// <param name="jump"> The jump input </param>
        public void Jump(bool jump)
        {
            // Jump
            if (jump && States.IsGrounded)
            {
                States.IsJumping = true;
                StartCoroutine(Jump_Routine());
            }
        }

        /// <summary>
        /// Rotate update
        /// Must be called each frame
        /// </summary>
        /// <param name="mouseX"> The mouse X input </param>
        public void Rotate(float mouseX)
        {
            // Rotate the player
            transform.Rotate(Vector3.up, mouseX * RotationSpeed);
        }

        /// <summary>
        /// Move update
        /// </summary>
        /// <param name="horizontal"> The horizontal input </param>
        /// <param name="vertical"> The vertical input </param>
        /// <param name="sprint"> The sprint input </param>
        public void Move(float horizontal, float vertical, bool sprint)
        {
            // Move the player
            Vector3 move = new Vector3(horizontal, 0, vertical);
            States.IsWalking = move.magnitude > 0;

            // Apply acceleration
            if (States.IsWalking)
            {
                float speed = 0f;
                if (sprint)
                {
                    sprintAccelerationTime += Time.deltaTime;
                    float t = sprintAccelerationTime / AccelerationDuration;
                    speed = Speed + SprintSpeed * AccelerationCurve.Evaluate(t);
                }
                else
                {
                    sprintAccelerationTime = 0;
                    accelerationTime += Time.deltaTime;
                    float t = accelerationTime / AccelerationDuration;
                    speed = Speed * AccelerationCurve.Evaluate(t);
                    // Apply deceleration
                    decelerationTime = 0;
                }

                // Apply jump speed multiplier
                if (States.IsJumping || States.IsFalling)
                {
                    speed *= JumpSpeedMultiplier;
                }

                move = CameraTransform ? CameraTransform.TransformDirection(move) : move;
                move.y = 0;
                move.Normalize();
                move = move * speed * Time.deltaTime;
                transform.position += move;
            }
            else
            {
                accelerationTime = 0;
                decelerationTime += Time.deltaTime;
                if (decelerationTime <= DecelerationDuration)
                {
                    float t = decelerationTime / DecelerationDuration;
                    float speed = Speed * DecelerationCurve.Evaluate(t);
                    move = CameraTransform ? CameraTransform.TransformDirection(move) : move;
                    move.y = 0;
                    move.Normalize();
                    move = move * speed * Time.deltaTime;
                    transform.position += move;
                }
            }
        }

        /// <summary>
        /// Sync the player state to the server
        /// </summary>
        public void Sync(NetSquareClient client)
        {
            // Send the player state to the server
            TransformSender.Update(client, States, transform);
        }

        /// <summary>
        /// Jump routine
        /// </summary>
        /// <returns> The IEnumerator </returns>
        private IEnumerator Jump_Routine()
        {
            float time = 0;
            float startY = transform.position.y;
            while (time < JumpTime)
            {
                time += Time.deltaTime;
                float t = time / JumpTime;
                float height = JumpCurve.Evaluate(t);
                transform.position = new Vector3(transform.position.x, startY + height * JumpHeight, transform.position.z);
                yield return null;
            }
            States.IsJumping = false;
        }

        private void OnDrawGizmos()
        {
            if (!debug)
            {
                return;
            }
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(GroundCheck.position, GroundCheckRadius);
        }

        private void OnGUI()
        {
            // Check if the player is local
            if (!IsLocalPlayer || !debug)
            {
                return;
            }
            // Display the player state
            GUI.Label(new Rect(10, 10, 200, 20), "States.IsJumping: " + States.IsJumping);
            GUI.Label(new Rect(10, 30, 200, 20), "States.IsGrounded: " + States.IsGrounded);
            GUI.Label(new Rect(10, 50, 200, 20), "States.IsFalling: " + States.IsFalling);
            GUI.Label(new Rect(10, 70, 200, 20), "States.IsWalking: " + States.IsWalking);
            // Display client time
            GUI.Label(new Rect(10, 90, 200, 20), "Client Time: " + NSClient.ServerTime);
        }
    }
}