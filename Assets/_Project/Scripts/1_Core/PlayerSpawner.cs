using System.Collections.Generic;
using UnityEngine;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Core
{
    /// <summary>
    /// Places 1–4 players onto <see cref="PlayerSpawnPoint"/> slots at session start / restart.
    /// Prototype defaults to 1 player; extra points stay in the scene for co-op later.
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        public const int MaxPlayers = PlayerSpawnPoint.MaxSlots;

        [SerializeField, Range(1, MaxPlayers)] private int playersToSpawn = 1;
        [SerializeField] private PlayerController playerPrefab;
        [SerializeField] private LayerMask blockedLayers;
        [SerializeField] private float spawnClearRadius = 0.4f;

        private readonly List<PlayerController> spawnedPlayers = new List<PlayerController>(MaxPlayers);
        private readonly Dictionary<int, PlayerSpawnPoint> pointsBySlot = new Dictionary<int, PlayerSpawnPoint>(MaxPlayers);

        public IReadOnlyList<PlayerController> SpawnedPlayers => spawnedPlayers;

        public int PlayersToSpawn => playersToSpawn;

        private void Awake()
        {
            // Fulfill criterion: "Player does not appear before game initialization is complete"
            // Hide any pre-existing scene players immediately until SpawnPlayers is called.
            PlayerController[] existing = FindObjectsByType<PlayerController>(FindObjectsInactive.Include);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] != null)
                {
                    existing[i].gameObject.SetActive(false);
                }
            }
        }

        public void CollectSpawnPoints()
        {
            pointsBySlot.Clear();
            PlayerSpawnPoint[] points = FindObjectsByType<PlayerSpawnPoint>();
            for (int i = 0; i < points.Length; i++)
            {
                PlayerSpawnPoint point = points[i];
                if (!pointsBySlot.ContainsKey(point.SlotIndex))
                {
                    pointsBySlot.Add(point.SlotIndex, point);
                }
            }
        }

        /// <summary>
        /// Spawns or teleports players onto slots 0..N-1. Existing scene players are reused before
        /// instantiating the prefab so the prototype can keep the authored Player object.
        /// </summary>
        public void SpawnPlayers()
        {
            CollectSpawnPoints();
            spawnedPlayers.Clear();

            List<PlayerController> existing = new List<PlayerController>(
                FindObjectsByType<PlayerController>(FindObjectsInactive.Include));

            int count = Mathf.Clamp(playersToSpawn, 1, MaxPlayers);
            for (int slot = 0; slot < count; slot++)
            {
                Vector2 position = ResolveSpawnPosition(slot);
                PlayerController player = AcquirePlayer(existing, slot);
                player.gameObject.SetActive(true);
                player.BindSlot(slot);
                player.ResetState(position);
                spawnedPlayers.Add(player);
            }

            for (int i = 0; i < existing.Count; i++)
            {
                PlayerController leftover = existing[i];
                if (leftover != null && !spawnedPlayers.Contains(leftover))
                {
                    leftover.gameObject.SetActive(false);
                }
            }
        }

        public void ResetSpawnedPlayers()
        {
            CollectSpawnPoints();
            for (int i = 0; i < spawnedPlayers.Count; i++)
            {
                PlayerController player = spawnedPlayers[i];
                if (player == null)
                {
                    continue;
                }

                player.ResetState(ResolveSpawnPosition(player.SlotIndex));
            }
        }

        public void SetControlEnabled(bool enabled)
        {
            for (int i = 0; i < spawnedPlayers.Count; i++)
            {
                if (spawnedPlayers[i] != null)
                {
                    spawnedPlayers[i].SetControlEnabled(enabled);
                }
            }
        }

        private PlayerController AcquirePlayer(List<PlayerController> existing, int slot)
        {
            for (int i = 0; i < existing.Count; i++)
            {
                PlayerController candidate = existing[i];
                if (candidate == null)
                {
                    continue;
                }

                existing.RemoveAt(i);
                return candidate;
            }

            if (playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] No unused PlayerController in the scene and no prefab assigned.");
                GameObject fallback = new GameObject($"Player_P{slot + 1}");
                fallback.AddComponent<Rigidbody2D>();
                fallback.AddComponent<CapsuleCollider2D>();
                return fallback.AddComponent<PlayerController>();
            }

            return Instantiate(playerPrefab);
        }

        private Vector2 ResolveSpawnPosition(int slot)
        {
            Vector2 desired = pointsBySlot.TryGetValue(slot, out PlayerSpawnPoint point)
                ? point.Position
                : Vector2.zero;

            return FindClearPosition(desired);
        }

        private Vector2 FindClearPosition(Vector2 desired)
        {
            if (!IsBlocked(desired))
            {
                return desired;
            }

            Vector2[] offsets =
            {
                Vector2.up, Vector2.down, Vector2.left, Vector2.right,
                new Vector2(1f, 1f), new Vector2(-1f, 1f),
                new Vector2(1f, -1f), new Vector2(-1f, -1f)
            };

            for (int radius = 1; radius <= 3; radius++)
            {
                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector2 candidate = desired + offsets[i] * radius;
                    if (!IsBlocked(candidate))
                    {
                        Debug.LogWarning($"[PlayerSpawner] Spawn {desired} was blocked; using {candidate}.");
                        return candidate;
                    }
                }
            }

            Debug.LogWarning($"[PlayerSpawner] Could not find a clear spawn near {desired}; using it anyway.");
            return desired;
        }

        private bool IsBlocked(Vector2 position)
        {
            LayerMask mask = blockedLayers != 0 ? blockedLayers : LayerMask.GetMask("Obstacle");
            if (mask == 0)
            {
                return false;
            }

            Collider2D hit = Physics2D.OverlapCircle(position, spawnClearRadius, mask);
            return hit != null;
        }
    }
}
