using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CyberPatriot.BitmapProvider
{
    public class NonImplementedGraphProviderService : IGraphProviderService
    {
        Task IGraphProviderService.WriteHistogramPngAsync(IEnumerable<decimal> dataset, string horizontalAxisLabel, string verticalAxisLabel, Func<decimal, string> getDataEdgeLabel, Color backColor, Color barColor, Color labelColor, Color frequencyGraphLineColor, Stream target)
            => Task.FromException(new NotImplementedException());
    }
}