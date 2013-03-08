
$credentials = Get-Credential
$container = Get-UploadLocation -Remote -ServiceUrl http://localhost:888 -Credential $credentials
$cred = Get-AzureCredentials -Remote -ServiceUrl http://localhost:888 -Credential $credentials -ContainerName $container[0]
new-psdrive -name temp -psprovider azure -root $container[1] -credential $cred
cd temp:
Copy-ItemEx -Path "C:\Users\Eric\Desktop\fold\Trident-x86.msi" -Destination .