# Design and review playbooks

These playbooks supplement OSM Phase A and Phase C. They do not replace Phase
A2, freeze a HANDOFF, create an evidence bundle, or satisfy Phase C'.

## Sketch architecture alternatives

Use this mode when a new boundary or data shape has several credible designs.
Do not use it for mechanical work with an established pattern.

1. Ground the design in the existing runtime flow and historical constraints.
2. Write realistic caller usage first. Derive data types, signatures, and
   module boundaries from that usage.
3. Produce at least two structurally different sketches. If independent
   executors are available, give them the same frozen inputs and isolated
   outputs. Otherwise create the alternatives sequentially without letting one
   silently replace the other.
4. Reject or revise designs with these red flags:
   - a broad interface that hides little complexity;
   - transport, storage, or framework representation leaking to callers;
   - modules split by execution order instead of owned knowledge;
   - pass-through layers that add no policy or adaptation;
   - shared mutable state where separate ownership would work;
   - invariants repeated across callers instead of encoded once.
5. Compare candidates with a task-specific rubric. Prefer a smaller public
   contract, shorter trace path, explicit ownership, testable core logic, and
   data structures that match dominant access patterns.
6. Select one base. Record what was retained from alternatives and why other
   shapes lost. Do not average incompatible designs.

Return the caller usage, type and module sketch, load-bearing decisions,
accepted tradeoffs, alternatives, open questions, and first implementation
step. Phase A3 and the human owner decide whether it becomes the frozen plan.

## Run an arena

Use an arena when several attempts at the same judgment-sensitive artifact
would expose useful differences.

1. Define one artifact and three to six gradeable criteria.
2. Give every candidate the same prompt and source snapshot. Keep candidate
   outputs isolated.
3. Prefer distinct model families or providers when available. More candidates
   do not compensate for a weak rubric.
4. Read every candidate fully. An independent judge may score them while the
   lead reviewer performs a separate scoring pass.
5. Pick the most maintainable base criterion by criterion. Graft only coherent
   ideas that improve the base. Record rejected ideas and dropouts.
6. Verify the synthesized artifact against the original rubric.

If candidates diverge because the input is underspecified, refine the prompt
and rerun. Do not blend the disagreement into a vague compromise.

## Adversarially review a fixed change

Use this mode on a commit-fixed diff. OSM Phase C still owns review scope,
finding buckets, tests, and completion judgment.

1. Fix the implementation base and head. Package the complete diff, intent,
   surrounding context, and frozen acceptance conditions.
2. Give each reviewer the same package. Prefer different model families or
   providers and do not expose reviewers to each other's findings.
3. Ask reviewers to trace concrete execution paths through these lenses:
   correctness, root cause, dependency and ownership boundaries, verification,
   complexity, and security where applicable.
4. Require each finding to name severity, exact location, evidence, and the
   failed condition. Hypothetical inputs without a reachable path are not
   findings.
5. The lead reviewer deduplicates and classifies findings as act on, consider,
   noted, or dismissed. Consensus increases signal but does not replace
   evidence. A single traced correctness or security defect can still block.
6. Map accepted findings into the two OSM buckets: a defect that violates a
   frozen condition or standing contract, or input for a later slice.

Do not auto-apply review suggestions. A change to the frozen responsibility map
returns to Phase A.
