# Copilot-only wl

## Summary

Make wl a workspace launcher exclusively for GitHub Copilot CLI. Remove Claude
execution, tool selection, and compatibility migrations. Keep the existing
workspace workflow, command names, path variables, prompts, and npm distribution.
This is a breaking release; recommend v0.9.0 with prominent upgrade notes.

## Decisions

- Copilot is the only supported runtime, not merely the default.
- The user chose a clean break: existing workspaces require manual updates.
- Do not migrate, delete, or rewrite old workspace files or global CLI settings.
- Implementation was approved after review of the plan. Publishing still requires separate approval.

## Workspace contract

- Require explicit `schemaVersion: 2` in `workspace.json`. Missing, older, or
  unsupported versions produce an error with the file path and upgrade guidance.
- Retain `name`, `primaryRepo`, `additionalDirs`, `yolo`, and `resume`; remove `tool`.
  Reject `tool`, even when set to `copilot`, rather than silently ignoring it.
- Remove `.config.json` tool selection. If an existing file contains
  `defaultTool`, report how to remove that obsolete setting before launch/create.
- Store skills under `<workspace>\.copilot\skills` and `.shared\.copilot\skills`.
  Continue using explicit `--plugin-dir` arguments, with generated `plugin.json`
  files in those `.copilot` folders; do not rely on implicit skill discovery.
- Keep `instructions.md` as the editable source and its generated `AGENTS.md`
  mirror, using the existing custom-instructions environment variable.
- Keep `.last-session` but store the new session's explicit Copilot UUID as plain
  text, retaining name-based resume for existing plain-text pointers. Reject old
  per-tool JSON maps when resuming; never interpret Claude IDs as Copilot sessions.

## User flows and errors

- `wl create` invokes Copilot with the create-workspace skill; `--basic` writes a
  version-2 workspace without invoking an AI. Neither command accepts `--tool`.
- `wl launch` always runs Copilot. Preserve `--new`, `--resume`, `--yolo`, prompts,
  additional directories, named fresh sessions with explicit UUIDs, and resume-by-reference behavior.
- `wl which` remains non-mutating and describes the same launch preparation.
  `wl setup` installs only the new shared skills and checks only Copilot.
- Validate legacy configuration before launch preparation or automatic setup can
  change files. `list` identifies incompatible workspaces instead of hiding them;
  `edit` still opens them so users can repair them.
- Missing Copilot, invalid configuration, and rejected legacy resume data produce
  actionable errors and nonzero exits. Explicit resume never silently starts fresh.
- Detect remaining legacy `.claude\skills` on relevant workspace/shared paths and
  explain the manual move; do not silently omit skills or load both layouts.

## Manual upgrade

Back up existing workspace files. Set `schemaVersion` to 2, remove `tool` and the
machine-local `defaultTool` setting, and move workspace/shared skills from
`.claude\skills` to `.copilot\skills`. Run `wl setup` to refresh bundled skills.
For session history, either copy the old map's `copilot` value into `.last-session`
as plain text, or remove that pointer and start fresh. Claude conversations cannot
be resumed through Copilot. Old generated plugin manifests can be removed manually;
update workspace-repository ignore rules for the new generated manifest paths.

## Architecture and affected surfaces

- `Program.cs`: remove tool flags, registry wiring, and tool-default resolution.
- `Services`: retain the proven Copilot argument/preparation logic as a concrete
  service; remove `ClaudeAdapter`, `IToolAdapter`, `ToolAdapterRegistry`, and the
  tool-only `ConfigService`/`WlConfig`. Rename `ClaudeRunner` to `CopilotRunner`.
  Preserve argument-list escaping; report child-process failures faithfully.
- `Workspace`, `WlPaths`, JSON serialization, launch/which/create/setup handlers:
  enforce the new contract and layout. Remove legacy migrations from `SetupService`.
- Embedded create/update skills: remove Claude assumptions, tool detection, and
  slash-command guidance; use Copilot instructions and the new skill paths.
  Keep installed skill copies synchronized when implementing.
- README, npm descriptions, examples, and release notes: document Copilot-only
  requirements and the manual upgrade without rewriting historical release notes.
- Unit/E2E coverage: replace Claude shims with Copilot shims; cover fresh/resumed
  launches, instruction/plugin preparation, rejected legacy inputs, and error exits.
  Isolate E2E workspace storage from the real user profile on every platform.

## Trade-offs

One runtime removes unused abstraction and unmaintainable Claude behavior. The
explicit schema boundary costs users a manual upgrade, but prevents old Claude
workspaces from unexpectedly launching Copilot or losing skills silently.
Preserve inherited custom-instruction directories, generate bounded plugin names
with stable hashes, and use UUIDs so session renames do not break resume.
Skill `allowed-tools` metadata is optional permission pre-approval, not a required
field. Named-skill prompts may include `/skill-name`. No blocking questions remain.
