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
    let armProvidedType = ProvidedTypeDefinition(thisAssembly, namespaceName, "ArmTypeProvider", baseType = Some typeof<obj>)

    let buildTypes (typeName : string) (args : obj []) =
        // Create the top level property
        let typeProviderForAccount = ProvidedTypeDefinition(thisAssembly, namespaceName, typeName, baseType = Some typeof<obj>)
        typeProviderForAccount.AddMember(ProvidedConstructor(parameters = [], InvokeCode = (fun _ -> <@@ null @@>)))

        let staticProp, otherTypes =
            let path = args.[0] :?> string
            path
            |> File.ReadAllText
            |> TemplateRepository.processData

        typeProviderForAccount.AddMember staticProp

        let generatedTypes = ProvidedTypeDefinition("GeneratedTypes", Some typeof<obj>)
        typeProviderForAccount.AddMember generatedTypes
        otherTypes |> generatedTypes.AddMembers
        
        typeProviderForAccount
    
    // Parameterising the provider
    let parameters =
        [ ProvidedStaticParameter("jsonPath", typeof<string>, String.Empty)
        //   ProvidedStaticParameter("accountName", typeof<string>, String.Empty)
        //   ProvidedStaticParameter("accountKey", typeof<string>, String.Empty)
        //   ProvidedStaticParameter("connectionStringName", typeof<string>, String.Empty)
        //   ProvidedStaticParameter("configFileName", typeof<string>, "app.config")
        ]
    
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
