namespace ESA.Uwp.Core
{
    using ESA.Common.Repositories.Implementations;
    using ESA.Common.Repositories.Interfaces;
    using ESA.Uwp.Views.Settings;
    using ESA.Uwp.Views.Dashboard;
    using ESA.Uwp.Views.Start;
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using ESA.Uwp.Repositories.Implementations;

    class IoCInitializer
    {
        /// <summary>
        /// Configures the services for the application.
        /// </summary>
        public static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Repositories
            services.AddSingleton<IDeviceConsumptionRepository, DeviceConsumptionRepository>();
            services.AddSingleton<ISettingsRepository, SettingsRepository>();   

            // Services
            services.AddSingleton(typeof(NavigationService));
            services.AddSingleton(typeof(ScheduledTaskService));

            // ViewModels
            services.AddSingleton(typeof(StartPageViewModel));
            services.AddSingleton(typeof(DashboardPageViewModel));
            services.AddSingleton(typeof(SettingsPageViewModel));

            return services.BuildServiceProvider();
        }

    }
}
