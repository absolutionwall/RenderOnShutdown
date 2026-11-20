using System;
using System.Threading;
using System.Windows;

namespace RenderOnShutdown
{
    public partial class App : System.Windows.Application
    {
        private const string SingleInstanceMutexName = @"Local\RenderOnShutdown.SingleInstance";
        private Mutex? _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
            if (!createdNew)
            {
                Shutdown();
                return;
            }
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_singleInstanceMutex != null)
            {
                try { _singleInstanceMutex.ReleaseMutex(); } catch { }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }
            base.OnExit(e);
        }
    }
}
