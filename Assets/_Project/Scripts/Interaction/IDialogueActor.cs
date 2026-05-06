using System.Collections.Generic;

public interface IDialogueActor
{
    List<DialogueChoiceData> GetAvailableChoices(DialogueNodeData node);
    void ApplyConsequence(DialogueConsequence consequence);
}
