#region License Header
/*
 * CyberScores - A Discord bot for interaction with the CyberPatriot scoreboard
 * Copyright (C) 2017-2024 Glen Husman, Matthew Ring, and contributors
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CyberPatriot.BitmapProvider;

public static class GraphProviderExtensions
{
    public static Task WriteHistogramPngAsync(this IGraphProviderService service, IEnumerable<decimal> dataset, string horizontalAxisLabel, string verticalAxisLabel, Func<decimal, string> getDataEdgeLabel, ColorPresets.HistogramColorPreset colorScheme, System.IO.Stream target)
    {
        return service.WriteHistogramPngAsync(dataset, horizontalAxisLabel, verticalAxisLabel, getDataEdgeLabel, colorScheme.BackgroundColor, colorScheme.BarFillColor, colorScheme.LabelTextColor, colorScheme.GridLineColor, target);
    }
}
