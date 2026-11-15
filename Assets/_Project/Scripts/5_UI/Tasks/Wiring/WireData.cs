using UnityEngine;

namespace TheLastSignal.UI.Tasks
{
    public enum WireColorType
    {
        Red = 0,
        Blue = 1,
        Yellow = 2,
        Green = 3
    }

    public struct WireVoltageInfo
    {
        public WireColorType ColorType;
        public int VoltageValue;
        public string VoltageText;
        public string SubsystemName;
        public Color LineColor;
        public int SequenceIndex;
    }

    public static class WireColorHelper
    {
        public static WireVoltageInfo GetVoltageInfo(WireColorType type)
        {
            return type switch
            {
                WireColorType.Green => new WireVoltageInfo
                {
                    ColorType = WireColorType.Green,
                    VoltageValue = 5,
                    VoltageText = "+5V",
                    SubsystemName = "LOGIC BUS",
                    LineColor = new Color(0.25f, 0.92f, 0.40f, 1f),
                    SequenceIndex = 0
                },
                WireColorType.Blue => new WireVoltageInfo
                {
                    ColorType = WireColorType.Blue,
                    VoltageValue = 12,
                    VoltageText = "+12V",
                    SubsystemName = "CTRL RELAY",
                    LineColor = new Color(0.18f, 0.65f, 1f, 1f),
                    SequenceIndex = 1
                },
                WireColorType.Yellow => new WireVoltageInfo
                {
                    ColorType = WireColorType.Yellow,
                    VoltageValue = 24,
                    VoltageText = "+24V",
                    SubsystemName = "AUX FEED",
                    LineColor = new Color(1f, 0.85f, 0.15f, 1f),
                    SequenceIndex = 2
                },
                WireColorType.Red => new WireVoltageInfo
                {
                    ColorType = WireColorType.Red,
                    VoltageValue = 48,
                    VoltageText = "+48V",
                    SubsystemName = "HIGH TENSION",
                    LineColor = new Color(0.95f, 0.25f, 0.25f, 1f),
                    SequenceIndex = 3
                },
                _ => default
            };
        }

        public static Color GetColor(WireColorType type) => GetVoltageInfo(type).LineColor;
        public static string GetVoltageText(WireColorType type) => GetVoltageInfo(type).VoltageText;
        public static string GetSubsystem(WireColorType type) => GetVoltageInfo(type).SubsystemName;
        public static int GetSequenceOrder(WireColorType type) => GetVoltageInfo(type).SequenceIndex;
    }
}
