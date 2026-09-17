# BS2b Phase C / C' evidence manifest, implementation v2

- Frozen: 2026-09-17 09:43 JST
- Implementation base: `ef3ad21`
- Implementation head: `c0fff31`
- Test target: Unity 6000.6.0f1, StandaloneWindows64 Player, Editor test runner
- Review inputs: This manifest and the listed files. C and C' receive identical inputs. No review conclusions are included.
- Earlier bundle for `78cd163` and its review results are superseded by this head.

| File | SHA-256 |
| --- | --- |
| `phase-a-snapshot.md` | `d18f4af428c18cb4ac1ee3807d9f8ea051d3f1906972c8685875ac0888f7da41` |
| `phase-b-result-v2.md` | `34408e677b340aeacf2995392b9624f1f30b67dd07c1a2ee2f5052db4999df4c` |
| `implementation-v2.diff` | `ce2b34d872b5d673bdae212d0b0e3dac72e1b418bf651a0c8821f20bb9ad6dfb` |
| `implementation-v2.stat` | `dc0f8e52f0d2a14222ad9528394dde79ba580d56cb0a9acfe822a499344b5a8e` |
| `implementation-v2.name-status` | `93107dfb514af3c6b010de2663b460ca4fad596ba48f4d24d9536dae211c345e` |
| `results-all-v2.xml` | `ae29e1050cc6fa024a7a047d36732e8333d1419686cc16a1f4aa5814c5f9e8aa` |
| `unity-all-v2.log` | `033bfd62cbf410d3f450f63ec55f3c377fc074175fb37601cd79df1bdf2bf81f` |
| `preflight-first-v2.json` | `feac01e63c43ff8405b5d6816b30486e1b20c2891435c00c039828956d232d0b` |
| `outcome-first-v2.json` | `aef98693b7e25b1a5646e19a4b3c426317f00d822572910f4ba9d89e9d77354a` |
| `preflight-second-v2.json` | `2334134b093404edc60f65aa03805d7057332ae158b3ce992e806101da936cd5` |
| `outcome-second-v2.json` | `febe1f2b80479b6fcc11fb8700644cff96948d66c27c8fff9bc438048126cb23` |

## Machine checks

- `pwsh -NoProfile -File tools/contract-audit.ps1`, 2026-09-17 09:43 JST: exit 0; changed Unity C# files 9; no machine detectable violations.
- `pwsh -NoProfile -File tools/run-tests.ps1 -Filter OneStarMaker.Tests.Editor.Build.BuildContent`, 2026-09-17 09:24–09:26 JST: raw XML in `TestResults/`, 5 passed, 0 failed.
- `pwsh -NoProfile -File tools/run-tests.ps1`, 2026-09-17 09:33–09:39 JST: Unity exit 0; raw XML total 761, passed 761, failed 0, skipped 0. The integration test includes two real builds in the same workspace.
- `git diff --check`, 2026-09-17 09:43 JST: exit 0 (line ending warning only).
- First successful build identity: `20260917T003406945Z-7d2dde5a555244d382c823057239c368`.
- Second successful build identity: `20260917T003428568Z-d8ceaaec753243b59645deb0590be679`.

The raw XML and Unity log are unchanged copies. The committed implementation diff is the review target, separate from later HANDOFF records.
