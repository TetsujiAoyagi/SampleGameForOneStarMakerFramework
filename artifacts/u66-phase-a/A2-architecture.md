# U66 A2 independent architecture review

- reviewer: `/root/u66_a2_architecture`
- model: GPT-6 Astra (OpenAI), separate context
- input: A1 snapshot plus base `3244f3635c6e18fffc1bbc40d3126e6d1eee009a` to implementation head `4263a33ee0d5e75b67e82e6260ff556f12fd1dce`; Phase C conclusions excluded
- verdict: A1 revision 1 cannot be frozen unchanged. A revision 2 may preserve the implementation without structural redesign if exact package, serialization, source compatibility, and restore contracts are made explicit.

Recommendations:

1. Accept the exact resolved package set explicitly, including Ads 4.19.0, collab-proxy 2.13.6, VisualScripting 1.9.12, Multiplayer Center 2.0.1, tetgen 1.0.0, graph-authoring 1.0.0, profiling.core 1.0.3, and Searcher 4.9.5. Do not confuse UPM graph changes with OSM asmdef edges.
2. Preserve manifest and tracked lock as a pair and verify the four Git package hashes after restore. Defer adding SHA fragments to manifest URLs as a separate change.
3. Explicitly authorize the one-line `DebugProfilerView` TMP compatibility repair. It stays inside the existing display responsibility and adds no public API, dependency, owner, lifetime, or policy.
4. Explicitly authorize the exact URP Global Settings, Addressables settings, and Project Auditor serialization deltas while retaining visual/build verification as Phase C evidence.
5. No Game→Framework, Runtime/Editor, public API, SceneState, AssetOwner, SceneResource payload, asmdef, class, manager, or lifetime change was found. The generated lock remains one cohesive dependency graph and does not need splitting.
