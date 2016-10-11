#I @"C:\Users\Isaac\Source\Repos\FSharp.Azure.ArmTypeProvider\src\FSharp.Azure.ArmTypeProvider\bin\Debug"
#r @"FSharp.Core"
#r @"Newtonsoft.Json"
#r @"FSharp.Azure.ArmTypeProvider"

open ProviderImplementation.ProvidedTypes
open FSharp.Azure.ArmTypeProvider

type ArmProvider = ArmTypeProvider< @"C:\Users\Isaac\Source\Repos\FSharp.Azure.ArmTypeProvider\data\templateListing.json">

ArmProvider.Templates.
