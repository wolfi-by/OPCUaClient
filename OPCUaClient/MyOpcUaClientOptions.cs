namespace OPCUaClient
{
    public class MyOpcUaClientOptions
    {
        public string endpointUrl { get; set; } = "opc.tcp://localhost:4840";
        public int ReconnectPeriod { get; set; } = 5; // in seconds
    }
}