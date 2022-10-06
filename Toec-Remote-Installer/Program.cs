using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Toec_Remote_Installer.ServiceHost;

namespace Toec_Remote_Installer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            ServiceBase.Run(new Host());
        }
    }
}
