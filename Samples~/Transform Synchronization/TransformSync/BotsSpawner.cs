using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace NetSquare.Client
{
    /// <summary>
    /// Spawns optional independently connected sample bots with bounded startup waiting.
    /// </summary>
    public sealed class BotsSpawner : MonoBehaviour
    {
        #region Fields
        [SerializeField]
        private GameObject botPrefab;
        [SerializeField]
        [Min(0)]
        private int botsCount = 10;
        [FormerlySerializedAs("MaxSpawnX")]
        [SerializeField]
        [Min(0f)]
        private float maxSpawnX = 100f;
        [FormerlySerializedAs("MaxSpawnY")]
        [SerializeField]
        [Min(0f)]
        private float maxSpawnZ = 100f;
        [SerializeField]
        [Min(0f)]
        private float spawnInterval = 2f;
        [SerializeField]
        [Min(1f)]
        private float primaryClientWaitTimeout = 30f;

        private readonly List<NetSquareClientBot> bots = new List<NetSquareClientBot>();
        #endregion

        #region Unity lifecycle
        /// <summary>
        /// Starts bounded bot creation when a prefab is configured.
        /// </summary>
        private void Start()
        {
            if (botPrefab == null)
            {
                Debug.LogError("[NetSquare] BotsSpawner requires a bot prefab.");
                enabled = false;
                return;
            }

            StartCoroutine(SpawnBots());
        }

        /// <summary>
        /// Updates existing bots and removes destroyed references without allocations.
        /// </summary>
        private void Update()
        {
            for (int i = bots.Count - 1; i >= 0; i--)
            {
                NetSquareClientBot bot = bots[i];
                if (bot == null)
                    bots.RemoveAt(i);
                else
                    bot.BotUpdate();
            }
        }
        #endregion

        #region Spawning
        /// <summary>
        /// Waits for the primary Client and incrementally spawns configured bots.
        /// </summary>
        /// <returns>Unity coroutine enumerator.</returns>
        private IEnumerator SpawnBots()
        {
            float deadline = Time.realtimeSinceStartup + primaryClientWaitTimeout;
            while ((!NSClient.IsConnected ||
                    NSClient.Client == null ||
                    !NSClient.Client.IsTimeSynchronized) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (!NSClient.IsConnected ||
                NSClient.Client == null ||
                !NSClient.Client.IsTimeSynchronized)
            {
                Debug.LogError(
                    "[NetSquare] Bot spawning stopped because the primary Client did not become ready.");
                yield break;
            }

            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(spawnInterval);
            for (int i = 0; i < botsCount; i++)
            {
                Vector3 position = new Vector3(
                    UnityEngine.Random.Range(0f, maxSpawnX),
                    0f,
                    UnityEngine.Random.Range(0f, maxSpawnZ));
                GameObject botObject = Instantiate(botPrefab, position, Quaternion.identity);
                NetSquareClientBot bot = botObject.GetComponent<NetSquareClientBot>();
                if (bot == null)
                {
                    Debug.LogError(
                        "[NetSquare] The bot prefab requires NetSquareClientBot.");
                    Destroy(botObject);
                }
                else
                {
                    bots.Add(bot);
                }

                if (spawnInterval > 0f)
                    yield return wait;
            }
        }
        #endregion
    }
}
