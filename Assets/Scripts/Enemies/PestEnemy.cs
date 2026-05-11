using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Petite sphère rouge qui se dirige vers la plante active via NavMesh,
/// puis lui inflige des dégâts quand elle l'atteint.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class PestEnemy : MonoBehaviour
{
    public int hp = 1;
    public float attackDamage = 10f;
    public float attackInterval = 1.0f;
    public float attackRange = 0.6f;

    private NavMeshAgent agent;
    private Transform target;
    private PlantManager targetPlant;
    private float lastAttackTime;

    public void Initialize(PlantManager plant)
    {
        targetPlant = plant;
        target = plant.transform;
    }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = 1.8f;
        agent.acceleration = 6f;
        agent.angularSpeed = 360f;
        agent.radius = 0.15f;
        agent.height = 0.3f;
    }

    private void Update()
    {
        if (target == null || targetPlant == null || targetPlant.isMature)
        {
            // La plante a mûri ou n'existe plus → on s'enfuit (au sens : on disparaît)
            Destroy(gameObject, 0.5f);
            return;
        }

        agent.SetDestination(target.position);

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= attackRange && Time.time - lastAttackTime > attackInterval)
        {
            lastAttackTime = Time.time;
            targetPlant.TakeDamage(attackDamage);
        }
    }

    public void TakeDamage(int dmg)
    {
        hp -= dmg;
        if (hp <= 0)
        {
            // Petit effet visuel : on flash blanc puis on détruit
            GetComponent<Renderer>().material.color = Color.white;
            Destroy(gameObject, 0.05f);
            if (WaveManager.Instance != null)
                WaveManager.Instance.NotifyPestKilled();
        }
    }
}
