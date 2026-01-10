namespace x402dev.Client.Models
{
    public record NetworkOption(string Value, string Name);

    public class SignatureBuilderState
    {
        public string? Pkey { get; set; }
        public string? Address { get; set; }
        public NetworkOption SelectedNetworkOption { get; set; } = new NetworkOption(84532.ToString(), "eip155:84532");
        public string? CustomNetworkId { get; set; }
        public string? TokenName { get; set; } = "USDC";
        public string? TokenVersion { get; set; } = "2";
        public string? TokenContractAddress { get; set; } = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
        public string? PayTo { get; set; } = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";
        public ulong Amount { get; set; } = 10000;
        public string? Network { get; set; } = "eip155:84532";
        public string? Resource { get; set; } = "https://www.x402.org/protected";
        public string? Base64Header { get; set; }
        public string? HeaderJson { get; set; }
        public int LineCount { get; set; } = 10;
        public int ValidAfter { get; set; } = 0;
        public int ValidBefore { get; set; } = 15;
    }
}
