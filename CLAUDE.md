# CLAUDE.md — ともきのゲーム (Tomoki's Game)

## Project Overview

A self-contained, browser-based town-building / life-simulation game written in pure vanilla JavaScript. No build tools, no dependencies, no server required — just open the HTML file in a browser.

**Main file:** `ともきのゲーム.html` (~1164 lines, all-in-one HTML + CSS + JavaScript)

**Language:** Japanese throughout (UI, comments, commit messages, variable names for in-game text)

---

## Running the Game

```bash
# Open directly in browser — no build step needed
open ともきのゲーム.html
# or on Linux:
xdg-open ともきのゲーム.html
```

There is no package manager, no `npm install`, no compilation step. The game initializes automatically on page load via `updateUI()` and `gameLoop()`.

---

## Architecture

### Single-File Structure

All CSS, HTML, and JavaScript live in `ともきのゲーム.html`:

1. `<style>` block — UI styling (flexbox layout, sidebar, tabs, canvas)
2. `<body>` — HTML structure (header stats bar, canvas, sidebar panels, tab UI)
3. `<script>` block — All game logic (~1000+ lines of vanilla JS)

### Core Game State Object

```javascript
const game = {
  day: 1,
  money: 5000,
  energy: 100,
  animals: [],          // NPC residents (max 30)
  buildings: [],        // Placed structures
  materials: { fish, bug, wood, stone, flower },
  crafted: 0,
  tools: { fishingRod: false },
  caughtFish: {},       // Fish encyclopedia { name: true/false }
  inventory: []         // Max 40 items
};
```

### Key Constants

```javascript
MAP_WIDTH = 2400        // World width in pixels
MAP_HEIGHT = 1800       // World height in pixels
// Player starts at { x: 300, y: 300 }
// DIY Station at { x: 400, y: 300 }
```

### Architecture Patterns

- **Game Loop:** `requestAnimationFrame` → `gameLoop()` → `update()` + `draw()`
- **Camera/Viewport:** `updateCamera()` and `worldToScreen()` — camera follows player, clamped to world bounds
- **Procedural Terrain:** `seededRandom(x, y, seed)` — deterministic tile generation using `Math.sin`-based hash
- **Collision Detection:** `isInWater()` and beach zone checks prevent player entering water
- **Tab UI:** Six sidebar tabs toggled via `.active` CSS class
- **Notifications:** `showNotification(msg)` — auto-dismiss after 1.5s
- **Day Progression:** ~0.02% probability per frame (~1 real second = possible day tick)

---

## Game Systems

### Player Controls

| Key | Action |
|-----|--------|
| Arrow keys / WASD | Move player (3px per frame) |
| Space | Talk to nearby animal (+5 happiness) |
| B | Enter build mode |
| Z | Pick flower (near ground) or fish (near water + fishing rod) |
| X | Open inventory tab |
| `◀` / `▶` buttons | Toggle sidebar |

### Resource Gathering (`doActivity(type)`)

| Resource | Energy Cost | Yield |
|----------|-------------|-------|
| Bug | 15 | 2–4 |
| Wood | 20 | 1–2 |
| Stone | 25 | 1–2 |
| Flower | 10 | via `pickFlower()` |

### Crafting (`craft(name)`)

Requires player to be near the DIY Station (`isDIYStationNear()`).

| Recipe | Materials | Result |
|--------|-----------|--------|
| Chair / 椅子 | wood + flower | Craftable item |
| Lamp / 灯 | stone + bug | Craftable item |
| Statue / 像 | wood + stone | Craftable item |
| Fishing Rod / 釣竿 | wood + bug | Tool (`isTool: true`) |

Tools set `game.tools.fishingRod = true` AND are added to inventory (bug fix: commit `f6f0046`).

### Fishing (`fish()`)

- Requires `game.tools.fishingRod === true`
- Player must be near water (`isInWater()` check)
- 20 fish species with rarity-weighted spawning
- First catch of each species recorded in `game.caughtFish` (fish encyclopedia)
- Rarest fish: トラフグ (1% chance, 800 currency value)

### Buildings (`selectBuilding(type)`)

| Building | Cost | Emoji |
|----------|------|-------|
| House / 家 | 600 | 🏠 |
| Shop / 店 | 800 | 🏪 |
| Park / 公園 | 1000 | 🌳 |

Click canvas to place after selecting a building type.

### Animals / NPCs

- Max 30 animals tracked in `game.animals`
- New animals spawn on day 10, then every 40 days
- Space key near animal: `happiness += 5`
- Animals have emoji, name, x/y position, and happiness stat

---

## Rendering

### Draw Pipeline (called every frame)

1. `drawNatureMap()` — Base terrain (grass, trees, flowers via seeded RNG)
2. `drawBeach()` — Sand zones at world edges
3. `drawWater()` — Water areas (player cannot enter)
4. DIY Station, buildings, animals rendered as emoji via canvas `fillText`
5. Player character rendered
6. Minimap (toggleable overlay)

### Canvas Resizing

`resizeCanvas()` is called on `window` resize and sets canvas to fill viewport. The camera system ensures the world view stays consistent.

---

## Conventions

### Naming

- **JS variables/functions:** camelCase (`seededRandom`, `updateCamera`, `doActivity`)
- **Constants:** UPPER_SNAKE_CASE (`MAP_WIDTH`, `MAP_HEIGHT`)
- **HTML element IDs:** kebab-case (`#gameArea`, `#minimapToggle`)
- **In-game text/items:** Japanese with emoji prefix (`🎣釣竿`, `💡灯`)

### State Mutations

- All game state lives on the single `game` object
- UI is updated via `updateUI()` — call this after any state change that affects displayed stats
- Inventory additions go through `addToInventory(item)` — enforces the 40-item cap

### No Persistence

Game state is in-memory only. Refreshing the browser resets everything. `localStorage` / `sessionStorage` are not currently used.

### Flower Tracking

```javascript
const takenFlowers = new Set(); // Stores "x,y" strings of harvested flower tiles
```

---

## Git Workflow

- **Primary development branch:** `claude/add-claude-documentation-90Y74`
- **Remote:** `origin` (local proxy at `http://local_proxy@127.0.0.1:35859/git/kirinrin-koubou/-`)
- **Commit messages:** Written in Japanese
- **Commit signing:** SSH key signing enabled (key at `/home/claude/.ssh/commit_signing_key.pub`)

### Committing Changes

```bash
git add ともきのゲーム.html
git commit -m "変更内容の簡潔な説明"
git push -u origin claude/add-claude-documentation-90Y74
```

---

## What to Know Before Editing

1. **Single file** — All changes go in `ともきのゲーム.html`. Do not split into multiple files unless explicitly asked.
2. **No build step** — Changes are immediately testable in a browser.
3. **Japanese localization** — In-game text, item names, and commit messages should remain in Japanese.
4. **Emoji rendering** — Game elements use Unicode emoji rendered via Canvas `fillText`. Font fallbacks matter for cross-platform rendering.
5. **Seeded terrain** — Terrain is procedurally generated and deterministic. Changes to `seededRandom()` or terrain constants will alter the entire map layout.
6. **Tool vs item distinction** — When adding new craftable tools, use `isTool: true` in the recipe AND ensure the crafted object is added to `game.tools` AND to inventory via `addToInventory()`.
7. **Energy gating** — Player actions that cost energy should check `game.energy >= cost` before executing and deduct with `game.energy -= cost`.
8. **Camera bounds** — World positions must be converted via `worldToScreen()` before drawing to canvas. Never draw at raw world coordinates.
