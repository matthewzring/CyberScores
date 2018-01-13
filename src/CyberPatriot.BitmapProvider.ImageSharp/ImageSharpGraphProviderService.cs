﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.Fonts;
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
            Func<decimal, string> getDataEdgeLabel,
            Color backColor, Color barColor, Color labelColor, Color frequencyGraphLineColor,
            Stream target
        )
        {
            return Task.Run(() => WriteHistogramPng(dataset, horizontalAxisLabel, verticalAxisLabel,
                                                    getDataEdgeLabel, backColor, barColor, labelColor, frequencyGraphLineColor, target));
        }

        public void WriteHistogramPng(
            IEnumerable<decimal> dataset,
            string horizontalAxisLabel, string verticalAxisLabel,
            Func<decimal, string> getDataEdgeLabel,
            Color backColor, Color barColor, Color labelColor, Color frequencyGraphLineColor,
            Stream target
        )
        {
            // size not given in the interface
            const int imageWidth = 1600;
            const int imageHeight = 800;

            const int drawRegionLeftOffset = 50;
            const int drawRegionRightOffset = 40;

            const int drawRegionTopOffset = 50;
            const int drawRegionBottomOffset = 150;

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

            float bucketPixelWidth = (imageWidth - (drawRegionLeftOffset + drawRegionRightOffset)) / ((float)countsByBucket.Length);
            float pixelsPerUnitCount = (imageHeight - (drawRegionTopOffset + drawRegionBottomOffset)) / ((float)countsByBucket.Max());

            // TODO better font selection
            FontFamily fontFamily = SystemFonts.Families.First();
            Font font = fontFamily.CreateFont(14, FontStyle.Bold);

            // relatively arbitrarily rounded
            // 15 is approximate desired number of lines
            int frequencyGraphLineStep = (int)(Math.Floor((countsByBucket.Max() / 15.0) / 5.0) * 5);
            if (frequencyGraphLineStep < 1)
            {
                // we don't want an infinite loop
                frequencyGraphLineStep = 1;
            }

            using (var image = new Image<Rgba32>(imageWidth, imageHeight))
            {
                image.Mutate(context =>
                {   // context's origin: top left
                    context.Fill(new SolidBrush<Rgba32>(backColor.ToRgba32()));

                    int highestRenderedY = -1;

                    // render the graph lines for the frequency axis
                    // it's imperative that we do this first so they can get drawn over
                    for (int i = frequencyGraphLineStep; i * pixelsPerUnitCount < imageHeight - (drawRegionBottomOffset + drawRegionTopOffset); i += frequencyGraphLineStep)
                    {
                        float yVal = imageHeight - drawRegionBottomOffset - (i * pixelsPerUnitCount);
                        context.DrawLines(frequencyGraphLineColor.ToRgba32(), 1, new PointF[] { new PointF(drawRegionLeftOffset, yVal), new PointF(imageWidth - drawRegionRightOffset, yVal) });


                        const float axisDistance = 5;
                        // less imperative to do early, but render the labels here too - we already have the appropriate i
                        RectangleF textBounds = TextMeasurer.MeasureBounds(i.ToString(), new RendererOptions(font));
                        // no over two because, idk why. I'd expect textBounds.Height/2 for centering but apparently you don't divide by two. Not sure why, ImageSharp quirk probably.
                        context.DrawText(i.ToString(), font, labelColor.ToRgba32(), new PointF(drawRegionLeftOffset - axisDistance - textBounds.Width, yVal - textBounds.Height));
                    }

                    void RenderSlantedXAxisText(string relevantText, PointF bottomLeft)
                    {
                        RectangleF textBounds = TextMeasurer.MeasureBounds(relevantText, new RendererOptions(font));
                        int higherDimension = (int)Math.Ceiling(Math.Max(textBounds.Width, textBounds.Height));
                        using (Image<Rgba32> textRenderImage = new Image<Rgba32>(higherDimension, higherDimension))
                        {
                            textRenderImage.Mutate(tempContext =>
                                tempContext
                                    .DrawText(relevantText, font, labelColor.ToRgba32(), PointF.Empty)
                                    .Rotate(-45));
                            textRenderImage.MutateCropToColored();

                            // find the tip of the text
                            int tipX = -1;
                            for (int textY = 0; textY < textRenderImage.Height; textY++)
                            {
                                for (int localX = 0; localX < textRenderImage.Width; localX++)
                                {
                                    if (textRenderImage[localX, textY].A > 0)
                                    {
                                        tipX = localX;
                                        break;
                                    }
                                }

                                if (tipX != -1)
                                {
                                    break;
                                }
                            }

                            // the top right hand corner of our generated image is the edge of text, which we want to line up with the bar's edge
                            // we also give a few Y pixel margin so it's not right up against the axis
                            PointF renderPosition = bottomLeft + new PointF(-tipX, 5);
                            context.DrawImage(textRenderImage, 100, textRenderImage.Size(), (Point)renderPosition);

                            if (textRenderImage.Height + renderPosition.Y > highestRenderedY)
                            {
                                highestRenderedY = textRenderImage.Height + (int)renderPosition.Y;
                            }
                        }
                    }


                    // start by drawing the data, we'll render on top of stuff that gets in our way
                    for (int i = 0; i < countsByBucket.Length; i++)
                    {
                        PointF bottomLeft = new PointF(drawRegionLeftOffset + (i * bucketPixelWidth), imageHeight - drawRegionBottomOffset);

                        PointF[] boundingBox = new PointF[]{
                                bottomLeft,
                                new PointF(drawRegionLeftOffset + ((i + 1) * bucketPixelWidth), imageHeight - drawRegionBottomOffset),
                                new PointF(drawRegionLeftOffset + ((i + 1) * bucketPixelWidth), imageHeight - drawRegionBottomOffset - (pixelsPerUnitCount * countsByBucket[i])),
                                new PointF(drawRegionLeftOffset + (i * bucketPixelWidth), imageHeight - drawRegionBottomOffset - (pixelsPerUnitCount * countsByBucket[i]))
                        };

                        // rectangle and border
                        context.FillPolygon(new SolidBrush<Rgba32>(barColor.ToRgba32()), boundingBox);
                        context.DrawLines(labelColor.ToRgba32(), 2, boundingBox);

                        RenderSlantedXAxisText(getDataEdgeLabel(lowerBoundsByBucket[i]), bottomLeft);

                        if (i == countsByBucket.Length - 1)
                        {
                            // Fencepost
                            // Render the last label
                            RenderSlantedXAxisText(getDataEdgeLabel(last), bottomLeft + new PointF(bucketPixelWidth, 0));
                        }
                    }

                    // X axis
                    context.DrawLines(new SolidBrush<Rgba32>(labelColor.ToRgba32()), 3, new PointF[]{
                        new PointF(drawRegionLeftOffset, imageHeight - drawRegionBottomOffset),
                        new PointF(imageWidth - drawRegionRightOffset, imageHeight - drawRegionBottomOffset)
                    });

                    // Y axis
                    context.DrawLines(new SolidBrush<Rgba32>(labelColor.ToRgba32()), 3, new PointF[]{
                        new PointF(drawRegionLeftOffset, imageHeight - drawRegionBottomOffset),
                        new PointF(drawRegionLeftOffset, drawRegionTopOffset)
                    });

                    Font labelFont = fontFamily.CreateFont(24, FontStyle.Regular);

                    // render X-axis label with a comfortable Y-margin
                    RectangleF xLabelBounds = TextMeasurer.MeasureBounds(horizontalAxisLabel, new RendererOptions(labelFont));
                    context.DrawText(horizontalAxisLabel, labelFont, labelColor.ToRgba32(), new PointF((drawRegionLeftOffset + imageWidth - drawRegionRightOffset) / 2 - (xLabelBounds.Width / 2), highestRenderedY + 10));
                });
                image.SaveAsPng(target);
            }
        }
    }
}