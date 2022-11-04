namespace ESA.Common.Repositories.Implementations
{
    using E3Collector; 
    using ESA.Common.Core;
    using ESA.Common.Extensions;
    using ESA.Common.Messaging;
    using ESA.Common.Models;
    using ESA.Common.Repositories.Interfaces;
    using Microsoft.Toolkit.Mvvm.Messaging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Reactive.Linq;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Text;
    using System.Threading.Tasks;
    using Windows.ApplicationModel.AppService;
    using Windows.Devices.Power;
    using Windows.Foundation;
    using Windows.Storage;

    public class DeviceConsumptionRepository : IDeviceConsumptionRepository
    {
        #region Constants and fields

        private const int MAX_POWER_RAW_VALUES_FOR_AVERAGE = 10;

        // Temporary constant used pendant the implementation of a specific Windows API (the value is calculed for laptops)
        private readonly double OS_POWER_IN_WATT = 5.28;

        private static HttpClient httpClient = new HttpClient(new SignatureDelegateHandler());

        private Signals_obj lastEcowattSignals = null;

        private ConsumptionLog lastConsumptionLog;

        private DeviceConsumption lastDeviceConsumption = null;

        private ElectricityNetworkState networkState;

        private int? lastBatteryChargeRateInMilliwatts;

        private List<long> lastPowerRawValues = new List<long>();

        #endregion Fields

        #region IDeviceConsumptionRepository members

        public event EventHandler<DeviceConsumptionChangedEventArgs> OnDeviceConsumptionChanged;

        public event EventHandler<ElectricityNetworkStateChangedEventArgs> OnNetworkStateChanged;

        public DeviceConsumption LastDeviceConsumption
        {
            get => lastDeviceConsumption;
            set
            {
                var previousValue = lastDeviceConsumption;
                lastDeviceConsumption = value;
                OnDeviceConsumptionChanged?.Invoke(this, new DeviceConsumptionChangedEventArgs(previousValue, lastDeviceConsumption));
            }
        }

        public ElectricityNetworkState NetworkState
        {
            get => networkState;
            private set
            {
                var previousValue = networkState;
                networkState = value;
                OnNetworkStateChanged?.Invoke(this, new ElectricityNetworkStateChangedEventArgs(previousValue, networkState));
            }
        }

        public async Task InitializeAsync()
        {
            try
            {
                await LoadNetworkStateFromRteApiAsync();
            }
            catch
            {
                // Ignore exception from RTE Ecowatt 4.0 signal api
            }
            
            LoadDeviceConsumptionFromJsonFile(); 
            
            WeakReferenceMessenger.Default.Register<ElectricityNetworkStateChangedMessage>(this, this.OnElectricityNetworkStateChangedMessageReceived);
            Battery.AggregateBattery.ReportUpdated += OnAggregateBatteryReportUpdated;
            RefreshBatteryChargeCarbonImpact(Battery.AggregateBattery.GetReport());
        }

        public async Task LoadNetworkStateFromRteApiAsync()
        {
            try
            {
                if (CoreConfigurationManager.Instance.Configuration == null)
                {
                    await CoreConfigurationManager.Instance.InitAsync();
                }

                Task<string> getAccessToken = GetRteAccessToken(CoreConfigurationManager.Instance.Configuration.RteAuthorizeUrl,
                    CoreConfigurationManager.Instance.Configuration.ClientId, CoreConfigurationManager.Instance.Configuration.ClientSecret);
                var accessToken = await getAccessToken;

                lastEcowattSignals = await GetEcowattSignalResponse(CoreConfigurationManager.Instance.Configuration.EcowattSignalUrl, accessToken);
                if (lastEcowattSignals != null)
                {
                    NetworkState = GetNetworkStateFromSignals(lastEcowattSignals);
                }
            }
            catch { }
        }


        private async Task<string> GetRteAccessToken(string rteAuthorizeUrl, string clientId, string clientSecret)
        {
            AuthToken token = null;
            
            if (rteAuthorizeUrl == null) throw new ArgumentNullException("RteAuthorizeUrl");
            if (clientId == null) throw new ArgumentNullException("ClientId");
            if (clientSecret == null) throw new ArgumentNullException("ClientSecret");

            try
            {
                HttpClientHandler handler = new HttpClientHandler() { UseDefaultCredentials = false };
                HttpClient httpClient = new HttpClient(handler);
                string body = "";
                
                httpClient.BaseAddress = new Uri(rteAuthorizeUrl);
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json")); //ACCEPT header
                httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + Convert.ToBase64String(
                        Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"))); // Authorization header

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, httpClient.BaseAddress);
                request.Content = new StringContent(body, Encoding.UTF8,
                                    "application/x-www-form-urlencoded"); // Content-Type header

                HttpResponseMessage response = await httpClient.PostAsync(rteAuthorizeUrl, null);
                if (response.IsSuccessStatusCode)
                {
                    token = JsonConvert.DeserializeObject<AuthToken>(response.Content.ReadAsStringAsync().Result);
                }
             }
            catch (HttpRequestException ex)
            {
                throw ex;
            }
            return token != null ? token.AccessToken : null;
        }

        private async Task<Signals_obj> GetEcowattSignalResponse(string ecowattSignalUrl, string accessToken)
        {
            Signals_obj signalsObj = null;

            if (ecowattSignalUrl == null) throw new ArgumentNullException("EcowattSignalUrl");
            if (accessToken == null) throw new ArgumentNullException("accessToken");

            try
            {
                HttpClientHandler handler = new HttpClientHandler() { UseDefaultCredentials = false };
                HttpClient httpClient = new HttpClient(handler);
                
                httpClient.BaseAddress = new Uri(ecowattSignalUrl);
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

                EcowattSignalResponse ecowattSignalResponse = new EcowattSignalResponse(httpClient);
                ecowattSignalResponse.BaseUrl = ecowattSignalUrl;

                // Gets signals from the Ecowatt 4.0 API
                signalsObj = await ecowattSignalResponse.Async();
            }
            catch { }

            return signalsObj;
        }

        private ElectricityNetworkState GetNetworkStateFromSignals(Signals_obj signalsObj)
        {
            if (signalsObj == null) throw new ArgumentNullException("signalsObj");

            Signals signals = signalsObj.Signals.First<Signals>();

            switch (signals.Dvalue)
            {
                case 1: // Green
                    return ElectricityNetworkState.Excellent;

                case 2: // Orange
                    return ElectricityNetworkState.Poor;

                case 3: // Red
                    return ElectricityNetworkState.Bad;

                default:
                    break;
            }

            return ElectricityNetworkState.Unknown;
        }
        public void LoadDeviceConsumptionFromJsonFile()
        {
            var filePath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, "e3log.json");

            if (File.Exists(filePath)) 
            {
                try   
                {
                    // retrieve last hour of data
                    ConsumptionLog consumptionLog = (ConsumptionLog) Newtonsoft.Json.JsonConvert.DeserializeObject(File.ReadAllText(filePath));
                    if (consumptionLog != null)
                    {
                        lastConsumptionLog = consumptionLog ;
                    }
                }
                catch {}

                File.Delete(filePath);
            }
        }

        public Task ReadDeviceConsumptionFor1MinuteAsync()
        {
            return AppService.LaunchFullTrustAndConnect("ECRealtime", "EnergyCollectorRealtime", (appServiceConnection, cts) =>
            {
                Observable
                    .FromEvent<TypedEventHandler<AppServiceConnection, AppServiceRequestReceivedEventArgs>, AppServiceRequestReceivedEventArgs>(
                        h => new TypedEventHandler<AppServiceConnection, AppServiceRequestReceivedEventArgs>((_sender, result) => h(result))
                        , h => appServiceConnection.RequestReceived += h
                        , h => appServiceConnection.RequestReceived -= h)
                    .Subscribe(OnRealTimeMessageReceived, cts.Token);

                return Task.FromResult(0);
            });
        }

        private static string GetDeviceId()
        {
            var deviceId = Windows.System.Profile.SystemIdentification.GetSystemIdForPublisher();
            return Convert.ToBase64String(deviceId.Id.ToArray());
        }

        private static double MilliJoulesToW(double milliJoules)
        {
            var powerWh = milliJoules / 1000;

            return powerWh;
        }

        private static double MilliJoulesToWH(double milliJoules)
        {
            var powerWh = milliJoules / 1000 / 3600;

            return powerWh;
        }

        private static double WHTogCo2(double powerWh, double co2GperKWh)
        {
            var mgCo2perh = powerWh * co2GperKWh;
            return mgCo2perh / 1000;
        }

        private void OnAggregateBatteryReportUpdated(Battery sender, object args)
        {
            RefreshBatteryChargeCarbonImpact(Battery.AggregateBattery.GetReport());
        }

        private void OnRealTimeMessageReceived(AppServiceRequestReceivedEventArgs args)
        {
            var deferal = args.GetDeferral();
            try
            {
                if (args.Request.Message.TryGetValue("realtimePower", out var p))
                {
                    var rawP = StorePowerRawValueAndGetAverage((long)p);
                    var powerW = MilliJoulesToW(rawP);
                    var batteryChargingPower = lastBatteryChargeRateInMilliwatts.HasValue && lastBatteryChargeRateInMilliwatts > 0 ? lastBatteryChargeRateInMilliwatts.Value / 1000 : 0.0;
                    var deviceTotalPowerW = MilliJoulesToW(OS_POWER_IN_WATT * 1000 + (double)rawP) + batteryChargingPower;
                    var co2Rate = 78.0; // gCo2/Kwh

                    Signals_obj lastSignalsObj = lastEcowattSignals;
                    if (lastSignalsObj != null)
                    {
                        // TODO: exploit the signals' values as per RTE Ecowatt 4.0 Signal API documentation 
                        // https://data.rte-france.com/catalog/-/api/doc/user-guide/Ecowatt/4.0
                    }

                    ConsumptionLog lastLog = lastConsumptionLog;
                    if (lastLog != null)
                    {
                        double mJOnBattery = 0;
                        double mJPluggedIn = 0;

                        foreach (Consumption value in lastLog.Values)
                        {
                            mJOnBattery += value.mJOnBattery;
                            mJPluggedIn += value.mJPluggedIn;
                        }

                        // TODO: exploit the consolidated consumption' values
                    }

                    var deviceConsumption = new DeviceConsumption
                    {
                        UserAppsConsumptionInWatt = powerW,
                        DeviceConsumptionInWatt = deviceTotalPowerW,
                        DeviceImpactInCO2 = WHTogCo2(deviceTotalPowerW / 3600, co2Rate) * 1000,
                        BatteryChargeConsumptionInWatt = batteryChargingPower,
                    };

                    LastDeviceConsumption = deviceConsumption;
                }
            }
            finally
            {
                deferal.Complete();
            }
        }

        private double StorePowerRawValueAndGetAverage(long rawP)
        {
            lastPowerRawValues.Insert(0, rawP);
            if (lastPowerRawValues.Count > MAX_POWER_RAW_VALUES_FOR_AVERAGE)
            {
                lastPowerRawValues.RemoveAt(MAX_POWER_RAW_VALUES_FOR_AVERAGE);
            }
            return lastPowerRawValues.Average();
        }

        #endregion IDeviceConsumptionRepository members

        #region Private methods

        private void OnElectricityNetworkStateChangedMessageReceived(object recipient, ElectricityNetworkStateChangedMessage message)
        {
            LoadNetworkStateFromRteApiAsync().NotAwaited();
        }

        private void RefreshBatteryChargeCarbonImpact(BatteryReport batteryReport)
        {
            lastBatteryChargeRateInMilliwatts = batteryReport.ChargeRateInMilliwatts;
        }

        #endregion Private methods
    }
}
