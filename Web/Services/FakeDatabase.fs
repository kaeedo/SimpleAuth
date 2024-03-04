namespace Web.Services

open System
open System.Collections.Generic

type FakeDatabase() =
    let dict = Dictionary<Guid, string>()

    member _.CreateUser(userId: Guid, username: string) = dict.TryAdd(userId, username)

    member _.GetUser(userId: Guid) = dict[userId]
