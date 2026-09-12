using System.Collections.Generic;
using UnityEngine;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Manages player spawn points for both Single-Player testing and Multi-Player co-op (1-4 players).
    /// Finds and registers all SpawnPoint instances in the scene, ordered by playerIndex.
    /// </summary>
    public class SpawnManager : MonoBehaviour
    {
        private static SpawnManager instance;
        public static SpawnManager Instance => instance;

        [SerializeField] private List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
        [SerializeField] private bool autoPositionLocalPlayer = true;

        public IReadOnlyList<SpawnPoint> SpawnPoints => spawnPoints;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;

            RefreshSpawnPoints();
        }

        private void Start()
        {
            if (autoPositionLocalPlayer)
            {
                PositionLocalPlayerAtSpawn();
            }
        }

        /// <summary>
        /// Collects all child or scene SpawnPoint components and sorts them by playerIndex.
        /// </summary>
        public void RefreshSpawnPoints()
        {
            spawnPoints.Clear();
            SpawnPoint[] found = GetComponentsInChildren<SpawnPoint>();
            if (found == null || found.Length == 0)
            {
                found = FindObjectsByType<SpawnPoint>();
            }

            if (found != null)
            {
                spawnPoints.AddRange(found);
                spawnPoints.Sort((a, b) => a.PlayerIndex.CompareTo(b.PlayerIndex));
            }
        }

        /// <summary>
        /// Returns the designated SpawnPoint for the given player index (0 to 3), or null if out of range.
        /// </summary>
        public SpawnPoint GetSpawnPoint(int playerIndex)
        {
            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                RefreshSpawnPoints();
            }

            if (playerIndex >= 0 && playerIndex < spawnPoints.Count)
            {
                return spawnPoints[playerIndex];
            }

            return null;
        }

        /// <summary>
        /// Finds the first unoccupied SpawnPoint. Marks it occupied and returns it.
        /// </summary>
        public SpawnPoint GetNextAvailableSpawnPoint()
        {
            if (spawnPoints == null || spawnPoints.Count == 0)
            {
                RefreshSpawnPoints();
            }

            foreach (var sp in spawnPoints)
            {
                if (!sp.IsOccupied)
                {
                    sp.IsOccupied = true;
                    return sp;
                }
            }

            return spawnPoints.Count > 0 ? spawnPoints[0] : null;
        }

        /// <summary>
        /// In Single-Player or initial test mode, positions the active Player at P1 spawn point.
        /// </summary>
        public void PositionLocalPlayerAtSpawn()
        {
            GameObject playerGo = GameObject.Find("Player");
            if (playerGo != null)
            {
                SpawnPoint p1 = GetSpawnPoint(0);
                if (p1 != null)
                {
                    playerGo.transform.position = p1.Position;
                    p1.IsOccupied = true;
                }
            }
        }
    }
}
