using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ActiveSetUI : MonoBehaviour
{
    [SerializeField] private PlayerArtifacts artifacts;
    [SerializeField] private TMP_Text text;

    private readonly HashSet<ArtifactSetType> activeSets = new();

    private void OnEnable() => artifacts.SetTierChanged += OnSetTierChanged;
    private void OnDisable() => artifacts.SetTierChanged -= OnSetTierChanged;

    private void OnSetTierChanged(ArtifactSetType setType, bool active)
    {
        if (active) activeSets.Add(setType);
        else activeSets.Remove(setType);

        UpdateText();
    }

    private void UpdateText()
    {
        if (activeSets.Count == 0)
        {
            text.text = "";
            return;
        }

        var names = new List<string>();
        foreach (var set in activeSets)
            names.Add($"{set} Tier1");

        text.text = string.Join("   ", names);
    }
}
