<#
   Copyright © Microsoft Open Technologies, Inc.
   All Rights Reserved
   Apache 2.0 License

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.

   See the Apache Version 2.0 License for specific language governing permissions and limitations under the License.
#>

$global:ErrorActionPreference = "Stop"

function logStatus {
    param ($message)
    Write-Host "info:   $message" -foregroundcolor "yellow"
}

function logStatus2 {
    param ($message)
    Write-Host "info:   $message" -foregroundcolor "blue"
}

function logErr {
    param ($message)
    Write-Host "error:  $message" -foregroundcolor "red"
}

function logSuccess {
    param ($message)
    Write-Host "info:   $message" -foregroundcolor "green"
}

function logInput {
    param ($message)
    Write-Host "input:  $message" -foregroundcolor "magenta"
}

<#
    Enable web server on the VM
#>
function Enable-Web-Server {
	Import-Module Servermanager

	$checkWebServerEnabled = Get-WindowsFeature | Where-Object {$_.Name -eq "Web-Server"}
	If ($checkWebServerEnabled.Installed -eq "True")
	{
		logSuccess "Web server already enabled on VM."
		return $True
	}

    $result = (Add-WindowsFeature Web-Server -restart).ExitCode

    if ($result -eq 0) {
        logSuccess "Done with enabling web server on VM."
        return $True
    } else {
        logErr "Enabling web server on VM failed."
        return $False
    }
}

logStatus "Start with enabling web server on VM"
logStatus "Adding firewall exception for web server port"
&netsh advfirewall firewall add rule name="Web server (TCP-In)" dir=in action=allow service=any enable=yes profile=any localport=5984 protocol=tcp
logStatus "Enabling web server on VM."
Enable-Web-Server
logStatus "Done with remote setup"
