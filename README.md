# GhostHats

[![Thunderstore Version](https://img.shields.io/thunderstore/v/IAteSpaghetti/GhostHats?style=for-the-badge)](https://thunderstore.io/c/peak/p/IAteSpaghetti/GhostHats/)
[![Thunderstore Downloads](https://img.shields.io/thunderstore/dt/IAteSpaghetti/GhostHats?style=for-the-badge)](https://thunderstore.io/c/peak/p/IAteSpaghetti/GhostHats/)
[![Thunderstore Likes](https://img.shields.io/thunderstore/likes/IAteSpaghetti/GhostHats?style=for-the-badge)](https://thunderstore.io/c/peak/p/IAteSpaghetti/GhostHats/)

Ghosts wear their player's hat. A small client-side cosmetic mod for PEAK.

This file is the developer side of things. The mod page copy, meaning what it does, how to install
it and how it behaves in multiplayer, lives in [thunderstore/README.md](thunderstore/README.md),
which is what ships in the package.

## How it works

`PlayerGhost.RPCA_InitGhost` is the buffered RPC every client runs when a dead player spawns their
spectator ghost, and it's where vanilla applies the rest of the ghost's cosmetics: skin, eyes,
mouth, accessory. There's no hat, because the ghost prefab has no hat objects at all. So a Harmony
postfix clones the owner's active `CustomizationRefs.playerHats[currentHat]` renderer, outfit hat
overrides included, onto the ghost and parents it to the ghost transform.

Nothing is synced. The clone is local to whoever is running the mod.

Own-ghost is skipped, mirroring vanilla hiding your own ghost from you.

### Placement

The ghost prefab's hierarchy isn't recoverable from a decompile, and the Unity name tables in
`resources.assets` are a jumbled pool rather than contiguous per prefab, so don't bother going that
way. That rules out anchoring to a named head bone.

Instead, placement is measured. Both the character and the ghost carry the same face pieces: two
eye renderers and a mouth renderer. Those define a frame with its origin at the eye midpoint, right
along the eye line, up from the mouth towards the eyes, and forward from their cross product. The
hat's transform is read in the character's face frame and re-applied in the ghost's, scaled by the
ratio of the two eye spacings.

Two consequences worth knowing:

- It self-calibrates. No magic numbers, and if the models change the measurement follows them.
- It only needs the char and ghost `EyeRenderers` arrays to agree on left/right ordering, not on
  any absolute convention. If both were flipped the mapping between frames would still hold. They
  do agree, confirmed in game.

The widest-apart pair of eye renderers is used, so extra shadow or overlay renderers in either
array don't throw the frame off.

There is deliberately **no config at all**: no BepInEx config file, no ModConfig tab. Placement
knobs would only give people a way to break a measurement that's already right, and an on/off
toggle for a mod this small is just a switch for "did you want the thing you installed". With
nothing bound, BepInEx never writes a `.cfg`, and ModConfig skips plugins with no visible entries.

Diagnostics go through `LogDebug` instead of a verbose-logging setting. That keeps them quiet by
default, and anyone chasing a problem can enable Debug in `BepInEx.cfg`. It logs the measured scale
ratio and resulting local position per ghost, which is what you want if placement ever looks off.

## Layout

| | |
|---|---|
| `src/Plugin.cs` | BepInEx entry point, Harmony bootstrap |
| `src/PlayerGhostPatch.cs` | the `RPCA_InitGhost` hook |
| `src/GhostHat.cs` | hat resolution, face-frame measurement, the clone |
| `thunderstore/` | manifest, 256x256 icon, changelog, package README |

## Building

```bash
dotnet build -c Release
```

Point `GameDir` at your PEAK install if it isn't at the csproj default. A `GhostHats.csproj.user`
with `<GameDir>...</GameDir>` works. The build copies `GhostHats.dll` into
`BepInEx/plugins/GhostHats`; pass `-p:SkipDeploy=true` to skip that. The copy fails while PEAK is
running, so use it if the game is open.

Requires BepInEx 5 (the PEAK BepInEx pack).

## Packaging

```bash
powershell -ExecutionPolicy Bypass -File .\package-thunderstore.ps1
```

Builds `artifacts/GhostHats-<version>.zip` and refreshes `release-assets/`, which is committed
because the GitHub runner can't build this. It needs PEAK's own DLLs.
`thunderstore/manifest.json` is the version source of truth.

Pushing a `v*` tag runs [.github/workflows/release.yml](.github/workflows/release.yml), which cuts
a GitHub release from `release-assets/` and, if the `THUNDERSTORE_API_TOKEN` secret is set, uploads
to Thunderstore. **[RELEASING.md](RELEASING.md) has the full sequence, including the manual step
that keeps releases anonymous.** Read it before tagging.
