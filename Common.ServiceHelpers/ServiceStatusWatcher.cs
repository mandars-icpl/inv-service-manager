namespace Common.ServiceHelpers;

public static class ServiceStatusWatcher
{
    private static readonly ILogger Logger = Log.ForContext(typeof(ServiceStatusWatcher));

    private static readonly Dictionary<string, ServiceController> MonitoredServices = [];
    private static readonly Dictionary<string, Task> MonitoringTasks = [];
    private static readonly Dictionary<string, ServiceControllerStatus> ServiceStatuses = [];

    private static CancellationTokenSource _cancellationTokenSource = new();

    public static event Action<string, ServiceControllerStatus> ServiceStatusChanged;

    public static void AddService(string serviceName)
    {
        if (MonitoredServices.ContainsKey(serviceName))
        {
            Logger.Information("{ServiceName} already monitored.", serviceName);
        }

        var services = ServiceHelper.GetServiceInfo(serviceName);

        if (services.Count == 0)
        {
            Logger.Error("Error in getting service information for : {ServiceName}", serviceName);
            return;
        }

        var detail = services[0];

        if (detail.InstallStatus != InstallStatus.Installed)
        {
            Logger.Error("{ServiceName} not installed", serviceName);
            return;
        }

        Logger.Information("Monitoring service status: {ServiceName} - {Version}.", serviceName, detail.Version);
        var service = new ServiceController(serviceName);
        MonitoredServices.Add(serviceName, service);
        ServiceStatuses.Add(serviceName, service.Status);
        MonitoringTasks.Add(serviceName, MonitorServiceAsync(service, _cancellationTokenSource.Token));
    }

    private static Task MonitorServiceAsync(ServiceController service, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var lastStatus = ServiceStatuses[service.ServiceName];

                        ServiceControllerStatus currentStatus = service.Status;

                        if (lastStatus != currentStatus)
                        {
                            ServiceStatuses[service.ServiceName] = currentStatus;
                            ServiceStatusChanged?.Invoke(service.ServiceName, currentStatus);
                        }

                        ServiceControllerStatus targetStatus = currentStatus == ServiceControllerStatus.Running ? ServiceControllerStatus.Stopped : ServiceControllerStatus.Running;
                        service.WaitForStatus(targetStatus, TimeSpan.FromSeconds(30)); // Set an appropriate timeout
                        ServiceStatuses[service.ServiceName] = service.Status;

                        if (!cancellationToken.IsCancellationRequested)
                        {
                            ServiceStatusChanged?.Invoke(service.ServiceName, service.Status);
                        }
                    }
                    catch (System.ServiceProcess.TimeoutException)
                    {
                        Logger.Verbose("Monitoring the service: {ServiceName} operation timed out.", service.ServiceName);
                    }
                    catch (InvalidOperationException)
                    {
                        Logger.Error("Monitoring the service: {ServiceName} not found", service.ServiceName);
                        break;
                    }

                    service.Refresh();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.Message);
            }
        });
    }

    public static void StopMonitoring()
    {
        Logger.Information("Stopping all task monitoring.");

        _cancellationTokenSource.Cancel();

        foreach (var mt in MonitoringTasks)
        {
            Logger.Information("Waiting for {ServiceName} to complete.", mt.Key);
            mt.Value.Wait(); // Ensuring all tasks are completed before exiting
        }

        Logger.Information("All task monitoring stopped.");
    }

    public static void ResumeMonitoring()
    {
        Logger.Information("Resume watching services");

        if (_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        foreach (var service in MonitoredServices.Values)
        {
            var serviceName = service.ServiceName;
            if (MonitoringTasks[serviceName].IsCompleted)
            {
                MonitoringTasks[serviceName] = MonitorServiceAsync(service, _cancellationTokenSource.Token);
            }
        }
    }
}
