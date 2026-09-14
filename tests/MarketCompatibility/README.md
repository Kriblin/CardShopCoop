# Market compatibility regression harness

```sh
dotnet run --project tests/MarketCompatibility/MarketCompatibility.csproj -c Release
```

Links the production card-market capture/apply/checksum logic and row DTO, with minimal
stand-ins for the game's price rows, enums, and Unity math. Checks Ascension classification,
wire rounding, authoritative zero prices, short/null-row repair, list/row alias preservation,
daily changes, repeated snapshots, reconnect repair, and preservation of existing history.

The metadata audit in `tests/GameCompatibility` separately checks that all native market
tables actually participate in the full snapshot and apply paths. Neither harness proves
network delivery or two-player behavior; those checks remain in `TODO.md`.
