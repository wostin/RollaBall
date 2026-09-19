using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class CrossSectionController : MonoBehaviour
{
    // Estructura optimizada para el renderizado
    public struct CrossSectionEntry
    {
        public Renderer renderer;
        public Material[] materials;
        public int[] submeshIndices;
    }

    public static readonly List<CrossSectionEntry> RegisteredEntries = new List<CrossSectionEntry>();

    [Header("Plano de corte")]
    public Transform cutPlane;

    [Header("Renderers afectados")]
    public Renderer[] targetRenderers;

    static readonly int PlaneNormalID   = Shader.PropertyToID("_PlaneNormal");
    static readonly int PlanePositionID = Shader.PropertyToID("_PlanePosition");

    MaterialPropertyBlock[] _propBlocks;
    private bool _needsRegistryUpdate = true;

    void OnEnable()
    {
        _needsRegistryUpdate = true;
        InitPropertyBlocks();
    }

    void OnDisable()
    {
        RegisteredEntries.Clear();
    }

    void OnValidate()
    {
        _needsRegistryUpdate = true;
    }

    void Update()
    {
        if (cutPlane == null || targetRenderers == null) return;

        if (_needsRegistryUpdate)
        {
            UpdateStaticRegistry();
            _needsRegistryUpdate = false;
        }

        if (_propBlocks == null || _propBlocks.Length != targetRenderers.Length)
            InitPropertyBlocks();

        Vector3 planeNormal   = cutPlane.up;
        Vector3 planePosition = cutPlane.position;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] == null) continue;

            targetRenderers[i].GetPropertyBlock(_propBlocks[i]);
            _propBlocks[i].SetVector(PlaneNormalID,   planeNormal);
            _propBlocks[i].SetVector(PlanePositionID, planePosition);
            targetRenderers[i].SetPropertyBlock(_propBlocks[i]);
        }
    }

    void UpdateStaticRegistry()
    {
        RegisteredEntries.Clear();
        if (targetRenderers == null) return;

        foreach (var r in targetRenderers)
        {
            if (r == null || !r.enabled) continue;

            List<Material> csMaterials = new List<Material>();
            List<int> csSubmeshes = new List<int>();

            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].shader.name == "Custom/URP/CrossSection")
                {
                    csMaterials.Add(mats[i]);
                    csSubmeshes.Add(i);
                }
            }

            if (csMaterials.Count > 0)
            {
                RegisteredEntries.Add(new CrossSectionEntry
                {
                    renderer = r,
                    materials = csMaterials.ToArray(),
                    submeshIndices = csSubmeshes.ToArray()
                });
            }
        }
    }

    void InitPropertyBlocks()
    {
        if (targetRenderers == null) return;
        _propBlocks = new MaterialPropertyBlock[targetRenderers.Length];
        for (int i = 0; i < targetRenderers.Length; i++)
            _propBlocks[i] = new MaterialPropertyBlock();
    }
}
