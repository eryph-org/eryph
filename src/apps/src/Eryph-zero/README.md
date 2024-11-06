## Installation

> 🔔 **Security Notice!**   
Please inspect https://raw.githubusercontent.com/eryph-org/eryph/main/src/apps/src/Eryph-zero/install.ps1 before running any of these scripts to ensure safety. We already know it's safe, but you should check the security and content of any script from the Internet that you're not familiar with. All of these scripts download a remote PowerShell script and run it on your computer.

### Enable Hyper-V

Before running the script below, you should enable Hyper-V to avoid running the script twice, as it will automatically enable Hyper-V if it is not installed, which requires a reboot. 
Follow these instructions to enable Hyper-V on your machine: 
https://learn.microsoft.com/en-us/virtualization/hyper-v-on-windows/quick-start/enable-hyper-v 

### Invitation code
Beta downloads require an invitation code. If you have not yet received an invitation code, please sign up for the eryph-zero waitlist at https://www.eryph.io.

### Install with Command Prompt (cmd.exe)

Run the following command:

``` cmd
@"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -InputFormat None -ExecutionPolicy Bypass -Command "[System.Net.ServicePointManager]::SecurityProtocol = 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://raw.githubusercontent.com/eryph-org/eryph/main/src/apps/src/Eryph-zero/install.ps1'))"
```

### Install with PowerShell

Run the following command:

``` ps
Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://raw.githubusercontent.com/eryph-org/eryph/main/src/apps/src/Eryph-zero/install.ps1'))
```

### Install with Options
To provide additional options the installation you have to use powershell.

``` ps
iex "& { $(irm https://raw.githubusercontent.com/eryph-org/eryph/main/src/apps/src/Eryph-zero/install.ps1) } [YOUR OPTIONS] "
```
Replace [YOUR OPTIONS] with one parameter of this script: https://github.com/eryph-org/eryph/blob/main/src/apps/src/Eryph-zero/install.ps1  
Common options are:
- Force:  Overwrites existing installation
- Email: email for invitation code
- InvitationCode: your beta invitation code
- Version: a specific version to install. A list of versions available can be found here: https://releases.dbosoft.eu/eryph/zero

## Getting started

Once the installation is complete, you are ready to breed your first catlets.  
Have a look at our tutorial to get started: https://github.com/eryph-org/samples/tree/main/tutorial



