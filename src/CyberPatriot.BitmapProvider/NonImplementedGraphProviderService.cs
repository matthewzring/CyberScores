using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CyberPatriot.BitmapProvider
{
    public class NonImplementedGraphProviderService : IGraphProviderService
    {
        Task IGraphProviderService.WriteHistogramPngAsync(IEnumerable<decimal> dataset, string horizontalAxisLabel, string verticalAxisLabel, Color backColor, Color barColor, Color labelColor, Stream target)
            => Task.FromException(new NotImplementedException());
    }
}