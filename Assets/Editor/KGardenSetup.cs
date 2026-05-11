using UnityEngine;
using UnityEditor;

public class KGardenSetup
{
    public static void Execute()
    {
        // Création des tags
        string[] tags = { "Pot", "SoilPile", "Pickup", "Plant", "BossPlant", "Door", "Key" };

        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        foreach (string tag in tags)
        {
            bool exists = false;
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            }
        }
        tagManager.ApplyModifiedProperties();
        Debug.Log("Tags créés : Pot, SoilPile, Pickup, Plant, BossPlant, Door, Key");

        // Création des dossiers Scripts
        if (!AssetDatabase.IsValidFolder("Assets/Scripts"))
            AssetDatabase.CreateFolder("Assets", "Scripts");

        string[] subFolders = { "Core", "Player", "Plants", "Enemies", "Items", "UI" };
        foreach (string sub in subFolders)
        {
            string path = "Assets/Scripts/" + sub;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder("Assets/Scripts", sub);
        }

        AssetDatabase.Refresh();
        Debug.Log("Dossiers Scripts créés avec succès.");
    }
}
