using NetSquare.Core;
using NetSquareClient;
using System.Collections.Concurrent;
using UnityEngine;

namespace NetSquare.Client
{
    [RequireComponent(typeof(NetSquarePlayerController))]
    public class NetsquareClientBot : MonoBehaviour
    {
        public NetSquarePlayerController PlayerController;
        public int NbMaxMessagesByFrame = 32;
        public bool IsConnected { get; private set; }
        private float horizontal;
        private float vertical;
        private float mouseX;
        private bool sprint;
        private bool jump;
        private Vector3 targetPosition;
        private NetSquare_Client client;
        private ConcurrentQueue<NetSquareActionData> netSquareActions = new ConcurrentQueue<NetSquareActionData>();
        private NetSquareActionData currentAction;
        private float jumpTime = 0f;
        private float sprintTime = 0f;
        private float positionTime = 0f;

        private void Start()
        {
            IsConnected = false;
            client = new NetSquare_Client(eProtocoleType.TCP, false);
            client.Dispatcher.SetMainThreadCallback(ExecuteInMainThread);
            client.OnException += Client_OnException;
            client.OnConnected += Client_OnConnected;
            client.Connect(NetSquareController.Instance.IPAdress, NetSquareController.Instance.Port);
        }

        private void Client_OnException(System.Exception obj)
        {
            Debug.LogError("NetSquare reception exception : \n" + obj.ToString());
        }

        private void Client_OnConnected(uint obj)
        {
            IsConnected = true;
            // connect to the world in main thread because it use a Unity transform
            ExecuteInMainThread((msg) =>
            {
                // Create a new transform sender
                PlayerController.TransformSender = new NetsquareTransformSender(PlayerController.NetworkSendRate, PlayerController.TransformFramesStoreRate, PlayerController.TransformFramesStoreRateFast);
                // Join a world
                PlayerController.TransformSender.JoinWorld(client, 1, transform);
            }, null);
        }

        private void OnDestroy()
        {
            client.Disconnect();
            client.OnConnected -= Client_OnConnected;
        }

        /// <summary>
        /// Enqueue Action and message and pack it as a delegate for NetSquare Dispatcher.
        /// that way, Dispatcher will invoke network messages actions from main thread
        /// </summary>
        /// <param name="action"></param>
        /// <param name="message"></param>
        public void ExecuteInMainThread(NetSquareAction action, NetworkMessage message)
        {
            netSquareActions.Enqueue(new NetSquareActionData(action, message));
        }

        public void BotUpdate()
        {
            // Execute the network messages
            short i = 0;
            while (netSquareActions.Count > 0 && i <= NbMaxMessagesByFrame)
            {
                i++;
                if (!netSquareActions.TryDequeue(out currentAction))
                    continue;

                currentAction.Action?.Invoke(currentAction.Message);
            }

            // Check if the player controller is null or the client is not connected
            if (PlayerController == null || !IsConnected)
            {
                return;
            }

            // Update the bot states
            if (jumpTime > Time.time)
            {
                jump = Random.Range(0, 2) == 0;
                jumpTime = Random.Range(4f, 8f) + Time.time;
            }
            else
            {
                jump = false;
            }
            if (sprintTime < Time.time)
            {
                sprint = Random.Range(0, 2) == 0;
                sprintTime = Random.Range(1f, 5f) + Time.time;
            }
            if (positionTime < Time.time)
            {
                targetPosition = new Vector3(Random.Range(0f, 200f), 0, Random.Range(0f, 200f));
                positionTime = Random.Range(2f, 5f) + Time.time;
            }

            // Update the player input 
            horizontal = 0;
            vertical = 0;
            Vector3 targetDir = targetPosition - PlayerController.transform.position;
            targetDir.Normalize();
            horizontal = targetDir.x;
            vertical = targetDir.z;

            if (horizontal < 0.1f && horizontal > -0.1f)
            {
                horizontal = 0;
            }
            if (vertical < 0.1f && vertical > -0.1f)
            {
                vertical = 0;
            }

            // determinate mouse X to face the target
            float angle = Vector3.SignedAngle(targetDir, -PlayerController.transform.forward, Vector3.up);
            mouseX = angle > 10f ? 1f : angle < -10f ? -1f : 0f;

            // Update the player states
            PlayerController.Move(horizontal, vertical, sprint);
            PlayerController.Rotate(mouseX);
            PlayerController.Jump(jump);
            PlayerController.UpdatePlayer();
            PlayerController.Sync(client);
        }
    }
}