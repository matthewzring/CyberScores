using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CyberPatriot.BitmapProvider
{
    public interface IGraphProviderService
    {
        Task WriteHistogramPngAsync(IEnumerable<decimal> dataset,
            string horizontalAxisLabel, string verticalAxisLabel,
            Func<decimal, string> getDataEdgeLabel,
            Color backColor, Color barColor, Color labelColor,
            Stream target);
    }
}