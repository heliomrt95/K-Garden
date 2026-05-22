// SachetRuntimeBuilder.cs
// -----------------------------------------------------------------------------
// Version RUNTIME de GraineBuilder : permet de créer un sachet de graines
// pendant le jeu (Play mode). Utilisé par SeedUnlockManager pour faire
// apparaître automatiquement le sachet N2 ou N3 quand le joueur débloque
// un nouveau niveau et qu'aucun sachet pré-existant n'a été référencé.
//
// Classe statique : ne pas attacher à un GameObject.
// -----------------------------------------------------------------------------

using UnityEngine;

public static class SachetRuntimeBuilder
{
    public static GameObject CreerSachet(int niveau, Vector3 position)
    {
        Color cPot, cGraines;
        switch (niveau)
        {
            case 2: cPot = new Color(0.40f, 0.30f, 0.55f); cGraines = new Color(0.60f, 0.45f, 0.85f); break;
            case 3: cPot = new Color(0.18f, 0.30f, 0.55f); cGraines = new Color(0.30f, 0.55f, 0.95f); break;
            case 4: cPot = new Color(0.20f, 0.05f, 0.05f); cGraines = new Color(0.85f, 0.15f, 0.15f); break;
            default: cPot = new Color(0.55f, 0.35f, 0.20f); cGraines = new Color(0.65f, 0.48f, 0.22f); break;
        }

        GameObject sachet = new GameObject("SachetGraines_N" + niveau);
        sachet.transform.position = position;

        // Récipient (cylindre)
        GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pot.name = "Pot";
        pot.transform.parent = sachet.transform;
        pot.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        pot.transform.localScale = new Vector3(0.14f, 0.05f, 0.14f);
        Material matPot = new Material(Shader.Find("Standard"));
        matPot.color = cPot;
        pot.GetComponent<Renderer>().sharedMaterial = matPot;

        // Tas (sphère écrasée)
        GameObject tas = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tas.name = "Tas";
        tas.transform.parent = sachet.transform;
        tas.transform.localPosition = new Vector3(0f, 0.11f, 0f);
        tas.transform.localScale = new Vector3(0.22f, 0.06f, 0.22f);
        Material matGraines = new Material(Shader.Find("Standard"));
        matGraines.color = cGraines;
        tas.GetComponent<Renderer>().sharedMaterial = matGraines;
        Object.Destroy(tas.GetComponent<Collider>());

        // Source
        Source source = sachet.AddComponent<Source>();
        source.typeItem = "graine_" + niveau;
        source.couleurEnMain = cGraines;
        source.tailleEnMain = 0.06f;

        // Collider global
        BoxCollider bc = sachet.AddComponent<BoxCollider>();
        bc.center = new Vector3(0f, 0.07f, 0f);
        bc.size = new Vector3(0.24f, 0.16f, 0.24f);

        return sachet;
    }
}
