# K-Garden — Guide de configuration Unity

## 1. Prérequis du projet

- Unity **2022.3 LTS** ou supérieur (testé avec 2022.3 et 2023.x)
- Package **AI Navigation** (Window → Package Manager → Unity Registry → "AI Navigation") — nécessaire pour `NavMeshSurface` qui permet de générer le NavMesh à partir de primitives créées en runtime
- Aucun asset 3D externe n'est requis : tout est construit par primitives

## 2. Mise en place de la scène (rapide — 5 minutes)

### Option A — Setup automatique (recommandé pour tester)

1. Crée une nouvelle scène vide
2. Crée un GameObject vide nommé **GameRoot**
3. Ajoute-lui le composant **SceneSetup** (script fourni)
4. Ajoute-lui un composant **NavMeshSurface** :
   - Collect Objects : `All Game Objects`
   - Use Geometry : `Render Meshes`
   - Agent Type : `Humanoid` (ou un agent custom plus petit)
5. Crée un script court **NavMeshBaker** (voir section 4) et attache-le, OU baker manuellement après le 1er Play en ajoutant un appel à `surface.BuildNavMesh()` en fin de `LevelBuilder.BuildLevel()`

> Le `SceneSetup` instanciera automatiquement le `LevelBuilder`, le `GameManager`, le `WaveManager` et le `Player` complet (CharacterController + caméra + InteractionManager).

### Option B — Setup manuel

| Objet | Composants à ajouter |
|---|---|
| `GameManager` (vide) | `GameManager.cs` |
| `WaveManager` (vide) | `WaveManager.cs` |
| `LevelBuilder` (vide) | `LevelBuilder.cs` + `NavMeshSurface` |
| `Player` (vide) | `CharacterController` (height 1.8, radius 0.4, center Y=0.9), `PlayerController.cs`, `InteractionManager.cs` |
| `Player → PlayerCamera` (vide enfant à Y=1.65) | `Camera`, `AudioListener` |
| `EndingCanvas` | voir section UI plus bas |

## 3. Configuration du NavMesh runtime

Comme la géométrie est créée au `Start()`, il faut bake le NavMesh **après** `LevelBuilder.BuildLevel()`.
Ajoute ce petit script ou complète `LevelBuilder.Awake()` ainsi :

```csharp
using Unity.AI.Navigation;

private void Start()
{
    var surface = GetComponent<NavMeshSurface>();
    if (surface == null) surface = gameObject.AddComponent<NavMeshSurface>();
    surface.collectObjects = CollectObjects.All;
    surface.BuildNavMesh();
}
```

(Place ce code dans un `Start()` dédié pour qu'il s'exécute après `Awake()` qui construit la scène.)

## 4. Configuration de l'UI de fin

1. Crée un **Canvas** (UI → Canvas)
2. Mode `Screen Space - Overlay`
3. Ajoute un **Image** enfant qui couvre tout l'écran, couleur noire
4. Ajoute un **Text** (legacy ou TextMeshPro) centré, blanc, taille ~36
5. Sur le Canvas, ajoute un **CanvasGroup** (alpha 0)
6. Ajoute un GameObject **EndingUI** avec le script `EndingUI.cs` :
   - `endingCanvas` → ton CanvasGroup
   - `endingText` → ton Text

## 5. Tags à créer (Project Settings → Tags and Layers)

- `Pot`
- `SoilPile`
- `Pickup`
- `Plant`
- `BossPlant`
- `Door`
- `Key`

## 6. Contrôles du joueur

| Touche | Action |
|---|---|
| **WASD / ZQSD** | Déplacement |
| **Souris** | Regarder |
| **E** | Interagir (ramasser, ajouter terre/engrais/graine/eau au pot) |
| **Clic gauche** | Pulvériser le spray anti-nuisibles (si équipé) |
| **1** | Équiper l'arrosoir |
| **2** | Équiper le spray d'engrais |
| **3** | Équiper le spray anti-nuisibles |
| **4** | Équiper la graine en cours |

## 7. Boucle de gameplay attendue

1. Le joueur ramasse l'arrosoir, les 2 sprays et la première graine (Drosera)
2. Il prend une poignée de terre au tas de terreau (touche **E**)
3. Il s'approche d'un pot, équipe la terre, **E** → pot rempli
4. Équipe l'engrais, **E** sur le pot
5. Équipe la graine, **E** sur le pot
6. Équipe l'arrosoir, **E** sur le pot → la croissance démarre + les nuisibles arrivent
7. Le joueur défend la plante avec le spray (clic gauche)
8. Si la plante mûrit, elle lâche la graine suivante (Nepenthes)
9. Cycle répété pour Nepenthes → Dionaea
10. Après les 3 plantes, un **pot boss** apparaît au centre — le joueur y plante les 3 graines
11. Boss : 5 arrosages + 3 fertilisations + survivre à 30 nuisibles violets
12. Mâchoires s'ouvrent → la clé tombe
13. Le joueur ramasse la clé → fade-to-black → texte de fin

## 8. Ajustements possibles

- **Difficulté** : modifier `pestWaveCount` et `growthDuration` dans `GameManager.plantDataset[]`
- **Vitesse des nuisibles** : `agent.speed` dans `PestEnemy.Awake()`
- **Nombre de pots** : `LevelBuilder.potsPerSide`
- **Couleurs des plantes** : `PlantData.stemColor` / `flowerColor` dans `GameManager`

## 9. Notes techniques

- **Tous les `GameObject.CreatePrimitive`** créent automatiquement un `MeshFilter`, `MeshRenderer` et `Collider`. Pour les éléments décoratifs sans collision (feuilles, fleurs), on retire le collider via `Destroy(go.GetComponent<Collider>())`.
- **Les pots utilisent à la fois un Collider de cylindre (physique)** et un `SphereCollider` en trigger plus large pour faciliter l'interaction au raycast.
- Le `LevelBuilder` met en cache les matériaux dans un `Dictionary` pour ne créer qu'une seule instance de chaque couleur.
- La transparence du verre utilise le shader Standard en mode Fade. Si tu utilises URP/HDRP, il faudra adapter (`Shader.Find("Universal Render Pipeline/Lit")` + setup des keywords transparents).
