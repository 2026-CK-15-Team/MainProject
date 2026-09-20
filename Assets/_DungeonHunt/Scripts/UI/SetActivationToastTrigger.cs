using UnityEngine;

public class SetActivationToastTrigger : MonoBehaviour
{
    [SerializeField] private PlayerArtifacts artifacts;
    [SerializeField] private float toastDuration = 1.0f;

    private void OnEnable() => artifacts.SetTierChanged += OnSetTierChanged;
    private void OnDisable() => artifacts.SetTierChanged -= OnSetTierChanged;

    private void OnSetTierChanged(ArtifactSetType setType, bool active)
    {
        if (!active) return;
        FindObjectOfType<CenterToast>()?.Show($"{setType} Set Tier1", toastDuration);
    }
}
