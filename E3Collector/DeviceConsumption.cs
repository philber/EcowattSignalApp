namespace E3Collector
{
    using ESA.Common.Core;
    using Newtonsoft.Json;
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Windows.Storage;

    public class DeviceConsumption
    {
        #region Methods

        internal static void ReportDeviceConsumption(ConsumptionLog consumptionLog)
        {
            var json = JsonConvert.SerializeObject(consumptionLog);

            File.WriteAllText(Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "e3log.json"), Convert.ToString(json));
            //File.WriteAllText(Path.Combine(System.IO.Path.GetTempPath(), "e3log.json"), Convert.ToString(fileContent));
        }

        #endregion Methods
    }
}