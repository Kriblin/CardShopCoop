namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class RequireComponent : Attribute
    {
        public Type m_Type0, m_Type1, m_Type2;
        public RequireComponent(Type a, Type b = null, Type c = null)
        { m_Type0 = a; m_Type1 = b; m_Type2 = c; }
    }
    public class Object
    {
        public bool Destroyed;
        public static bool operator ==(Object a, Object b) =>
            ReferenceEquals(a, b) || ((ReferenceEquals(a, null) || a.Destroyed) && (ReferenceEquals(b, null) || b.Destroyed));
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => base.GetHashCode();
        public static void DestroyImmediate(Object value)
        {
            var candidate = (Component)value;
            foreach (var other in candidate.gameObject.Components.Where(c => c != null && c != candidate))
                foreach (RequireComponent requirement in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                    foreach (var type in new[] { requirement.m_Type0, requirement.m_Type1, requirement.m_Type2 })
                        if (type != null && type.IsAssignableFrom(candidate.GetType()))
                            throw new Exception("Unity rejects removal of " + candidate.GetType().Name);
            candidate.Destroyed = true;
        }
    }
    public class GameObject : Object
    {
        public string name = "fixture";
        public List<Component> Components = new();
        public List<GameObject> Children = new();
        public T Add<T>() where T : Component, new()
        { var component = new T { gameObject = this }; Components.Add(component); return component; }
        public T[] GetComponentsInChildren<T>(bool inactive) where T : Component =>
            Components.OfType<T>().Concat(Children.SelectMany(c => c.GetComponentsInChildren<T>(inactive))).Where(c => c != null).ToArray();
    }
    public class Component : Object { public GameObject gameObject; }
    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour { }
    public class Collider : Component { public bool enabled = true; }
    public class Rigidbody : Component { public bool isKinematic; public bool detectCollisions = true; }
    public class Animator : Behaviour { }
}
namespace UnityEngine.AI
{
    public class NavMeshAgent : UnityEngine.Behaviour { }
    public class NavMeshObstacle : UnityEngine.Behaviour { }
}
namespace Pathfinding
{
    public class Seeker : UnityEngine.MonoBehaviour { }
    [UnityEngine.RequireComponent(typeof(Seeker))]
    public class SimpleSmoothModifier : UnityEngine.MonoBehaviour { }
    public class FunnelModifier : UnityEngine.MonoBehaviour { }
    [UnityEngine.RequireComponent(typeof(Seeker))]
    public class AIBase : UnityEngine.MonoBehaviour { }
    public class AIPath : AIBase { }
}
public class Customer : UnityEngine.MonoBehaviour { }
public class Worker : UnityEngine.MonoBehaviour { }
public class WorkerCollider : UnityEngine.MonoBehaviour { }
public class CharacterCustomization : UnityEngine.MonoBehaviour { }
public class CopyPose : UnityEngine.MonoBehaviour { }
public class BlendshapeManager : UnityEngine.MonoBehaviour { }
public class ModdedWorker : Worker { }
public class ModdedSmooth : Pathfinding.SimpleSmoothModifier { }
[UnityEngine.RequireComponent(typeof(UnityEngine.Collider), typeof(UnityEngine.Rigidbody), typeof(Pathfinding.Seeker))]
public class Dependent : UnityEngine.MonoBehaviour { }
[UnityEngine.RequireComponent(typeof(Dependent))]
public class Chain : UnityEngine.MonoBehaviour { }
namespace Retained
{
    [UnityEngine.RequireComponent(typeof(Pathfinding.Seeker))]
    public class CopyPose : UnityEngine.MonoBehaviour { }
}
