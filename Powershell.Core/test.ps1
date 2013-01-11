import-module .\ClrPlus.Powershell.Core.dll
import-module .\ClrPlus.Powershell.Provider.dll
#import-module .\ClrPlus.Powershell.Azure.dll

# add-restcmdlet -command show-test -publishas test
# add-restcmdlet -command show-test -ForcedParameter P1:garrett -DefaultParameter P2:serack,P3:tea,P3:indyanna

# start-restservices

set-RestService -config .\test.props
start-restservice