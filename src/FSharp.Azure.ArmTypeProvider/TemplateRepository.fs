module TemplateRepository

open Newtonsoft.Json
open ProviderImplementation.ProvidedTypes
open System.Reflection
open System.Net
open System

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

let private buildTemplateType template =
    printfn "Creating template %s" template.name
    let buildReadme name =
        try
            use wc = new WebClient()
            let readme = wc.DownloadString(sprintf "https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/%s/README.md" name)
            let property = ProvidedProperty("Readme", typeof<string>, GetterCode = fun _ -> <@@ readme @@>)
            property.AddXmlDoc readme
            Some property
        with _ -> None
    let buildDeploy name =
        let fromType = function
            | AzureDeployParser.ArmParameterType.String -> typeof<string>
            | AzureDeployParser.ArmParameterType.Int -> typeof<int>
            | AzureDeployParser.ArmParameterType.Bool -> typeof<bool>
        use wc = new WebClient()
        let deployJson = wc.DownloadString(sprintf "https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/%s/azuredeploy.json" name)
        let parameters =
            let mandatory, optional =
                AzureDeployParser.getParameters deployJson
                |> List.map(fun p ->
                    match p.DefaultValue with
                    | Some defaultValue -> false, ProvidedParameter(p.Name, fromType p.Type, false, defaultValue)
                    | None -> true, ProvidedParameter(p.Name, fromType p.Type, false))
                |> List.partition fst
            List.map snd (mandatory @ optional)
        ProvidedMethod("Deploy", parameters, typeof<obj>, InvokeCode = fun _ -> <@@ () @@>)

    let typeName = template.name.ToCharArray() |> Array.filter Char.IsLetter |> String
    let templateType = ProvidedTypeDefinition(typeName, None, HideObjectMethods = true)
    templateType.AddMembersDelayed(fun _ ->
        printfn "Building properties for %s" template.name
        List.choose id [
            buildReadme template.name |> Option.map (fun x -> x :> MemberInfo)
            buildDeploy template.name :> MemberInfo |> Some ])
    templateType, ProvidedProperty(template.name, templateType, GetterCode = fun _ -> <@@ () @@>)

let private createTemplatesContainer() =
    let templatesType = ProvidedTypeDefinition("Templates", None, HideObjectMethods = true)
    let templatesProp = ProvidedProperty("Templates", templatesType, GetterCode = (fun _ -> <@@ () @@>), IsStatic = true)
    templatesProp.AddXmlDoc "A collection of ready-made templates to use."
    templatesType, templatesProp

let processData data =
    let templatesType, templatesProp = createTemplatesContainer()
    let templates = data |> parseJson |> List.take 50 |> List.map buildTemplateType
    printfn "Created all templates"
    templates |> List.iter (snd >> templatesType.AddMember)
    templatesProp :> MemberInfo, templatesType :: (List.map fst templates)