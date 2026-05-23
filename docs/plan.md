# Ethereal Dreams MMORPG — Implementation Plan

3 phases, 24 tickets. Each ticket includes scope, acceptance criteria, and estimated effort.

---

## Phase 1: Foundation & Infrastructure (Tickets 1–8)

Goal: A working API skeleton with authentication, character creation, and database connectivity.

---

### Ticket 1 — Project Scaffolding & DevOps Setup

**Scope**
- Initialize ASP.NET Core 8 Web API project
- Configure Docker and Docker Compose (API, PostgreSQL, Redis)
- Set up GitHub Actions CI pipeline (build + test)
- Configure Serilog structured logging

**Acceptance Criteria**
- `dotnet run` starts the API with Swagger UI
- `docker compose up` starts all services
- CI pipeline passes on every push to `main`

**Effort:** 3 points

---

### Ticket 2 — PostgreSQL Database Schema & Migrations

**Scope**
- Apply the full schema from `docs/database-schema.sql`
- Set up Entity Framework Core with code-first migrations
- Configure connection pooling via Npgsql
- Seed reference data (character classes, skill trees, item types, zones)

**Acceptance Criteria**
- All tables created with correct FK constraints and indexes
- `dotnet ef migrations add InitialCreate` succeeds
- Seed data present after `dotnet ef database update`

**Effort:** 5 points

---

### Ticket 3 — Authentication System (JWT + Refresh Tokens)

**Scope**
- Player registration endpoint with bcrypt password hashing
- Login endpoint returning JWT access token (15 min) and refresh token (7 days)
- Refresh token rotation endpoint
- Logout and token revocation

**Acceptance Criteria**
- `POST /api/auth/register` creates a player account
- `POST /api/auth/login` returns valid JWT + refresh token
- `POST /api/auth/refresh` issues a new token pair, invalidates old refresh token
- `POST /api/auth/logout` marks refresh token as revoked

**Effort:** 5 points

---

### Ticket 4 — Player Profile Management

**Scope**
- Get and update player profile (username, email, avatar)
- Email verification flow
- Account deletion (soft delete)
- Admin: list and ban players

**Acceptance Criteria**
- `GET /api/players/{id}` returns player profile
- `PATCH /api/players/{id}` updates allowed fields
- Banned players receive 403 on protected endpoints

**Effort:** 3 points

---

### Ticket 5 — Character Creation & Management

**Scope**
- Create character (name, class, appearance)
- List characters per account (max 5)
- Delete character (with 24-hour recovery window)
- Character select screen data endpoint

**Acceptance Criteria**
- `POST /api/characters` creates a new character with starting stats for selected class
- `GET /api/characters` returns all characters for the authenticated player
- Duplicate character names within same account are rejected

**Effort:** 5 points

---

### Ticket 6 — Core REST API & Middleware

**Scope**
- Global error handling middleware with RFC 7807 Problem Details
- Request validation with FluentValidation
- Rate limiting per IP and per account (Aspire rate limiting)
- API versioning (`/api/v1/`)
- Correlation ID middleware

**Acceptance Criteria**
- Invalid requests return structured error responses
- Rate limit exceeded returns 429 with `Retry-After` header
- All responses include `X-Correlation-Id` header

**Effort:** 3 points

---

### Ticket 7 — World & Zone Data Models

**Scope**
- Zone definitions with terrain types, level ranges, and PvP flags
- Zone transition (portal) system
- Character location tracking (zone + coordinates)
- Zone population counts via Redis

**Acceptance Criteria**
- `GET /api/world/zones` returns all zones with metadata
- `GET /api/world/zones/{id}` returns zone detail including connected zones
- Character location updates stored and retrievable

**Effort:** 3 points

---

### Ticket 8 — Testing Framework & Quality Gates

**Scope**
- xUnit project for unit tests
- Integration tests using `WebApplicationFactory` + Testcontainers (PostgreSQL)
- Test coverage reporting (Coverlet → GitHub Actions summary)
- Minimum 70% coverage gate in CI

**Acceptance Criteria**
- `dotnet test` runs all tests
- Integration tests run against a real PostgreSQL container
- Coverage report generated and uploaded as CI artifact

**Effort:** 3 points

---

## Phase 2: Core Gameplay (Tickets 9–16)

Goal: Fully playable game loop — combat, inventory, quests, leveling, and real-time communication.

---

### Ticket 9 — Combat System

**Scope**
- Turn-based combat state machine with round resolution
- Damage calculation engine (base stats + modifiers + RNG)
- Status effects (poison, stun, burn, heal-over-time)
- PvE combat: fight NPCs in zones
- Combat log persistence

**Acceptance Criteria**
- `POST /api/combat/engage` starts a combat session
- `POST /api/combat/{sessionId}/action` submits a player action
- Combat resolves correctly with correct damage ranges
- Dead NPCs respawn after configurable timer

**Effort:** 8 points

---

### Ticket 10 — Inventory & Item System

**Scope**
- Player inventory (bag slots, weight limits)
- Item pickup, drop, and transfer
- Item stacking for consumables
- Equipment slot system (head, chest, legs, hands, feet, weapon, offhand, ring ×2, neck)
- Item tooltips endpoint (full stat breakdown)

**Acceptance Criteria**
- `GET /api/characters/{id}/inventory` returns all carried items
- `POST /api/characters/{id}/equip` equips an item to the correct slot
- Overweight characters receive movement penalty flag
- Equipped items contribute stat bonuses to character sheet

**Effort:** 5 points

---

### Ticket 11 — Quest System

**Scope**
- Quest definitions (kill, collect, explore, escort, delivery types)
- Quest acceptance, progress tracking, and completion
- Quest rewards (XP, gold, items)
- Daily and repeatable quest flags
- Quest prerequisites and chains

**Acceptance Criteria**
- `GET /api/quests/available` returns quests the character qualifies for
- `POST /api/quests/{id}/accept` adds quest to character's active quest log
- Quest progress updates automatically on relevant game events
- `POST /api/quests/{id}/complete` grants rewards and advances quest chain

**Effort:** 8 points

---

### Ticket 12 — Character Progression & Skill System

**Scope**
- XP gain from combat, quests, crafting
- Level-up calculation and stat increases per class
- Skill tree with unlock prerequisites
- Ability cooldown tracking
- Character sheet endpoint (full stats, skills, buffs)

**Acceptance Criteria**
- Characters level from 1 to 50
- Each level grants stat points and unlocks skill tree nodes
- `GET /api/characters/{id}/sheet` returns full character state
- Cooldown enforcement: abilities cannot be used before cooldown expires

**Effort:** 5 points

---

### Ticket 13 — Real-time Communication (SignalR)

**Scope**
- SignalR hub for game events (combat updates, chat, zone events)
- Zone-scoped channel groups
- Party chat and whisper (private message)
- Server-to-client push for NPC actions and world events

**Acceptance Criteria**
- Clients receive real-time combat round results without polling
- Zone chat messages delivered to all players in the same zone
- Whisper delivered only to target player
- Connection authenticated via JWT

**Effort:** 8 points

---

### Ticket 14 — Party System

**Scope**
- Party creation, invite, and kick
- Party leader transfer
- Shared XP distribution model
- Party-wide buff effects
- Party member location sharing

**Acceptance Criteria**
- `POST /api/parties` creates a party (max 5 members)
- Party invites delivered via SignalR in real time
- XP split equally (or by contribution weight) among living members
- Party disbands when leader leaves with no transfer

**Effort:** 5 points

---

### Ticket 15 — NPC & Enemy System

**Scope**
- NPC type definitions (vendor, quest giver, enemy, boss)
- Spawn point management with respawn timers
- Basic AI behavior tree (patrol, aggro, flee)
- Vendor NPC buy/sell endpoints
- Boss encounter mechanics (phases, enrage timer)

**Acceptance Criteria**
- NPCs appear in correct zones based on configuration
- Enemy NPCs aggro on nearby players below the NPC's level threshold
- `POST /api/npcs/{id}/buy` purchases item from vendor NPC
- Boss phases transition at configured HP thresholds

**Effort:** 8 points

---

### Ticket 16 — Crafting System

**Scope**
- Recipe definitions (required ingredients + skill level)
- Crafting station types (forge, alchemy bench, enchanting table)
- Success/fail/critical success RNG based on character skill
- Crafted item quality tiers (common, uncommon, rare, epic, legendary)
- Ingredient consumption on crafting attempt

**Acceptance Criteria**
- `GET /api/crafting/recipes` returns recipes the character can learn
- `POST /api/crafting/craft` consumes ingredients and produces item
- Critical success produces item one quality tier higher
- Skill XP granted on each crafting attempt

**Effort:** 5 points

---

## Phase 3: Advanced Features & Polish (Tickets 17–24)

Goal: Endgame content, social systems, economy, admin tooling, and production readiness.

---

### Ticket 17 — Guild System

**Scope**
- Guild creation, disbanding, and management
- Roles: Guild Master, Officer, Member, Recruit
- Guild bank with permission-gated access
- Guild XP and level system (max guild level 25)
- Guild roster and activity feed

**Acceptance Criteria**
- `POST /api/guilds` creates a guild (unique name required)
- Guild Master can promote/demote members
- `GET /api/guilds/{id}/bank` returns bank contents (role-gated)
- Guild XP accumulates from member activity

**Effort:** 8 points

---

### Ticket 18 — PvP System

**Scope**
- Duel system (challenge and accept)
- Rated arena (2v2, 3v3) with matchmaking queue
- Open-world PvP zone flagging
- PvP rating (ELO) tracking
- Season leaderboards

**Acceptance Criteria**
- `POST /api/pvp/duel/challenge` sends a duel request
- Arena queue places players in rated matches with similar ELO
- PvP-flagged characters can be attacked in PvP zones
- `GET /api/pvp/leaderboard` returns top 100 players by rating

**Effort:** 8 points

---

### Ticket 19 — Economy & Marketplace

**Scope**
- Auction house: list, bid, buyout, cancel
- Auction expiry (24h / 48h seller choice)
- Gold currency: earn, spend, transfer (with transaction fee)
- Price history endpoint for market analysis
- Anti-duplication and anti-exploit validation

**Acceptance Criteria**
- `POST /api/marketplace/listings` creates an auction listing
- `POST /api/marketplace/listings/{id}/bid` places a bid
- Expired listings auto-return items to sellers via mail
- Gold sinks enforced (listing fees, buyout tax)

**Effort:** 8 points

---

### Ticket 20 — Achievement System

**Scope**
- Achievement definitions with progress tracking
- Achievement categories (combat, exploration, crafting, social, PvP)
- Achievement rewards (titles, cosmetic items, achievement points)
- Account-wide vs character-specific achievements

**Acceptance Criteria**
- Achievements trigger automatically on qualifying events
- `GET /api/characters/{id}/achievements` returns earned and in-progress achievements
- Titles unlock on achievement completion and can be equipped
- `GET /api/achievements/leaderboard` ranks players by total achievement points

**Effort:** 5 points

---

### Ticket 21 — Social Features

**Scope**
- Friends list (add, remove, block)
- In-game mail system with item/gold attachments
- Global, zone, trade, and LFG chat channels
- Player reporting system
- Ignore list enforcement across all social features

**Acceptance Criteria**
- `POST /api/social/friends/{playerId}` sends a friend request
- `POST /api/mail/send` sends mail with optional item/gold attachment
- Blocked players cannot send messages, mail, or party invites
- Chat messages stored for 30 days for moderation

**Effort:** 5 points

---

### Ticket 22 — Admin Dashboard API

**Scope**
- Admin role with scoped permissions
- Player lookup, ban/unban, and note system
- Item grant and gold adjustment tools
- Server metrics endpoint (active sessions, zone populations)
- Audit log for all admin actions

**Acceptance Criteria**
- Admin endpoints protected by `[Authorize(Roles = "Admin")]`
- All admin actions recorded in `admin_audit_log` table
- `GET /api/admin/metrics` returns real-time server stats
- Ban system supports temporary and permanent bans with reason

**Effort:** 5 points

---

### Ticket 23 — Performance Optimization & Caching

**Scope**
- Redis caching for zone data, item definitions, and character sheets
- Database query optimization (index review, N+1 elimination)
- Response compression (Brotli + gzip)
- Background job queue (Hangfire) for mail delivery, auction expiry, respawns
- Load testing with k6 (target: 1,000 concurrent users)

**Acceptance Criteria**
- p99 API response time < 200ms under 500 concurrent users
- Cache hit rate > 80% for item and zone lookups
- k6 load test report included in CI artifacts
- No N+1 queries on character sheet or inventory endpoints

**Effort:** 8 points

---

### Ticket 24 — Deployment & CI/CD Pipeline

**Scope**
- Production Docker images (multi-stage, minimal base)
- Kubernetes manifests (Deployment, Service, Ingress, HPA)
- GitHub Actions: build → test → push image → deploy to staging
- Secrets management via Kubernetes Secrets + Azure Key Vault
- Monitoring: Prometheus metrics + Grafana dashboard
- Health check endpoints (`/health/live`, `/health/ready`)

**Acceptance Criteria**
- `git push` to `main` triggers full CI/CD pipeline
- Zero-downtime rolling deployment on Kubernetes
- `/health/ready` returns 503 until DB connection is confirmed
- Grafana dashboard shows request rate, error rate, and latency

**Effort:** 8 points

---

## Summary

| Phase | Tickets | Total Points |
|-------|---------|-------------|
| Phase 1: Foundation | 1–8 | 30 |
| Phase 2: Core Gameplay | 9–16 | 52 |
| Phase 3: Advanced Features | 17–24 | 55 |
| **Total** | **24** | **137** |

Assuming 2-week sprints at 20 points/sprint capacity: approximately **7 sprints (14 weeks)** to full delivery.
