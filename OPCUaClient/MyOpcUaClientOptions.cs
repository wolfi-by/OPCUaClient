using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace OPCUaClient
{
    public class MyOpcUaClientOptions
    {
        [DisplayName("Server endpoint URL")]
        [Required(AllowEmptyStrings = false)]
        [DisplayFormat(ConvertEmptyStringToNull = false)]
        public string endpointUrl { get; set; } = "opc.tcp://localhost:4840";
        public int ReconnectPeriod { get; set; } = 5; // in seconds
    }
}