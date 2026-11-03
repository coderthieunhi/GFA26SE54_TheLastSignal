using UnityEngine;

namespace TheLastSignal.UI.Tasks
{
    public enum WireColorType
    {
        Red,
        Blue,
        Yellow,
        Green
    }

    public static class WireColorHelper
    {
        public static Color GetColor(WireColorType type)
        {
            return type switch
            {
                WireColorType.Red => new Color(0.92f, 0.25f, 0.25f, 1f),
                WireColorType.Blue => new Color(0.2f, 0.55f, 1f, 1f),
                WireColorType.Yellow => new Color(1f, 0.88f, 0.15f, 1f),
                WireColorType.Green => new Color(0.25f, 0.88f, 0.35f, 1f),
                _ => Color.white
            };
        }
    }
}
