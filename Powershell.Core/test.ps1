import-module .\ClrPlus.Powershell.Core.dll
import-module .\ClrPlus.Powershell.Provider.dll
#import-module .\ClrPlus.Powershell.Azure.dll

add-restcmdlet -command show-test -publishas test
start-restservices