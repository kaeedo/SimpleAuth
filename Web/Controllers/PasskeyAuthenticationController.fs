namespace Web.Controllers

open System
open System.Text
open System.Threading
open Fido2NetLib
open Fido2NetLib.Objects
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Web.Services

type MakeCredentialOptionsBody = {
    Username: string
    DisplayName: string
    AttType: string
    AuthType: string
    ResidentKey: string
    UserVerification: string
}

type AssertionOptionsBody = {
    Username: string
    UserVerification: string
}

type PasskeyAuthenticationController
    (ctxAccessor: IHttpContextAccessor, fakeDb: PasskeyFakeDatabase, fido2: IFido2, authService: AuthService) =
    inherit Controller()

    [<HttpGet>]
    member this.SignUp() = this.View()

    [<HttpGet>]
    member this.SignIn() = this.View()

    [<HttpPost>]
    member this.MakeCredentialOptions([<FromBody>] options: MakeCredentialOptionsBody) =
        task {
            let (id, user) =
                fakeDb.GetOrAddUser(
                    options.Username,
                    fun () ->
                        Guid.NewGuid(),
                        Fido2User(
                            DisplayName = options.DisplayName,
                            Name = options.Username,
                            Id = Encoding.UTF8.GetBytes(options.Username)
                        )
                )

            let existingKeys =
                fakeDb.GetCredentialsByUser(user)
                |> List.map _.Descriptor

            let authenticatorSelection =
                let attachment =
                    if String.IsNullOrEmpty(options.AuthType) then
                        FSharp.Core.Option.None
                    else
                        Some(options.AuthType.ToEnum<AuthenticatorAttachment>())

                AuthenticatorSelection(
                    ResidentKey = options.ResidentKey.ToEnum<ResidentKeyRequirement>(),
                    UserVerification = options.ResidentKey.ToEnum<UserVerificationRequirement>(),
                    AuthenticatorAttachment = (attachment |> Option.toNullable)
                )

            let extensions =
                AuthenticationExtensionsClientInputs(
                    Extensions = true,
                    UserVerificationMethod = true,
                    DevicePubKey = AuthenticationExtensionsDevicePublicKeyInputs(Attestation = options.AttType),
                    CredProps = true
                )

            let options =
                fido2.RequestNewCredential(
                    user,
                    existingKeys,
                    authenticatorSelection,
                    options.AttType.ToEnum<AttestationConveyancePreference>(),
                    extensions
                )

            ctxAccessor.HttpContext.Session.SetString("fido2.attestationOptions", options.ToJson())

            return this.Json(options)
        }

    [<HttpPost>]
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

            let id, _ =
                fakeDb.GetUser(options.User.DisplayName)
                |> _.Value

            let username = credential.User.DisplayName

            do! authService.SignIn(id, username)

            return this.Json(credential)
        }

    [<HttpPost>]
    member this.AssertionOptions([<FromBody>] assertionOptions: AssertionOptionsBody) =
        task {
            let existingCredentials =
                if String.IsNullOrWhiteSpace(assertionOptions.Username) then
                    []
                else
                    let id, user =
                        fakeDb.GetUser(assertionOptions.Username)
                        |> _.Value

                    fakeDb.GetCredentialsByUser(user)
                    |> List.map (_.Descriptor)

            let extensions =
                AuthenticationExtensionsClientInputs(
                    Extensions = true,
                    UserVerificationMethod = true,
                    DevicePubKey = AuthenticationExtensionsDevicePublicKeyInputs()
                )

            let uv =
                if String.IsNullOrEmpty(assertionOptions.UserVerification) then
                    UserVerificationRequirement.Discouraged
                else
                    assertionOptions.UserVerification.ToEnum<UserVerificationRequirement>()

            let options = fido2.GetAssertionOptions(existingCredentials, uv, extensions)

            ctxAccessor.HttpContext.Session.SetString("fido2.assertionOptions", options.ToJson())

            return this.Json(options)
        }

    [<HttpPost>]
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

            let! id =
                task {
                    let! users = fakeDb.GetUsersByCredentialIdAsync(result.CredentialId)
                    let user = users[0]

                    return user |> fst
                }

            let username = Encoding.Default.GetString(credentials.UserHandle)

            do! authService.SignIn(id, username)

            return this.Json(result)
        }
