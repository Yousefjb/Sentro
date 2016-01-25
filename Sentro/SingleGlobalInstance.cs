using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace Sentro
{
    /*
        Responsipility : Prevent multiple instances of sentro to be executed at the same time
    */
    class SingleGlobalInstance : IDisposable
    {
        public bool HasHandle = false;
        Mutex _mutex;

        private void InitMutex()
        {
            string appGuid = ((GuidAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(GuidAttribute), false).GetValue(0)).Value.ToString();
            string mutexId = $"Global\\{{{appGuid}}}";
            _mutex = new Mutex(false, mutexId);

            var allowEveryoneRule = new MutexAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), MutexRights.FullControl, AccessControlType.Allow);
            var securitySettings = new MutexSecurity();
            securitySettings.AddAccessRule(allowEveryoneRule);
            _mutex.SetAccessControl(securitySettings);
        }

        public SingleGlobalInstance(int timeOut)
        {
            InitMutex();
            try
            {
                if (timeOut < 0)
                    HasHandle = _mutex.WaitOne(Timeout.Infinite, false);
                else
                    HasHandle = _mutex.WaitOne(timeOut, false);

                if (HasHandle == false)
                    throw new TimeoutException("Only single instance of Sentro is allowed to run at the same time");
            }
            catch (AbandonedMutexException)
            {
                HasHandle = true;
            }
        }

        public void Dispose()
        {
            if (_mutex != null)
            {
                if (HasHandle)
                    _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }
    }
}
