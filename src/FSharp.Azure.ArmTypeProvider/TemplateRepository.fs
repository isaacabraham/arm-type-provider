module TemplateRepository

open Newtonsoft.Json
open ProviderImplementation.ProvidedTypes
open System.Reflection
open System.Net
open System
open System.Text

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

let private buildTemplateType templateName =
    printfn "Creating template %s" templateName
    let buildReadme name =
        try
            use wc = new WebClient()
            let readme = wc.DownloadString(sprintf "https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/%s/README.md" name)
            let property = ProvidedProperty("Readme", typeof<string>, GetterCode = (fun args -> <@@ readme @@>))
            property.AddXmlDoc readme
            Some property
        with _ -> None
    let buildDeploy name =
        let fromType = function
            | AzureDeployParser.String -> typeof<string>
            | AzureDeployParser.Int -> typeof<int>
            | AzureDeployParser.Bool -> typeof<bool>
            | AzureDeployParser.Array -> typeof<string array>

        use wc = new WebClient()
        let deployJson = wc.DownloadString(sprintf "https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/%s/azuredeploy.json" name)
        let parameters =
            let mandatory, optional =
                AzureDeployParser.getParameters deployJson
                |> List.map(fun p ->
                    match p.DefaultValue with
                    | Some defaultValue -> false, (p.Description, ProvidedParameter(p.Name, fromType p.Type, optionalValue = defaultValue))
                    | None -> true, (p.Description, ProvidedParameter(p.Name, fromType p.Type)))
                |> List.partition fst
            List.map snd (mandatory @ optional)

        let output = ProvidedMethod("Deploy", parameters |> List.map snd, typeof<obj>, InvokeCode = (fun args -> <@@ 10 @@>))
        output.AddXmlDocDelayed <| fun _ ->
            let sb = StringBuilder()
            sb.Append("Deploys the template to the specified subscription.\r\n\r\n") |> ignore
            for (description, parameter) in parameters do
                match description with
                | Some description ->
                    sb.Append(sprintf "%s: %s\r\n" parameter.Name description) |> ignore
                | _ -> ()
            sb.ToString()
        output

    let typeName = templateName.ToCharArray() |> Array.filter Char.IsLetter |> String
    let templateType = ProvidedTypeDefinition(typeName, None, HideObjectMethods = false)
    templateType.AddMembersDelayed(fun _ ->
        printfn "Building properties for %s" templateName
        List.choose id [
            buildReadme templateName |> Option.map (fun x -> x :> MemberInfo)
            buildDeploy templateName :> MemberInfo |> Some ])
    templateType, ProvidedProperty(templateName, templateType, GetterCode = fun _ -> <@@ () @@>)

let private createTemplatesContainer() =
    let templatesType = ProvidedTypeDefinition("Templates", None, HideObjectMethods = true)
    let templatesProp = ProvidedProperty("Templates", templatesType, GetterCode = (fun _ -> <@@ () @@>), IsStatic = true)
    templatesProp.AddXmlDoc "A collection of ready-made templates to use."
    templatesType, templatesProp

let processData data =
    let templatesType, templatesProp = createTemplatesContainer()
    let templates = data |> parseJson |> List.map (fun x -> buildTemplateType x.name)
    //let templates = [ "101-app-service-certificate-standard" ] |> List.map buildTemplateType
    printfn "Created all templates"
    templates |> List.iter (snd >> templatesType.AddMember)
    templatesProp :> MemberInfo, templatesType :: (List.map fst templates)