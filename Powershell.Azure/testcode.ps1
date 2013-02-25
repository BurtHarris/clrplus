$secString = ConvertTo-SecureString "6lNxAESWewQ32GAVsCEDkah+wJzy4TrhJdfyCkVhrekT/ulZR2ecjwt69DIIzyIXXf7wiZqo0IaHFPK1gNu31g==" -AsPlainText -Force
$storageCred = New-Object System.Management.Automation.PSCredential ("coapp", $secString)

$container = Get-UploadLocation -AzureStorageCredential $storageCred
$cred = Get-AzureCredentials -AzureStorageCredential $storageCred -ContainerName $container[0]
new-psdrive -name temp -psprovider azure -root $container[1] -credential $cred
cd temp:
Copy-ItemEx -Path "C:\Users\Eric\Desktop\fold\Trident-x86.msi" -Destination .