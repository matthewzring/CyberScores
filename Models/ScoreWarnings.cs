using System;

namespace CyberPatriot.Models
{
    [Flags]
    public enum ScoreWarnings
    {
        MultiImage = 1 << 0,
        TimeOver = 1 << 1
    }
}