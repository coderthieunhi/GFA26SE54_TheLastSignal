using UnityEngine;

namespace TheLastSignal.Entities.Player
{
    /// <summary>
    /// Marked world position for one player slot (0=P1 … 3=P4). Scene layout:
    /// Spawn Area with P1 P2 / P3 P4. Prototype may only spawn slot 0.
    /// </summary>
    public class PlayerSpawnPoint : MonoBehaviour
    {
        public const int MaxSlots = 4;

        [SerializeField, Range(0, MaxSlots - 1)] private int slotIndex;

        public int SlotIndex => slotIndex;

        public Vector2 Position => transform.position;

        public void Configure(int slot)
        {
            slotIndex = Mathf.Clamp(slot, 0, MaxSlots - 1);
            gameObject.name = $"SpawnPoint_P{slotIndex + 1}";
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = slotIndex == 0 ? Color.cyan : new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.5f);
        }
    }
}
