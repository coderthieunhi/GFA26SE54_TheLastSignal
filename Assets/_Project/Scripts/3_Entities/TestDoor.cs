using UnityEngine;
using TheLastSignal.Core.Interaction;
using TheLastSignal.Entities.Player;

namespace TheLastSignal.Entities
{
    /// <summary>
    /// A test implementation of InteractableBase to demonstrate decoupled events.
    /// </summary>
    public class TestDoor : InteractableBase
    {
        private void Awake()
        {
            promptMessage = "[E] Mở cửa";
        }

        protected override void ProcessInteraction(PlayerController player)
        {
            Debug.Log($"[TestDoor] Cửa đang được mở bởi {player.name}.");
            // Ghi chú: Có thể gọi ServerRpc ở đây để đồng bộ việc mở cửa.
        }

        protected override void OnInteractionFailed(PlayerController player)
        {
            Debug.Log($"[TestDoor] Interaction thất bại đối với {player.name}. Cửa có thể bị khóa hoặc người chơi đứng quá xa.");
        }

        // Hàm này được tạo ra để chứng minh ta có thể kéo UnityEvent trong Inspector 
        // để kích hoạt logic bổ sung (Audio, Particle) mà không cần code cứng.
        public void PlayDoorOpenSound(PlayerController player)
        {
            Debug.Log($"[TestDoor] UnityEvent Kích Hoạt! Đang chạy âm thanh mở cửa.");
        }
    }
}
