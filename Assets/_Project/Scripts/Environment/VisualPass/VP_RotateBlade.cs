using UnityEngine;

namespace TheLastSignal.Entities.Environment.VisualPass
{
    public class VP_RotateBlade : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 36.0f; // 30-45 deg/sec as specified

        private void Update()
        {
            transform.Rotate(0f, 0f, -rotationSpeed * Time.deltaTime);
        }
    }
}
