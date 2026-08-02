namespace SimulcastUtility.Application.Requests
{
    public sealed record ReceiverCreateRequest(string Name, string ReceiverId, string IpAddress);
}