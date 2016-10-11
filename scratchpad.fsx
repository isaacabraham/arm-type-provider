#I @"C:\Users\Isaac\Source\Repos\FSharp.Azure.ArmTypeProvider\src\FSharp.Azure.ArmTypeProvider\bin\Debug"
#r @"Newtonsoft.Json"
#r @"FSharp.Azure.ArmTypeProvider"

open FSharp.Azure.ArmTypeProvider
type ArmProvider = ArmTypeProvider< @"C:\Users\Isaac\Source\Repos\FSharp.Azure.ArmTypeProvider\data\templateListing.json">

ArmProvider.Templates.``101-automation-runbook-getvms``.Deploy()

