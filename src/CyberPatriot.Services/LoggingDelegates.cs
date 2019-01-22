using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
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
