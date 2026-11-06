using UnityEngine;

namespace Soul
{
    // Container asset: the importer stores each model's generated meshes and materials as
    // sub-assets of one SoulMeshStore (Assets/SOUL/Generated/<model>.asset).
    public class SoulMeshStore : ScriptableObject { public string source; }
}
