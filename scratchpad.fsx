#r @"C:\Users\Isaac\Source\Repos\FSharp.Azure.ArmTypeProvider\src\FSharp.Azure.ArmTypeProvider\bin\Debug\Newtonsoft.Json.dll"
#r @"C:\Users\Isaac\Source\Repos\FSharp.Azure.ArmTypeProvider\src\FSharp.Azure.ArmTypeProvider\bin\Debug\FSharp.Azure.ArmTypeProvider.dll"

open FSharp.Azure.ArmTypeProvider

type Arm = ArmProvider< @"C:\Users\Isaac\Source\Repos\FSharp.Azure.ArmTypeProvider\data\templateListing.json">

let x = Arm.Templates.``101-security-group-create``.Deploy()
