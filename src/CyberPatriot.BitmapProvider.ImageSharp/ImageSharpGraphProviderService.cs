using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Brushes;
using SixLabors.Primitives;

namespace CyberPatriot.BitmapProvider.ImageSharp
{
    public class ImageSharpGraphProviderService : IGraphProviderService
    {
        public Task WriteHistogramPngAsync(
            IEnumerable<decimal> dataset,
            string horizontalAxisLabel, string verticalAxisLabel,
            Color backColor, Color barColor, Color labelColor,
            Stream target
        )
        {
            // size not given in the interface
            const int imageWidth = 1000;
            const int imageHeight = 700;

            const int drawRegionSideOffset = 50;

            decimal[] data = dataset.OrderBy(x => x).ToArray();

            if (data.Length < 2)
            {
                throw new ArgumentException("2 or more elements required for a histogram.");
            }

            decimal first = data[0], last = data[data.Length - 1];

            decimal bucketWidth = (last - first) / Math.Round((decimal)Math.Sqrt(data.Length));

            int[] countsByBucket = data.GroupBy(datum => (int)((datum - first) / bucketWidth)).Select(group => group.Count()).ToArray();
            decimal[] lowerBoundsByBucket = new decimal[countsByBucket.Length];
            for (int i = 0; i < lowerBoundsByBucket.Length; i++)
            {
                lowerBoundsByBucket[i] = first + (i * bucketWidth);
            }

            float bucketPixelWidth = (imageWidth - (2 * drawRegionSideOffset)) / ((float)countsByBucket.Length);
            float pixelsPerUnitCount = (imageHeight - (2 * drawRegionSideOffset)) / ((float)countsByBucket.Max());

            using (var image = new Image<Rgba32 >(imageWidth, imageHeight))
            {
                image.Mutate(context =>
                {
                    // context's origin: top left
                    context.Fill(new SolidBrush<Rgba32>(backColor.ToRgba32()));

                    // start by drawing the data, we'll render on top of stuff that gets in our way
                    for (int i = 0; i < countsByBucket.Length; i++)
                    {
                        PointF[] boundingBox = new PointF[]{
                            new PointF(drawRegionSideOffset + (i * bucketPixelWidth), imageHeight - drawRegionSideOffset),
                            new PointF(drawRegionSideOffset + ((i + 1) * bucketPixelWidth), imageHeight - drawRegionSideOffset),
                            new PointF(drawRegionSideOffset + ((i + 1) * bucketPixelWidth), imageHeight - drawRegionSideOffset - (pixelsPerUnitCount * countsByBucket[i])),
                            new PointF(drawRegionSideOffset + (i * bucketPixelWidth), imageHeight - drawRegionSideOffset - (pixelsPerUnitCount * countsByBucket[i])),

                        };

                        // rectangle and border
                        context.FillPolygon(new SolidBrush<Rgba32>(barColor.ToRgba32()), boundingBox);
                        context.DrawLines(labelColor.ToRgba32(), 2, boundingBox);
                    }

                    context.DrawLines(new SolidBrush<Rgba32>(labelColor.ToRgba32()), 3, new PointF[]{
                        new PointF(drawRegionSideOffset, imageHeight - drawRegionSideOffset),
                        new PointF(imageWidth - drawRegionSideOffset, imageHeight - drawRegionSideOffset)
                    });

                    context.DrawLines(new SolidBrush<Rgba32>(labelColor.ToRgba32()), 3, new PointF[]{
                        new PointF(drawRegionSideOffset, imageHeight - drawRegionSideOffset),
                        new PointF(drawRegionSideOffset, drawRegionSideOffset)
                    });
                });
                image.SaveAsPng(target);
            }

            return Task.CompletedTask;
        }
    }
}