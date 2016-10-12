module TemplateRepository

open Newtonsoft.Json
open ProviderImplementation.ProvidedTypes
open System.Reflection
open System.Net
open System
open System.Text
open AzureDeployParser

type GitHubEntry =
    { name : string
      ``type`` : string }

let parseJson data =
    let filterRules =
        let hasName name row = name = row.name
        let isGitHub = hasName ".github"
        let isContribution = hasName "1-CONTRIBUTION-GUIDE"
        let isBlank = hasName "100-blank-template"
        let isDir row = row.``type`` = "dir"
        [ isDir; not << isGitHub; not << isContribution; not << isBlank ]
        |> List.reduce(fun a b data -> a data && b data)
    
    JsonConvert.DeserializeObject<GitHubEntry[]>(data)
    |> Seq.filter filterRules
    |> Seq.toList

let private buildTemplateType (templateName:string) =
    let readmeProp() =
        try
            use wc = new WebClient()
            let readme = wc.DownloadString(sprintf "https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/%s/README.md" templateName)
            let property = ProvidedProperty("Readme", typeof<string>, GetterCode = (fun args -> <@@ readme @@>))
            property.AddXmlDoc readme
            Some property
        with _ -> None

    let deployMethod() =
        let buildTypedParameter (parameterName:string) values =
            let parameterName =
                let chars = parameterName.ToCharArray()
                chars.[0] <- Char.ToUpper chars.[0]
                String chars
            let parameterType = ProvidedTypeDefinition(parameterName, None, HideObjectMethods = true)
            for value in values do
                parameterType.AddMember(ProvidedProperty(value, parameterType, IsStatic = true, GetterCode = (fun _ -> <@@ () @@>)))
            parameterType

        let fromType = function
            | { ParameterKind = (Some (AllowedValues values)) } as parameter ->
                buildTypedParameter parameter.Name values :> Type
            | { ParameterType = StringParam } -> typeof<string>
            | { ParameterType = IntParam } -> typeof<int>
            | { ParameterType = BoolParam } -> typeof<bool>
            | { ParameterType = ArrayParam } -> typeof<string array>

        use wc = new WebClient()
        let deployJson = wc.DownloadString(sprintf "https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/%s/azuredeploy.json" templateName)
        let parameters =
            let mandatory, optional =
                AzureDeployParser.getParameters deployJson
                |> List.map(fun p ->
                    let parameterType = fromType p
                    match p.DefaultValue with
                    | Some defaultValue -> false, (p.Description, ProvidedParameter(p.Name, parameterType, optionalValue = defaultValue))
                    | None -> true, (p.Description, ProvidedParameter(p.Name, parameterType)))
                |> List.partition fst
            List.map snd (mandatory @ optional)

        let generatedParameterTypes =
            parameters
            |> List.choose(fun (_, p) ->
                match p.ParameterType with
                | :? ProvidedTypeDefinition as ptd -> Some ptd
                | _ -> None)

        let providedMethod = ProvidedMethod("Deploy", parameters |> List.map snd, typeof<obj>, InvokeCode = (fun args -> <@@ 10 @@>))
        providedMethod.AddXmlDocDelayed <| fun _ ->
            let sb = StringBuilder()
            sb.AppendLine("Deploys the template to the specified subscription.") |> ignore
            for (description, parameter) in parameters do
                match description with
                | Some description -> sb.AppendLine(sprintf "%s: %s" parameter.Name description) |> ignore
                | _ -> ()
            sb.ToString()
        
        providedMethod, generatedParameterTypes

    let typeName = templateName
    let templateType = ProvidedTypeDefinition(typeName, None, HideObjectMethods = false)
    templateType.AddMembersDelayed(fun _ ->
        let deployMethod, customParameterTypes = deployMethod()
        let readmeProp = readmeProp() |> Option.map (fun x -> x :> MemberInfo) |> Option.toList
        deployMethod :> MemberInfo :: (customParameterTypes |> List.map (fun x -> x :> MemberInfo)) @ readmeProp)

    templateType, ProvidedProperty(templateName, templateType, GetterCode = fun _ -> <@@ () @@>)

let private createTemplatesContainer() =
    let templatesType = ProvidedTypeDefinition("Templates", None, HideObjectMethods = true)
    let templatesProp = ProvidedProperty("Templates", templatesType, GetterCode = (fun _ -> <@@ () @@>), IsStatic = true)
    templatesProp.AddXmlDoc "A collection of ready-made templates to use."
    templatesType, templatesProp

let processData data =
    let templatesType, templatesProp = createTemplatesContainer()
    let templates = data |> parseJson |> List.map (fun x -> buildTemplateType x.name)
    templates |> List.iter (snd >> templatesType.AddMember)
    templatesProp :> MemberInfo, templatesType :: (List.map fst templates)