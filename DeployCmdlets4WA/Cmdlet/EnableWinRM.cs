using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management.Automation;
using DeployCmdlets4WA.Properties;
using Microsoft.Win32;
using DeployCmdlets4WA.Utilities;

namespace DeployCmdlets4WA.Cmdlet
{
    [Cmdlet(VerbsLifecycle.Enable, "WinRM")]
    public class EnableWinRM : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            base.ProcessRecord();

            //Change the Remote UAC LocalAccountTokenFilterPolicy registry setting
            UpdateRegistry();

            //Enable WinRM.
            Enable();

            //Configure.
            Configure();
        }

        private static void Configure()
        {
            string[] configureCmds = new string[3] 
            {
                "Set-WSManInstance -ComputerName \"localhost\" -resourceuri \"winrm/config/service/auth\" -valueset @{Basic=\"true\"} ",
                "Set-WSManInstance -ComputerName \"localhost\" -resourceuri \"winrm/config/client\" -valueset @{AllowUnencrypted=\"true\"} ",
                "Set-WSManInstance -ComputerName \"localhost\" -resourceuri \"winrm/config/client\" -valueset @{TrustedHosts=\"*\"} "
            };
            string[] messages = new string[3]
            {
                Resources.ConfigWinRMAuth,
                Resources.ConfigWinRMEnc,
                Resources.ConfigWinRMTrustedHost
            };
            for (int i = 0; i < 3; i++)
            {
                ExecutePSCmdlet executeWinRmConfigCmd = new ExecutePSCmdlet();
                executeWinRmConfigCmd.Execute(messages[i], configureCmds[i]);
                if (executeWinRmConfigCmd.ErrorOccurred == true)
                {
                    throw new ApplicationFailedException(Resources.ErrorConfigWinRM);
                }
            }
        }

        private static void Enable()
        {
            ExecutePSCmdlet executeEnableWinRmCmd = new ExecutePSCmdlet();
            string enableWinRmCmd = "Set-WSManQuickConfig -Force";
            executeEnableWinRmCmd.Execute(Resources.EnablingWinRM, enableWinRmCmd);
            if (executeEnableWinRmCmd.ErrorOccurred == true)
            {
                throw new ApplicationFailedException(Resources.ErrorEnablingWinRM);
            }
        }

        private void UpdateRegistry()
        {
            WriteObject(Resources.UpdateRegistryForWinRM);
            Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "LocalAccountTokenFilterPolicy", 1, RegistryValueKind.DWord);
        }
    }
}
