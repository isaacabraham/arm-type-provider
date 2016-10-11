namespace ProviderImplementation

open Microsoft.FSharp.Core.CompilerServices
open ProviderImplementation.ProvidedTypes
open System
open System.Collections.Generic
open System.Reflection
open System.IO

[<TypeProvider>]
/// [omit]
type public ArmTypeProvider(config : TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()

    let namespaceName = "FSharp.Azure.ArmTypeProvider"
    let thisAssembly = Assembly.GetExecutingAssembly()
    let armProvidedType = ProvidedTypeDefinition(thisAssembly, namespaceName, "ArmProvider", baseType = Some typeof<obj>)

    let buildTypes (typeName : string) (args : obj []) =
        // Create the top level property
        let typeProviderForAccount = ProvidedTypeDefinition(thisAssembly, namespaceName, typeName, baseType = Some typeof<obj>)
        typeProviderForAccount.AddMember(ProvidedConstructor(parameters = [], InvokeCode = (fun _ -> <@@ null @@>)))

        let staticProp, otherTypes =
            let path = args.[0] :?> string
            printfn "Reading file %s" path
            path
            |> File.ReadAllText
            |> TemplateRepository.processData

        typeProviderForAccount.AddMember staticProp

        let generatedTypes = ProvidedTypeDefinition("GeneratedTypes", Some typeof<obj>)
        generatedTypes.AddMembers otherTypes
        typeProviderForAccount.AddMember generatedTypes
        
        typeProviderForAccount
    
    let parameters =
        [ ProvidedStaticParameter("jsonPath", typeof<string>, String.Empty) ]
    
    let memoize func =
        let cache = Dictionary()
        fun argsAsString args ->
            if not (cache.ContainsKey argsAsString) then
                cache.Add(argsAsString, func argsAsString args)
            cache.[argsAsString]

    do
        armProvidedType.DefineStaticParameters(parameters, memoize buildTypes)
        this.AddNamespace(namespaceName, [ armProvidedType ])
        armProvidedType.AddXmlDoc("The entry type to use Azure ARM resources.")

[<TypeProviderAssembly>]
do ()
