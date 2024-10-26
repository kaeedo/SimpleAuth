namespace Web.Services

open System
open System.Collections.Concurrent
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
    DevicePublicKeys: byte array list
    UserId: byte array
    UserHandle: byte array
    AttestationFormat: string
    RegDate: DateTimeOffset
    AaGuid: Guid
} with

    member this.Descriptor() =
        PublicKeyCredentialDescriptor(PublicKeyCredentialType.PublicKey, this.Id, this.Transports)

type PasskeyFakeDatabase() =
    let storedUsers = ConcurrentDictionary<string, Fido2User>()
    let storedCredentials: StoredCredential list = []

    member this.GetOrAddUser(username: string, addCallback: unit -> Fido2User) : Fido2User =
        storedUsers.GetOrAdd(username, addCallback ())

    member this.GetUser(username: string) : Fido2User option =
        let success, user = storedUsers.TryGetValue(username)

        match success with
        | true -> Some user
        | false -> FSharp.Core.Option.None

(*

    public List<StoredCredential> GetCredentialsByUser(Fido2User user)
    {
        return _storedCredentials.Where(c => c.UserId.AsSpan().SequenceEqual(user.Id)).ToList();
    }

    public StoredCredential? GetCredentialById(byte[] id)
    {
        return _storedCredentials.FirstOrDefault(c => c.Descriptor.Id.AsSpan().SequenceEqual(id));
    }

    public Task<List<StoredCredential>> GetCredentialsByUserHandleAsync(byte[] userHandle, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_storedCredentials.Where(c => c.UserHandle.AsSpan().SequenceEqual(userHandle)).ToList());
    }

    public void UpdateCounter(byte[] credentialId, uint counter)
    {
        var cred = _storedCredentials.First(c => c.Descriptor.Id.AsSpan().SequenceEqual(credentialId));
        cred.SignCount = counter;
    }

    public void AddCredentialToUser(Fido2User user, StoredCredential credential)
    {
        credential.UserId = user.Id;
        _storedCredentials.Add(credential);
    }

    public Task<List<Fido2User>> GetUsersByCredentialIdAsync(byte[] credentialId, CancellationToken cancellationToken = default)
    {
        // our in-mem storage does not allow storing multiple users for a given credentialId. Yours shouldn't either.
        var cred = _storedCredentials.FirstOrDefault(c => c.Descriptor.Id.AsSpan().SequenceEqual(credentialId));

        if (cred is null)
            return Task.FromResult(new List<Fido2User>());

        return Task.FromResult(_storedUsers.Where(u => u.Value.Id.SequenceEqual(cred.UserId)).Select(u => u.Value).ToList());
    }
    *)
