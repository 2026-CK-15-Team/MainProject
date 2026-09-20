using System.Collections.Generic;
using UnityEngine;

public static class ArtifactReservation
{
    private static readonly HashSet<ArtifactId> reserved = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnPlay() => reserved.Clear();

    public static bool IsReserved(ArtifactId id) => reserved.Contains(id);
    public static void Reserve(ArtifactId id) => reserved.Add(id);
    public static void Release(ArtifactId id) => reserved.Remove(id);
}
