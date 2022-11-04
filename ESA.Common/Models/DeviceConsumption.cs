namespace ESA.Common.Models
{
    using System.Runtime.Serialization;

    [DataContract]
    public class DeviceConsumption
    {
        #region Properties

        [DataMember(Name = "batteryChargeConsumptionInWatt")]
        public double BatteryChargeConsumptionInWatt { get; set; }

        [DataMember(Name = "deviceConsumptionInWatt")]
        public double DeviceConsumptionInWatt { get; set; }

        [DataMember(Name = "userAppsConsumptionInWatt")]
        public double UserAppsConsumptionInWatt { get; set; }

        [DataMember(Name = "deviceImpactInCO2")]
        public double DeviceImpactInCO2 { get; set; }

        [DataMember(Name = "userCarbonEffortInCO2g")]
        public int UserCarbonEffortInCO2g { get; set; }

        #endregion
    }
}