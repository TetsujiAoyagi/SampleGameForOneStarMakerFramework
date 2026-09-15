# Writing and evidence playbooks

## Edit technical writing

Choose the document's purpose before editing:

- Tutorial: a learner builds something and sees results at each step.
- How-to: a competent reader follows steps to a goal.
- Reference: facts, options, limits, and errors for lookup.
- Explanation: context, rationale, constraints, and alternatives.

Keep one primary purpose per document. Split and link when action and explanation
would otherwise compete.

Apply these checks:

1. Use the repository's exact symbols, paths, flags, and commands.
2. Put conditions before instructions. Write instructions as direct commands.
3. Give each sentence one main thought. Split dense sentences.
4. Prefer active voice and short, ordinary words.
5. Use one name for one thing. Do not cycle synonyms.
6. Make pronouns and modifiers point to one unambiguous target.
7. Use sentence-case headings. Use numbered lists for sequences and bullets for
   unordered sets.
8. Remove filler, vague attribution, unsupported certainty, decorative
   formatting, forced groups of three, generic conclusions, and chatbot stock
   phrases.
9. Replace abstract metaphors with the concrete mechanism, value, or action.
10. Verify every path, count, command, and link against the current commit.

Preserve the author's intended tone. Do not flatten deliberate voice into short
uniform sentences.

## Keep an optional decision trail

Use a decision trail only for long-running work where a later reviewer needs to
understand forks, pivots, and verification. It supplements the HANDOFF. It is
not the Phase A snapshot, Phase B result, evidence bundle, or C' blind bundle.

Use a single append-only TSV with these columns:

```text
ts	phase	decision	why	evidence	result
```

- `ts`: ISO 8601 timestamp.
- `phase`: owning OSM phase or workstream.
- `decision`: one concrete choice or checkpoint.
- `why`: plain-language reason.
- `evidence`: a commit, file and line, test log, trace, or artifact path.
- `result`: observed outcome such as `tests green`, `reverted`, `open`, or
  `INCONCLUSIVE`.

Keep cells on one line. Prefix a cell that starts with `=`, `+`, `-`, or `@`
with a single quote before opening the TSV in spreadsheet software. Log
decisions, not every command. A later correction appends a new row and never
rewrites history.

At handoff, verify that every row maps to a real action and that every evidence
pointer resolves. Record review independence limits. Do not claim a second
reviewer when none was available.
