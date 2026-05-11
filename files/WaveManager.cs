using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Singleton qui gère le spawn de vagues de nuisibles autour d'un pot actif.
/// Une vague s'arrête quand la plante mûrit ou meurt.
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [Header("Spawn")]
    public float spawnRadius = 10f;
    public float spawnInterval = 2.5f;
    public Transform[] spawnPoints; // optionnel : si laissé vide, on spawn à l'extérieur du pot

    private Coroutine currentWave;
    private PotInteraction currentPot;
    private List<PestEnemy> alivePests = new List<PestEnemy>();
    private int killCount;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void StartWave(PotInteraction pot, int totalEnemies)
    {
        if (currentWave != null) StopCoroutine(currentWave);
        currentPot = pot;
        killCount = 0;
        currentWave = StartCoroutine(SpawnWaveRoutine(pot, totalEnemies));
    }

    public void StopWave(PotInteraction pot)
    {
        if (currentPot != pot) return;
        if (currentWave != null) StopCoroutine(currentWave);
        currentWave = null;

        // Détruit les nuisibles restants
        foreach (var p in alivePests) if (p != null) Destroy(p.gameObject);
        alivePests.Clear();
    }

    private IEnumerator SpawnWaveRoutine(PotInteraction pot, int total)
    {
        int spawned = 0;
        while (spawned < total)
        {
            if (pot == null) yield break;
            PlantManager plant = pot.GetComponentInChildren<PlantManager>();
            if (plant == null || plant.isMature) yield break;

            SpawnOne(pot, plant);
            spawned++;
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SpawnOne(PotInteraction pot, PlantManager plant)
    {
        GameObject pest = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pest.name = "Pest";
        pest.transform.localScale = Vector3.one * 0.25f;

        // Position de spawn : un point aléatoire dans un cercle autour du pot
        Vector2 rnd = Random.insideUnitCircle.normalized * spawnRadius;
        Vector3 spawnPos = pot.transform.position + new Vector3(rnd.x, 0.15f, rnd.y);

        // Snap sur le NavMesh
        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            spawnPos = hit.position;

        pest.transform.position = spawnPos;

        Material m = new Material(Shader.Find("Standard"));
        m.color = new Color(0.9f, 0.1f, 0.1f);
        pest.GetComponent<Renderer>().material = m;

        NavMeshAgent agent = pest.AddComponent<NavMeshAgent>();
        agent.baseOffset = 0.15f;

        PestEnemy enemy = pest.AddComponent<PestEnemy>();
        enemy.Initialize(plant);

        alivePests.Add(enemy);
    }

    public void NotifyPestKilled()
    {
        killCount++;
    }
}
