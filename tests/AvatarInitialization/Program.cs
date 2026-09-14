using CardShopCoop.Sync;
using Newtonsoft.Json;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition)
        throw new Exception(name);
    Console.WriteLine("PASS " + name);
    passed++;
}
void Throws(Action action, string name)
{
    bool threw = false;
    try
    {
        action();
    }
    catch (Exception) { threw = true; }
    Check(threw, name);
}
CC.CC_CharacterData Preset(string name) => new()
{
    CharacterName = name,
    HairNames = new() { "hair", "brows", "obsolete slot" },
    HairColor = null,
    ApparelNames = new() { "shirt", "obsolete slot" },
    ApparelMaterials = null
};
CC.CharacterCustomization Custom(bool female)
{
    var custom = new CC.CharacterCustomization();
    custom.Presets.Presets.Add(Preset(female ? "Female0" : "Male0"));
    return custom;
}

foreach (bool female in new[] { false, true })
{
    string gender = female ? "female" : "male";
    var custom = Custom(female);
    var preset = custom.Presets.Presets[0];
    string original = JsonConvert.SerializeObject(preset);
    var ui = custom.UI;
    Throws(() => custom.Initialize(), gender + " inherited init flag reproduces missing runtime slots");
    CharacterTemplate.InitializeFresh(custom, female);
    Check(custom.Applies == 1 && custom.m_HasInit && !custom.Autoload,
        gender + " bootstrap applies once after initializing runtime slots");
    Check(ReferenceEquals(ui, custom.UI), gender + " bootstrap restores UI reference without invoking it");
    Check(custom.StoredCharacterData.CharacterName == (female ? "Female0" : "Male0"),
        gender + " correct gender preset selected");
    Check(custom.StoredCharacterData.HairNames.Count == 2
        && custom.StoredCharacterData.HairColor.Count == 2
        && custom.StoredCharacterData.ApparelNames.Count == 1
        && custom.StoredCharacterData.ApparelMaterials.Count == 1,
        gender + " stale preset lists trimmed and missing lists padded");
    Check(!ReferenceEquals(custom.StoredCharacterData.HairColor[0], custom.StoredCharacterData.HairColor[1]),
        gender + " padded colors do not alias");
    Check(original == JsonConvert.SerializeObject(preset), gender + " shared preset remains unchanged");
    custom.StoredCharacterData.HairNames[0] = "edited";
    custom.CharacterMeshes[0].enabled = false;
    CharacterTemplate.ApplyDefault(custom, female ? "Female0" : "Male0");
    Check(custom.StoredCharacterData.HairNames[0] == "hair" && custom.Applies == 2,
        gender + " repeat default application uses a fresh copy");
    Check(custom.CharacterMeshes[0].enabled, gender + " default reset restores hidden body meshes");
}
var fallbackPreset = Custom(true);
fallbackPreset.Presets.Presets[0].CharacterName = "Female7";
CharacterTemplate.InitializeFresh(fallbackPreset, true);
Check(fallbackPreset.StoredCharacterData.CharacterName == "Female7", "missing default uses same-gender preset");
foreach (bool female in new[] { false, true })
{
    var customer = Custom(female);
    string name = female ? "Female12" : "Male12";
    var tournamentPreset = Preset(name);
    customer.Presets.Presets.Add(tournamentPreset);
    CharacterTemplate.InitializeFresh(customer, female, name);
    Check(customer.StoredCharacterData.CharacterName == name,
        "customer mirror preserves host tournament preset " + name);
    CharacterTemplate.ApplyDefault(customer, female ? "Female0" : "Male0");
    Check(customer.StoredCharacterData.CharacterName != name && customer.Applies == 2,
        "customer mirror can redress after a host appearance change");
    Check(tournamentPreset.HairNames.Count == 3 && tournamentPreset.HairColor == null,
        "tournament preset asset remains unchanged");
}
Throws(() => CharacterTemplate.InitializeFresh(Custom(false), true), "wrong-gender presets are not used");
Throws(() => CharacterTemplate.InitializeFresh(new CC.CharacterCustomization(), false), "missing presets fail explicitly");
var throwing = Custom(false);
throwing.ThrowOnInitialize = true;
var throwingUi = throwing.UI;
Throws(() => CharacterTemplate.InitializeFresh(throwing, false), "bootstrap failure propagates to template owner");
Check(ReferenceEquals(throwingUi, throwing.UI), "UI reference restored on failure");
var empty = new CC.CC_CharacterData();
var zero = new CC.CharacterCustomization { HairTables = new(), ApparelTables = new() };
CharacterTemplate.NormalizeCharacterData(zero, empty);
Check(empty.HairNames.Count == 0 && empty.HairColor.Count == 0
    && empty.ApparelNames.Count == 0 && empty.ApparelMaterials.Count == 0
    && empty.Blendshapes != null && empty.ColorProperties != null,
    "zero slots and null appearance lists normalize safely");

int destroyed = 0, attempts = 0, reports = 0;
var source = new object();
var cache = new AvatarTemplateCache<object, Template>(t => { t.Destroyed = true; destroyed++; });
Template lastFailed = null;
Template Get(object prefab, bool female, bool fail = false) => cache.Get(prefab, female,
    t => !t.Destroyed,
    t =>
    {
        attempts++;
        t.Custom = Custom(female);
        t.Custom.ThrowOnApply = fail;
        CharacterTemplate.InitializeFresh(t.Custom, female);
    },
    (t, e) => { reports++; lastFailed = t; });
Check(Get(null, false) == null && attempts == 0, "missing prefab is not attempted or recorded as failed");
Check(Get(source, false, true) == null && destroyed == 1 && reports == 1 && lastFailed.Destroyed,
    "partially initialized candidate is destroyed and never published");
Check(Get(source, false) == null && attempts == 1 && reports == 1,
    "same failed prefab is suppressed without repeated logs");
var femaleTemplate = Get(source, true);
Check(femaleTemplate != null && attempts == 2, "female failure tracking is independent");
Check(ReferenceEquals(femaleTemplate, Get(source, true)) && attempts == 2, "successful template is reused");
Check(Get(source, false) == null && femaleTemplate.Destroyed, "switching to failed gender releases old template");
var replacement = new object();
Check(Get(replacement, false) != null && attempts == 3, "changed prefab permits a new attempt");
Check(Get(source, false) != null && attempts == 4, "returning prefab is retried after replacement");
cache.RejectCurrent();
Check(Get(source, false) == null && attempts == 4, "later default failure invalidates and suppresses cached template");
cache.Clear();
Check(Get(source, false) != null && attempts == 5, "session reset clears failure suppression");
int beforeClear = destroyed;
cache.Clear();
cache.Clear();
Check(destroyed == beforeClear + 1, "cleanup is idempotent");

int stateReports = 0;
var snapshot = new object();
Check(ReferenceEquals(OptionalAppearanceState.Build(() => snapshot, _ => stateReports++), snapshot),
    "valid optional snapshot is unchanged");
Check(OptionalAppearanceState.Build<object>(() => throw new ArgumentOutOfRangeException("slot"), _ => stateReports++) == null
    && stateReports == 1, "appearance exception returns no optional snapshot without escaping");
Console.WriteLine($"{passed} regression checks passed.");

public sealed class Template
{
    public CC.CharacterCustomization Custom;
    public bool Destroyed;
}
