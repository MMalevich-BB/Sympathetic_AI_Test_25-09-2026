# Sympathetic_AI_Test_25-09-2026
A snapshot of AI functionality in the game Sympathetic as it was on September 25, 2026


Sympathetic is a psychological survival horror game. As such, it has several types of enemies. This snapshot contains a basic human-type enemy (a Black Ops Soldier armed with an assault rifle) and an anomaly (a ghost-like entity called the Doobie).

Both AI perform patrol tasks, react to distractions, investigate and can hunt the player down. The Soldier fires at the player as part of his attack, while Doobie approaches close and attacks in melee range.

While the Soldier can run fast when he needs to, Doobie is incredibly slow. However, Doobie can phase through walls (slowly) and even teleport if the player looks away while too far to catch.

C# scripts, designed for Unity.

# CONCEPT #
## Black Ops ##
The Black Ops soldiers are separated into Riot Soldiers (armed with a stun baton and a shield) and Rifle Soldiers (armed with a G36 assault rifle). The Soldiers are able to patrol, play animations when idle, and chase the player through an A* pathfinding algorithm defined elsewhere in the project.
The cone of vision is attached to the soldier's eyes, meaning that regardless of where the hull is facing,  the soldier will only see out of its own head. When spotting the player, it will briefly become Distracted. If the player goes away in that time, the soldier decides whether to go back to patrol, whether to Investigate, or whether to Hunt. Investigate, Patrol and Hunt are similar, except Investigate is slow and cautious and Hunt is fast. Plus, Hunt will actually track the player for a few seconds.
When in Attack mode, the soldier will trace lines towards the player as it fires, and the bullets call TakeDamageBullet() on the player when hitting them.
If the soldier loses track of the player, it will eventually go to Idle or Patrol.
The soldier constantly emits a breathing noise through its mask on loop, signalling where it is in the map.
There's capability of the AI playing scripted animations if the level designer decides to include some.

## Doobie ##
Doobie is the first anomalous entity in the game. It's a ghostlike entity with two floating eyes, emitting smoke and visually having a transparent, barely visible humanoid figure. Doobie floats around nodes aimlessly, waiting for long periods of time and loudly announcing its presence idly. When noticing the player, it will become Distracted and roll a chance to either ignore or investigate. It chases the player linearly, phasing through walls (and becoming slow when it's inside walls). If the player steps far enough away and turns away, Doobie will disappear into the floor and reemerge at the closest AI node to the player. Doobie is completely deaf, so even when I add hearing, it will only be able to *see* the player.
If the player goes too far away and Doobie can't teleport because of its cooldown, it will go to the last position it saw the player at, wait, and then return to its normal patrolling.
