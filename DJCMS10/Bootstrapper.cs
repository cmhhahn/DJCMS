using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Windows;
using DJCMS.ViewModels;
using Serilog;

namespace DJCMS
{
    public class Bootstrapper : BootstrapperBase
    {
        private SimpleContainer _container;

        public Bootstrapper()
        {
            Initialize();
        }

        protected override void Configure()
        {
            _container = new SimpleContainer();

            _container.Singleton<IWindowManager, WindowManager>();
            _container.Singleton<IEventAggregator, EventAggregator>();

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File("logs\\DJCMS-log-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // Register the Serilog logger instance in the container so it can be injected
            _container.Instance<Serilog.ILogger>(Log.Logger);

            _container.PerRequest<MainWindowViewModel>();
        }

        protected override object GetInstance(Type service, string key)
        {
            return _container.GetInstance(service, key);
        }

        protected override IEnumerable<object> GetAllInstances(Type service)
        {
            return _container.GetAllInstances(service);
        }

        protected override void BuildUp(object instance)
        {
            _container.BuildUp(instance);
        }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            DisplayRootViewForAsync<MainWindowViewModel>();
        }
    }
}
