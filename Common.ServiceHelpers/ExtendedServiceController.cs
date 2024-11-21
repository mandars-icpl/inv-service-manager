namespace Common.ServiceHelpers;

public class ExtendedServiceController : ServiceController
{
    private readonly Dictionary<ServiceControllerStatus, Task> _tasks = [];

    public ExtendedServiceController(string serviceName) : base(serviceName)
    {
        foreach (ServiceControllerStatus status in Enum.GetValues(typeof(ServiceControllerStatus)))
        {
            _tasks.Add(status, null);
        }   
    }

    public new ServiceControllerStatus? Status
    {
        get
        {
            Refresh();
            try
            {
                return base.Status;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public event EventHandler<ServiceStatusEventArgs> StatusChanged;

    public void StartListening()
    {
        if (Status == null) return;

        foreach (ServiceControllerStatus status in Enum.GetValues(typeof(ServiceControllerStatus)))
            if (Status != status && (_tasks[status] == null || _tasks[status].IsCompleted))
                _tasks[status] = Task.Run(() =>
                {
                    try
                    {
                        WaitForStatus(status);
                        OnStatusChanged(new ServiceStatusEventArgs(status));
                        StartListening();
                    }
                    catch
                    {
                        // You can either raise another event here with the exception or ignore it
                        // since it most likely means the service was uninstalled/lost communication
                    }
                });
    }

    protected virtual void OnStatusChanged(ServiceStatusEventArgs e)
    {
        var handler = StatusChanged;
        handler?.Invoke(this, e);
    }
}