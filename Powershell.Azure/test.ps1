Import-Module ".\ClrPlus.Core.dll"
Import-Module ".\Microsoft.WindowsAzure.Storage.dll"
import-module ".\ClrPlus.Powershell.Core.dll"
import-module ".\ClrPlus.Powershell.Rest.dll"
Import-Module ".\ClrPlus.Powershell.Provider.dll"
Import-Module ".\ClrPlus.Powershell.Azure.dll"

start-restservice -config .\test.props