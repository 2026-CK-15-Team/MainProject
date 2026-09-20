using UnityEngine;

public static class ModalGate
{
    private static int openCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetOnPlay() => openCount = 0;

    public static bool AnyModalOpen => openCount > 0;

    public static void Register(bool isOpen)
    {
        openCount += isOpen ? 1 : -1;
        if (openCount < 0) openCount = 0;
    }
}