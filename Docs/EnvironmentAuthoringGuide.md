# Sandbox Gameplay Environment Authoring

## What Unity understands

NavMesh does not identify a road or blockade from its appearance. Baking uses geometry, colliders, slope/step/agent settings, the `NavMeshSurface` collection mask, and optional `NavMeshModifier` components.

The generated prefabs encode the intended meaning through the `EnvironmentAsset` component:

- **Road**: walkable, with mesh colliders for NavMesh baking.
- **Normal blockade**: movement blocker with a `Not Walkable` modifier; also blocks combat sight through `BlockingObstacle`.
- **See-through blockade**: movement blocker with a `Not Walkable` modifier; its `BlockingObstacle` deliberately has `blocksLineOfSight` disabled.
- **Obstacle / Concrete Fence**: every gameplay obstacle is a destructible tactical cover object. It has one shared reservation by default, cover points, team-shared used/abandoned state, universal health, and a world-space health bar.
- **Decoration**: visual only by default, so distant buildings cannot unexpectedly alter the baked route.

## Recommended Sandbox_Gameplay setup

1. Add one `NavMeshSurface` to a stable scene root such as `Environment`.
2. Set **Use Geometry** to **Physics Colliders**. Keep the collection scope limited to the environment root or the environment layers you deliberately use.
3. Drag road prefabs into the scene and assemble the route. Curved and different-length pieces are fine.
4. Slightly overlap adjoining road pieces. The visible textures do not need pixel-perfect contact, but the baked blue NavMesh polygons must form one continuous region wide enough for the agent radius.
5. Avoid vertical seams higher than the agent step height. If a decorative road mesh is too thin or uneven, place a simple invisible Box Collider underneath it as the walkable proxy.
6. Place blockade prefabs, then bake the surface. Static prefabs use `NavMeshModifier` and do not need `NavMeshObstacle` carving. Use carving only for objects that move during play.
7. In Navigation visualization, verify a continuous blue route from the player to the test enemy. A small visual crack is acceptable only if the blue baked regions remain connected.
8. Enter Play Mode and verify the unit reaches the enemy, routes around movement blockers, stops/repositions for normal blockers, and can attack through see-through blockers when in range.

## Cover and projectiles

Every obstacle uses the same cover behavior. Place `CoverPoint` objects where a character should stand and assign the physical obstacle collider; the physical obstacle owns the shared capacity, so multiple points provide approach choices without allowing multiple occupants when capacity is one.

Current attacks apply damage after the AI line-of-sight check rather than spawning physical bullets. When projectile effects are added, use the same `BlockingObstacle.blocksLineOfSight` decision: normal blockades stop the projectile, while see-through blockades are ignored by the projectile query.

## Generated prefab locations

- `Assets/Prefabs/Environment/Road`
- `Assets/Prefabs/Environment/Blockade/Normal`
- `Assets/Prefabs/Environment/Blockade/SeeThrough`
- `Assets/Prefabs/Environment/Obstacle`
- `Assets/Prefabs/Environment/Decoration`

Building 3 is also split into ten individually placeable building prefabs. Building 4 is split into two. Building 6 stays combined because its pieces use generic names and are not clearly independent buildings.

## Reusing the level opening

`Sandbox_Gameplay` keeps the reusable opening setup under `SquadCoordinate`:

- `Opening Camera` is the authored introduction view.
- `Squad Spawn 1` through `Squad Spawn 4` are the chosen-character start markers.
- `LevelOpeningSequence` holds the camera references, two-second duration, 10-degree negative-X camera motion, and NavMesh placement radius.

For another level, copy the `SquadCoordinate` hierarchy into that scene, position the root once, and assign its `LevelOpeningSequence` to the level's `BattleDirector`. Keep the four markers close enough to the baked road for the configured NavMesh search radius. The selected roster is moved to these markers at runtime, remains in Idle during the introduction, and only begins AI, battle UI, timers, and enemy waves after the main camera takes over.
