#r @"C:\Users\Isaac\Source\Repos\FSharp.Azure.ArmTypeProvider\src\FSharp.Azure.ArmTypeProvider\bin\Debug\Newtonsoft.Json.dll"
#r @"C:\Users\Isaac\Source\Repos\FSharp.Azure.ArmTypeProvider\src\FSharp.Azure.ArmTypeProvider\bin\Debug\FSharp.Azure.ArmTypeProvider.dll"

open FSharp.Azure.ArmTypeProvider
type Arm = ArmProvider< @"C:\Users\Isaac\Source\Repos\FSharp.Azure.ArmTypeProvider\data\templateListing.json">

type Parameters = Arm.GeneratedTypes

let x = Arm.Templates.``101-acs-dcos``.Deploy("dns", "rsa", 0, Parameters.``101-acs-dcos``.MasterCount.``1``)

//Arm.GeneratedTypes.``101-acs-dcos``.