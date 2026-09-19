using CardShopCoop.Sync;
using UnityEngine;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    Console.WriteLine("PASS " + name);
    passed++;
}
var broken = new GameObject();
var first = broken.Add<Pathfinding.Seeker>();
broken.Add<Pathfinding.SimpleSmoothModifier>();
bool rejected = false;
try { UnityEngine.Object.DestroyImmediate(first); }
catch (Exception) { rejected = true; }
Check(rejected, "fixture reproduces Unity refusing seeker removal before its modifier");
foreach (bool reverse in new[] { false, true })
foreach (bool preview in new[] { false, true })
{
    var clone = new GameObject();
    var seeker = clone.Add<Pathfinding.Seeker>();
    var smooth = clone.Add<ModdedSmooth>();
    clone.Add<Pathfinding.FunnelModifier>();
    clone.Add<Pathfinding.AIPath>();
    clone.Add<UnityEngine.AI.NavMeshAgent>();
    clone.Add<UnityEngine.AI.NavMeshObstacle>();
    clone.Add<Customer>();
    clone.Add<Collider>();
    clone.Add<Rigidbody>();
    clone.Add<Dependent>();
    clone.Add<Chain>();
    var pose = clone.Add<CopyPose>();
    var custom = clone.Add<CharacterCustomization>();
    var anim = clone.Add<Animator>();
    var child = new GameObject();
    child.Add<Pathfinding.Seeker>();
    child.Add<Pathfinding.SimpleSmoothModifier>();
    var blend = child.Add<BlendshapeManager>();
    clone.Children.Add(child);
    if (reverse) clone.Components.Reverse();
    int reports = 0;
    MirrorComponents.StripAvatar(clone, preview, _ => reports++);
    Check(seeker.Destroyed && smooth.Destroyed && reports == 0, $"{reverse}/{preview}: dependent modifiers removed before seeker");
    Check(clone.GetComponentsInChildren<Component>(true).All(c => c == pose || c == anim || c == blend || (preview && c == custom)),
        $"{reverse}/{preview}: only visual components survive nested cleanup");
    Check(pose.enabled && anim.enabled && blend.enabled && (preview ? custom.enabled : custom.Destroyed),
        $"{reverse}/{preview}: rig and preview customization retain their state");
    MirrorComponents.StripAvatar(clone, preview, _ => reports++);
    Check(reports == 0, $"{reverse}/{preview}: repeated cleanup is safe");
}
var npc = new GameObject();
npc.Add<Customer>(); npc.Add<ModdedWorker>(); npc.Add<WorkerCollider>();
npc.Add<Pathfinding.Seeker>(); npc.Add<ModdedSmooth>(); npc.Add<Pathfinding.FunnelModifier>();
npc.Add<Pathfinding.AIPath>(); npc.Add<UnityEngine.AI.NavMeshAgent>(); npc.Add<UnityEngine.AI.NavMeshObstacle>();
var cosmetics = npc.Add<CopyPose>();
var npcAnimator = npc.Add<Animator>();
var npcCustom = npc.Add<CharacterCustomization>();
MirrorComponents.DisableNpcLogic(npc);
Check(npc.Components.OfType<Behaviour>().Where(b => b != cosmetics && b != npcAnimator && b != npcCustom).All(b => !b.enabled && !b.Destroyed),
    "NPC local logic including derived scripts and native navigation is disabled, retained for UI");
Check(cosmetics.enabled && npcAnimator.enabled && npcCustom.enabled, "NPC cosmetics and animation remain enabled");
var retained = new GameObject();
var protectedSeeker = retained.Add<Pathfinding.Seeker>();
retained.Add<Retained.CopyPose>();
int warnings = 0;
MirrorComponents.StripAvatar(retained, true, _ => warnings++);
Check(!protectedSeeker.Destroyed && !protectedSeeker.enabled && warnings == 1,
    "retained cosmetic dependency is reported and its prerequisite stays disabled");
Console.WriteLine($"{passed} mirror cleanup checks passed.");
