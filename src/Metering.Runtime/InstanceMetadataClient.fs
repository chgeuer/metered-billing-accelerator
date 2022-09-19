﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Metering.Integration

open System
open System.Collections.Generic
open System.Threading.Tasks
open System.Net.Http
open System.Net.Http.Json
open System.Web
open Microsoft.FSharp.Control

module InstanceMetadataClient = 
    type TokenResponse = { access_token: string }
    
    // When running in managed app, use the managed identity
    // When running in SaaS, use service principal

    let clientWithMetadataTrue endpoint =
        let client = new HttpClient(BaseAddress = new Uri(endpoint))
        client.DefaultRequestHeaders.Add("Metadata", "true")
        client

    let private get_access_token : (MeteringAPICredentials -> string -> Task<string>) =
        let getMSIConfigFromEnvironment :  (string * (string * string) option * string) =
            let env s = 
                match s |> Environment.GetEnvironmentVariable with
                | v when String.IsNullOrWhiteSpace(v) -> None
                | v -> Some v

            // https://docs.microsoft.com/en-us/azure/app-service/overview-managed-identity?tabs=dotnet#using-the-rest-protocol
            match (env "IDENTITY_ENDPOINT", env "IDENTITY_HEADER", env "MSI_ENDPOINT", env "MSI_SECRET") with
            | (None, None, Some msiEndpoint, Some msiSecret) -> (msiEndpoint, Some ("secret", msiSecret), "2017-09-01")
            | (Some identityEndpoint, Some identityHeader, _, _) -> (identityEndpoint, Some ("X-IDENTITY-HEADER", identityHeader), "2019-08-01")
            | _ -> ("http://169.254.169.254/metadata/identity/oauth2/token", None, "2019-08-01")
            
        let access_token_retriever (cred: MeteringAPICredentials) (resource: string) : Task<string> =
            task { 
                match cred with
                | ManagedIdentity ->
                    let (endpoint, headerAndSecret, apiVersion) = getMSIConfigFromEnvironment
                    use tokenClient = clientWithMetadataTrue endpoint
                    match headerAndSecret with
                    | Some (h, v) -> tokenClient.DefaultRequestHeaders.Add(h, v) 
                    | None -> ()

                    let query = $"?resource={resource |> HttpUtility.UrlEncode}&api-version={apiVersion}"
                    let! { access_token = access_token } =
                        tokenClient.GetFromJsonAsync<TokenResponse>(query)
                        
                    //let client = new HttpClient()
                    //client.DefaultRequestHeaders.Add("Authorization", $"Bearer {access_token}")

                    return access_token
                | ServicePrincipalCredential spc ->
                    let uri = 
                        $"https://login.microsoftonline.com/{spc.TenantId}/oauth2/token"

                    let content = 
                        [ "grant_type", "client_credentials"
                          "client_id", spc.ClientId
                          "client_secret", spc.ClientSecret 
                          "resource", resource |> HttpUtility.UrlEncode ]
                        |> List.map (fun (x,y) -> new KeyValuePair<string,string>(x,y))
                        |> (fun x -> new FormUrlEncodedContent(x))
                              
                    let request = new HttpRequestMessage(
                        method = HttpMethod.Post,
                        requestUri = uri,
                        Content = content)
                        
                    let tokenClient = new HttpClient()
                    let! response = tokenClient.SendAsync(request)

                    let! { access_token = access_token } =
                        response.Content.ReadFromJsonAsync<TokenResponse>()

                    return access_token

                    //let! response = tokenClient.GetStringAsync(query)
                    //return 
                    //    response
                    //    |> Decode.fromString (Decode.object (fun get -> get.Required.Field "access_token" Decode.string))
                    //    |> function 
                    //        | Ok access_token -> access_token
                    //        | Error _ -> failwith "Could not find access_token"
            }
                
        access_token_retriever
 
    let private create cred resource uri =
        task {
            let! access_token = get_access_token cred resource
            let client = new HttpClient(BaseAddress = new Uri(uri))
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {access_token}")
            return client
        }

    let createMarketplaceClient cred =
        create
            cred
            "20e940b3-4c77-4b0b-9a53-9e16a1b010a7" // resource according to https://docs.microsoft.com/en-us/azure/marketplace/partner-center-portal/pc-saas-registration#get-the-token-with-an-http-post
            "https://saasapi.azure.com" // "https://marketplaceapi.microsoft.com"
        
    //let createArmClient () =
    //    create
    //        ManagedIdentity
    //        "https://management.azure.com/" // resource
    //        "https://management.azure.com/" // uri
    //
    // //Retrieves the resource group's ResourceGroup.ManagedBy property from Azure Resource Manager.
    //let retrieveManagedByFromARM (currentId: MarketplaceResourceId) : Task<MarketplaceResourceId> =
    //    Task.FromResult(currentId)
    //    match currentId with
    //    | SaaSSubscription _ -> currentId |> Task.FromResult
    //    | ManagedApplication m ->
    //        match m with
    //        | ManagedAppResourceGroupID _ -> currentId |> Task.FromResult
    //        | ManagedAppIdentity ->
    //            let RecourceGroupIdDecoder : Decoder<string>  =
    //                Decode.object (fun get -> 
    //                    // tap into JSON path './compute/{x}'
    //                    let get x = get.Required.At [ "compute"; x ] Decode.string
    //    
    //                    $"""/subscriptions/{get "subscriptionId"}/resourceGroups/{get "resourceGroupName"}"""
    //                )
    //    
    //            let ManagedByDecoder : Decoder<string> =
    //                Decode.object (fun get -> get.Required.At [ "managedBy" ] Decode.string )
    //    
    //            task {
    //                // Determine the resource ID we're running in, using the instance metadata endpoint
    //                let c = clientWithMetadataTrue "http://169.254.169.254/"
    //                let! imdsJson = c.GetStringAsync "metadata/instance?api-version=2021-02-01" // TODO do we need &format=json as well ?
    //                let resourceGroupId = 
    //                    match Decode.fromString RecourceGroupIdDecoder imdsJson with
    //                    | Ok x -> x
    //                    | Error e -> failwith e
    //    
    //                let! armClient = createArmClient()
    //                let! armResponse = armClient.GetStringAsync $"{resourceGroupId}?api-version=2019-11-01"  // or 2019-07-01?
    //                let managedBy = 
    //                    match Decode.fromString ManagedByDecoder armResponse with
    //                    | Ok x -> x
    //                    | Error e -> failwith e
    //    
    //                return managedBy |> ManagedAppResourceGroupID |> ManagedApplication
    //            }
    //
    //let retrieveDummyID (dummyValue: string) : Task<MarketplaceResourceId> =
    //    match Guid.TryParse(dummyValue) with
    //    | (true, _) -> ResourceURI dummyValue
    //    | (false, _) -> ResourceID dummyValue
    //    |> Task.FromResult
