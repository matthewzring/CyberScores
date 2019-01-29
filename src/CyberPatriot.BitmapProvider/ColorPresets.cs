using System;
using System.Collections.Generic;
using System.Text;

namespace CyberPatriot.BitmapProvider
{
    public static class ColorPresets
    {
        public sealed class HistogramColorPreset
        {
            internal HistogramColorPreset()
            {

            }

            public Color BackgroundColor { get; internal set; }
            public Color BarFillColor { get; internal set; }
            public Color LabelTextColor { get; internal set; }
            public Color GridLineColor { get; internal set; }
        }

        public static HistogramColorPreset DiscordDark { get; } = new HistogramColorPreset()
        {
            BackgroundColor = Color.Parse("#32363B"),
            BarFillColor = Color.Parse("#7289DA"),
            LabelTextColor = Color.White,
            GridLineColor = Color.Gray
        };

        public static HistogramColorPreset DiscordLight { get; } = new HistogramColorPreset()
        {
            BackgroundColor = Color.White,
            BarFillColor = Color.Parse("#7289DA"),
            LabelTextColor = Color.Parse("#4F545C"),
            GridLineColor = Color.Parse("#CACBCE")
        };
    }
}
