using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetSquare.Client
{
    public class BotsSpawner : MonoBehaviour
    {
        public GameObject botPrefab;
        public int botsCount = 10;
        public float MaxSpawnX = 100;
        public float MaxSpawnY = 100;
        public float spawnInterval = 2;
        private List<NetsquareClientBot> bots = new List<NetsquareClientBot>();

        private void Start()
        {
            StartCoroutine(SpawnBots());
        }

        private void Update()
        {
            foreach (var bot in bots)
            {
                bot.BotUpdate();
            }
        }

        private IEnumerator SpawnBots()
        {
            while (!NSClient.IsConnected || !NSClient.Client.IsTimeSynchonized)
            {
                yield return null;
            }

            for (int i = 0; i < botsCount; i++)
            {
                Vector3 spawnPosition = new Vector3(Random.Range(0, MaxSpawnX), 0, Random.Range(0, MaxSpawnY));
                GameObject botGO = Instantiate(botPrefab, spawnPosition, Quaternion.identity);
                NetsquareClientBot bot = botGO.GetComponent<NetsquareClientBot>();
                bots.Add(bot);
                yield return new WaitForSeconds(spawnInterval);
            }
        }
    }
}