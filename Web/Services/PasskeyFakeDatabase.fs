namespace Web.Services

open System
open System.Collections.Concurrent
open System.Threading.Tasks
open Fido2NetLib
open Fido2NetLib.Objects

type StoredCredential = {
    Id: byte array
    PublicKey: byte array
    SignCount: uint
    Transports: AuthenticatorTransport array
    IsBackupEligible: bool
    IsBackedUp: bool
    AttestationObject: byte array
    AttestationClientDataJson: byte array
    mutable DevicePublicKeys: byte array list
    UserId: byte array
    UserHandle: byte array
    AttestationFormat: string
    RegDate: DateTimeOffset
    AaGuid: Guid
} with

    member this.Descriptor =
        PublicKeyCredentialDescriptor(PublicKeyCredentialType.PublicKey, this.Id, this.Transports)

    member this.AddDevicePublicKey key =
        this.DevicePublicKeys <- key :: this.DevicePublicKeys

type PasskeyFakeDatabase() =
    let storedUsers = ConcurrentDictionary<string, Fido2User>()
    let mutable storedCredentials: StoredCredential list = []

    member this.GetOrAddUser(username: string, addCallback: unit -> Fido2User) : Fido2User =
        storedUsers.GetOrAdd(username, addCallback ())

    member this.GetUser(username: string) : Fido2User option =
        let success, user = storedUsers.TryGetValue(username)

        match success with
        | true -> Some user
        | false -> FSharp.Core.Option.None

    member this.GetCredentialsByUser(user: Fido2User) : StoredCredential list =
        storedCredentials
        |> List.filter (_.UserId.AsSpan().SequenceEqual(user.Id))

    member this.GetCredentialById(id: byte array) : StoredCredential option =
        storedCredentials
        |> List.tryFind (_.Descriptor.Id.AsSpan().SequenceEqual(id))

    member this.GetCredentialsByUserHandleAsync(userHandle: byte array) : Task<StoredCredential list> =
        task {
            return
                storedCredentials
                |> List.filter (_.UserHandle.AsSpan().SequenceEqual(userHandle))
        }

    member this.UpdateCounter(credentialId: byte array, counter: uint) : unit =
        let credentialIndex =
            storedCredentials
            |> List.findIndex (_.Descriptor.Id.AsSpan().SequenceEqual(credentialId))

        let credential = {
            storedCredentials[credentialIndex] with
                SignCount = counter
        }

        let updatedCredentials =
            storedCredentials
            |> List.updateAt credentialIndex credential

        storedCredentials <- updatedCredentials

    member this.AddCredentialToUser(user: Fido2User, credential: StoredCredential) : unit =
        let credential = { credential with UserId = user.Id }
        storedCredentials <- credential :: storedCredentials

    member this.GetUsersByCredentialIdAsync(credentialId: byte array) : Task<Fido2User list> =
        task {
            let credential =
                storedCredentials
                |> List.tryFind (_.Descriptor.Id.AsSpan().SequenceEqual(credentialId))

            match credential with
            | None -> return []
            | Some c ->
                return
                    storedUsers
                    |> Seq.filter (_.Value.Id.AsSpan().SequenceEqual(c.UserId))
                    |> Seq.map (_.Value)
                    |> Seq.toList
        }
