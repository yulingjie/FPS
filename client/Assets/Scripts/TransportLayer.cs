


public interface IInputStream
{
    uint Length();
    uint PeekUInt();
    uint ReadUInt();
}
public class TransportLayer
{
    public void ProcessInput(IInputStream inputStream)
    {

    }
}