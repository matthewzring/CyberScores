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

using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace CyberPatriot.Services
{
    /// <summary>
    /// A temporary delegate type representing a method through which log messages can be sent.
    /// This delegate will be made obsolete when the CyberPatriot.Services APIs fully integrate with Microsoft's logging extension.
    /// </summary>
    /// <param name="severity">The severity of this log event.</param>
    /// <param name="message">The message to be logged.</param>
    /// <param name="exception">The optional exception to be logged alongside the given message.</param>
    /// <param name="source">The source of this message.</param>
    /// <returns>A task which completes when the message has been logged.</returns>
    public delegate Task PostLogAsyncHandler(LogLevel severity, string message, Exception exception = null, string source = "Application");
}
