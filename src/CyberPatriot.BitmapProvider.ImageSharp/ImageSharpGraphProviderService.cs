#region License Header
/*


    CyberPatriot Discord Bot - an unofficial tool to integrate the AFA CyberPatriot
    competition scoreboard with the Discord chat platform
    Copyright (C) 2017, 2018, 2019  Glen Husman and contributors

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.

*/
#endregion

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

        private bool IsSorted(decimal[] arr)
        {
            decimal prev = decimal.MinValue;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] < prev)
                {
                    return false;
                }
                prev = arr[i];
            }
            return true;
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

            const int drawRegionLeftOffset = 110;
            const int drawRegionRightOffset = 40;

            const int drawRegionTopOffset = 50;
            const int drawRegionBottomOffset = 110;

            decimal[] data = dataset.ToArray();

            if (data.Length < 2)
            {
                throw new ArgumentException("2 or more elements required for a histogram.");
            }

            if (!IsSorted(data))
            {
                throw new ArgumentException("The given data are not sorted.", nameof(dataset));
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
            // leading float multiplier: N% of the draw region can have data, the rest (at the top) is padding
            float pixelsPerUnitCount = 0.95f * (imageHeight - (drawRegionTopOffset + drawRegionBottomOffset)) / ((float)countsByBucket.Max());

            // TODO better font selection
            FontFamily fontFamily = SystemFonts.Families.FirstOrDefault(ff => ff.Name == "Arial") ?? SystemFonts.Families.FirstOrDefault(ff => ff.Name == "Liberation Sans") ?? SystemFonts.Families.First();
            Font font = fontFamily.CreateFont(14, FontStyle.Bold);

            // relatively arbitrarily rounded
            // divisor is approximate desired number of lines
            int frequencyGraphLineStep = (int)Math.Round(countsByBucket.Max() / 20.0);
            if (frequencyGraphLineStep < 1)
            {
                // we don't want an infinite loop
                frequencyGraphLineStep = 1;
            }
            else if (frequencyGraphLineStep > 5)
            {
                // make the steps clean multiples once we're past small numbers
                frequencyGraphLineStep = (int)(Math.Round(frequencyGraphLineStep / 5.0) * 5.0);
            }

            float textFontHeight = TextMeasurer.Measure("0123456789", new RendererOptions(font)).Height;

            while (frequencyGraphLineStep * pixelsPerUnitCount < 1.1f * textFontHeight)
            {
                // make sure we have enough space so the vertical label numbers don't overlap
                // we won't get the nice-multiple mechanic, but if we're in this situation we're really crunching the graph
                frequencyGraphLineStep++;
            }

            using (var image = new Image<Rgba32>(imageWidth, imageHeight))
            {
                image.Mutate(context =>
                {   // context's origin: top left
                    context.Fill(new SolidBrush<Rgba32>(backColor.ToRgba32()));

                    // for axis label placement
                    int highestRenderedY = -1;
                    int lowestRenderedX = int.MaxValue;

                    // render the graph lines for the frequency axis
                    // it's imperative that we do this first so they can get drawn over
                    for (int i = frequencyGraphLineStep; i * pixelsPerUnitCount < imageHeight - (drawRegionBottomOffset + drawRegionTopOffset); i += frequencyGraphLineStep)
                    {
                        float yVal = imageHeight - drawRegionBottomOffset - (i * pixelsPerUnitCount);
                        context.DrawLines(frequencyGraphLineColor.ToRgba32(), 1, new PointF[] { new PointF(drawRegionLeftOffset, yVal), new PointF(imageWidth - drawRegionRightOffset, yVal) });


                        const float axisDistance = 5;
                        // less imperative to do early, but render the labels here too - we already have the appropriate i
                        RectangleF textBounds = TextMeasurer.MeasureBounds(i.ToString(), new RendererOptions(font));
                        float x = drawRegionLeftOffset - axisDistance - textBounds.Width;
                        if (x < lowestRenderedX)
                        {
                            lowestRenderedX = (int)x;
                        }
                        // no over two because, idk why. I'd expect textBounds.Height/2 for centering but apparently you don't divide by two. Not sure why, ImageSharp quirk probably.
                        context.DrawText(i.ToString(), font, labelColor.ToRgba32(), new PointF(x, yVal - textBounds.Height));
                    }

                    void RenderSlantedXAxisText(string relevantText, PointF bottomLeft)
                    {
                        RectangleF textBounds = TextMeasurer.MeasureBounds(relevantText, new RendererOptions(font));
                        int higherDimension = (int)Math.Ceiling(1.1 * Math.Max(textBounds.Width, textBounds.Height));
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
                            context.DrawImage(textRenderImage, (Point)renderPosition, 1f);

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
                                new PointF(drawRegionLeftOffset + (i * bucketPixelWidth), imageHeight - drawRegionBottomOffset - (pixelsPerUnitCount * countsByBucket[i])),
                                bottomLeft
                        };

                        // rectangle and border
                        context.FillPolygon(new SolidBrush<Rgba32>(barColor.ToRgba32()), boundingBox);
                        context.DrawLines(labelColor.ToRgba32(), 2, boundingBox);

                        RenderSlantedXAxisText(getDataEdgeLabel(lowerBoundsByBucket[i]), bottomLeft);

                        if (i == countsByBucket.Length - 1)
                        {
                            // Fencepost
                            // Render the last label
                            // Last should be exclusive
                            decimal displayLast = Math.Ceiling(last);
                            if (displayLast <= last)
                            {
                                displayLast++;
                            }
                            RenderSlantedXAxisText(getDataEdgeLabel(displayLast), bottomLeft + new PointF(bucketPixelWidth, 0));
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

                    const float labelAxisOffset = 15;

                    // render X-axis label centered in the horizontal space below the X-axis draw region
                    // comfortable margin under existing labels
                    context.DrawText(new TextGraphicsOptions(true)
                    {
                        HorizontalAlignment = HorizontalAlignment.Center
                    }, horizontalAxisLabel, labelFont, labelColor.ToRgba32(), new PointF((drawRegionLeftOffset + imageWidth - drawRegionRightOffset) / 2, highestRenderedY + labelAxisOffset));

                    // Y-axis label, this one's rotated though
                    RectangleF vLabelTextBounds = TextMeasurer.MeasureBounds(verticalAxisLabel, new RendererOptions(labelFont));
                    int vLabelHigherDimension = (int)Math.Ceiling(Math.Max(vLabelTextBounds.Width, vLabelTextBounds.Height));
                    using (Image<Rgba32> textRenderImage = new Image<Rgba32>(vLabelHigherDimension, vLabelHigherDimension))
                    {
                        textRenderImage.Mutate(tempContext =>
                            tempContext
                                .DrawText(verticalAxisLabel, labelFont, labelColor.ToRgba32(), PointF.Empty)
                                .Rotate(-90));
                        textRenderImage.MutateCropToColored();

                        PointF renderPosition = new PointF(lowestRenderedX - textRenderImage.Width - labelAxisOffset, ((imageHeight - drawRegionTopOffset - drawRegionBottomOffset) / 2) + drawRegionTopOffset - (textRenderImage.Height / 2));
                        context.DrawImage(textRenderImage, (Point)renderPosition, 1f);
                    }
                });
                image.SaveAsPng(target);
            }
        }
    }
}