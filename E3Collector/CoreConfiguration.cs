namespace ESA.Common.Configuration
{
    using System.Runtime.Serialization;

    [DataContract]
    public class CoreConfiguration
    {
        [DataMember(Name = "clientId")]
        public string ClientId { get; set; }

        [DataMember(Name = "clientSecret")]
        public string ClientSecret { get; set; }

        [DataMember(Name = "rteAuthorizeUrl")]
        public string RteAuthorizeUrl { get; set; }

        [DataMember(Name = "ecowattSignalUrl")]
        public string EcowattSignalUrl { get; set; }

        [DataMember(Name = "apiKey")]
        public string ApiKey { get; set; }

        [DataMember(Name = "osPowerInWatt")]
        public double OsPowerInWatt { get; set; }

        [DataMember(Name = "signatureId")]
        public string SignatureId { get; set; }

        [DataMember(Name = "signatureKey")]
        public string SignatureKey { get; set; }

        [DataMember(Name = "telemetryInstrumentationKey")]
        public string TelemetryInstrumentationKey { get; set; }
    }
}
