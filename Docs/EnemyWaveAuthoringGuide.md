# Sandbox Gameplay Enemy Waves

## Current scene setup

- `Enemy Wave Spawner` owns wave progression.
- `EnemySpawnPoint/EnemySpawnPoint1` is wave 1 and currently spawns three `Enemy_Normal` prefabs.
- `EnemySpawnPoint/EnemySpawnPoint1 (1)` is wave 2 and currently spawns two `Enemy_Normal` prefabs plus one `Enemy_Heavy` prefab.
- The previous `Enemies` group is disabled, not deleted. It is kept only as a reference for the old setup.
- `Environment` uses a static, child-only `NavMeshSurface` bake. Runtime enemy spawning does not rebuild the NavMesh.

## Editing a wave

Select a child spawn marker and edit its `EnemySpawnPoint` component:

1. Set **Wave Number**.
2. Add an **Enemies** list entry.
3. Assign an enemy prefab and set **Count**.
4. Adjust **Horizontal Spacing**, **Units Per Row**, and **Row Spacing** for formation spacing.
5. Keep the marker near the intended road. Its height does not need to be exact; each formation position is snapped to the nearest baked NavMesh within **Nav Mesh Search Radius**.

Multiple markers may share the same wave number. The manager spawns every configured marker for that wave and advances only after all enemies spawned in the current wave die.

## Replacing the capsule visuals

Open `Enemy_Normal` or `Enemy_Heavy` Prefab Mode and replace the child named `Capsule Visual (Replace Model Here)` with the final model. Keep these components on the prefab root:

- `CombatUnit`
- `AutoCombatAI`
- `CapsuleCollider`

The root represents the feet/NavMesh position. Place the model so its feet are at local Y = 0, then adjust the root `CapsuleCollider` so its bottom also rests at local Y = 0. `AutoCombatAI` derives grounding from that collider and preserves the offset while moving.

## Imported enemy and boss prefabs

The model prefabs below are ready to assign to any `EnemySpawnPoint` entry. Their root contains `CombatUnit`, `AutoCombatAI`, a grounded capsule collider, the world health HUD, and procedural attack recoil. Adjust the child named `Model Visual (Adjust Here)` if a visual rotation or offset needs manual tuning.

- Regular enemies: `Enemy_Robot`, `Enemy_ToramaruTank`, `Enemy_HelmetGangVehicle`, and `Enemy_Sensei`.
- Level 1 boss: `Boss_SaibaMomoi`.
- Level 2 boss: `Boss_KisakiBall`.
- Level 3/final boss: `Boss_KoyukiPrism`.

Sensei intentionally remains in the source T-pose and uses whole-model recoil when attacking. Source licences and attribution are preserved under `Assets/ThirdParty/EnemyModels`; Toramaru is CC BY-NC-SA 4.0 and must remain non-commercial.

## NavMesh workflow

Rebake only after changing roads or static blockades. Spawning a new enemy does not require a new bake. The baked data remains external at `Assets/Scenes/Sandbox_Gameplay/NavMesh-Envirnoment.asset`, keeping the scene text-serialized and mergeable in Git.
