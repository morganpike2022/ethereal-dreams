# Ethereal Dreams MMORPG — Technical Design Document

Version 1.0 · 2026-05-24  
Covers ETH-25 through ETH-36: Stormgate city, The Eternal Spire, and core gameplay systems.

---

## Table of Contents

1. [Stormgate — Hub City Layout](#1-stormgate--hub-city-layout)
2. [The Eternal Spire — Tower Content](#2-the-eternal-spire--tower-content)
3. [Lobby & Matchmaking](#3-lobby--matchmaking)
4. [NPC System](#4-npc-system)
5. [Procedural Dungeons](#5-procedural-dungeons)
6. [Level Scaling](#6-level-scaling)
7. [Aggro System](#7-aggro-system)
8. [Class Roles](#8-class-roles)
9. [Affixes](#9-affixes)
10. [Boss Phase AI — Difficulty Tiers](#10-boss-phase-ai--difficulty-tiers)

---

## 1. Stormgate — Hub City Layout

**ETH-25**

Stormgate is the capital city of Aethoria and the primary hub for all player activity. It sits at the geographic center of the continent, accessible from all starting zones via road or teleport crystal. Every new character begins the game here after the tutorial.

### 1.1 City Districts

| District | Zone Type | Purpose |
|---|---|---|
| The Grand Promenade | city | Central plaza, notice boards, world events, main teleport nexus |
| Ironworks Quarter | city | Blacksmithing, armorers, siege engineers, weapon vendors |
| The Alchemist's Row | city | Potion vendors, herbalists, enchanting table, inscription |
| Mage Sanctum | city | Arcane trainers, portal hub to remote zones, spellbook vendors |
| The Thorn Market | city | Auction house, bank vault, trade broker, currency exchange |
| Barrack District | city | PvP duel arena (safe), war board, arena queue NPC |
| Harbor Docks | city | Fishing, sea-route transport, cargo import events |
| The Spire Gate | city | Entrance lobby to The Eternal Spire, raid finder NPC |
| Undercroft | instanced | Black market, rogue faction HQ, hidden quests (access gated by reputation) |

### 1.2 Key Structural Rules

- Stormgate is a **permanent safe zone**: PvP flagging is disabled within city boundaries.
- All class trainers, mount vendors, and the primary auction house are located here.
- The city is partitioned into **zone layers** in the database (`zones` table, `zone_type = 'city'`). Each district is a separate zone record with `is_pvp = false` and no level requirement.
- The **Grand Promenade** hosts scheduled world events (market festivals, invasion alerts) broadcast via SignalR to all connected clients.
- **Portal nexus** at the Grand Promenade teleports players to any continent capital they have previously discovered. Teleporting costs 50 gold and has a 3-second cast time interruptible by taking damage.

### 1.3 Stormgate NPC Roster (reference)

| NPC Role | Count | Location | Function |
|---|---|---|---|
| Class Trainers | 6 (one per class) | Mage Sanctum / Barracks | Sell and unlock skills |
| Weapons & Armor Vendors | 8 | Ironworks Quarter | Tier-1 through Tier-3 gear |
| Potion Vendors | 4 | Alchemist's Row | Consumables, reagents |
| Auction Master | 1 | Thorn Market | List, bid, buyout items |
| Bank Teller | 3 | Thorn Market | Personal and guild bank |
| Dungeon Finder NPC | 1 | Spire Gate | Queues player for group content |
| Raid Finder NPC | 1 | Spire Gate | Queues for The Eternal Spire raid wings |
| World Quest Board | 2 | Grand Promenade | Daily/weekly rotating world quests |
| Guard Captain | 1 | Barrack District | Starts law-enforcement quest chain |
| Harbor Master | 1 | Harbor Docks | Transport routes, fishing license |

---

## 2. The Eternal Spire — Tower Content

**ETH-26**

The Eternal Spire is a 60-floor vertical dungeon located at the eastern edge of Aethoria. It is Ethereal Dreams' primary endgame content pillar: infinitely replayable, procedurally modified above floor 30, and fully leaderboard-tracked.

### 2.1 Structure

```
Floors 1–10   — Tutorial wings. Fixed layout. Solo or group. All difficulties available.
Floors 11–20  — Standard wing. Fixed boss pool, randomised trash corridors.
Floors 21–30  — Advanced wing. Weekly affix rotation applied.
Floors 31–50  — Mythic wing. Fully procedural layout, 2–4 random affixes, time limit per floor.
Floors 51–60  — World tier. No public leaderboard preview; first-clears announced server-wide.
```

### 2.2 Progression Model

- Players purchase a **Spire Key** from the Thorn Market or earn one from daily quests.
- A Spire Key locks to a **floor range** (1–10, 11–20, etc.) and a **difficulty tier** (see §10).
- Completing a floor within the time limit grants a **floor chest**: guaranteed gear of item level = `base_floor_ilvl + floor_number * 2`.
- Depleting the timer on floors 31+ immediately ends the run; no chest awarded.
- Floors 51–60 issue soulbound **Spire Tokens** redeemable for Season-exclusive cosmetics.

### 2.3 Database Representation

The Spire maps to the `zones` table as `zone_type = 'instanced'` with an `is_instanced = true` flag. Each floor is a separate zone record. The `max_players` column caps party size at 5 for floors 1–50 and 20 for floors 51–60 (raid wing).

Instance lifecycle is tracked via a dedicated `spire_runs` table (added in ETH-26 migration):

```sql
CREATE TABLE spire_runs (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    floor_start     SMALLINT NOT NULL,
    floor_end       SMALLINT NOT NULL,
    difficulty      VARCHAR(20) NOT NULL,
    affix_ids       INT[] NOT NULL DEFAULT '{}',
    party_id        UUID REFERENCES parties (id),
    started_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at    TIMESTAMPTZ,
    timed_out       BOOLEAN NOT NULL DEFAULT FALSE,
    score           BIGINT NOT NULL DEFAULT 0
);
```

### 2.4 Leaderboard

- Per-floor leaderboards keyed by `(floor, difficulty, week)`.
- World-first clears for floors 51–60 stored in `admin_audit_log` with `action = 'spire_world_first'`.
- `GET /api/spire/leaderboard?floor=55&difficulty=raid` returns top 100 fastest clears.

---

## 3. Lobby & Matchmaking

**ETH-27**

The lobby system handles group formation for all instanced content: dungeons, Spire floors, arenas, and raids.

### 3.1 Role Queue

Every group-content queue requires players to declare a role before joining:

| Role | Abbreviation | Required for |
|---|---|---|
| Tank | T | All group content |
| Healer | H | Groups of 3+ |
| Damage | D | All group content |

A **5-player dungeon** requires: 1 Tank + 1 Healer + 3 Damage.  
A **20-player Spire raid wing** requires: 2 Tank + 4 Healer + 14 Damage.

Players may queue for multiple roles simultaneously. The matchmaking algorithm fills the highest-priority role gap first (Tank → Healer → Damage).

### 3.2 Matchmaking Algorithm

```
1. Player joins queue: (content_id, difficulty, role_preferences[], avg_item_level)
2. Server maintains a priority queue per (content_id, difficulty)
3. Every 5 seconds, attempt group formation:
   a. Select earliest-queued Tank (if available)
   b. Select earliest-queued Healer (if available)
   c. Fill remaining slots with Damage players
   d. Apply item-level window: all members within ±15 ilvl of group average
   e. If no valid group can be formed, skip this cycle
4. On successful formation: create instance, notify all members via SignalR
5. Members have 60 seconds to accept; decline or timeout = re-queue individual members
```

Wait tolerance per role:
- Tanks: estimated 30 s average
- Healers: estimated 90 s average
- Damage: estimated 4–8 min average

### 3.3 Lobby State Machine

```
QUEUED → FORMING (group assembled, awaiting accepts)
       → READY_CHECK (all accepted, loading instance)
       → IN_PROGRESS
       → COMPLETED | ABANDONED | TIMED_OUT
```

State stored in Redis as `lobby:{groupId}` with a 10-minute TTL on FORMING state.

### 3.4 API Surface

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/lobby/queue` | Join matchmaking queue |
| DELETE | `/api/lobby/queue` | Leave queue |
| POST | `/api/lobby/{id}/accept` | Accept ready check |
| GET | `/api/lobby/{id}/status` | Poll lobby state |
| WS | `/hubs/lobby` | Real-time lobby events |

### 3.5 Pre-formed Groups

Players who have already assembled a full party can bypass matchmaking via `/api/lobby/premade`. The server validates role coverage and item-level eligibility, then directly creates the instance.

---

## 4. NPC System

**ETH-28**

### 4.1 NPC Types

| Type | Subtype | Aggro | Health Pool | Notes |
|---|---|---|---|---|
| Vendor | — | Never | Invulnerable | Buy/sell items |
| Quest Giver | — | Never | Invulnerable | Delivers and receives quests |
| Trainer | — | Never | Invulnerable | Unlocks skills for gold |
| Guard | Patrol | Reactive | Standard | Attacks flagged or criminal players |
| Critter | Passive | Never | Minimal | Ambient life, no drops |
| Standard Enemy | Melee / Ranged / Caster | Proximity | Tier-scaled | Drops loot, grants XP |
| Elite Enemy | Any | Proximity | 3× standard | Named, guaranteed uncommon+ drop |
| Champion | Any | Proximity | 10× standard | Zone miniboss, weekly loot lockout |
| Boss | Any | Phase-driven | Per-encounter config | See §10 |

### 4.2 Behavior Tree

All hostile NPC AI runs on a server-side tick (250 ms interval). The behavior tree evaluates nodes in priority order:

```
Root
├── [Priority 1] Phase transition check (bosses only)
├── [Priority 2] Death check → drop loot, begin respawn timer
├── [Priority 3] Enrage check → if time_in_combat > enrage_timer: apply enrage buff
├── [Priority 4] Flee check → if current_hp_pct < 10% AND npc_type = 'critter': flee
├── [Priority 5] Active combat
│   ├── Select primary target (highest threat, see §7)
│   ├── Select ability (cooldown check → weight-based random selection)
│   └── Execute ability (apply damage / effect)
├── [Priority 6] Aggro scan → if player in aggro_radius AND LOS: add to threat table
└── [Priority 7] Patrol → move to next waypoint
```

### 4.3 Spawn System

Spawn points are stored in `npc_spawns` table. The server-side spawn manager:

1. On startup, reads all `npc_spawns` records and schedules initial spawns.
2. When an NPC dies, its `is_alive` flag is set to `false` and `died_at` is recorded.
3. A background job (Hangfire, 5 s polling interval) queries `npc_spawns WHERE is_alive = false AND died_at + respawn_secs * INTERVAL '1 second' < NOW()` and triggers respawn.
4. On respawn: NPC is reconstructed at its spawn point with full health, threat table cleared.

### 4.4 Vendor NPC Protocol

```
GET  /api/npcs/{npcId}/inventory    → returns items for sale with buy_price
POST /api/npcs/{npcId}/buy          → body: { itemId, quantity }
POST /api/npcs/{npcId}/sell         → body: { characterInventorySlotId, quantity }
```

Sell price = `item.sell_price * quantity`. Buy price = `item.buy_price * quantity`. Vendor stock is unlimited by default; rare vendors have a `stock_limit` column with daily reset.

### 4.5 Dialogue System

NPC dialogue is stored as JSON in `npc_types.ai_behavior`:

```json
{
  "type": "vendor",
  "greeting": "Welcome to Stormgate, adventurer. What do you need?",
  "dialogue_tree": [
    { "id": 1, "text": "Show me your wares.", "action": "open_shop" },
    { "id": 2, "text": "Goodbye.", "action": "close" }
  ]
}
```

The client drives dialogue node selection; the server validates that the selected `action` is legal for the NPC's type.

---

## 5. Procedural Dungeons

**ETH-29**

Procedural generation applies to Spire floors 31+ and to the Mythic+ difficulty tier of all standard dungeons.

### 5.1 Room Graph Generation

Each dungeon instance is represented as a directed acyclic graph of rooms:

```
[Entrance Room]
     │
 [Room A] ─── [Room B]
     │              │
 [Room C]       [Miniboss Room]
     │              │
     └──────── [Boss Room]
```

Generation algorithm:

1. **Seed selection**: `seed = hash(instance_id + floor_number + week_number)`. Using week_number makes floors consistent within a week for leaderboard fairness.
2. **Room pool selection**: Sample from the zone's `room_templates` table, filtered by floor range and zone type.
3. **Graph construction**: Place Entrance at depth 0. Randomly branch (1–3 exits per room) to depth `target_depth` (5–8 for Spire Mythic). Guarantee a Boss Room at max depth.
4. **Enemy population**: Each non-boss room is filled with `base_count + floor_bonus` enemies drawn from the zone's enemy pool. Elite rooms (1–2 per dungeon) receive an Elite enemy instead of the standard pool.
5. **Secret rooms**: 20% chance per branch to insert a detour room with bonus loot. Secret rooms do not appear on the minimap until discovered.

### 5.2 Room Templates

Room templates are configured data (not code-generated). Each template specifies:
- Geometry reference (wall layout, door positions)
- Enemy slot count (min/max)
- Chest spawn point (optional)
- Hazard zones (lava, pressure plates, arcane fields)

### 5.3 Affix Application

After the room graph is built, weekly affixes (see §9) are overlaid. Affixes modify:
- Enemy stats (e.g., Fortified: +20% HP to non-boss enemies)
- Hazard frequency (e.g., Volcanic: random lava geysers)
- Mechanics (e.g., Bolstering: enemies near a dying target gain stacks)

### 5.4 Reproducibility

Given the same `seed`, the room graph, enemy placements, and chest positions are deterministic. This enables:
- Speedrun verification (same seed = same dungeon)
- Replay sharing
- Debugging by reproducing a specific run exactly

---

## 6. Level Scaling

**ETH-30**

### 6.1 Player Level Range

Characters progress from level 1 to 50. Zone recommended level ranges:

| Zone | Min Level | Max Level | Notes |
|---|---|---|---|
| Stormgate Outskirts | 1 | 5 | Tutorial zone |
| Verdenmoor Coast | 5 | 15 | |
| Frostspire Foothills | 10 | 25 | |
| Aethoria Highlands | 20 | 35 | |
| The Ashlands | 30 | 45 | |
| Spire Base Camp | 40 | 50 | |
| The Eternal Spire | 50 | 50 | Endgame; item-level-gated not level-gated |

### 6.2 Enemy Scaling Formula

Enemy base stats are calculated from the `npc_types` table and scaled by floor level:

```
enemy_hp       = npc_types.hp * (1 + 0.08 * (floor_level - 1))
enemy_attack   = npc_types.attack_power * (1 + 0.05 * (floor_level - 1))
enemy_xp       = base_xp * (1 + 0.10 * (floor_level - 1))
enemy_gold_min = npc_types.gold_min + (floor_level - 1) * 2
```

When a player is more than 5 levels below the enemy: **underleveled penalty** — player damage reduced 20%, incoming damage increased 15%.  
When a player is more than 5 levels above the enemy: **trivialized** — XP and gold rewards reduced by 5% per level of difference (minimum 10% of base).

### 6.3 Gear Item Level

Item level (`ilvl`) is the primary power axis at max level. Each gear piece's stat contribution:

```
stat_contribution = base_stat * (1 + (ilvl - base_ilvl) * 0.03)
```

`base_ilvl = 100` for all items. Gear above ilvl 100 is endgame-only (Spire drops start at ilvl 110).

### 6.4 Dynamic Level Scaling (Party)

When a party member's level differs from the zone's recommended level:
- The zone-entry check warns but does not block entry.
- XP awarded to each member is based on their **personal level**, not the party average.
- Loot rolls use the **character's ilvl**, not party average — preventing boosting exploits.

### 6.5 Catch-up Mechanics

Characters below max level receive a **Rested XP** bonus after being offline for 4+ hours:
- Rested XP pool = `character_level * 300`
- Rested XP doubles all XP gained until pool is exhausted

Characters returning after a season break receive a **Veteran's Boost**: +20% XP from all sources for 7 days.

---

## 7. Aggro System

**ETH-31**

### 7.1 Threat Table

Each NPC in combat maintains a per-combat **threat table**: a map of `character_id → threat_value`. The NPC always attacks the character with the highest threat value (primary target).

### 7.2 Threat Generation

Threat is generated by every action a character takes during combat:

| Action | Threat Generated |
|---|---|
| Damage dealt | `damage_dealt * 1.0` |
| Healing done | `healing_done * 0.5` (split evenly across all enemies in combat) |
| Buff/support cast (no damage) | `50 + spell_power * 0.2` flat |
| Tank-stance ability | `ability_base_threat * 3.0` (multiplied by threat stance modifier) |
| Taunt (hard aggro) | Sets caster's threat to `current_top_threat + 1` instantly |
| Fade / Feign Death (rogue) | Resets caster's threat to `0` |

### 7.3 Threat Stance Modifiers

| Stance / Form | Modifier | Available To |
|---|---|---|
| Defensive Stance | ×2.0 threat from all actions | Warrior |
| Holy Presence | ×1.5 threat from healing | Paladin |
| Shadow Form | ×0.7 threat from all actions | Necromancer |
| Stealth | No threat generated until first attack | Rogue |

### 7.4 Aggro Radius

NPCs check for nearby players on every AI tick (250 ms). Proximity aggro:

```
aggro_radius is defined per npc_type (default: 10 units)
line_of_sight check: ray cast to player position; blocked by walls
condition: player_is_alive AND NOT player_in_stealth AND distance <= aggro_radius
```

When a player is added to the threat table for the first time, the NPC locks into combat (`is_in_combat = true`). The NPC will pursue its primary target up to `leash_distance = aggro_radius * 3`. Beyond that, it resets: full health restore, threat table cleared, combat ended.

### 7.5 Shared Aggro (Pack Behavior)

If an NPC receives damage and has a `pack_group_id` set, all NPCs sharing that pack group within `pack_alert_radius = 20 units` are also aggroed onto the attacker with threat = `50`. This models patrol groups and guard patrols calling for help.

### 7.6 Tank Swap Mechanics

Some boss encounters require a coordinated **tank swap**. This is triggered by a boss ability that applies a debuff to the active tank:

- **Stacking Debuff**: boss applies a stacking debuff on every melee hit to the primary target. At stacks 3/5/7 (configurable per boss), the incoming damage multiplier increases by 15% per stack.
- **Off-tank** takes over when the main tank calls the swap via party chat or a macro; the off-tank uses a taunt ability to become the primary target.
- The main tank's debuff decays at `1 stack per 4 seconds` while not being the primary target.

Tank swap is required on **Veteran** difficulty and above (see §10).

---

## 8. Class Roles

**ETH-32**

### 8.1 Role Matrix

| Class | Primary Role | Secondary Role | Signature Mechanic |
|---|---|---|---|
| Warrior | Tank | Melee DPS | Defensive Stance doubles threat; Heroic Strike builds Rage |
| Mage | Ranged DPS | Off-Healer (Arcane Healing spec) | Arcane Missiles combo; Mana management through Evocation |
| Rogue | Melee DPS | — | Combo Points (5-point finishers); Stealth opener; Feign Death aggro drop |
| Paladin | Tank / Healer | Melee DPS | Dual spec: Holy Presence (healer) vs Righteous Stance (tank); Blessings buff party |
| Ranger | Ranged DPS | — | Focus resource; Trap preparation; Pet management (Hunter variant) |
| Necromancer | Ranged DPS | Support | Minion army; Death Coil; Corpse Explosion; Dark Pact resource sacrifice |

### 8.2 Role Responsibilities in Group Content

**Tank**
- Maintain top threat on all enemies at all times.
- Position enemies to avoid cleave hitting non-tank party members.
- Call and execute tank swaps (see §7.6).
- Use mitigation cooldowns proactively on telegraphed boss abilities.

**Healer**
- Monitor party health via party frame HP events (SignalR push from server).
- Manage mana; mana is a finite resource that requires efficient spell selection.
- Dispel debuffs when a `dispellable` flag is set on the debuff.
- Maintain HoTs (heal-over-time effects) on tank during sustained damage phases.
- On Elite difficulty and above (see §10), mana management becomes the primary healer constraint.

**Damage (DPS)**
- Maximize damage output while adhering to positioning requirements.
- Switch targets on mechanic-flagged adds (sub-targets spawned by bosses).
- Stack in designated positions for group-wide AoE healing.
- Avoid standing in ground effects (damage zones marked as circles/cones on the game board).

### 8.3 Class Stat Priorities

| Class | Primary Stat | Secondary Stats |
|---|---|---|
| Warrior | Endurance | Strength → Armor |
| Mage | Intelligence | Spirit (mana regen) → Crit |
| Rogue | Agility | Crit → Haste |
| Paladin (Tank) | Endurance | Strength → Spirit |
| Paladin (Healer) | Intelligence | Spirit → Crit |
| Ranger | Agility | Haste → Crit |
| Necromancer | Intelligence | Spirit → Spell Power |

### 8.4 Stat Derivation Formulas

```
attack_power    = strength * 2 + (level * 3)
spell_power     = intelligence * 2 + (level * 3)
crit_chance (%) = base_crit + (agility / 50) + (intelligence / 80)
dodge_chance(%) = base_dodge + (agility / 60)
mana_regen/5s   = spirit * 0.4 + (intelligence * 0.1)
armor           = endurance * 3 + equipped_armor_sum
```

---

## 9. Affixes

**ETH-33**

Affixes are modifiers applied to dungeon instances that change enemy behaviour, environment hazards, or loot. Affixes rotate on a **weekly cadence** for floors 21+ of The Eternal Spire and Mythic+ standard dungeons.

### 9.1 Affix Tiers

Each dungeon instance applies:
- 1 affix for floors 21–30 (Apprentice and above)
- 2 affixes for floors 31–40 (Veteran and above)
- 3 affixes for floors 41–50 (Elite and above)
- 4 affixes for floors 51–60 (Legendary / Raid)

Affixes are drawn from a pool; certain affix combinations are **banned** (e.g., Volcanic + Storming would be unplayably punishing).

### 9.2 Affix Reference

| Affix | Category | Effect |
|---|---|---|
| **Fortified** | Enemy — HP | Non-boss enemies gain +20% max HP and +30% damage |
| **Tyrannical** | Enemy — Boss | Boss gains +40% max HP and +15% damage; boss abilities deal +20% damage |
| **Bolstering** | Enemy — Interaction | When any non-boss enemy dies, nearby enemies gain +20% damage and +10% max HP (stacking, up to 5× per enemy) |
| **Sanguine** | Enemy — Healing | On death, enemies leave a blood pool that heals remaining enemies for 5% max HP/s |
| **Bursting** | Enemy — Debuff | On death, enemies explode applying a stackable 3% max HP DoT per stack to all players; 6 stacks removed at a time per player with a 2 s interval |
| **Volcanic** | Environment | Periodic random lava geysers in each room; standing in geyser deals 15% max HP per second |
| **Storming** | Environment | Intermittent wind gusts that push players toward room edges; hitting a wall stuns for 1.5 s |
| **Grievous** | Player Debuff | Players below 90% HP suffer a stacking bleed (1.5% max HP / s per stack); removed on reaching 90%+ HP |
| **Quaking** | Player Debuff | Random players periodically emit an AoE shockwave; players standing within 5 units of the emitter take 20% max HP damage |
| **Inspiring** | Enemy — Aura | A random non-boss enemy becomes Inspired: immune to crowd control; nearby enemies gain 30% cast speed |
| **Raging** | Enemy — Threshold | Non-boss enemies enrage below 30% HP: +100% damage, immune to slows |
| **Necrotic** | Player Debuff | Every enemy melee hit applies a stacking healing reduction (5% per stack); decays 1 stack per 4 s out of combat |
| **Spiteful** | Enemy — Death | Dying non-boss enemies spawn a Spiteful Shade that fixates on a random healer; Shade must be interrupted or killed within 8 s |
| **Overflowing** | Loot | All chests contain one additional item; gold drops increased by 40% |
| **Cursed** | Loot / Mechanic | One chest per floor is cursed; opening it triggers a combat encounter with the chest's guardian |

### 9.3 Affix Banned Combinations

```
Volcanic  + Storming   (no safe ground)
Bursting  + Sanguine   (enemies refuse to die cleanly)
Grievous  + Necrotic   (healer lockout feedback loop)
Inspiring + Raging     (un-crowd-controllable enraged enemies)
```

### 9.4 Affix Data Model

Affixes are stored in an `affixes` table (added in ETH-33 migration):

```sql
CREATE TABLE affixes (
    id          SERIAL PRIMARY KEY,
    name        VARCHAR(50) NOT NULL UNIQUE,
    category    VARCHAR(30) NOT NULL,
    description TEXT NOT NULL,
    effect_data JSONB NOT NULL DEFAULT '{}'
);

CREATE TABLE affix_bans (
    affix_a_id  INT NOT NULL REFERENCES affixes (id),
    affix_b_id  INT NOT NULL REFERENCES affixes (id),
    PRIMARY KEY (LEAST(affix_a_id, affix_b_id), GREATEST(affix_a_id, affix_b_id))
);
```

The weekly affix schedule is stored in `affix_schedules`:

```sql
CREATE TABLE affix_schedules (
    week_start  DATE NOT NULL,
    floor_tier  SMALLINT NOT NULL,  -- 21, 31, 41, 51
    affix_ids   INT[] NOT NULL,
    PRIMARY KEY (week_start, floor_tier)
);
```

---

## 10. Boss Phase AI — Difficulty Tiers

**ETH-34 / ETH-35 / ETH-36**

Boss encounters are defined by a phase sequence. Each phase is a struct with:
- HP threshold that triggers entry (% of max HP)
- Ability pool available during the phase
- Phase-transition animation / cinematic flag
- Enrage timer (optional, per phase)

The number of phases, complexity of ability patterns, and required coordination mechanics scale per difficulty tier.

### 10.1 Difficulty Tiers Overview

| Tier | Phases | Enrage Timer | Required Mechanics | Target Audience |
|---|---|---|---|---|
| Novice | 1 | None | None | First-time players, story experience |
| Apprentice | 2 | Soft (damage buff) | Basic interrupt | Players learning group content |
| Veteran | 3 | Hard (wipe) | Tank swap, interrupt | Coordinated 5-player groups |
| Elite | 4 | Hard (wipe) | Tank swap, dispels, healer mana budget | Experienced players |
| Legendary | 5 | Hard (wipe) | All previous + positional wipe mechanic | Hardcore progression |
| Raid | 6+ | Hard (per phase) | All previous + world-competition execution | World-first race contenders |

---

### 10.2 Novice Difficulty

**1 phase — simple, accessible, story-focused**

```
Phase 1 (100% → 0%):
  Ability pool:
    - Basic Melee Strike     (1.5 s cast; melee range; 80% of normal auto-attack damage)
    - Slow Cleave            (2.5 s cast; 90° frontal cone; clearly telegraphed red cone)
    - Health Potion (NPC)    (triggered at 40% HP; heals boss for 15% max HP; visible cast bar)
  
  Enrage: None
  Scripted events: Boss taunts player verbally at 75%, 50%, 25% HP for immersion
  Death: 5-second death animation; drops loot chest; respawn timer starts
```

Design goals:
- No instant-kill mechanics.
- All abilities are telegraphed with a minimum 2-second warning.
- Healers can spam their cheapest heal indefinitely without running out of mana.
- Solo-able by any class at the recommended level.

---

### 10.3 Apprentice Difficulty

**2 attack patterns, 1 phase shift at 50% HP**

```
Phase 1 (100% → 50%):
  Pattern A — Sustained Pressure (default):
    - Auto Attack (0.8 s swing timer)
    - Cleave (1.8 s cast; frontal 180°; moderate damage)
    - Shadow Bolt (1.5 s cast; ranged; interruptible)
  
  Pattern B — Triggered by [Enraged] buff (activated when 3 party members are below 50% HP):
    - Frenzied Strikes (faster auto-attack; 0.5 s swing timer)
    - Feral Lunge (instant cast; charges random target; knocks back 3 units)
  
  Boss alternates Pattern A and Pattern B every 20 seconds.

Phase 2 (50% → 0%): [PHASE SHIFT — 2-second stagger animation]
  Boss gains [Shadowed Resolve]: +25% damage, +10% movement speed
  Additional ability unlocked:
    - Void Rupture (2 s cast; places a void zone AoE on a random player's location;
                    persists 15 s; standing in it deals 8% max HP / s)
  
  Pattern A and B from Phase 1 remain active in Phase 2.
  
  Enrage (soft): At 8:00 into the fight, boss gains +50% damage until death.
                 Does not wipe group but accelerates kill requirement.
```

Design goals:
- Teaches players to watch for cast bars and interrupt Shadow Bolt.
- Phase shift is a visual and audio event that signals increased danger.
- Void Rupture introduces spatial awareness: players must move out of ground effects.

---

### 10.4 Veteran Difficulty

**3 phases, tank swap required**

```
Phase 1 (100% → 65%):
  - Auto Attack (0.8 s swing; stacking Corrosive Wound on tank: +12% incoming damage per stack)
  - Shockwave (2 s cast; targeted line AoE toward tank; knockback)
  - Crippling Poison (instant; targets random non-tank; reduces movement speed 60% for 8 s; dispellable)
  
  Tank swap mechanic:
    - Corrosive Wound stacks are applied to the current primary-threat holder on every melee hit.
    - At 3 stacks: incoming physical damage +36% — survivable but stressful.
    - At 5 stacks: incoming physical damage +60% — lethal with Shockwave combo.
    - Off-tank must taunt before 5 stacks. Main tank's stacks decay at 1/4 s when not primary target.
    - Recommended swap cadence: swap at 3 stacks.

Phase 2 (65% → 30%): [PHASE SHIFT — full room darkness for 3 s; torches relight]
  - All Phase 1 abilities remain.
  - New: Summon Adds (every 40 s; spawns 2× Corrupted Soldier; must be killed within 20 s
         or they buff the boss with [Empowered] for 60 s: +40% damage)
  - New: Seismic Slam (3 s cast; room-wide knockback; knocks all players back 8 units;
         players at wall take 25% max HP fall damage)

Phase 3 (30% → 0%): [PHASE SHIFT — boss roars; room partially collapses]
  - All Phase 1 and Phase 2 abilities remain.
  - Seismic Slam cooldown reduced by 50%.
  - New: Berserker Rage (passive; every 10 s boss gains a temporary +30% damage buff for 5 s)
  - Enrage (hard): At 10:00 total fight time, boss one-shots all party members.
```

Design goals:
- Introduces a true mechanical requirement: the tank swap. A group that does not execute the swap will wipe at Phase 3.
- Add management teaches split DPS priority.
- Hard enrage forces meaningful DPS check.

---

### 10.5 Elite Difficulty

**4 phases, healer mana management is the primary constraint**

```
Phase 1 (100% → 70%):
  [All Veteran Phase 1 abilities, with increased values]
  - Corrosive Wound stacks faster (every 2nd melee hit instead of every hit).
  - New: Mana Drain Pulse (4 s cast; room-wide; drains 8% mana from all players;
         channeled; interruptible by a single player interrupt within the 4 s window)
  - Healer constraint: Mana Drain Pulse + sustained healing pressure means healers
    must use Evocation / Innervate on cooldown and select efficient spells only.

Phase 2 (70% → 45%): [PHASE SHIFT]
  - All Phase 1 abilities remain.
  - New: Soul Siphon (targets healer specifically; 5 s channel; deals 5% max HP / s
         to healer while healing the boss for the same amount; interrupt or LOS break required)
  - New: Pestilence Nova (instant; applies [Festering Plague] to all party members;
         a DoT dealing 3% max HP / s; duration 12 s; dispellable but dispel costs 30% mana)
  
  Healer decision point: dispel Pestilence (mana-expensive) or heal through it (raw throughput risk).

Phase 3 (45% → 20%): [PHASE SHIFT — floor tiles crack; molten fissures open]
  - New: Molten Surge (every 25 s; 3 random floor tiles become lava for 10 s; standing in lava = 10% max HP / s)
  - New: Bloodletting (instant cast; applies a stacking bleed on the tank that can only be removed by
         a specific Paladin ability [Cleanse] or by the Rogue's [Neutralising Powder]; 2% max HP / s per stack)
  - Mana budget: by Phase 3, healers should have used 1 major mana cooldown in Phase 2;
    they must plan their second for the Phase 3 / 4 transition.

Phase 4 (20% → 0%): [PHASE SHIFT — [Deathmark] applied to all party members: total damage taken +20%]
  - Boss enters [Final Reckoning]: all ability cooldowns halved.
  - Mana Drain Pulse now uninterruptible.
  - Enrage (hard): At 14:00 total fight time; instant group wipe.
```

Design goals:
- Healer mana is the axis of difficulty. A healer who panic-heals will go out of mana in Phase 3 and the group dies.
- Soul Siphon creates a moment where the healer is the interrupt target — teaches healers to not purely focus on party frames.
- Multiple dispel decisions with cost-benefit trade-offs.

---

### 10.6 Legendary Difficulty

**5 phases, positional wipe mechanic**

```
Phase 1 (100% → 75%):
  [All Elite Phase 1 abilities, further tuned]
  - New: Mark of Doom (applied to one random player every 20 s;
         after 6 s, the marked player explodes for 60% of the party's max HP as AoE damage
         to all players within 8 units; marked player must run to a designated "safe corner" before detonation)
  
  POSITIONAL REQUIREMENT: each room has 4 pre-defined "safe corners" marked by floor runes.
  Marked player must reach a safe corner before detonation; the explosion still fires but deals
  0 damage to players in a safe corner. A player who does not reach a corner wipes the group.

Phase 2 (75% → 55%): [PHASE SHIFT]
  - Mark of Doom frequency doubles (every 10 s).
  - New: Gravity Well (boss plants a gravity anchor in the centre of the room;
         all players are slowly pulled toward it; players sucked into the anchor take 100% max HP per second;
         players must walk against the pull; movement speed reduced 30% by the pull force)
  
  Combined mechanic: running from Mark of Doom while fighting the Gravity Well pull is the
  primary coordination challenge. Rogues should use Sprint; Warriors should use Charge to avoid.

Phase 3 (55% → 35%): [PHASE SHIFT]
  - All Phase 1 and 2 abilities active.
  - New: Soul Fracture (boss splits into 3 spectral copies for 20 s;
         only the true boss takes full damage; copies deal 50% damage and share aggro table;
         true boss identified by a unique particle effect; killing a copy heals the true boss 5% max HP)
  
  DPS check: during Soul Fracture, group must deal enough damage to the true copy to force it
  to reassemble. Reassembly triggers if true copy reaches 70% HP during split phase.

Phase 4 (35% → 15%): [PHASE SHIFT — room hazard changes; floor becomes partially electrified]
  - New: Chain Lightning (instant; bounces to nearest player not already hit; 3 bounces;
         each bounce 80% of previous hit; deals 30% max HP to first target)
  - Electrified Floor: static zones that deal 15% max HP / s; zones shift every 12 s.
  - Mark of Doom detonation radius doubled (16 units); safe corners now require 2 players stacking in them
    (solo safe corner = no reduction, must have a second player present for the 0-damage benefit).

Phase 5 (15% → 0%): [PHASE SHIFT — [Legendary's Curse]: all healing received reduced 50%]
  - Boss enters [Ascendant Form]: all previous abilities simultaneously active, cooldowns removed.
  - Mark of Doom detonation now deals 80% max HP to non-safe players.
  - New: [Extinction Beam] (12 s channel on boss; deals 200% max HP total to the primary target;
         must be interrupted or tank uses a major defensive cooldown to survive)
  - Enrage (hard): At 18:00 total fight time.
```

Design goals:
- Mark of Doom + Gravity Well in Phase 2 creates the "wipe mechanic" that will claim the vast majority of groups on first encounter.
- Soul Fracture introduces a "who to hit" decision under heavy pressure.
- Phase 5 is intentionally overloaded — groups that have not perfected execution of Phases 1–4 will not have the resources to complete it.

---

### 10.7 Raid Difficulty

**6+ phases, world-first competition**

Raid encounters are designed in collaboration with Ethereal Dreams' content team for each content season. They apply to 20-player instances of The Eternal Spire floors 51–60 and seasonal world bosses.

**Base structure** (all Raid encounters include):

1. All Legendary mechanics remain active, with values tuned for 20 players.
2. **Minimum 6 phases** with unique abilities in each phase; phases do not recycle previous-phase ability pools verbatim (new abilities per phase).
3. **Role-specific mechanics**: at least one ability per phase that specifically targets and requires action from a Tank, a Healer, and a Damage player separately.
4. **World-race rules**:
   - Boss health, timers, and ability damage values are published only after world-first clear.
   - Server announces first clear, first 10 clears, and realm-first clears via in-game broadcast.
   - World-first clear earns the title `[Firstborn of the Spire]` (account-wide, permanent).

**Phase design constraints for Raid encounters**:

| Constraint | Value |
|---|---|
| Minimum phases | 6 |
| Maximum enrage timer per phase | 4:00 |
| Minimum unique abilities per phase | 3 |
| Mandatory role mechanics per phase | Tank + Healer + DPS (1 each, minimum) |
| Simultaneous active mechanics in Phase 6+ | 4 minimum |
| Healer mana budget per phase | Must exhaust at least 1 major mana cooldown per 2 phases |
| Tank swap frequency in final phase | Every 2 melee hits (stack application on each hit) |

**Wipe-mechanic quota per encounter**: every Raid boss must have at minimum:
- 1 positional wipe mechanic (Mark of Doom variant or equivalent)
- 1 split-group mechanic (soak / spread as applicable)
- 1 enrage check that actually wipes under-geared groups

**Data model for Raid phases**:

Boss encounter phases are stored in a `boss_phases` table (ETH-36 migration):

```sql
CREATE TABLE boss_phases (
    id              SERIAL PRIMARY KEY,
    npc_type_id     INT NOT NULL REFERENCES npc_types (id),
    phase_index     SMALLINT NOT NULL,
    hp_threshold    NUMERIC(5,2) NOT NULL,  -- % of max HP to enter this phase
    difficulty      VARCHAR(20) NOT NULL,
    ability_ids     INT[] NOT NULL DEFAULT '{}',
    enrage_seconds  INT,                     -- NULL = no hard enrage in this phase
    transition_vfx  VARCHAR(100),
    phase_data      JSONB NOT NULL DEFAULT '{}',

    UNIQUE (npc_type_id, difficulty, phase_index)
);

CREATE TABLE boss_abilities (
    id              SERIAL PRIMARY KEY,
    name            VARCHAR(100) NOT NULL,
    description     TEXT,
    target_type     VARCHAR(20) NOT NULL,   -- 'primary', 'random', 'healer', 'all', 'aoe_zone'
    damage_pct      NUMERIC(6,2),           -- % of target max HP
    cast_time_ms    INT NOT NULL DEFAULT 0,
    cooldown_ms     INT NOT NULL DEFAULT 0,
    interruptible   BOOLEAN NOT NULL DEFAULT FALSE,
    dispellable     BOOLEAN NOT NULL DEFAULT FALSE,
    applies_debuff  VARCHAR(50),
    effect_data     JSONB NOT NULL DEFAULT '{}'
);
```

---

## Appendix: Ticket Mapping

| Ticket | System | Key Deliverables |
|---|---|---|
| ETH-25 | Stormgate city | Zone records, NPC roster, portal nexus endpoint |
| ETH-26 | The Eternal Spire | `spire_runs` table, floor generation, leaderboard endpoint |
| ETH-27 | Lobby & Matchmaking | Queue API, role validation, SignalR ready-check |
| ETH-28 | NPC system | Behavior tree, spawn manager, vendor API |
| ETH-29 | Procedural dungeons | Seed-based room graph, room templates, secret rooms |
| ETH-30 | Level scaling | Scaling formula applied in combat service, catch-up XP |
| ETH-31 | Aggro system | Threat table, stance modifiers, leash reset, pack aggro |
| ETH-32 | Class roles | Stat derivation service, role validation in lobby, class data |
| ETH-33 | Affixes | `affixes` table, `affix_schedules`, ban matrix, weekly rotation |
| ETH-34 | Boss phase AI (Novice–Veteran) | `boss_phases` table, phase FSM, tank-swap trigger |
| ETH-35 | Boss phase AI (Elite–Legendary) | Mana drain mechanic, positional wipe logic, split-phase |
| ETH-36 | Boss phase AI (Raid) | `boss_abilities` table, world-first tracking, 20-player tuning |
