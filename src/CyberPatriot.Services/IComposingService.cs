namespace CyberPatriot.Services
{
    public interface IComposingService<out TService>
    {
        TService Backend { get; }
    }
}