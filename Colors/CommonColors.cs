namespace ModsCommon.Colors {
    #region Using Statements

    using System;
    using UnityEngine;

    #endregion

    /// <summary>
    /// Shared color palette and color-manipulation helpers — near-verbatim port of CS1 ModsCommon's
    /// <c>Colors.cs</c>, used both for overlay drawing (<see cref="Overlay"/>/<see cref="OverlayColors"/>)
    /// and, later, for UI theming.
    /// </summary>
    /// <remarks>
    /// Drops the CS1 <c>Shortcut</c>-extension overloads of <c>AddInfoColor</c>/<c>AddErrorColor</c>/etc. —
    /// no CS2 <c>Shortcut</c> type exists yet (that's tool-phase territory); the plain <see cref="string"/>
    /// overloads are kept.
    /// </remarks>
    public static class CommonColors {
        private const byte Alpha = 224;
        public const float DefaultContrast = 4.5f;

        public static Color32 White { get; } = new Color32(255, 255, 255, 255);
        public static Color32 White192 { get; } = new Color32(255, 255, 255, 192);
        public static Color32 White128 { get; } = new Color32(255, 255, 255, 128);
        public static Color32 White64 { get; } = new Color32(255, 255, 255, 64);
        public static Color32 Green { get; } = new Color32(0, 200, 81, 255);
        public static Color32 Red { get; } = new Color32(255, 68, 68, 255);
        public static Color32 Blue { get; } = new Color32(0, 180, 255, 255);
        public static Color32 Orange { get; } = new Color32(255, 136, 0, 255);
        public static Color32 Yellow { get; } = new Color32(255, 187, 51, 255);
        public static Color32 Gray224 { get; } = new Color32(224, 224, 224, 255);
        public static Color32 Gray192 { get; } = new Color32(192, 192, 192, 255);
        public static Color32 Gray128 { get; } = new Color32(128, 128, 128, 255);
        public static Color32 Gray64 { get; } = new Color32(64, 64, 64, 255);
        public static Color32 Purple { get; } = new Color32(148, 87, 255, 255);
        public static Color32 Hover { get; } = new Color32(217, 251, 255, 255);

        public static Color32[] OverlayColors { get; } = {
            new Color32(218, 33, 40, Alpha),   // Red
            new Color32(72, 184, 94, Alpha),   // Green
            new Color32(0, 120, 191, Alpha),   // Blue

            new Color32(245, 130, 32, Alpha),  // Orange
            new Color32(142, 71, 155, Alpha),  // Purple
            new Color32(180, 212, 69, Alpha),  // Lime

            new Color32(53, 201, 159, Alpha),  // Turquoise
            new Color32(255, 198, 26, Alpha),  // Yellow
            new Color32(230, 106, 192, Alpha), // Pink

            new Color32(0, 193, 243, Alpha)    // SkyBlue
        };

        public enum Overlay {
            Red,
            Green,
            Blue,
            Orange,
            Purple,
            Lime,
            Turquoise,
            Yellow,
            Pink,
            SkyBlue
        }

        public static Color32 GetOverlayColor(int index, byte alpha = Alpha, byte hue = 255) {
            var color = OverlayColors[index % OverlayColors.Length];
            color.a = alpha;
            return hue == 255 ? color : color.SetHue(hue);
        }

        public static Color32 GetOverlayColor(Overlay index, byte alpha = Alpha, byte hue = 255) => GetOverlayColor((int)index, alpha, hue);

        public static Color32 SetHue(this Color32 color, byte hue) => new Color32(SetHue(color.r, hue), SetHue(color.g, hue), SetHue(color.b, hue), color.a);
        private static byte SetHue(byte value, byte hue) => (byte)(byte.MaxValue - (byte.MaxValue - value) / 255f * hue);
        public static Color32 SetAlpha(this Color32 color, byte alpha) => new Color32(color.r, color.g, color.b, alpha);
        public static Color32 SetOpacity(this Color32 color, int opacity) => new Color32(color.r, color.g, color.b, (byte)Mathf.RoundToInt(opacity * 2.55f));
        public static Color SetOpacity(this Color color, int opacity) => new Color(color.r, color.g, color.b, opacity * 0.01f);

        public static Color32 GetStyleIconColor(this Color32 color) {
            var ratio = 255 / (float)Math.Max(Math.Max(color.r, color.g), color.b);
            var styleColor = new Color32((byte)(color.r * ratio), (byte)(color.g * ratio), (byte)(color.b * ratio), 255);
            return (Color)styleColor == Color.black ? (Color32)Color.white : styleColor;
        }

        public static Vector4 ToX3Vector(this Color32 c) => ToX3Vector((Color)c);
        public static Vector4 ToX3Vector(this Color c) => new Vector4(ColorChange(c.r), ColorChange(c.g), ColorChange(c.b), Mathf.Pow(c.a, 2));
        private static float ColorChange(float c) => Mathf.Pow(c, 4);

        public static string AddColor(this string text, Color32 color) => $"<color #{color.r:X2}{color.g:X2}{color.b:X2}>{text}</color>";
        public static string AddInfoColor(this string text) => $"<color #87D3FF>{text}</color>";
        public static string AddErrorColor(this string text) => $"<color #FF7E00>{text}</color>";
        public static string AddActionColor(this string text) => $"<color #5CE66E>{text}</color>";
        public static string AddWarningColor(this string text) => $"<color #FFD119>{text}</color>";

        public static float GetContrast(this Color a, Color b) {
            var la = GetLuminance(a);
            var lb = GetLuminance(b);
            return (la + 0.05f) / (lb + 0.05f);
        }

        public static float GetContrast(this Color32 a, Color32 b) {
            var la = GetLuminance(a);
            var lb = GetLuminance(b);
            return (la + 0.05f) / (lb + 0.05f);
        }

        private static float GetLuminance(Color c) => 0.2126f * GetComponent(c.r) + 0.7152f * GetComponent(c.g) + 0.0722f * GetComponent(c.b);

        private static float GetComponent(float value) => value <= 0.03928f ? value / 12.92f : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);

        public static Color32 Overlap(this Color32 a, Color32 b) => Overlap((Color)a, (Color)b);

        public static Color Overlap(this Color a, Color b) {
            a.r = Mathf.Clamp01(a.r * a.a + b.r * b.a);
            a.g = Mathf.Clamp01(a.g * a.a + b.g * b.a);
            a.b = Mathf.Clamp01(a.b * a.a + b.b * b.a);
            a.a = 1f;
            return a;
        }
    }
}
