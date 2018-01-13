using System;
using System.Globalization;

namespace CyberPatriot.BitmapProvider
{
    /// <summary>
    /// Represents an immutable RGBA color.
    /// </summary>
    public struct Color : IEquatable<Color>
    {
        #region Equality members

        public bool Equals(Color other)
        {
            return R == other.R && G == other.G && B == other.B && A == other.A;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Color && Equals((Color)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = R.GetHashCode();
                hashCode = (hashCode * 397) ^ G.GetHashCode();
                hashCode = (hashCode * 397) ^ B.GetHashCode();
                hashCode = (hashCode * 397) ^ A.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(Color left, Color right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Color left, Color right)
        {
            return !left.Equals(right);
        }

        #endregion

        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public byte A { get; }

        public Color(byte r, byte g, byte b) : this(r, g, b, 255)
        {
        }

        public Color(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }


        /// <summary>
        /// Constructs a color from an RGB(A) array of components.
        /// </summary>
        /// <param name="components"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public Color(byte[] components)
        {
            if (components == null)
            {
                throw new ArgumentNullException(nameof(components));
            }

            if (components.Length != 3 && components.Length != 4)
            {
                throw new ArgumentException("The components array must have exactly 3 or exactly 4 components.");
            }

            R = components[0];
            G = components[1];
            B = components[2];
            A = components.Length > 3 ? components[3] : (byte)255;
        }

        /// <summary>
        /// Serializes the string to an 8-digit hex RGBA color string.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{R:X2}{G:X2}{B:X2}{A:X2}";

        /// <summary>
        /// Creates a grayscale color, with identical RGB components and a potentially differing opacity component.
        /// </summary>
        /// <param name="magnitude">The magnitude of each component.</param>
        /// <param name="opacity">The opacity of the new color.</param>
        /// <returns>A Color object representing a shade of gray.</returns>
        public static Color FromGrayscaleMagnitude(byte magnitude, byte opacity = 255) =>
            new Color(magnitude, magnitude, magnitude, opacity);

        public static bool TryParse(string value, out Color color)
        {
            color = default(Color);

            if (value == null)
            {
                return false;
            }

            // TODO performance?

            if (value.StartsWith("#"))
            {
                value = value.Substring(1);
            }

            if (value.Length != 8 && value.Length != 6)
            {
                return false;
            }

            // RRGGBB(AA)
            byte[] components = new byte[value.Length / 2];
            for (int i = 0; i < value.Length; i += 2)
            {
                // hacky
                if (!Byte.TryParse(value.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                    out byte thisComponent))
                {
                    return false;
                }

                components[i / 2] = thisComponent;
            }

            color = new Color(components);
            return true;
        }

        public static Color Parse(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!TryParse(value, out Color retVal))
            {
                throw new FormatException("Could not parse the given string as a color.");
            }

            return retVal;
        }

        #region Predefined colors

        public static Color Black { get; } = new Color(0, 0, 0);
        public static Color White { get; } = new Color(255, 255, 255);
        public static Color Transparent { get; } = new Color(0, 0, 0, 0);
        public static Color Red { get; } = new Color(255, 0, 0);
        public static Color Green { get; } = new Color(0, 255, 0);
        public static Color Blue { get; } = new Color(0, 0, 255);
        public static Color Gray { get; } = new Color(0x80, 0x80, 0x80);

        #endregion
    }
}