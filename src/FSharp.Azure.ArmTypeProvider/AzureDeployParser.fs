﻿module AzureDeployParser

open Newtonsoft.Json.Linq

let toJProperty (token:JToken) =
    match token with
    | :? JProperty as property -> Some property
    | _ -> None

let safe x = x |> Option.ofObj
let safeS x = x |> safe |> Option.map string
let chain handler value (token:JToken option) = token |> Option.bind(fun v -> handler v.[value])

type ArmParameterKind =
    | Range of (int * int)
    | Minimum of int
    | Maximum of int
    | AllowedValues of string list
type ArmParameterType = | String | Int | Bool
type ArmParameter =
    { Name : string
      Type : ArmParameterType
      DefaultValue : string option
      ArmParameterType : ArmParameterKind option
      Description : string option }

let toArmType (text:string) =
    match text.ToLower() with    
    | "string" -> String
    | "int" -> Int
    | "bool" -> Bool
    | argh -> failwithf "%A" argh

let toParameterKind (parameter:JToken) =
    let minValue = parameter.["minValue"] |> safeS |> Option.map int
    let maxValue = parameter.["maxValue"] |> safeS |> Option.map int
    let allowedValues = parameter.["allowedValues"] |> safe |> Option.map(fun t -> t.Children() |> Seq.toList)

    match minValue, maxValue, allowedValues with
    | Some minValue, Some maxValue, None -> Some(Range(minValue, maxValue))
    | Some minValue, None, None -> Some(Minimum minValue)
    | None, Some maxValue, None -> Some(Maximum maxValue)
    | None, None, Some items ->
        match items |> List.map string with
        | [] -> None
        | items -> Some(AllowedValues items)
    | _ -> None

let buildArmParameter (parameter:JProperty) =
    match parameter.Children() |> Seq.toList with
    | [ node ] ->
        { Name = parameter.Name
          Type = match node.["type"] |> safeS with | Some x -> x |> toArmType | None -> failwith "Argh - no type!"
          DefaultValue = safeS node.["defaultValue"]
          ArmParameterType = node |> toParameterKind
          Description = node.["metadata"] |> safe |> chain safeS "description" }
        |> Some
    | _ -> None

let getParameters (json:string) =
    let deployFile = JObject.Parse json
    deployFile.["parameters"]
    |> Seq.choose (toJProperty >> Option.bind buildArmParameter)
    |> Seq.toList