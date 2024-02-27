using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetSquare.Client
{
    public class BotsSpawner : MonoBehaviour
    {
        #region Variables
        [SerializeField]
        private GameObject botPrefab;
        [SerializeField]
        private int botsCount = 10;
        [SerializeField]
        private float MaxSpawnX = 100;
        [SerializeField]
        private float MaxSpawnY = 100;
        [SerializeField]
        private float spawnInterval = 2;
        private List<NetSquareClientBot> bots = new List<NetSquareClientBot>();
        #endregion

        /// <summary>
        /// Start is called before the first frame update
        /// </summary>
        private void Start()
        {
            StartCoroutine(SpawnBots());
        }

        /// <summary>
        /// Update is called once per frame
        /// </summary>
        private void Update()
        {
            // Update the bots
            foreach (var bot in bots)
            {
                bot.BotUpdate();
            }
        }

        /// <summary>
        /// Spawns the bots
        /// </summary>
        private IEnumerator SpawnBots()
        {
            // Wait for the client to connect and time to be synchronized
            while (!NSClient.IsConnected || !NSClient.Client.IsTimeSynchonized)
            {
                yield return null;
            }

            // Spawn the bots
            for (int i = 0; i < botsCount; i++)
            {
                Vector3 spawnPosition = new Vector3(Random.Range(0, MaxSpawnX), 0, Random.Range(0, MaxSpawnY));
                GameObject botGO = Instantiate(botPrefab, spawnPosition, Quaternion.identity);
                NetSquareClientBot bot = botGO.GetComponent<NetSquareClientBot>();
                bots.Add(bot);
                yield return new WaitForSeconds(spawnInterval);
            }
        }
    }
}