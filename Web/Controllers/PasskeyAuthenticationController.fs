namespace Web.Controllers

open System
open System.Text
open System.Threading
open Fido2NetLib
open Fido2NetLib.Objects
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Configuration
open Web.Services

type PasskeyAuthenticationController
    (ctxAccessor: HttpContextAccessor, config: IConfiguration, fakeDb: PasskeyFakeDatabase, fido2: IFido2) =
    inherit Controller()

    [<HttpPost>]
    //[Route("/makeCredentialOptions")]
    member this.MakeCredentialOptions
        (
            [<FromForm>] username: string,
            [<FromForm>] displayName: string,
            [<FromForm>] attType: string,
            [<FromForm>] authType: string,
            [<FromForm>] residentKey: string,
            [<FromForm>] userVerification: string
        ) =
        task {
            let user =
                fakeDb.GetOrAddUser(
                    username,
                    fun () ->
                        Fido2User(DisplayName = displayName, Name = username, Id = Encoding.UTF8.GetBytes(username))
                )

            let existingKeys =
                fakeDb.GetCredentialsByUser(user)
                |> List.map _.Descriptor

            let authenticatorSelection =
                let attachment =
                    if String.IsNullOrEmpty(authType) then
                        FSharp.Core.Option.None
                    else
                        Some(authType.ToEnum<AuthenticatorAttachment>())

                AuthenticatorSelection(
                    ResidentKey = residentKey.ToEnum<ResidentKeyRequirement>(),
                    UserVerification = userVerification.ToEnum<UserVerificationRequirement>(),
                    AuthenticatorAttachment = (attachment |> Option.toNullable)
                )

            let extensions =
                AuthenticationExtensionsClientInputs(
                    Extensions = true,
                    UserVerificationMethod = true,
                    DevicePubKey = AuthenticationExtensionsDevicePublicKeyInputs(Attestation = attType),
                    CredProps = true
                )

            let options =
                fido2.RequestNewCredential(
                    user,
                    existingKeys,
                    authenticatorSelection,
                    attType.ToEnum<AttestationConveyancePreference>(),
                    extensions
                )

            ctxAccessor.HttpContext.Session.SetString("fido2.attestationOptions", options.ToJson())

            return this.Json(options)
        }

    [<HttpPost>]
    //[<Route("/makeCredential")>]
    member this.MakeCredential([<FromBody>] attestationResponse: AuthenticatorAttestationRawResponse) =
        task {
            let jsonOptions =
                ctxAccessor.HttpContext.Session.GetString("fido2.attestationOptions")

            let options = CredentialCreateOptions.FromJson(jsonOptions)

            let callback =
                IsCredentialIdUniqueToUserAsyncDelegate
                    (fun
                        (credentialIdUserParams: IsCredentialIdUniqueToUserParams)
                        (cancellationToken: CancellationToken) ->
                        task {
                            let! users = fakeDb.GetUsersByCredentialIdAsync(credentialIdUserParams.CredentialId)

                            return not (users.Length > 0)
                        })

            let! credential = fido2.MakeNewCredentialAsync(attestationResponse, options, callback)
            let credential = credential.Result

            let storedCredential: StoredCredential = {
                Id = credential.Id
                PublicKey = credential.PublicKey
                UserHandle = credential.User.Id
                SignCount = credential.SignCount
                AttestationFormat = credential.AttestationFormat
                RegDate = DateTimeOffset.UtcNow
                AaGuid = credential.AaGuid
                Transports = credential.Transports
                IsBackupEligible = credential.IsBackupEligible
                IsBackedUp = credential.IsBackedUp
                AttestationObject = credential.AttestationObject
                AttestationClientDataJson = credential.AttestationClientDataJson
                DevicePublicKeys = [ credential.DevicePublicKey ]

                UserId = [||]
            }

            fakeDb.AddCredentialToUser(options.User, storedCredential)

            return this.Json(credential)
        }

    [<HttpPost>]
    //  [<Route("/assertionOptions")>]
    member this.AssertionOptionsPost([<FromForm>] username: string, [<FromForm>] userVerification: string) =
        task {
            let existingCredentials =
                if String.IsNullOrWhiteSpace(username) then
                    []
                else
                    let user = fakeDb.GetUser(username)

                    fakeDb.GetCredentialsByUser(user.Value)
                    |> List.map (_.Descriptor)

            let extensions =
                AuthenticationExtensionsClientInputs(
                    Extensions = true,
                    UserVerificationMethod = true,
                    DevicePubKey = AuthenticationExtensionsDevicePublicKeyInputs()
                )

            let uv =
                if String.IsNullOrEmpty(userVerification) then
                    UserVerificationRequirement.Discouraged
                else
                    userVerification.ToEnum<UserVerificationRequirement>()

            let options = fido2.GetAssertionOptions(existingCredentials, uv, extensions)

            ctxAccessor.HttpContext.Session.SetString("fido2.assertionOptions", options.ToJson())

            return this.Json(options)
        }

    [<HttpPost>]
    // [<Route("/makeAssertion")>]
    member this.MakeAssertion([<FromBody>] clientResponse: AuthenticatorAssertionRawResponse) =
        task {
            let jsonOptions =
                ctxAccessor.HttpContext.Session.GetString("fido2.assertionOptions")

            let options = AssertionOptions.FromJson(jsonOptions)

            let credentials = fakeDb.GetCredentialById(clientResponse.Id).Value

            let callback =
                IsUserHandleOwnerOfCredentialIdAsync
                    (fun
                        (ownerOfCredentials: IsUserHandleOwnerOfCredentialIdParams)
                        (cancellationToken: CancellationToken) ->
                        task {
                            let! storedCredentials =
                                fakeDb.GetCredentialsByUserHandleAsync(ownerOfCredentials.UserHandle)

                            return
                                storedCredentials
                                |> List.exists (
                                    _.Descriptor.Id
                                        .AsSpan()
                                        .SequenceEqual(ownerOfCredentials.CredentialId)
                                )
                        })

            let! result =
                fido2.MakeAssertionAsync(
                    clientResponse,
                    options,
                    credentials.PublicKey,
                    credentials.DevicePublicKeys,
                    credentials.SignCount,
                    callback
                )

            fakeDb.UpdateCounter(result.CredentialId, result.SignCount)

            if result.DevicePublicKey <> null then
                credentials.AddDevicePublicKey result.DevicePublicKey

            return this.Json(result)
        }
