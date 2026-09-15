# Forensics playbooks

Forensics produces a cited diagnosis. It does not authorize a fix. Use only the
observation tools permitted by the owning OSM phase and `osm-unity-editor`.

## Diagnose a live runtime symptom

Use this mode when the symptom is currently observable.

1. Capture the matching live signal: a CPU profile for active CPU use, a heap
   snapshot for retained memory, or a timeline for a visual or scheduling
   glitch.
2. Reduce the artifact to a hot path, retainer chain, repeated loop, blocked
   thread, or another concrete mechanism. Use an isolated executor or a parser
   for large artifacts so raw data does not displace the reasoning context.
3. Confirm the mechanism with the least invasive permitted instrumentation.
   Do not rely on source inspection alone.
4. Map the observation to a source file, symbol, and line.
5. Return the captured signal, reduced finding, confirmation method, source
   mapping, artifact paths, and remaining uncertainty.

If live instrumentation is unavailable, say so and return the strongest
supported hypothesis. Do not label it confirmed.

## Diagnose an existing trace

Use this mode when the capture already exists. Treat the artifact as a fixed
dataset and do not recapture it unless the user separately asks.

1. Identify the format and choose a parser that preserves samples, frames,
   timestamps, or object edges.
2. Transform large data into a queryable form before reading it manually.
3. Find the dominant path: the frames with most time, the retainer chain to a
   garbage-collection root, or the thread and wait reason.
4. Resolve frames to source file, symbol, and line. If symbols are absent, state
   that source attribution is incomplete.
5. Compare with a paired capture when available. Without a pair, report the
   strongest hypothesis supported by the artifact, not a confirmed cause.
6. Return the format, reduced finding, source mapping, artifact paths, and
   confirmation level.
