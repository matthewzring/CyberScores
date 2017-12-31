namespace CyberPatriot.DiscordBot
{
    public interface IComposingService<out TService>
    {
        TService Backend { get; }
    }
}