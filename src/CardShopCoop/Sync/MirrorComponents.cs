using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardShopCoop.Sync
{
    // Only call on co-op-owned, inactive clones. Never alter the source prefab.
    internal static class MirrorComponents
    {
        internal static bool IsLocalLogic(Type type)
        {
            for (; type != null; type = type.BaseType)
            {
                if (type.Namespace == "Pathfinding" || (type.Namespace ?? "").StartsWith("Pathfinding.", StringComparison.Ordinal))
                    return true;
                switch (type.FullName)
                {
                    case "Customer":
                    case "Worker":
                    case "WorkerCollider":
                    case "InteractableObject":
                    case "UnityEngine.AI.NavMeshAgent":
                    case "UnityEngine.AI.NavMeshObstacle":
                        return true;
                }
            }
            return false;
        }

        internal static void DisableNpcLogic(GameObject clone)
        {
            foreach (var behaviour in clone.GetComponentsInChildren<Behaviour>(true))
                if (behaviour != null && IsLocalLogic(behaviour.GetType()))
                    behaviour.enabled = false;
        }

        internal static void StripAvatar(GameObject clone, bool keepCustomization, Action<string> report)
        {
            var all = clone.GetComponentsInChildren<Component>(true);
            var pending = new List<Component>();
            foreach (var component in all)
            {
                if (component == null)
                    continue;
                bool cosmetic = false;
                switch (component.GetType().Name)
                {
                    case "CopyPose":
                    case "BlendshapeManager":
                    case "ScaleCharacter":
                    case "TransformBone":
                    case "MipBiasAdjust":
                        cosmetic = true;
                        break;
                    case "CharacterCustomization":
                        cosmetic = keepCustomization;
                        break;
                }
                if ((component is MonoBehaviour && !cosmetic) || IsLocalLogic(component.GetType())
                    || component is Collider || component is Rigidbody)
                {
                    // Even an unexpected retained dependency must not run local AI/physics.
                    if (component is Behaviour behaviour)
                        behaviour.enabled = false;
                    if (component is Collider collider)
                        collider.enabled = false;
                    if (component is Rigidbody body)
                    {
                        body.isKinematic = true;
                        body.detectCollisions = false;
                    }
                    pending.Add(component);
                }
            }

            // Unity refuses removal while another component on the same object requires
            // this type. Process dependents first, including inherited RequireComponent
            // attributes. Retained cosmetic components also protect their prerequisites.
            while (pending.Count > 0)
            {
                bool progress = false;
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    var candidate = pending[i];
                    if (candidate == null)
                    {
                        pending.RemoveAt(i);
                        continue;
                    }
                    bool required = false;
                    foreach (var other in all)
                    {
                        if (other == null || other == candidate || other.gameObject != candidate.gameObject)
                            continue;
                        if (Requires(other.GetType(), candidate.GetType()))
                        {
                            required = true;
                            break;
                        }
                    }
                    if (required)
                        continue;
                    UnityEngine.Object.DestroyImmediate(candidate);
                    pending.RemoveAt(i);
                    progress = true;
                }
                if (progress || pending.Count == 0)
                    continue;
                report?.Invoke("Retained disabled components required by remaining components on " + clone.name
                    + ": " + string.Join(", ", pending.ConvertAll(c => c.GetType().FullName)));
                break;
            }
        }

        private static bool Requires(Type dependent, Type prerequisite)
        {
            foreach (RequireComponent requirement in dependent.GetCustomAttributes(typeof(RequireComponent), true))
                if ((requirement.m_Type0 != null && requirement.m_Type0.IsAssignableFrom(prerequisite))
                    || (requirement.m_Type1 != null && requirement.m_Type1.IsAssignableFrom(prerequisite))
                    || (requirement.m_Type2 != null && requirement.m_Type2.IsAssignableFrom(prerequisite)))
                    return true;
            return false;
        }
    }
}
