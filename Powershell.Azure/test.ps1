Import-Module ".\ClrPlus.Core.dll"
Import-Module ".\Microsoft.WindowsAzure.Storage.dll"
Import-Module ".\ClrPlus.Powershell.Azure.dll"

$secString = ConvertTo-SecureString "6lNxAESWewQ32GAVsCEDkah+wJzy4TrhJdfyCkVhrekT/ulZR2ecjwt69DIIzyIXXf7wiZqo0IaHFPK1gNu31g==" -AsPlainText -Force
$storageCred = New-Object System.Management.Automation.PSCredential ("coapp", $secString)

$cred = Get-AzureCredentials -Credential $storageCred
new-psdrive -name temp -psprovider azure -root https://coapp.blob.core.windows.net/container -credential $cred
dir temp:
cd temp:

