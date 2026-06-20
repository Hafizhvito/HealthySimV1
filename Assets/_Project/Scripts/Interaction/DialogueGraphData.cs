using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DialogueGraph", menuName = "HealthSim/Dialogue Graph")]
public class DialogueGraphData : ScriptableObject
{
    public string npcId = "npc_utama";
    public string npcDisplayName = "Warga Kota";
    [Range(0f, 100f)] public float initialTrust = 50f;
    public string startNodeId = "start";
    public Sprite backgroundSprite;
    public string backgroundResourcePath;
    public List<DialogueNodeData> nodes = new List<DialogueNodeData>();

    public DialogueNodeData GetNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) || nodes == null)
            return null;

        for (int i = 0; i < nodes.Count; i++)
        {
            DialogueNodeData node = nodes[i];
            if (node != null && string.Equals(node.nodeId, nodeId, StringComparison.OrdinalIgnoreCase))
                return node;
        }

        return null;
    }
}

[Serializable]
public class DialogueNodeData
{
    public string nodeId = "node";
    [TextArea(2, 5)] public string fallbackLine = "Halo, bagaimana kabarmu hari ini?";
    public List<string> variationPool = new List<string>();
    public List<DialogueChoiceData> choices = new List<DialogueChoiceData>();
    [TextArea(1, 4)] public string npcFollowUpText = string.Empty;
    public bool isConversationEnd;
    public bool isTerminal;

    public string PickLine(int seed)
    {
        if (variationPool != null && variationPool.Count > 0)
        {
            int index = Mathf.Abs(seed) % variationPool.Count;
            return variationPool[index];
        }

        return fallbackLine;
    }
}

[Serializable]
public class DialogueChoiceData
{
    [TextArea(1, 3)] public string choiceText = "Saya setuju.";
    public string nextNodeId = string.Empty;
    public DialogueConsequence consequence;
    public DialogueCondition gate;

    public string GetDisplayLabel()
    {
        if (string.IsNullOrWhiteSpace(choiceText))
            return "Pilihan";

        return choiceText.Trim();
    }
}

[Serializable]
public class DialogueConsequence
{
    public float energyDelta;
    public float moodDelta;
    public float calorieDelta;
    public float trustDelta;
    public PlayerActionTracker.ActionType trackerAction = PlayerActionTracker.ActionType.GenericInteraction;
}

[Serializable]
public class DialogueCondition
{
    public bool useGate;
    public float minEnergyPercent;
    public float minMoodPercent;
    public float minTrust;
    public List<TimeManager.TimePeriod> allowedPeriods = new List<TimeManager.TimePeriod>();

    public bool CanPass(float energyPercent, float moodPercent, float trustValue, TimeManager.TimePeriod period)
    {
        if (!useGate)
            return true;

        if (energyPercent < minEnergyPercent)
            return false;

        if (moodPercent < minMoodPercent)
            return false;

        if (trustValue < minTrust)
            return false;

        if (allowedPeriods != null && allowedPeriods.Count > 0 && !allowedPeriods.Contains(period))
            return false;

        return true;
    }
}
