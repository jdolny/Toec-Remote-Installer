using log4net;
using log4net.Repository.Hierarchy;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;

namespace Toec_Remote_Installer
{
    internal class Installer
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string _basePath;

        public Installer()
        {
            _basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Magaeric Solutions");
            Log.Info($"Base path set to {_basePath}");
        }

        public int Start()
        {
            var configObject = ReadConfigFile();
            if(configObject == null)
            {
                Log.Info("Could not read file RemoteInstall.json");
                return 1;
            }

            var arch = Environment.Is64BitOperatingSystem ? "-x64.msi" : "-x86.msi";
            var filename = ($"Toec-{configObject.Version}{arch}");
            var fullPath = Path.Combine(_basePath, filename);
            int result = 0;

            if (configObject.Install)
            {
                var isInstalled = ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == "Toec") != null;
                if (isInstalled)
                {
                    if (new ServiceController("Toec").Status == ServiceControllerStatus.Running)
                    {
                        Log.Info("Toec is already installed.  Exiting.");
                        return 1;
                    }
                    else
                    {
                        Log.Info("Toec is already installed, but not running.  Trying to repair.");
                        if (File.Exists(fullPath))
                            result = RunProcess("/i", "\"" + fullPath + "\"");
                    }
                }
                else
                {
                    Log.Info("Installing Toec");
                    if (File.Exists(fullPath))
                        result = RunProcess("/i", "\"" + fullPath + "\"");
                }

            }
            else if (configObject.Reinstall || configObject.Uninstall)
            {
                Log.Info("Uninstalling Toec");
                var uninstallGuid = GetUninstallString();
                if (!string.IsNullOrEmpty(uninstallGuid))
                    result = RunProcess("/x", "\"" + uninstallGuid + "\"");
                else
                {
                    result = 1;
                    Log.Info("Could not find any Toec versions to remove.");
                }
                //target is any cpu so specialfolder.program files does not work properly, Toec always installs in program files, not x86
                var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                pf = pf.Replace(" (x86)", "");
                var toecPath = Path.Combine(pf, "Toec");

                if (Directory.Exists(toecPath))
                {
                    try
                    {
                        Directory.Delete(toecPath, true);
                    }
                    catch
                    {//ignored 
                    }

                }
                if (configObject.Reinstall)
                {
                    Log.Info("Installing Toec (Reinstall)");
                    result = RunProcess("/i", "\"" + fullPath + "\"");
                }
            }

            if (result == 0)
            {
                try
                {
                    var successFilePath = Path.Combine(_basePath, configObject.DeployId + ".success");
                    File.WriteAllText(successFilePath, "");
                }
                catch { }
            }
            else
            {
                try
                {
                    var successFilePath = Path.Combine(_basePath, configObject.DeployId + ".failed");
                    File.WriteAllText(successFilePath, "");
                }
                catch { };
            }
            return result;
        }
        private DtoConfig ReadConfigFile()
        {
            Log.Info("Reading Config File");
            var configFile = Path.Combine(_basePath, "RemoteInstall.json");
            try
            {
                var configText = File.ReadAllText(configFile);
                Log.Info(configText);
                return JsonConvert.DeserializeObject<DtoConfig>(configText);
            }
            catch { return null; }
            
        }

        private string GetUninstallString()
        {
            RegistryKey machineUninstallKey;
            if (Environment.Is64BitOperatingSystem)
            {
                machineUninstallKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                machineUninstallKey = machineUninstallKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            }
            else
            {
                machineUninstallKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                machineUninstallKey = machineUninstallKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            }

            foreach (var subKey in machineUninstallKey.GetSubKeyNames())
            {
                var softwareKey = machineUninstallKey.OpenSubKey(subKey);
                if (softwareKey == null) continue;
                var name = Convert.ToString(softwareKey.GetValue("DisplayName"));
                if (string.IsNullOrEmpty(name)) continue;
                var publisher = Convert.ToString(softwareKey.GetValue("Publisher"));
                if(string.IsNullOrEmpty(publisher)) continue;
                if(name.ToLower().Contains("toec") && publisher.ToLower().Contains("magaeric solutions"))
                {
                    return subKey.ToString();
                }
            }

            return null;
        }

        private int RunProcess(string msiSwitch, string command)
        {
            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();
            var process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.FileName = "msiexec.exe";
            process.StartInfo.Arguments = $" {msiSwitch} {command} /q /norestart";
            process.OutputDataReceived += (sender, args) => output.AppendLine(args.Data);
            process.ErrorDataReceived += (sender, args) => error.AppendLine(args.Data);
            Log.Info($"Running process {process.StartInfo.FileName} {process.StartInfo.Arguments}");
         
            process.Start();
            process.WaitForExit(5 * 1000 * 60);

            Log.Info(output.ToString());
            Log.Info(error.ToString());
            Log.Info($"Exit Code: {process.ExitCode}");
            return process.ExitCode;

        }
    }
}
