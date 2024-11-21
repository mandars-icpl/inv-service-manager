namespace Common.ServiceHelpers.Wrapper;

public static class ServiceWrapper
{
    private const int StandardRightsRequired = 0xF0000;
    private const int ServiceWin32OwnProcess = 0x00000010;

    #region OpenSCManager

    [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern IntPtr OpenSCManager(string machineName, string databaseName,
        ScmAccessRights dwDesiredAccess);

    #endregion

    #region OpenService

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr OpenService(IntPtr hScManager, string lpServiceName,
        ServiceAccessRights dwDesiredAccess);

    #endregion

    #region CreateService

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateService(IntPtr hScManager, string lpServiceName, string lpDisplayName,
        ServiceAccessRights dwDesiredAccess, int dwServiceType, ServiceBootFlag dwStartType,
        ServiceError dwErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, IntPtr lpdwTagId,
        string lpDependencies, string lp, string lpPassword);

    #endregion

    #region CloseServiceHandle

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr hScObject);

    #endregion

    #region QueryServiceStatus

    [DllImport("advapi32.dll")]
    private static extern int QueryServiceStatus(IntPtr hService, ServiceStatus lpServiceStatus);

    #endregion

    #region DeleteService

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteService(IntPtr hService);

    #endregion

    #region ControlService

    [DllImport("advapi32.dll")]
    private static extern int ControlService(IntPtr hService, ServiceControl dwControl,
        ServiceStatus lpServiceStatus);

    #endregion

    #region StartService

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int StartService(IntPtr hService, int dwNumServiceArgs, string[] lpServiceArgVectors);

    #endregion

    public static void Uninstall(string serviceName)
    {
        var scm = OpenScManager(ScmAccessRights.AllAccess);

        try
        {
            var service = OpenService(scm, serviceName, ServiceAccessRights.AllAccess);
            if (service == IntPtr.Zero)
                throw new KeyNotFoundException(serviceName);

            try
            {
                StopService(service);
                if (!DeleteService(service))
                {
                    throw new AccessViolationException("Could not delete service " + Marshal.GetLastWin32Error());
                }
                    
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    public static bool ServiceIsInstalled(string serviceName)
    {
        var scm = OpenScManager(ScmAccessRights.Connect);

        try
        {
            var service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus);

            if (service == IntPtr.Zero)
                return false;

            CloseServiceHandle(service);
            return true;
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    public static void InstallAndStart(string serviceName, string displayName, string fileName, string[] args)
    {
        var scm = OpenScManager(ScmAccessRights.AllAccess);

        try
        {
            var service = OpenService(scm, serviceName, ServiceAccessRights.AllAccess);

            if (service == IntPtr.Zero)
                service = CreateService(scm, serviceName, displayName, ServiceAccessRights.AllAccess,
                    ServiceWin32OwnProcess, ServiceBootFlag.AutoStart, ServiceError.Normal, fileName, null,
                    IntPtr.Zero, null, null, null);

            if (service == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to install service.");
            }                

            try
            {
                StartService(service, args);
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    public static void StartService(string serviceName, string[] args)
    {
        var scm = OpenScManager(ScmAccessRights.Connect);

        try
        {
            var service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus | ServiceAccessRights.Start);
            if (service == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not open service.");
            }                

            try
            {
                StartService(service, args);
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    public static void StopService(string serviceName)
    {
        var scm = OpenScManager(ScmAccessRights.Connect);

        try
        {
            var service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus | ServiceAccessRights.Stop);
            
            if (service == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not open service.");
            }
                

            try
            {
                StopService(service);
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    private static void StartService(IntPtr service, string[] args)
    {
        StartService(service, args.Length, args);
        var changedStatus = WaitForServiceStatus(service, ServiceState.StartPending, ServiceState.Running);
        if (!changedStatus)
        {
            throw new InvalidOperationException("Unable to start service");
        }
    }

    private static void StopService(IntPtr service)
    {
        var status = new ServiceStatus();
        ControlService(service, ServiceControl.Stop, status);
        var changedStatus = WaitForServiceStatus(service, ServiceState.StopPending, ServiceState.Stopped);
        if (!changedStatus) 
        {
            throw new InvalidOperationException("Unable to stop service");
        }
    }

    public static ServiceState GetServiceStatus(string serviceName)
    {
        var scm = OpenScManager(ScmAccessRights.Connect);

        try
        {
            var service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus);
            if (service == IntPtr.Zero)
                return ServiceState.NotFound;

            try
            {
                return GetServiceStatus(service);
            }
            finally
            {
                CloseServiceHandle(service);
            }
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    private static ServiceState GetServiceStatus(IntPtr service)
    {
        var status = new ServiceStatus();

        if (QueryServiceStatus(service, status) == 0)
        {
            throw new InvalidOperationException("Failed to query service status.");
        }            

        return status.dwCurrentState;
    }

    private static bool WaitForServiceStatus(IntPtr service, ServiceState waitStatus, ServiceState desiredStatus)
    {
        var status = new ServiceStatus();

        QueryServiceStatus(service, status);
        if (status.dwCurrentState == desiredStatus) return true;

        var dwStartTickCount = Environment.TickCount;
        var dwOldCheckPoint = status.dwCheckPoint;

        while (status.dwCurrentState == waitStatus)
        {
            // Do not wait longer than the wait hint. A good interval is
            // one tenth the wait hint, but no less than 1 second and no
            // more than 10 seconds.

            var dwWaitTime = status.dwWaitHint / 10;

            if (dwWaitTime < 1000) dwWaitTime = 1000;
            else if (dwWaitTime > 10000) dwWaitTime = 10000;

            Thread.Sleep(dwWaitTime);

            // Check the status again.

            if (QueryServiceStatus(service, status) == 0) break;

            if (status.dwCheckPoint > dwOldCheckPoint)
            {
                // The service is making progress.
                dwStartTickCount = Environment.TickCount;
                dwOldCheckPoint = status.dwCheckPoint;
            }
            else
            {
                if (Environment.TickCount - dwStartTickCount > status.dwWaitHint)
                    // No progress made within the wait hint
                    break;
            }
        }

        return status.dwCurrentState == desiredStatus;
    }

    private static IntPtr OpenScManager(ScmAccessRights rights)
    {
        var scm = OpenSCManager(null, null, rights);
        if (scm == IntPtr.Zero)
        {
            throw new InvalidOperationException("Could not connect to service control manager.");
        }            

        return scm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private sealed class ServiceStatus
    {
        public readonly int dwCheckPoint = 0;
        public int dwControlsAccepted = 0;
        public readonly ServiceState dwCurrentState = 0;
        public int dwServiceSpecificExitCode = 0;
        public int dwServiceType = 0;
        public readonly int dwWaitHint = 0;
        public int dwWin32ExitCode = 0;
    }
}
