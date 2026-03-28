// GOExtensions.cs
// Shared extension helper for all UI panel scripts.
using UnityEngine;

internal static class GOExtensions
{
    internal static T GetOrAddComponent<T>(this GameObject go) where T : Component
        => go.GetComponent<T>() ?? go.AddComponent<T>();
}
