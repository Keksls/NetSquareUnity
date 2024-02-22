using NetSquare.Core;
using NetSquareClient;
using System.Collections;
using System.Threading;
using UnityEngine;

namespace NetSquare.Client
{
    [RequireComponent(typeof(NetSquarePlayerController))]
    public class NetsquareClientBot : MonoBehaviour
    {
        public NetSquarePlayerController PlayerController;
        private float horizontal;
        private float vertical;
        private float mouseX;
        private bool sprint;
        private bool jump;
        private Vector3 targetPosition;
        private NetSquare_Client client;
        public bool IsConnected { get; private set; }

        private void Start()
        {
            IsConnected = false;
            client = new NetSquare_Client(eProtocoleType.TCP, false);
            client.OnConnected += Client_OnConnected;
            client.Connect(NetSquareController.Instance.IPAdress, NetSquareController.Instance.Port);
        }

        private void Client_OnConnected(uint obj)
        {
            IsConnected = true;
            Thread t = new Thread(() =>
            {
                client.SyncTime(5);
            });
            t.Start();
            // Create a new transform sender
            PlayerController.TransformSender.WorldsManager = client.WorldsManager;
            PlayerController.TransformSender = new NetsquareTransformSender(PlayerController.NetworkSendRate, PlayerController.TransformFramesStoreRate, PlayerController.TransformFramesStoreRateFast);
            // Join a world
            PlayerController.TransformSender.JoinWorld(1, transform);
            // Start the bot routines
            StartCoroutine(JumpRoutine());
            StartCoroutine(DeterminateDestination());
            StartCoroutine(SprintRoutine());
        }

        private void OnDestroy()
        {
            client.Disconnect();
            client.OnConnected -= Client_OnConnected;
        }

        private void Update()
        {
            // Check if the player controller is null or the client is not connected
            if (PlayerController == null || !IsConnected)
            {
                return;
            }

            // Update the player input 
            horizontal = 0;
            vertical = 0;
            Vector3 targetDir = targetPosition - PlayerController.transform.position;
            horizontal = targetDir.x;
            vertical = targetDir.z;

            // determinate mouse X to face the target
            float angle = Vector3.SignedAngle(targetDir, PlayerController.transform.forward, Vector3.up);
            mouseX = angle > 10f ? 1f : angle < -10f ? -1f : 0f;

            // Update the player states
            PlayerController.Move(horizontal, vertical, sprint);
            PlayerController.Rotate(mouseX);
            PlayerController.Jump(jump);
            PlayerController.UpdatePlayer();
            PlayerController.Sync();
        }

        IEnumerator JumpRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(1f, 5f));
                jump = true;
                yield return null;
                jump = false;
            }
        }

        IEnumerator DeterminateDestination()
        {
            while (true)
            {
                targetPosition = new Vector3(Random.Range(0f, 100f), 0, Random.Range(0f, 100f));
                yield return new WaitForSeconds(Random.Range(5f, 20f));
            }
        }

        IEnumerator SprintRoutine()
        {
            while (true)
            {
                sprint = Random.Range(0, 2) == 0;
                yield return new WaitForSeconds(Random.Range(1f, 5f));
            }
        }
    }
}