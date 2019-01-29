using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CyberPatriot.BitmapProvider
{
    public static class GraphProviderExtensions
    {
        public static Task WriteHistogramPngAsync(this IGraphProviderService service, IEnumerable<decimal> dataset, string horizontalAxisLabel, string verticalAxisLabel, Func<decimal, string> getDataEdgeLabel, ColorPresets.HistogramColorPreset colorScheme, System.IO.Stream target)
        {
            return service.WriteHistogramPngAsync(dataset, horizontalAxisLabel, verticalAxisLabel, getDataEdgeLabel, colorScheme.BackgroundColor, colorScheme.BarFillColor, colorScheme.LabelTextColor, colorScheme.GridLineColor, target);
        }
    }
}
