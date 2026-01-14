using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class WeaponPrefabValidator
{
    private const string WeaponsFolder = "Assets/Resources/Prefabs/Weapons";

    [MenuItem("Tools/Validate Weapon Prefabs")]
    public static void ValidateWeaponPrefabs()
    {
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { WeaponsFolder });
        var failures = new List<string>();

        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefabName = Path.GetFileNameWithoutExtension(path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var child = root.transform.Find(prefabName);
                if (child == null)
                {
                    failures.Add($"{path} | Missing child named '{prefabName}'");
                    continue;
                }

                var renderer = child.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    failures.Add($"{path} | Child '{prefabName}' missing SpriteRenderer");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        if (failures.Count == 0)
        {
            Debug.Log($"Weapon Prefab Validation: PASS ({prefabGuids.Length} prefabs checked)");
        }
        else
        {
            Debug.LogError($"Weapon Prefab Validation: FAIL ({failures.Count}/{prefabGuids.Length} prefabs failed)");
            foreach (var failure in failures)
            {
                Debug.LogError(failure);
            }
        }
    }
}
