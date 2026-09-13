# AI for Game Assignment prototype

Open the project in Unity 6000.3.18f1 and play **Assets/Scenes/GameplayAI.unity**. This is a playable assault-stage slice built on the original `qh` scene.

Before the stage, choose one to four squad members and cycle the leader. During battle, select a unit in the bottom Canvas HUD. The squad automatically finds the nearest enemy, follows calculated NavMesh paths, checks line of sight, and repeats an ordered basic-attack/finisher action set. Members outside the leader's radius return after their current attack. A timed enemy and a second wave complete the assault objective.

The HUD's **Move** button lets you click a destination for that unit's charge, dash, or flash. **Character Skill** uses the unit's power-up immediately or waits for a target click for burst/heal. Movement points regenerate per unit; character skills and support actions spend the shared universal gauge. **Heal** restores the selected unit, and **Cover** places a carving NavMeshObstacle at the clicked ground position. A placed cover blocks basic-attack sight. Damage evaluates flat stat changes before percentage changes, then subtracts modified defense. Resistance causes a small retreat after repeated hits.

The UI is saved as Canvas, Button, Image, and Text objects in the scene. No runtime UI is constructed with scripts or OnGUI. The existing NavMeshSurface and baked data are retained; movement uses `NavMesh.CalculatePath` and transforms. No NavMeshAgent is present.

The Word document describes a larger game. This prototype does not yet include cover occupancy/abandonment AI, traps, boss abilities, multiple objective types, or individual authored character kits. These are follow-on systems rather than claimed completed features.

For a structural check from the Unity editor, run **AIFG → Validate Playable Scene**. The saved scene is also enabled in Build Settings.
