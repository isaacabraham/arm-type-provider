module TemplateRepository

open Newtonsoft.Json
open ProviderImplementation.ProvidedTypes
open System.Reflection
open System.Net

type GitHubTemplate =
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
    JsonConvert.DeserializeObject<GitHubTemplate[]>(data)
    |> Array.filter filterRules

type ArmTemplate() =
    member __.Deploy() = ()

let private buildTemplateProperties (allTemplates:array<_>) =
    let templates = ProvidedTypeDefinition("Templates", Some typeof<obj>, HideObjectMethods = true)

    let templateCount = allTemplates.Length
    let templatesProp = ProvidedProperty("Templates", templates, GetterCode = (fun _ -> <@@ templateCount @@>), IsStatic = true)
    templatesProp.AddXmlDoc "Collection of ready-made templates to use."

    let templateTypes =
        let buildMember name () =
            try
                printfn "Getting readme for %s" name
                use wc = new WebClient()
                let readme = wc.DownloadString(sprintf "https://raw.githubusercontent.com/Azure/azure-quickstart-templates/master/%s/README.md" name)
                let property = ProvidedProperty("Readme", typeof<string>, GetterCode = fun _ -> <@@ readme @@>)
                property.AddXmlDoc readme
                Some property
            with _ -> None

        [ for template in allTemplates ->
            let templateType = ProvidedTypeDefinition("template" + template.name, Some (typeof<ArmTemplate>))
            templateType.AddMembersDelayed(buildMember template.name >> Option.toList)
            let prop = ProvidedProperty(template.name, templateType, GetterCode = fun _ -> <@@ ArmTemplate() @@>)
            templates.AddMember prop
            templateType :> MemberInfo ]

    templatesProp :> MemberInfo, [ templates :> MemberInfo ] @ templateTypes

let processData = getData >> buildTemplateProperties