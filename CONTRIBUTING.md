# Contributing

## Branches

Create every change from the latest `main`.

Allowed prefixes:

- `feature/` for player-facing work
- `fix/` for defects
- `chore/` for repository and tooling work
- `docs/` for documentation-only work
- `test/` for test-only work
- `release/` for release preparation

Do not use personal names, agent names, or tool names as branch prefixes.
The `codex/` prefix is explicitly prohibited.

Examples:

```text
feature/continuous-ring-input
feature/json-level-loader
fix/deadlock-false-positive
chore/repository-foundation
```

## Commits

Use concise imperative subjects. Conventional prefixes are encouraged:

```text
feat: add one-way gate transfer
fix: prevent rotation on a full ring
docs: define level catalog versioning
test: cover initial deadlock validation
chore: update Unity ignore rules
```

Keep generated Unity directories out of commits. Always include `.meta` files for tracked assets.

## Pull requests

- Keep the scope small enough to review.
- Explain player-visible behavior and technical tradeoffs.
- Include screenshots or video for visual changes.
- Add or update tests for gameplay-model changes.
- Run all repository validation commands.
- Do not merge with unresolved validation failures.
- Prefer squash merge and delete the source branch afterward.

## Unity project rules

- Use Unity `6000.4.0f1`.
- Keep Asset Serialization set to Force Text.
- Keep Version Control mode set to Visible Meta Files.
- Avoid editing the same scene or prefab from multiple branches.
- Keep gameplay state deterministic; physics and animation are presentation, not the source of truth.
- Never hand-edit generated Unity cache folders.

## Level-data rules

- Edit the versioned JSON catalog rather than hard-coding levels in scenes.
- Preserve stable level, ring, gate, and exit IDs.
- Do not change existing schema meaning without increasing `schemaVersion`.
- Run `python3 Tools/validate_levels.py` before opening a pull request.
