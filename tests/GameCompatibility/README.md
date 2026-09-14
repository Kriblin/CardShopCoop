# Game compatibility metadata audit

This harness requires .NET SDK 10 and a local game installation configured through the
repository's usual `GamePath` settings. It uses Roslyn from that SDK and Mono.Cecil from
BepInEx. Game code is inspected as metadata and IL; it is never executed or distributed.

Build the plugin first, then run from the repository root:

```sh
dotnet build src/CardShopCoop/CardShopCoop.csproj -c Release
dotnet run --project tests/GameCompatibility/GameCompatibility.csproj -c Release -- . "/path/to/TCG Card Shop Simulator"
```

Append `--details` to list dynamic references requiring manual review. The tool exits
nonzero for missing or ambiguous literal members, incompatible Harmony hook arguments,
or failed native save / market integration assertions.

Coverage:

- Literal `Try` patch registrations and typed reflection lookups, including inherited
  members; provided type signatures; hook argument names/types and instance/result types.
- Native save fields and the customer tournament appearance restoration path.
- Every `CPlayerData.m_GenCardMarketPriceList*` table in the plugin's snapshot DTO,
  capture, apply, and diagnostic checksum paths. New native tables fail this check
  until support is added. Ascension's price-generation guard is also checked.
- The installed game assembly MVID and built plugin version are printed to identify
  precisely what was inspected.

Limits: this is a source-pattern and metadata audit, not a complete C# semantic analyzer
or a live Harmony installation test. It cannot prove runtime call order, dynamically
chosen overloads, Unity behavior, or optional-mod compatibility. For the current M2
review, remaining dynamic references are shared patch/reflection helpers, optional
TV/Grading Overhaul integrations, native save aliases (also asserted explicitly), and
optional object price-tag lookup. Refer to `TODO.md` for pending in-game validation.
