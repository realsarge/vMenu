# Code6 identity adapter checks

Run `dotnet run --project tests/IdentityAdapter/IdentityAdapter.csproj -c Release`.
The harness compiles the actual NameSyncService, HiddenPlayerConfig and
NameMapClient against mocked Cfx events, exports and connected players. It does
not call a forum endpoint or launch FiveM. Build production with
`dotnet build vMenu.sln -c Release` and copy only the two rebuilt vMenu DLLs to
the game repository. FiveM/Scaleform rendering requires owner activation and
in-game acceptance; these checks do not prove runtime interoperability.

The adapter uses code6_selfmanage:GetIdentity. Successful provider refreshes
emit the server-only code6_selfmanage:identitiesUpdated event. The 60-second
loop reads local exports for recovery and terminal metadata only. Existing
Steam/full-info exports now cover connected players, including last-known
identities; no offline account directory is fetched or exposed.
