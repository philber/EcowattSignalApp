﻿namespace ESA.Common.Repositories.Interfaces
{
    using ESA.Common.Models;
    using System;
    using System.Threading.Tasks;

    public interface IDeviceConsumptionRepository
    {
        #region Properties

        DeviceConsumption LastDeviceConsumption { get; set; }

        ElectricityNetworkState NetworkState { get; }

        #endregion

        #region Events

        event EventHandler<DeviceConsumptionChangedEventArgs> OnDeviceConsumptionChanged;

        event EventHandler<ElectricityNetworkStateChangedEventArgs> OnNetworkStateChanged;

        #endregion Events

        #region Methods

        Task InitializeAsync();

        Task LoadNetworkStateFromRteApiAsync();

        void LoadDeviceConsumptionFromJsonFile();
        
        Task ReadDeviceConsumptionFor1MinuteAsync();

        #endregion Methods
    }
}