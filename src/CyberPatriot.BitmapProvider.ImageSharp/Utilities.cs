using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;

namespace CyberPatriot.BitmapProvider.ImageSharp
{

    internal static class Utilities
    {
        public static PointF Position(this RectangleF rect)
        {
            return new PointF(rect.X, rect.Y);
        }

        public static Size Size<TPixel>(this Image<TPixel> img) where TPixel : struct, IPixel<TPixel> => new Size(img.Width, img.Height);

        /// <summary>
        /// Computes the center of the given "rectangle," assuming a top left corner of 0,0.
        /// </summary>
        public static Point Center(this Size sizeObj)
        {
            return new Point(sizeObj.Width / 2, sizeObj.Height / 2);
        }

        /// <summary>
        /// Computes the center of the given "rectangle," assuming a top left corner of 0,0.
        /// </summary>
        public static PointF Center(this SizeF sizeObj)
        {
            return new PointF(sizeObj.Width / 2, sizeObj.Height / 2);
        }

        // TODO this feels like it could be supported for arbitrary TPixel, since many have alpha
        public static void MutateCropToColored(this Image<Rgba32> img)
        {
            int minColorX = img.Width, maxColorX = 0;
            int minColorY = img.Height, maxColorY = 0;
            for (int x = 0; x < img.Width; x++)
            {
                for (int y = 0; y < img.Height; y++)
                {
                    // TODO is there a better way than calling the read accessor every time
                    Rgba32 pix = img[x, y];
                    if (pix.A > 0)
                    {
                        if (x < minColorX) minColorX = x;
                        if (x > maxColorX) maxColorX = x;
                        if (y < minColorY) minColorY = y;
                        if (y > maxColorY) maxColorY = y;
                    }
                }
            }
            img.Mutate(context => context.Crop(new Rectangle(minColorX, minColorY, maxColorX - minColorX, maxColorY - minColorY)));
        }
    }
}