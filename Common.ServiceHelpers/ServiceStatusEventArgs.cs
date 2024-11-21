namespace Common.ServiceHelpers;

public class ServiceStatusEventArgs : EventArgs
{
    public ServiceStatusEventArgs(ServiceControllerStatus status)
    {
        Status = status;
    }

    public ServiceControllerStatus Status { get; private set; }
}
