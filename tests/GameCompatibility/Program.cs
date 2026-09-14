using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil;

if (args.Length < 2 || args.Length > 3 || (args.Length == 3 && args[2] != "--details"))
    throw new ArgumentException("Usage: <repository root> <game path> [--details]");
string root = Path.GetFullPath(args[0]), game = Path.GetFullPath(args[1]);
var resolver = new DefaultAssemblyResolver();
string managed = Path.Combine(game, "Card Shop Simulator_Data", "Managed");
string bepin = Path.Combine(game, "BepInEx", "core");
resolver.AddSearchDirectory(managed);
resolver.AddSearchDirectory(bepin);
var modules = new List<ModuleDefinition>();
foreach (string path in Directory.GetFiles(managed, "*.dll").Concat(Directory.GetFiles(bepin, "*.dll"))
    .Append(Path.Combine(root, "src/CardShopCoop/bin/Release/CardShopCoop.dll")))
{
    try
    {
        modules.Add(ModuleDefinition.ReadModule(path, new ReaderParameters { AssemblyResolver = resolver }));
    }
    catch (BadImageFormatException) { }
}
var types = modules.SelectMany(m => m.GetTypes()).ToList();
TypeDefinition Find(string name) => types.FirstOrDefault(t => t.FullName == name.Replace("global::", ""))
    ?? types.FirstOrDefault(t => t.Name == name);
IEnumerable<TypeDefinition> Parents(TypeDefinition type)
{
    while (type != null)
    {
        yield return type;
        try
        {
            type = type.BaseType?.Resolve();
        }
        catch (AssemblyResolutionException) { type = null; }
    }
}
string Text(ExpressionSyntax e) => e is LiteralExpressionSyntax l ? l.Token.ValueText
    : e is InvocationExpressionSyntax i && i.Expression.ToString() == "nameof"
    ? i.ArgumentList.Arguments[0].Expression.ToString().Split('.').Last() : null;
string TypeName(ExpressionSyntax e) => e is TypeOfExpressionSyntax t ? t.Type.ToString() : null;
string Location(SyntaxNode node) => Path.GetRelativePath(root, node.SyntaxTree.FilePath) + ":" + (node.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
int checkedMembers = 0, checkedHooks = 0, unresolved = 0, errors = 0, contracts = 0;
void Fail(SyntaxNode node, string text)
{
    Console.WriteLine("FAIL " + Location(node) + " " + text);
    errors++;
}
var aliases = new Dictionary<string, string> { ["bool"] = "System.Boolean", ["int"] = "System.Int32", ["float"] = "System.Single", ["string"] = "System.String", ["byte"] = "System.Byte", ["ushort"] = "System.UInt16", ["long"] = "System.Int64", ["object"] = "System.Object" };
string Canonical(string name) => aliases.TryGetValue(name, out var full) ? full : Find(name)?.FullName ?? name;
foreach (string path in Directory.GetFiles(Path.Combine(root, "src/CardShopCoop"), "*.cs", SearchOption.AllDirectories)
    .Where(p => !p.Contains("/obj/") && !p.Contains("/bin/")))
{
    var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
    foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
    {
        string method = call.Expression.ToString().Split('.').Last();
        bool patch = method == "Try" || method == "TryPatch";
        bool field = method is "Field" or "RequiredField" or "OptionalField" or "GetField";
        bool lookup = method is "Method" or "RequiredMethod" or "OptionalMethod" or "GetMethod";
        if (!patch && !field && !lookup)
            continue;
        var aa = call.ArgumentList.Arguments.Select(a => a.Expression).ToList();
        int typeIndex = aa.FindIndex(a => a is TypeOfExpressionSyntax);
        string typeName = typeIndex >= 0 ? TypeName(aa[typeIndex]) : null;
        string member = typeIndex >= 0 && aa.Count > typeIndex + 1 ? Text(aa[typeIndex + 1]) : null;
        if (call.Expression is MemberAccessExpressionSyntax access && access.Expression is TypeOfExpressionSyntax receiver)
        {
            typeName = receiver.Type.ToString();
            member = aa.Count > 0 ? Text(aa[0]) : null;
        }
        if (typeName == null || member == null)
        {
            unresolved++;
            if (args.Length == 3)
                Console.WriteLine("REVIEW " + Location(call) + " " + call.ToString().Replace("\n", " "));
            continue;
        }
        var type = Find(typeName);
        if (type == null)
        {
            Fail(call, "type not found: " + typeName);
            continue;
        }
        var parents = Parents(type).ToList();
        if (field)
        {
            if (!parents.Any(t => t.Fields.Any(f => f.Name == member)))
                Fail(call, "field not found: " + typeName + "." + member);
            checkedMembers++;
            continue;
        }
        var candidates = parents.SelectMany(t => t.Methods.Where(m => m.Name == member)).ToList();
        // Prefer members declared on the closest type, matching inherited lookup behavior.
        if (candidates.Count > 0)
            candidates = candidates.Where(m => m.DeclaringType == candidates[0].DeclaringType).ToList();
        var signature = aa.Skip(typeIndex + 2)
            .Where(a => a is TypeOfExpressionSyntax or ArrayCreationExpressionSyntax or ImplicitArrayCreationExpressionSyntax)
            .SelectMany(a => a.DescendantNodesAndSelf().OfType<TypeOfExpressionSyntax>())
            .Select(t => Canonical(t.Type.ToString()) + (t.Parent?.ToString().EndsWith("MakeByRefType") == true ? "&" : "")).ToList();
        if (signature.Count > 0)
            candidates = candidates.Where(m => m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(signature)).ToList();
        if (candidates.Count != 1)
        {
            Fail(call, typeName + "." + member + " resolves to " + candidates.Count + " methods");
            continue;
        }
        checkedMembers++;
        if (!patch)
            continue;
        var target = candidates[0];
        foreach (var hookCreation in call.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            .Where(n => n.Type.ToString().EndsWith("HarmonyMethod")))
        {
            var ha = hookCreation.ArgumentList.Arguments;
            if (ha.Count < 2 || TypeName(ha[0].Expression) is not string hookType || Text(ha[1].Expression) is not string hookName)
                continue;
            var hook = Find(hookType)?.Methods.FirstOrDefault(m => m.Name == hookName);
            if (hook == null)
            {
                Fail(call, "hook not found: " + hookType + "." + hookName);
                continue;
            }
            checkedHooks++;
            foreach (var parameter in hook.Parameters)
            {
                string name = parameter.Name;
                if (name.StartsWith("___"))
                {
                    if (!parents.Any(t => t.Fields.Any(f => f.Name == name.Substring(3))))
                        Fail(call, hookName + " injects missing field " + name);
                    continue;
                }
                if (name == "__instance")
                {
                    string instanceType = parameter.ParameterType.FullName.TrimEnd('&');
                    if (target.IsStatic || (instanceType != "System.Object" && !parents.Any(t => t.FullName == instanceType)))
                        Fail(call, hookName + " has incompatible __instance " + instanceType);
                    continue;
                }
                if (name == "__result")
                {
                    string resultType = parameter.ParameterType.FullName.TrimEnd('&');
                    if (resultType != target.ReturnType.FullName && resultType != "System.Object")
                        Fail(call, hookName + " has incompatible __result " + resultType);
                    continue;
                }
                bool indexed = name.StartsWith("__") && int.TryParse(name.Substring(2), out _);
                if (name.StartsWith("__") && !indexed)
                    continue;
                var originalParameter = indexed
                    ? target.Parameters.ElementAtOrDefault(int.Parse(name.Substring(2)))
                    : target.Parameters.FirstOrDefault(p => p.Name == name);
                if (originalParameter == null)
                    Fail(call, hookName + " injects missing parameter " + name + " into " + typeName + "." + member);
                else
                {
                    string actual = originalParameter.ParameterType.FullName.TrimEnd('&');
                    string expected = parameter.ParameterType.FullName.TrimEnd('&');
                    if (expected != actual && expected != "System.Object")
                        Fail(call, hookName + " parameter " + name + ": " + expected + " != " + actual);
                }
            }
        }
    }
}
// Explicit dynamic save aliases and flow checks use metadata only: no game code runs.
void Require(bool condition, string message)
{
    contracts++;
    if (condition)
        return;
    Console.WriteLine("FAIL contract: " + message);
    errors++;
}
MethodDefinition Method(string type, string name) => Find(type)?.Methods.FirstOrDefault(m => m.Name == name);
bool UsesField(MethodDefinition method, string declaringType, string name) => method?.HasBody == true
    && method.Body.Instructions.Any(i => i.Operand is FieldReference f && f.DeclaringType.Name == declaringType && f.Name == name);
bool Calls(MethodDefinition method, string declaringType, string name) => method?.HasBody == true
    && method.Body.Instructions.Any(i => i.Operand is MethodReference m && m.DeclaringType.Name == declaringType && m.Name == name);
foreach (string name in new[] { "m_SaveIndex", "m_SaveCycle" })
    Require(Find("CPlayerData").Fields.Any(f => f.Name == name && f.IsStatic), "CPlayerData save counter " + name);
foreach (string name in new[] { "instance", "m_CurrentDay", "m_PlayerName", "m_SaveIndex" })
    Require(Find("CGameData").Fields.Any(f => f.Name == name), "in-memory save identity " + name);
Require(Find("CSaveLoad").Fields.Any(f => f.Name == "m_SavedGame" && f.FieldType.Name == "CGameData"), "save transfer uses CGameData");
foreach (string name in new[] { "m_CustomerSaveDataList", "m_GenCardMarketPriceListAscension", "m_CardCollectedListAscension", "m_GradedCardPriceSetListAscension" })
{
    Require(UsesField(Method("CGameData", "SaveGameData"), "CPlayerData", name), "native save includes " + name);
    Require(UsesField(Method("CGameData", "PropagateLoadData"), "CPlayerData", name), "native load restores " + name);
}
Require(UsesField(Method("Customer", "GetCustomerSaveData"), "CustomerSaveData", "customerTournamentData"), "customer save includes tournament appearance/state");
Require(Calls(Method("Customer", "LoadCustomerSaveData"), "Customer", "UpdateCharacterModel"), "customer load restores saved tournament model");
Require(Calls(Method("SaveTransfer", "BuildHostPayload"), "CGameManager", "SaveGameData"), "world transfer invokes native save pipeline");
Require(Calls(Method("SaveTransfer", "SerializeInMemorySave"), "JsonUtility", "ToJson"), "in-memory transfer serializes the complete native save");
Require(Calls(Method("CSaveLoad", "Save"), "JsonUtility", "ToJson"), "native disk backend uses the same serialization");

// Every native market table must be present in full snapshots, applies, and diagnostics.
var marketFields = Find("CPlayerData").Fields.Where(f => f.Name.StartsWith("m_GenCardMarketPriceList")).ToList();
var syncMethods = Find("MarketSync").Module.GetTypes().Where(t => t.FullName.StartsWith("CardShopCoop.Sync.MarketSync"))
    .SelectMany(t => t.Methods).Where(m => m.Name == "ClientApplyInner" || m.Name.StartsWith("<ClientApplyInner>")).ToList();
foreach (var field in marketFields)
{
    string messageField = field.Name.Substring(2);
    Require(Find("MarketStateMessage").Fields.Any(f => f.Name == messageField), "snapshot DTO includes " + messageField);
    Require(UsesField(Method("MarketSync", "BuildState"), "CPlayerData", field.Name), "capture includes " + field.Name);
    Require(syncMethods.Any(m => UsesField(m, "CPlayerData", field.Name)), "apply includes " + field.Name);
    Require(UsesField(Method("MarketSync", "WireChecksum"), "MarketStateMessage", messageField), "wire checksum includes " + messageField);
    Require(UsesField(Method("MarketSync", "WireChecksumFromLists"), "CPlayerData", field.Name), "local checksum includes " + field.Name);
}
Require(UsesField(Method("GamePatches", "GenerateCardMarketPriceBlockPrefix"), "CPlayerData", "m_GenCardMarketPriceListAscension"), "generation guard includes Ascension");
// M3: native ownership, persistence, and host-only battle/reward boundaries.
foreach (string name in new[] { "m_DeckCompactCardDataList", "m_CurrentSelectedDeckIndex", "m_PlayerTournamentData", "m_IsPlayerRegisteredForTournament" })
{
    Require(UsesField(Method("CGameData", "SaveGameData"), "CPlayerData", name), "native TCG save includes " + name);
    Require(UsesField(Method("CGameData", "PropagateLoadData"), "CPlayerData", name), "native TCG load restores " + name);
    Require(UsesField(Method("TcgPlayerState", "Capture"), "CPlayerData", name), "TCG capture includes " + name);
    Require(UsesField(Method("TcgPlayerState", "Apply"), "CPlayerData", name), "TCG apply includes " + name);
}
Require(Calls(Method("DeckListScreen", "DeleteDeck"), "CPlayerData", "AddCard"), "deleting a native deck returns its cards");
Require(Calls(Method("DeckCardPlusMinusScreen", "OnPressAddBtn"), "CPlayerData", "ReduceCard"), "native deck addition consumes inventory cards");
Require(Calls(Method("DeckCardPlusMinusScreen", "OnPressRemoveAllBtn"), "CPlayerData", "AddCard"), "native deck removal returns inventory cards");
Require(Calls(Method("DeckEditScreen", "OnPressPasteButton"), "CPlayerData", "AddCard")
    && Calls(Method("DeckEditScreen", "OnPressPasteButton"), "CPlayerData", "ReduceCard"), "native paste exchanges collection cards");
Require(Calls(Method("PlayTableGame", "EvaluateEndGameGift"), "ItemSpawnManager", "GetItem"), "battle gifts create host items");
Require(Calls(Method("PlayTableGame", "TakeEndGameGiftItem"), "InteractionPlayerController", "AddHoldItemToFront"), "battle gifts go to the host hand");
var exitSteps = Find("PlayTableGame").NestedTypes.Where(t => t.Name.Contains("DelayExit")).SelectMany(t => t.Methods);
Require(exitSteps.Any(m => Calls(m, "PlayTableGame", "EvaluateEndGameGift"))
    && exitSteps.Any(m => Calls(m, "PlayTableGame", "TakeEndGameGiftItem")), "native exit coroutine creates and collects gifts");
Require(Calls(Method("TournamentSync", "BuildState"), "TcgPlayerState", "Capture")
    && Calls(Method("TournamentSync", "BuildState"), "TcgPlayerState", "PlayerBracket"), "tournament message captures player state and sentinel");
Require(Calls(Method("TournamentSync", "ClientApplyInner"), "TcgPlayerState", "Apply"), "guest applies authoritative TCG state");
Require(!Find("TcgPlayerState").Methods.Any(m => Calls(m, "CPlayerData", "AddCard") || Calls(m, "CPlayerData", "ReduceCard")
    || Calls(m, "ItemSpawnManager", "GetItem")), "TCG snapshots never replay inventory/reward operations");
Require(Calls(Method("PlayTableSync", "HostKickTable"), "TcgAuthority", "PlayerAtTable"), "host validates battle occupancy before guest kick");
Require(Calls(Method("FurnitureBoxOps", "HostApplyBoxUp"), "TcgAuthority", "PlayerAtTable"), "host validates battle occupancy before guest box-up");
foreach (string field in new[] { "DeckBox", "Playmat" })
    Require(Find("TcgDeckState").Fields.Any(f => f.Name == field && f.FieldType.Name == "EItemType"), "deck cosmetic enum translation: " + field);
Require(UsesField(Method("ReportSync", "BuildState"), "GameReportDataCollect", "duelWinCount")
    && UsesField(Method("ReportSync", "ClientApplyInner"), "GameReportDataCollect", "duelWinCount"), "daily duel count captured and applied");
Console.WriteLine($"Game assembly MVID: {Find("CPlayerData").Module.Mvid}; mod version: {Find("MarketSync").Module.Assembly.Name.Version}");
Console.WriteLine($"Checked {checkedMembers} literal member references, {checkedHooks} patch hooks, and {contracts} game integration contracts; {unresolved} dynamic/helper references require review; {errors} failures.");
foreach (var module in modules) module.Dispose();
return errors == 0 ? 0 : 1;
