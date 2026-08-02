namespace SimulcastUtility.Application.Requests
{
    public sealed record ReceiverUpdateRequest(string Name, string ReceiverId, string IpAddress);
}