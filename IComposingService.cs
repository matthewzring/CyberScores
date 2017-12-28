namespace CyberPatriot
{
    public interface IComposingService<out TService>
    {
        TService Backend { get; }
    }
}