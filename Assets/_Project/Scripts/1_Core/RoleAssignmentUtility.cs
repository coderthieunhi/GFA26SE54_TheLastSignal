using System;

namespace TheLastSignal.Core
{
    /// <summary>
    /// Pure, dependency-free role-shuffling logic — no NetworkBehaviour, no UnityEngine.Random, so
    /// EditMode tests can assert uniqueness across many seeds without any Unity object graph. The
    /// server-only caller (GameManager.AssignRoles) is the only thing that ever touches NetworkVariables.
    /// </summary>
    public static class RoleAssignmentUtility
    {
        private static readonly SurvivorRole[] AllRoles = { SurvivorRole.Engineer, SurvivorRole.Mechanic, SurvivorRole.Logistics };

        /// <summary>
        /// Returns a random, unique assignment of up to 3 roles — one per requested slot, each role
        /// used at most once. <paramref name="survivorCount"/> is clamped to [0,3] (a room can have
        /// fewer than 3 Survivors during testing; see ROOM_MIN_PLAYERS).
        /// </summary>
        public static SurvivorRole[] AssignRandomRoles(int survivorCount, Random rng)
        {
            int count = Math.Clamp(survivorCount, 0, AllRoles.Length);
            SurvivorRole[] shuffled = (SurvivorRole[])AllRoles.Clone();

            // Fisher-Yates.
            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            SurvivorRole[] result = new SurvivorRole[count];
            Array.Copy(shuffled, result, count);
            return result;
        }
    }
}
