# Investigation playbooks

Choose one mode. All modes are read-only unless the user separately asks for a
change and the owning OSM phase permits it.

## Explain how a subsystem works

Use this mode for runtime flow, ownership, layering, and placement questions.

1. State the question and scope. If the scope is ambiguous, state the working
   interpretation and continue.
2. Find the entry point and trace the real call path. Read implementations,
   types, data transformations, and boundaries. Do not infer behavior from
   names alone.
3. For a large subsystem, split exploration into distinct angles. Independent
   executors may inspect them in parallel. A single investigator can inspect
   them sequentially when parallel execution is unavailable.
4. Reconcile overlaps and contradictions against the code.
5. Explain the overview, key concepts, flow, file map, and non-obvious risks.
   Omit sections that add no value. Cite exact symbols and paths.

Keep open questions visible. A gap is better than an invented connection.

## Explain why code has its current shape

Use this mode for design rationale, regressions, thresholds, and historical
constraints. Code proves mechanics, not author intent.

1. Anchor the question in target paths, symbols, line ranges, and recent
   commits.
2. Search source history with `git log --follow`, `git blame`, pickaxe, commit
   bodies, review discussion, tests, and in-repository design records.
3. Search other evidence sources that are available and relevant, such as an
   issue tracker, long-form documents, team discussion, runtime observations,
   error tracking, or product data. Record unavailable categories as gaps.
4. Keep exact searches and citations. Surface contradictions instead of
   selecting the tidier story.
5. Classify each conclusion:
   - **Direct:** a source explicitly states the reason.
   - **Supported:** several indirect sources converge.
   - **Inferred:** a reasonable interpretation with no explicit statement.
   - **Speculative:** one of several plausible explanations.
   - **Unknown:** the searched evidence does not answer the question.
6. Put direct and supported claims next to citations. Hedge inferred and
   speculative claims. State what remains unknown and what was searched.

If the investigation feeds a change plan, finish with Preserve, Change, Avoid,
and Risk constraints. The owning HANDOFF decides whether to adopt them.

## Prove a change's blast radius

Use this mode when a small diff may affect behavior outside its visible callers.

1. Read the complete diff and its history. State what behavior changed.
2. Find the one or two facts on which safety depends.
3. Trace beyond symbol search: lifecycle order, serialized data, wire formats,
   pinned dependency behavior, feature flags, and downstream readers.
4. Separate confirmed risks from checked and cleared risks. Give each risk a
   concrete failure path, likelihood, impact, and evidence location.
5. Prove each load-bearing safety fact as far as the phase permits:
   - source line;
   - failure-path trace;
   - deterministic script or test using the real code;
   - observation in the running product.
6. Mark any fact that cannot reach executable proof as unproven. Never convert
   a convincing explanation into proof.

The OSM phase owns which commands may run. In Phase B, do not use a stronger
proof step when it violates the Unity or test restrictions.

Return what changed, the load-bearing safety fact and proof level, confirmed
risks, cleared risks, and the cheapest permitted pre-merge check.
