module TemplateRepository

open Newtonsoft.Json
open ProviderImplementation.ProvidedTypes
open System.Reflection
open System.Net

type GitHubEntry =
    { name : string
      ``type`` : string }

let private filterRules =
    let hasName name row = name = row.name
    let isGitHub = hasName ".github"
    let isContribution = hasName "1-CONTRIBUTION-GUIDE"
    let isBlank = hasName "100-blank-template"
    let isDir row = row.``type`` = "dir"
    [ isDir; not << isGitHub; not << isContribution; not << isBlank ]
    |> List.reduce(fun a b data -> a data && b data)

let getData data =
    JsonConvert.DeserializeObject<GitHubEntry[]>(data)
    |> Array.filter filterRules

type ArmTemplate() =
    class end

let private buildTemplateProperties (allTemplates:array<_>) =
    let templatesType = ProvidedTypeDefinition("Templates", None, HideObjectMethods = true)
    let templateTypes =
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
                AzureDeployParser.getParameters deployJson
                |> Seq.map(fun p -> ProvidedParameter(p.Name, fromType p.Type, false))
                |> Seq.toList
            ProvidedMethod("Deploy", parameters, typeof<obj>)            

        [ for template in allTemplates ->
            let templateType = ProvidedTypeDefinition(template.name, Some (typeof<ArmTemplate>))
            templateType.AddMembersDelayed(fun _ -> List.choose id [ buildReadme template.name |> Option.map (fun x -> x :> MemberInfo); Some(buildDeploy template.name :> MemberInfo) ])           
            templatesType.AddMember(ProvidedProperty(template.name, templateType, GetterCode = fun _ -> <@@ ArmTemplate() @@>))
            templateType :> MemberInfo ]

    let templatesProp = ProvidedProperty("Templates", templatesType, GetterCode = (fun _ -> <@@ () @@>), IsStatic = true)
    templatesProp.AddXmlDoc "Collection of ready-made templates to use."
    templatesProp :> MemberInfo, [ templatesType :> MemberInfo ] @ templateTypes

let processData = getData >> buildTemplateProperties