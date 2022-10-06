using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Toec_Remote_Installer.ServiceHost
{
    partial class Host : ServiceBase
    {
        private static Thread _serviceStartThread;
        public Host()
        {
            InitializeComponent();
            CanShutdown = true;
            _serviceStartThread = new Thread(DoWork);
        }

        private void DoWork()
        {
            for (int i = 0; i < 3; i++)
            {
                var result = new Installer().Start();
                if (result == 0)
                    break;
                Thread.Sleep(5000);
            }
            Environment.Exit(0);
        }

        protected override void OnStart(string[] args)
        {

           _serviceStartThread.Start();

        }

        protected override void OnStop()
        {

        }
    }
}
