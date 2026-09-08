using UnityEngine;

namespace TheLastSignal.Systems.Monster
{
    /// <summary>
    /// Designer-tunable configuration for <see cref="MonsterFSM"/>. Values are placeholders only —
    /// real balance numbers are owned by the design team, see SRS FR-AI-01..05.
    /// </summary>
    [CreateAssetMenu(menuName = "TheLastSignal/Configs/Monster Config", fileName = "MonsterConfigSO")]
    public class MonsterConfigSO : ScriptableObject
    {
        [SerializeField] private float lurkToStalkDetectionRange = 8f; // placeholder, see SRS FR-AI-02
        [SerializeField] private float stalkToAttackRange = 2f; // placeholder, see SRS FR-AI-03
        [SerializeField] private float stunDurationSeconds = 5f; // placeholder, see SRS FR-AI-05

        public float LurkToStalkDetectionRange => lurkToStalkDetectionRange;
        public float StalkToAttackRange => stalkToAttackRange;
        public float StunDurationSeconds => stunDurationSeconds;
    }
}
