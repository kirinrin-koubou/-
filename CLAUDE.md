# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This repository contains a single-file Japanese town-building browser game (`ともきのゲーム.html`) inspired by Animal Crossing. It has no build system, no package manager, and no tests — open the HTML file directly in a browser to run it.

The file `ホームページ作成` is an empty placeholder.

## Running the Game

Open `ともきのゲーム.html` directly in any modern browser. No server needed.

## Architecture

The entire game lives in one HTML file with inline CSS and JavaScript.

### World vs. Screen Coordinates

The world is 2400×1800 pixels. The canvas only renders the viewport (browser window size). Always convert between the two using `worldToScreen(x, y)`, which subtracts `viewport.x/y`. The camera is updated each frame by `updateCamera()` to center on the player with boundary clamping.

### Game State

All mutable state lives in the `game` object: `day`, `money`, `energy`, `animals`, `buildings`, `materials`, `tools`, `inventory`, `caughtFish`. The `player` object and `diyStation` object hold positions separately. `takenFlowers` is a `Set` of `"x,y"` keys for flowers the player has collected — it persists only for the browser session.

### Game Loop

`requestAnimationFrame`-based loop: `update()` → `move()` → `updateCamera()` → `draw()` → `updateUI()`. `updateUI()` rebuilds all sidebar DOM on every frame, so changes to `game` state are automatically reflected.

### Procedural Map

`seededRandom(x, y, seed)` generates deterministic pseudorandom values from tile coordinates. `drawNatureMap()` iterates only visible tiles (snapped to 100px grid). Trees and flowers are placed based on `seededRandom` thresholds (rand < 0.25 = tree, 0.25–0.45 = flower).

### Water / Collision

`isInWater(x, y)` defines all impassable water:
- Sea: 100px margin around all map edges
- River: sinusoidal path centered at x=1200 with ±60px width (`Math.sin(y/300)*150`)
- Ponds: 4 hardcoded circles at fixed world coordinates

Player movement in `move()` checks `isInWater` before updating position. Fish can only be caught when standing within 250px of water.

### Crafting

Recipes are defined in the `recipes` array. Crafting requires the player to be within 120px of the DIY station at (400, 300), checked by `isDIYStationNear()`. Tools (fishing rod) set flags in `game.tools` in addition to entering the inventory.

### Key Controls

| Key | Action |
|-----|--------|
| Arrow / WASD | Move |
| Space | Talk to nearby animals |
| B | Open build tab |
| Z | Pick flower (or fish if rod equipped) |
| X | Open inventory tab |
| Click on canvas | Place building (when build mode active) |
