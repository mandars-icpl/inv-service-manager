namespace Common.ServiceHelpers.Mappers;

public static class ServiceStatusMapper
{
    public static ToolStatus Map(ToolGroup toolGroup, ServiceControllerStatus status)
    {
        return status switch
        {
            ServiceControllerStatus.StopPending or ServiceControllerStatus.PausePending or ServiceControllerStatus.Paused or ServiceControllerStatus.Running => new ToolStatus(toolGroup, InstallStatus.Installed, RunningStatus.Running),
            ServiceControllerStatus.ContinuePending or ServiceControllerStatus.Stopped or ServiceControllerStatus.StartPending => new ToolStatus(toolGroup, InstallStatus.Installed, RunningStatus.Stopped),
            _ => new ToolStatus(toolGroup, InstallStatus.Unknown, RunningStatus.Unknown),
        };
    }

    public static RunningStatus Map(ServiceControllerStatus status)
    {
        return status switch
        {
            ServiceControllerStatus.StopPending or ServiceControllerStatus.PausePending or ServiceControllerStatus.Running => RunningStatus.Running,
            ServiceControllerStatus.Stopped or ServiceControllerStatus.StartPending => RunningStatus.Stopped,
            ServiceControllerStatus.Paused or ServiceControllerStatus.ContinuePending => RunningStatus.Warning,
            _ => RunningStatus.Unknown,
        };
    }
}
