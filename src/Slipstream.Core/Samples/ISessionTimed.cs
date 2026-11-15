namespace Slipstream.Core;

public interface ISessionTimed
{
    TimeSpan SessionTime { get; }
}