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
using System.Threading.Tasks;

namespace CyberPatriot.BitmapProvider
{
    public interface IGraphProviderService
    {
        /// <summary>
        /// Creates a histogram of the given data and saves it as a PNG to the given stream.
        /// </summary>
        /// <param name="dataset">The sorted set of data from which a histogram should be produced.</param>
        /// <param name="horizontalAxisLabel">The data axis label.</param>
        /// <param name="verticalAxisLabel">The frequency axis label.</param>
        /// <param name="getDataEdgeLabel">A function which formats a datum for display as an axis label.</param>
        /// <param name="backColor">The background color.</param>
        /// <param name="barColor">The color with which the bars are filled.</param>
        /// <param name="labelColor">The color of text in the various labels.</param>
        /// <param name="frequencyGraphLineColor">The color of the background grid lines.</param>
        /// <param name="target">The stream to which the histogram image should be written.</param>
        /// <returns>A task which resolves when the histogram has been produced.</returns>
        Task WriteHistogramPngAsync(IEnumerable<decimal> dataset,
            string horizontalAxisLabel, string verticalAxisLabel,
            Func<decimal, string> getDataEdgeLabel,
            Color backColor, Color barColor, Color labelColor, Color frequencyGraphLineColor,
            Stream target);
    }
}
