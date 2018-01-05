using System;
using System.Drawing;
using SixLabors.ImageSharp;

namespace CyberPatriot.BitmapProvider.ImageSharp
{
    internal static class Converters
    {
        public static Rgba32 ToRgba32(this Color color) => new Rgba32(color.R, color.G, color.B, color.A);
    }
}