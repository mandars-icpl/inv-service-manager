namespace Common.ServiceHelpers;

public static class ServiceHelper
{
    private static readonly ILogger Logger = Log.ForContext(typeof(ServiceHelper));

    public static bool IsServiceInstalled(string serviceName)
    {
        var services = ServiceController.GetServices().ToList();
        return services.Exists(sc => sc.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
    }

    public static ServiceControllerStatus GetServiceStatus(string serviceName)
    {
        try
        {
            ServiceController sc = new(serviceName);
            return sc.Status;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, ex.Message);
        }

        return ServiceControllerStatus.Stopped;
    }

    public static bool StopService(string serviceName, int wait = 0)
    {
        Logger.Information("Stopping service {ServiceName}", serviceName);

        try
        {
            ServiceController sc = new(serviceName);

            if(sc.Status == ServiceControllerStatus.Stopped)
            {
                Logger.Information("Service {ServiceName} is already stopped", serviceName);
                return true;
            }

            if (sc.Status == ServiceControllerStatus.Running)
            {
                sc.Stop();

                if (wait > 0)
                {
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(wait));
                }
                else
                {
                    sc.WaitForStatus(ServiceControllerStatus.Stopped);
                }

                Logger.Information("Service {ServiceName} stopped", serviceName);
                return true;
            }
            
            Logger.Information("Service {ServiceName} is not running", serviceName);
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error("Error stopping service {ServiceName}. {Message}", serviceName, ex.Message);
        }

        return false;
    }

    public static bool StartService(string serviceName, int wait = 0)
    {
        Logger.Information("Starting service {ServiceName}", serviceName);

        try
        {
            ServiceController sc = new(serviceName);

            if (sc.Status == ServiceControllerStatus.Running)
            {
                Logger.Information("Service {ServiceName} is already running", serviceName);
                return true;
            }

            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                sc.Start();

                if (wait > 0)
                {
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(wait));
                }
                else
                {
                    sc.WaitForStatus(ServiceControllerStatus.Running);
                }

                Logger.Information("Service {ServiceName} started", serviceName);
                return true;
            }

            Logger.Information("Service {ServiceName} is not stopped", serviceName);
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error("Error starting service {ServiceName}. {Message}", serviceName, ex.Message);
        }

        return false;
    }

    public static List<InstallStatusWithDetail> GetServiceInfo(string serviceName)
    {
        using var rk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        var subKey = rk.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}", false);

        if (subKey == null)
        {
            Logger.Information("GetServiceInfo : Service {ServiceName} not found.", serviceName);
            return [];
        }

        var productInfo = new InstallStatusWithDetail
        {
            InstallStatus = InstallStatus.Installed,
            Name = subKey.GetValue("DisplayName") as string
        };

        var imagePath = subKey.GetValue("ImagePath") as string;
        productInfo.InstallPath = ExtractExecutableFilePath(imagePath);
        productInfo.FileDate = CommonFileHelpers.GetFileDate(productInfo.InstallPath);
        productInfo.Architecture = Helpers.GetExecutableArchitecture(productInfo.InstallPath);
        var success = CommonFileHelpers.GetFileVersion(productInfo.InstallPath, out var version);

        productInfo.Version = success ? version : null;

        Logger.Information("Product {ProductName} {Architecture} found on {Path}.", serviceName, productInfo.Architecture, imagePath);

        return [productInfo];
    }

    private static string ExtractExecutableFilePath(string path)
    {
        var absoluteImagePath = Regex.Replace(path, "%(.*?)%", m => Environment.GetEnvironmentVariable(m.Groups[1].Value));

        if (absoluteImagePath.Length == 0) return "";

        return absoluteImagePath[0] == '\"' ? absoluteImagePath.Split('\"')[1] : absoluteImagePath.Split(' ')[0];
    }
}
