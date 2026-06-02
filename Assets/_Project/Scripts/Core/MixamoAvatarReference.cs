using UnityEngine;

/// <summary>
/// Humanoid avatar reference from Mixamo (used when body FBX has no embedded avatar yet).
/// </summary>
[CreateAssetMenu(fileName = "MixamoAvatarReference", menuName = "HealthSim/Mixamo Avatar Reference")]
public class MixamoAvatarReference : ScriptableObject
{
    public Avatar avatar;
}
