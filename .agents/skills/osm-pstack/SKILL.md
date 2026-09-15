---
name: osm-pstack
description: >-
  Use when explicitly asked to apply OSM's pstack-derived playbooks for code
  archaeology, blast-radius proof, competing design sketches, adversarial
  review, runtime or trace forensics, technical-writing cleanup, or a decision
  trail. Do not invoke for ordinary implementation, routine review, or general
  prose editing.
---

# OSM pstack playbooks

Use these optional playbooks under the existing OSM workflow. They add
investigation and review techniques. They do not own an OSM phase.

## Precedence

Apply instructions in this order:

1. `AGENTS.md`.
2. `osm-workflow` and `osm-unity-editor`.
3. The frozen HANDOFF for the current slice.
4. This skill and its references.

If a playbook conflicts with a higher item, stop using the playbook. Do not
reinterpret the higher contract.

In particular, this skill never authorizes Unity Editor startup, Unity tests,
`tools/run-tests.ps1`, Addressables builds, Player builds, commits, pull
requests, external writes, or expanded scope. Phase B keeps every prohibition
from `AGENTS.md` and `osm-workflow`.

## Select one playbook

Read only the reference needed for the request:

- For a subsystem walkthrough, historical rationale, or change blast radius,
  read [investigation](references/investigation.md).
- For competing architecture sketches or adversarial review, read
  [design and review](references/design-and-review.md).
- For a live symptom or an existing profiling artifact, read
  [forensics](references/forensics.md).
- For technical prose cleanup or an optional decision trail, read
  [writing and evidence](references/writing-and-evidence.md).

Do not chain every playbook by default. Name the selected playbook in the work
update and return to the owning OSM phase when the playbook finishes.

## Execution independence

The playbooks describe roles and evidence, not a product API. When independent
execution would improve confidence, use any available independent executor.
Prefer a different model family or provider for judgment-sensitive comparison.
Give each executor the same frozen input and keep their work isolated.

If independent execution is unavailable, complete the work sequentially and
record the independence limit. Never claim an independent review occurred.

## Removal

This directory owns the complete adaptation, including attribution. Removing
`.agents/skills/osm-pstack/` removes it without changing other OSM skills.
