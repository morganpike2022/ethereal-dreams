# Ethereal Dreams MMORPG

A massively multiplayer online role-playing game backend built with C# ASP.NET Core and PostgreSQL.

## Overview

Ethereal Dreams is a fantasy MMORPG featuring real-time combat, deep character progression, guild warfare, a player-driven economy, and a vast open world with hundreds of zones to explore.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| API | C# ASP.NET Core 8 |
| Database | PostgreSQL 16 |
| Real-time | SignalR |
| Auth | JWT + Refresh Tokens |
| ORM | Entity Framework Core 8 |
| Caching | Redis |
| Messaging | RabbitMQ |
| Containerization | Docker / Docker Compose |

## Project Structure

```
Ethereal-Dreams-MMORPG/
├── docs/
│   ├── plan.md                  # Implementation plan (3 phases, 24 tickets)
│   └── database-schema.sql      # Full PostgreSQL schema
├── src/
│   └── MMORPG.Api/              # ASP.NET Core Web API
│       ├── Controllers/
│       ├── Models/
│       ├── Services/
│       ├── Data/
│       └── DTOs/
├── .gitignore
└── README.md
```

## Documentation

- [Implementation Plan](docs/plan.md) — 3 phases, 24 tickets covering full feature delivery
- [Database Schema](docs/database-schema.sql) — Complete PostgreSQL schema with all tables, indexes, and constraints

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 16](https://www.postgresql.org/download/)
- [Redis](https://redis.io/download/)
- [Docker](https://www.docker.com/get-started) (optional)

### Local Setup

```bash
# Clone the repository
git clone https://github.com/your-username/Ethereal-Dreams-MMORPG.git
cd Ethereal-Dreams-MMORPG

# Apply the database schema
psql -U postgres -d ethereal_dreams -f docs/database-schema.sql

# Configure connection strings
cd src/MMORPG.Api
cp appsettings.Development.json.example appsettings.Development.json
# Edit appsettings.Development.json with your DB credentials

# Run the API
dotnet run
```

The API will be available at `https://localhost:7001` and Swagger UI at `https://localhost:7001/swagger`.

## Key Features

- **Character System** — 6 classes, 50 levels, 200+ skills and abilities
- **Combat** — Turn-based + real-time hybrid with party mechanics
- **Guilds** — Hierarchy, guild wars, shared vaults, and territory control
- **Economy** — Player-driven auction house, crafting, and resource gathering
- **Quests** — Dynamic quest system with branching storylines
- **World** — 50+ explorable zones across 5 continents
- **PvP** — Duels, arena matches, and open-world PvP zones

## API Endpoints

| Group | Base Path |
|-------|-----------|
| Authentication | `/api/auth` |
| Characters | `/api/characters` |
| World / Zones | `/api/world` |
| Guilds | `/api/guilds` |
| Items & Inventory | `/api/items` |
| Quests | `/api/quests` |
| Combat | `/api/combat` |
| Marketplace | `/api/marketplace` |

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Commit changes: `git commit -m "feat: add your feature"`
4. Push: `git push origin feature/your-feature`
5. Open a pull request

## License

MIT License — see [LICENSE](LICENSE) for details.
