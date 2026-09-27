# BuildSystem Artifact Storage Program

## 0. Metadata

- type: `program`
- status: program進行中。r1境界凍結済み、ローカル段1（スライス0）の実装・検証完了、Phase D承認済み。後続スライスは未凍結。r2案はA2指摘反映済み・再検討用・未承認。
- program policy revision: `r1` — A3 boundary freeze; individual implementation slices are not frozen
- proposed revision: `r2-draft` — Evidence-first sequence and first-use milestone; A2 feedback incorporated; revised draft awaits re-review and A3 approval
- branch: `docs/artifact-evidence-first-plan`
- implementation base commit: `0ba1961506c8f34c2cb4e1dd5efd35c9562bcc54` (`develop`)
- implementation head commit: not applicable; no implementation changes are planned in this program document
- risk: `normal`
- owner: OSM maintainers
- created: 2026-09-26
- expires: 2026-12-26
- harvest to: `docs/README.md` only if it remains a current workflow; otherwise delete after the program

## 1. Purpose and current state

Choose and establish one artifact workflow that supports BuildSystem outputs and review Evidence, can be used by local work and remote Cursor / Codex agents, and does not require a human to move files between agents. Keep large binary payloads out of Git history; keep a small, stable pointer and hashes in Git.

This is a multi-slice program, not an implementation HANDOFF. Revision r1 freezes only the storage, responsibility, and security boundaries listed below, following the supplied freeze review and the owner's request to incorporate it. Detailed designs and acceptance proposals for individual slices still require their own Phase A review/freeze. No R2 connectivity or cloud capability is declared proven by this freeze. The r2-draft in section 3 proposes a usable Evidence milestone before full BuildSystem UX integration; it does not replace the r1 freeze or authorize implementation before review and owner approval.

The owner expects most reads and writes to be performed by local Windows agents. Prioritize a usable local credential store and transfer tool. Cloud agents are intended consumers and occasional producers through task-scoped grants, but R2 support is unverified; a continuously available cloud credential broker is not a prerequisite for the local workflow.

Unity is a replaceable build backend. The primary user/agent interface is an engine-independent CLI outside Unity; no Unity GUI, Editor installation, or Unity assemblies are required to publish, fetch, inspect, or manage credentials for already-produced files. Engine-specific build execution may require its engine, but that dependency stays in its backend adapter. A future engine replacement must not require rewriting artifact storage or its user interface.

### Verified so far

- The repository is public. A synthetic GitHub Release probe was published at `artifact-probe-20260926` with a PNG, text log, ZIP, and SHA-256 manifest. Local unauthenticated download, hash verification, ZIP extraction, and content inspection passed.
- Cursor Cloud downloaded all four probe files without GitHub authentication, verified all three payload hashes, compared ZIP contents to individually downloaded files, read marker `OSM-PROBE-20260926`, and opened the PNG. It reported the blue rectangle, orange circle, white background, and text `OSM PROBE 726`. Its worktree remained clean.
- Codex Cloud could not reach GitHub. Its configured HTTPS proxy returned `403` to the CONNECT request; direct access also failed because DNS resolution was unavailable. The agent did not download or inspect any file. This is an outbound network policy failure, not evidence of a GitHub permission failure.
- The Cloudflare account has an R2 Standard bucket named `osm-artifacts` in APAC. It is currently private, contains only the zero-byte `probe/` prefix marker, and has no uploaded payloads. No R2 API token has been created. The R2 public development URL is disabled.
- The same synthetic files are available from the GitHub Release probe for transport testing; that release is not the proposed long-term store for frequent Build / Evidence runs.

## 2. Decision boundary

### Frozen program boundaries (r1)

- Local question (slices 0, local 1, and 2): can a local agent transfer to the private store without changing the repository and verify byte identity against the trusted ledger hash? Remote capability is not a condition for answering this question.
- Separate cloud question (cloud part of slice 1, per platform): can Cursor Cloud or Codex Cloud independently perform the same operations without recurring human grant/file handling? Record each platform and read/write capability separately. Codex's current 403 does not block the local question; no unattended cloud capability is frozen as established.
- Storage direction: use R2 as the routine object store; reserve GitHub Releases for named distribution versions. The uploader must not modify the repository. Staging defaults to a private directory outside the checkout and outside sync folders; an explicitly selected alternative must be verified as ignored and safe before use. `artifacts/` as a whole is NOT ignored (only specific existing subdirectories are). Record a short ledger in the active HANDOFF or PR body: base/head, stable object key, SHA-256, retrieval instructions, and retention deadline. Do not commit payloads, generated manifests per run, signed URLs, or credentials. Use unique object keys per repository, commit, and run. Never overwrite or delete an Evidence bundle that a review has referenced; update the ledger to select a newer build rather than overwriting a mutable latest key.
- Storage/publication boundary: retain the existing `osm-artifacts` bucket as permanently private for Evidence, logs, verification images, and internal builds (the role called `osm-evidence` in the supplied review). Never enable a public domain or `r2.dev` on this bucket. Public named distributions go to GitHub Releases as explicitly selected copies. If R2 CDN distribution becomes necessary, create a separate `osm-distributions` bucket and separate publishing credentials then. Prefixes and object ACLs are not a private/public boundary. Ordinary artifact credentials have no account administration or public-distribution bucket access. Reusing the existing name avoids an unnecessary bucket migration; no Cloudflare setting is changed by this plan.
- Evidence bundle identity: package the existing review-evidence inputs without inventing a new Evidence format. Store the generated manifest and checksums with the payload in R2. C and C' share only immutable, findings-free judgment inputs for the same base/head: frozen snapshots, diff, raw results, images, and machine output as required by the existing workflow. C findings and conclusions remain separate and inaccessible to the blind C' input path until Phase D; never append them to the shared object. A changed payload requires a new key and hash.
- Local flow: BuildSystem writes into its existing local output; a wrapper packages and hashes its output, uploads to a unique R2 prefix, verifies downloaded bytes, and returns a stable ledger entry. `D:\OneDrive\OSM-Artifacts` is not part of this workflow: no mirror, staging, credential storage, or acceptance dependency.
- Remote boundary: remote agents receive only expiring, scope-limited grants; their credential delivery and unattended transfer route remain unverified and belong to the cloud investigation.
- Access should be least-privilege and predictable: give a remote reviewer read access to the intended Evidence only; give a remote producer write/read-back access to its own run. A bucket-wide Object Read & Write token is not an append-only permission and is not the production default for Cloud Agents. Parent credentials are protected at rest on the owner's Windows machine; the local transfer/signing process uses them without printing or exporting them. This trusts processes running as that Windows user and is not isolation from a malicious local agent. Remote agents receive scoped, expiring grants. The repository must contain no secret values. Platform secret handling differs; see the setup boundary below.
- Ordering: implement slice 0 and prove local transfer first; cloud failures remain separate unsupported capabilities and do not require an always-on broker to unblock local work.
- Probe-only safety gate: until slice 2 verifies server-enforced protection of finalized objects, upload only synthetic files under `probe/`. Do not upload real Evidence or real builds even to the private bucket. The local bucket-wide credential can overwrite/delete objects; CLI prohibitions are not an R2 permission boundary.
- Accepted threat boundary: protect against leakage into Git, logs, and sync folders, ordinary other-user access to stored keys, and accidental public distribution. DPAPI does not isolate a local agent running as the same Windows user. Usable keys exist in runtime memory; bucket administrators can change lock policy; initial signed-URL probes do not provide write-once storage.
- Engine boundary: Unity remains a replaceable backend and the primary interface remains outside Unity as stated in section 1.

### Not frozen; owners of remaining decisions

- Slice 1 local: actual R2 round trip and credential connectivity. Slice 1 cloud: each platform's credential delivery, unattended issuance/renewal, Codex egress configuration, and runtime grant handling. Cursor secret redaction is not permission reduction.
- Slice 2: retention durations, concrete server-side protection and its lifecycle interaction, and finite numeric extraction limits. Lock verification gates all real payload uploads. Proposed cost controls (Standard storage and a measured budget alert) require implementation verification; no automatic retention deletion is authorized here.
- Slice HANDOFFs: command syntax, exact file/class layout, and the detailed implementation/testing choices in section 3 are planning inputs, not frozen implementation instructions.
- The missing fourth inline review comment remains pending receipt. On receipt, record its disposition in a new program revision; do not mark unseen text resolved.

## 3. Program slices

### 現在の到達点と段2への引継ぎ

ローカル段1（スライス0）は [PR #76](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/76) で実装・検証を完了し、人間がPhase Dを承認した。現在のCLIと運用契約は [ローカル資格情報管理README](../../tools/Artifacts/README.md) を正とする。完了した段1HANDOFFは削除し、証拠台帳はPR本文へ移した。programのr1境界は変更していない。

ダミー鍵の登録・DPAPI CurrentUser保管・置換・削除・失敗時の保全・非露出はローカル検証済み。実鍵、R2接続、Cloudflare変更、実payload転送は未実施で、別WindowsユーザーDPAPIは未実測。同一ユーザーのAgentからの隔離は主張しない。

次の作業は段2（スライス1ローカル）のPhase Aである。所有者はOSM保守担当。接続probe、実鍵の登録とサーバー検証付きrotationの凍結に加え、レビューから次の4点を引き継ぐ。段1の完了条件を追加するものではなく、着手時に責務と受け入れ条件を具体化する。

- 共有OneStarMakerが未作成の場合の制限ACLと、既存共有親を変更しない方針の整理。
- ロック取得後にACL設定が失敗したときのハンドル解放責務。
- 矢印キー等で入力が中止される現在の挙動と、対話入力の操作仕様。
- 危険なACLのactiveは削除も拒否され暗号文が残るため、所有者による復旧・清掃手順。

### r2案 — 最初に使える区切りと実施順

> 最初に開始できるのは、成立を確認したローカルWindows・同一ユーザー境界での限定的なEvidence運用だけである。Cloud対応、権限隔離、C / C'合格、program全体の運用開始・完了を意味しない。
>
> A2指摘を反映した再検討用のPhase A案であり、修正版の再レビューとA3承認は未了。現行r1の安全・責務境界と段1完了を維持する。以下の順序・最低条件は、承認後に各スライスの自己完結したHANDOFFへ切り出す設計入力であり、実装開始の指示ではない。

先に答える問いは「既存のレビューEvidenceを、作成側から別セッションへ、人間のファイル運搬なしに渡して確認できるか」である。BuildSystem全体のUX整備を、この運用改善の前提にしない。

スライス番号は既存参照を保つため変更せず、4を最小接続の`4-local`とCloud環境拡大の`4-cloud`に分ける。世界実証のS-4a〜S-4dとは別の名前とする。提案順序は **0（完了）→ 1-local → 2（初回CLI）→ 4-local → 一区切り**。各段は別HANDOFF / 別ブランチとする。

| 対象 | 最初の区切りで扱うこと | 後続へ残すことと所有先 |
|---|---|---|
| 1-local | 既存の段2引継ぎ、実鍵の接続probe、サーバー検証付きrotation、syntheticな往復 | Cloudごとの接続・grant発行 / 更新は1-cloud。ローカル完了を待たせない |
| 2（初回CLI） | `publish` / `fetch`自身が返す安定した終了コードと機械可読の合否・検証結果・失敗理由、台帳、manifest、保持期限、安全な梱包・展開、実payload前のサーバー側保護 | 独立した`inspect` / `prune`コマンドと操作性拡充は2の後続スライス。初回CLI自身の検証責務、保持・保護条件は延期しない |
| 4-local | 新しいEvidence形式を作らず既存形式を接続する。1-local / 2の安全条件、有限上限、秘密の非記録を引き継ぎ、同一Windowsユーザーの別セッションで取得・検証・ログと画像の閲覧を確認 | Cloudごとの閲覧確認は4-cloud。Build実行のorchestration、進捗・キャンセル等のUX統合は3 |

4-localの前提と完了に必要な最低条件案は次のとおり。各条件の担当、操作経路、観測、証拠の受け渡しは着手時Phase Aで具体化し、未知の閲覧経路は凍結前に疎通を確認する。このprogram案への承認だけで、その経路や各スライスの受け入れ条件まで検証・凍結したことにはしない。

- 前提として、1-localと2の当該HANDOFFを完了する。非公開bucket、サーバー側の上書き・削除保護とlifecycle相互作用、信頼する台帳hash、安全な展開の有限上限、秘密の非記録を含む既存の安全条件を省かない。成立前は`probe/`のsyntheticデータだけを使う。
- 作成側は固定base/headの既存Evidenceから所見を含まない入力を明示選択する。4-localのEvidence接続側は、`publish`のアップロード前に実際の保存対象payloadのmanifest / entry一覧を凍結済みの入力一覧と照合し、想定外entryやCの所見文書が含まれないことを確認する。不一致なら保存せず、照合済みの同じpayloadだけを新規keyへ保存・読戻し確認する。manifestと照合記録はpayloadと共に非公開R2へ置き、台帳にはその参照・hash・照合結果のみを残す。台帳更新は別操作であり、所見内容や除外した所見文書の一覧をC'の取得案内へ混ぜない。manifestの一致だけで内容中の所見非混入を自動証明したとは扱わず、B resultや一次観察への所見転載禁止は既存ワークフローどおり維持する。新しいEvidence形式やテスト再実行の仕組みは作らない。
- 別セッションは作成側のstagingや元bundleを直接読む代わりに、所見を含まない取得案内に切り出した台帳のkeyと期待hashから`fetch`し、同一性を検証して専用領域へ展開する。C'へPR本文や可変HANDOFF全文を取得案内として渡さない。`publish` / `fetch`自身の終了コードと機械可読の検証結果で転送の成否を判定し、条件が求めるログと画像を、そのローカルセッションの実際の閲覧経路で確認する。byte一致だけを目視確認済みと扱わない。
- 所有者による初回鍵登録後、上記の往復に人間のファイルコピーやgrantの手渡しを要しない。既存の同一Windowsユーザーの信頼境界を使い、別セッション成功をCloud対応や同ユーザーAgent間の権限隔離の証明にしない。Cloudを作成側または確認側に含める経路は4-cloudが所有し、当該1-cloudの無人grant取得・更新を含む条件と、その担当の閲覧確認を別途満たす。

**停止規則案:** 凍結した4-localの最低条件を満たしたら、そのスライスを閉じ、成立を確認したローカルWindows・同一ユーザー境界でのみ限定的なEvidence運用へ入れる。そこで世界実証（S-4d / S-5）の通常のPhase Aへ戻れるが、各スライスがCloud担当を使う場合の受け渡しまで解決したことにはしない。3、4-cloud、2の操作性拡充、全Cloud対応、常駐broker、GUIを4-localの追加の完了条件にしない。4-local完了はCloud対応、権限隔離、転送したEvidenceのC / C'合格、program全体の運用開始・完了の証明ではない。

残件の所有者はOSM保守担当とし、上表の所有スライスに残す。各Cloudのread / write / grant / 閲覧可否を個別に判定し、未成立はunsupportedとして残す。未達を人間の反復作業で埋めたり、段1を再開したりしない。新しい証拠により凍結条件・常時契約への違反が判明した場合の扱いは、既存ワークフローに従う。

以下はprogramのスライス分割である。スライス0の現在の実装は上記READMEを参照し、スライス1以降の詳細は各Phase Aで凍結する。

0. **Local credential management.** Implement the Windows credential lifecycle described below before handling a real key. Prove registration, protected storage, redacted status/errors, replacement, and local removal using dummy credentials. Then the owner registers a bucket-scoped real credential through masked local input and the local transport probe validates it. No production credential is required for the storage implementation tests.
1. **R2 transport and agent access proof.** Track local and cloud work separately. Upload only synthetic files to unique keys under `probe/`; prove authenticated access while ordinary unsigned requests are denied. A signed URL is a credential, not anonymous access. Verify download, SHA-256, ZIP extraction, upload, and read-back. The local prerequisite is owner credential setup; Codex egress is a prerequisite only for its cloud probe. Proposed cloud probe: per-object, short-lived signed GET/PUT URLs with the parent key held locally; this proves transport, not unattended credential issuance. Establish automated grant delivery/renewal before accepting routine unattended cloud use. Its absence does not block local completion.
2. **Artifact contract and CLI wrapper.** Define the object key, manifest, checksums, upload/read-back behavior, retention metadata, and stable reference format. The initial `publish` / `fetch` commands own their stable exit codes and machine-readable success/failure and verification results; neither slice 3 nor `inspect` is required to judge their outcome. Cleanup may touch only keys created by the current run that have not been published in the ledger; no broad prefix deletion or deletion of referenced Evidence. Run-specific naming and hashes detect errors but do not enforce immutability. Before production, select and verify a server-enforced protection for finalized objects (for example R2 bucket locks or a trusted finalization service with agents limited to staging). Keep provider credentials outside Git. Test with synthetic payloads before connecting BuildSystem.
3. **BuildSystem UX integration.** In the r2 proposal, schedule this after the first-use Evidence milestone rather than making it a prerequisite for 4-local. Make an external CLI the primary interface for humans, local agents, and automation. Preserve existing build logic behind a Unity backend adapter and retain current output locations initially. The build orchestrator passes ordinary output paths and versioned metadata to the artifact CLI; it does not know credentials, S3, or ZIP internals. Provide progress, cancellation, stable exit codes, and machine-readable results. Unity GUI integration is an optional thin caller, never a required path. Any future standalone GUI calls the same CLI/application layer. Do not invent a second engine implementation now; establish and verify the boundary with a fake backend and plain file fixtures.
4. **Evidence workflow integration.** In the r2 proposal, 4-local connects existing Evidence after 1-local and 2 and verifies the actual log/image-viewing routes only for the participating local Windows sessions within the same-user boundary. 4-cloud owns each additional Cloud producer/reviewer role's log/image-viewing verification after its applicable 1-cloud access and unattended-grant gates. Both reuse the existing bundle format and safety conditions; C and C' retrieve the same immutable findings-free judgment payload by key/hash, with findings kept separately and checked against the pre-upload manifest / entry list described above. Successful byte storage alone does not prove visual review. Do not replace unavailable image inspection with recurring human file transfer. Neither 3 nor all-cloud support is a prerequisite for 4-local.

Each slice gets its own Phase A HANDOFF, branch, acceptance gates, and cleanup. The proposed first-use path is local 1, initial 2, then 4-local; slice 0 remains completed. The engine-independent artifact boundary applies from the first CLI, but full BuildSystem orchestration belongs to 3. Preserve 3, 4-cloud, later slice 2 usability work, and both clouds' read/write probes as remaining work; unresolved cloud access must not be advertised as supported or block the mostly local workflow on an always-on broker. This proposed ordering requires the r2 review and owner approval above.

### Artifact CLI and package contract (slice 2)

- **First-use commands (r2 proposal):** `publish` packages explicitly selected inputs, hashes, uploads, verifies read-back, and emits a stable ledger entry only on success; it does not make anything public. `fetch` consumes a trusted ledger/key plus expected package SHA-256, downloads, verifies, and safely extracts. Credential management and grant issuance remain separate administrative commands.
- **First-use result contract (slice 2):** `publish` / `fetch` themselves return stable exit codes and machine-readable success/failure, verification status, and nonsecret failure reasons. Success requires all applicable checks and read-back or extraction to finish; failure returns a nonzero code, no success ledger, and no output marked ready for review. Exact codes and result fields belong to the slice 2 HANDOFF. These are initial CLI acceptance conditions, not deferred BuildSystem UX or `inspect` responsibilities.
- **Later commands (slice 2 follow-up):** `inspect` shows manifest, sizes, retention, base/head, and verification status without running payloads; without a trusted ledger, it may show metadata only as unverified. `prune` only presents deletion candidates from the current owner's runs that are expired and proven unreferenced. Unknown reference status or an active lock excludes a candidate; a candidate listing does not authorize deletion. Neither standalone command is a prerequisite for the first-use milestone.
- **r2 first-use scope proposal:** manifest, retention metadata, failure results, and the shared trust-anchor, packaging, integrity, extraction, non-recording, and protection conditions below apply to the initial `publish` / `fetch`. The separate later-command conditions apply when those commands are implemented. Deferring commands does not authorize automatic deletion or weaken real-payload gates.
- **Trust anchor:** obtain the expected package hash from the fixed HANDOFF/PR ledger supplied for review, not from the same R2 package being verified. Evidence `fetch` rejects an absent or mismatching trusted hash before extraction. A matching hash proves identity with the referenced bytes, not that their producer or content is trustworthy. Never execute fetched scripts or binaries as part of transfer/inspection.
- **Package boundary:** only explicit input roots/file lists are eligible. Do not recursively collect a whole repository, user profile, credential store, or staging parent by default. Reject symlinks/reparse points and input paths escaping the selected roots; disallow credential/grant files. Snapshot eligible files into isolated staging before packaging so hashes describe the uploaded bytes. Credential exclusion is not a claim that arbitrary selected logs are secret-free.
- **Safe extraction:** verify the outer archive hash first, then validate every entry before writing into a new private temporary directory. Reject absolute/drive/UNC paths, parent traversal, links/reparse entries, Windows alternate streams/reserved names, duplicate or case-colliding destinations, and any normalized path outside the extraction root. Bound compressed input, entry count, individual/total expanded bytes, and expansion ratio; enforce actual streamed-byte limits as well as header checks. Never overwrite existing files or extract directly into the checkout. On failure, remove only this operation's temporary output and return a nonzero result. Numeric limits are named settings with finite defaults to freeze in slice 2, appropriate to large builds, not silently unlimited.
- **Non-recording acceptance:** use dummy credential/grant sentinels to check normal, verbose, HTTP-error, timeout, and cancellation paths. Original keys, session tokens, Authorization headers, and signed URL queries must not appear in stdout/stderr, exceptions, diagnostic logs, ledger output, child-process command arguments, or Git changes. Private grant output is the sole intentional exception, protected by ACLs and an expiry-aware cleanup rule. Do not feed its contents into agent tool calls/prompts that retain them until that delivery surface is reviewed; unattended Cloud delivery remains unverified. If a chosen HTTP tool cannot avoid recording full URLs, use a controlled adapter or reject that path.
- **Immutability:** propose a server-side lock on finalized Evidence prefixes with retention agreed before enabling it; keep disposable probes/staging separately scoped. Verify overwrite/delete denial and interaction with lifecycle rules before production use. Bucket administration can change policy, so this is protection against the restricted writer role, not against the account owner. Retention expiry alone never proves an object is unreferenced.
- **Scope exclusions:** mutable compilation caches do not share the Evidence retention/immutability contract. Backend-specific build types, SBOMs, profiler captures, and other file formats can use the transport later without becoming mandatory integrations in this slice.

### Engine-independent BuildSystem boundary (slice 3)

- External CLI/application layer owns build requests, backend selection, status/cancellation, and orchestration. A backend adapter owns engine invocation and produces a versioned result containing output paths, target/platform, source commit, logs, and success/failure.
- Unity-specific APIs, project settings, Addressables operations, and executable discovery remain in the Unity adapter. Artifact packaging and transport depend only on the result's ordinary paths/metadata; neither imports Unity assemblies or relies on an Editor menu, AssetDatabase, ScriptableObject, or Unity serialization.
- PowerShell may be the initial Windows launcher; keep application contracts independent of the launcher and DPAPI in a Windows credential provider. Engine independence does not imply cross-platform credential storage is already implemented.
- Acceptance: invoke the external entry point with a fake backend, publish/fetch ordinary fixtures with no Unity installation, and separately demonstrate the Unity adapter using the supported build route. Unity builds/tests remain Phase C work. An Editor window may be an engine execution detail, but no menu-click sequence is the canonical BuildSystem workflow.

References: [R2 public buckets](https://developers.cloudflare.com/r2/buckets/public-buckets/), [R2 S3 compatibility](https://developers.cloudflare.com/r2/api/s3/api/), [R2 bucket locks](https://developers.cloudflare.com/r2/buckets/bucket-locks/).

### 資格情報管理の設計入力と後続段の境界

以下の設計入力のうちローカル保管・管理CLIはスライス0で実装済み。現在の実装契約は上記READMEを正とする。実鍵、転送/署名、接続確認日時、サーバー検証付きrotationはスライス1以降の未実装範囲であり、以下を現行機能の一覧とは扱わない。

- **Storage:** use Windows DPAPI `CurrentUser` via .NET `ProtectedData`, with the encrypted credential file under `%LOCALAPPDATA%\OneStarMaker\Artifacts\credentials\`. Keep it outside the checkout and outside `D:\OneDrive\OSM-Artifacts`. Restrict directory/file ACLs to the owning user and required Windows administrators/system principals; never use the `LocalMachine` DPAPI scope or a project-stored encryption password. Fail closed on unsupported OS, decryption failure, or an unsafe storage path; no plaintext fallback.
- **Scope:** create a dedicated local-machine R2 Object Read & Write credential for `osm-artifacts` only, with a recognizable owner/device label and a recorded token ID for revocation. Do not store a Global API Key or account administration credential. Treat its ability to overwrite/delete objects as real authority; the CLI's guardrails are not an R2 permission boundary.
- **Registration UX:** proposed command `pwsh tools/artifacts.ps1 credentials set --profile osm` opens masked interactive input for the S3 Access Key ID and Secret Access Key. Exact CLI syntax is finalized in the slice HANDOFF. Do not accept secret values in command-line arguments, chat, committed files, shell history, or persistent environment variables. Reject noninteractive registration rather than hanging an agent. The human performs initial secret entry in their own terminal; daily transfers require no re-entry. Store only the S3 key pair needed by the client, not an additional displayed Cloudflare bearer token.
- **Use:** upload/download/sign operations accept a profile name. A private credential provider decrypts only in the transfer/signing process, passes credentials directly to the S3 client, and clears disposable plaintext buffers where feasible. Do not spawn a child CLI with secrets in arguments, dump environment values, enable signed-request debug logging, or expose a command that exports the original key. Runtime memory still contains usable credentials during the operation.
- **Status and audit:** `credentials status` reports profile, bucket, endpoint, credential generation label, creation/rotation date, and last successful connectivity check. It never prints credentials or signed URL query strings. Redact secrets from exception text, diagnostic files, and ordinary stdout/stderr. Signed grants, when requested, go to a separate explicitly selected private delivery file/channel, not the ordinary ledger. State when a status is local-only rather than implying a successful R2 check.
- **Rotation:** owner creates a replacement bucket-scoped token, enters it through masked input, and the tool validates read/write/read-back with disposable data before atomically switching the active encrypted record. A failed validation preserves the old record. Owner then revokes the old token in Cloudflare; record that revocation is confirmed rather than equating local replacement with server revocation. Remove the retired encrypted record after confirmation. The tool does not require account administration rights to rotate its local store.
- **Loss and incident response:** `credentials remove` removes the local encrypted record and stops future local use; clearly report that it does not revoke a token at Cloudflare. For exposure/lost PC, revoke the affected token in Cloudflare first, then register a replacement. Do not sync credential blobs to OneDrive or rely on copying DPAPI files to another PC; provision a distinct credential on a new PC and revoke the lost device's token.
- **Trust boundary:** DPAPI protects stored data for the Windows user; software running as that same user can also decrypt it. This design reduces accidental Git/log/sync leakage and protects stored secrets from ordinary other-user access. If isolation from the local agent itself becomes required, use a separately protected service/account with constrained operations; that is a separate design, not a property of the wrapper.
- **Implementation ownership:** proposed entry point `tools/artifacts.ps1`; separate Windows credential-store, S3 transfer/signing, and packaging responsibilities under `tools/Artifacts/`. The credential store owns encryption/filesystem lifecycle; the S3 adapter consumes credentials without exposing them; packaging never sees credentials. Unit checks use dummy secrets and a fake S3 adapter; actual key validation belongs to the explicit connectivity probe. Avoid adding Unity or BuildSystem dependencies to credential management.
- **Completion evidence:** dummy-secret round trip; no plaintext persisted or emitted on success/failure; tampered/wrong-user store rejected; failed rotation retains the previous profile; successful replacement is atomic; local removal and remote revocation distinguished; no repository or OneDrive payload changes during credential operations. Record any cross-user check that could not actually be executed as unverified. Local connectivity is a separate gate using the owner's real token.

DPAPI reference: [Microsoft data protection](https://learn.microsoft.com/en-us/dotnet/standard/security/how-to-use-data-protection). ローカル保管と管理コマンドは実装済みで、実接続と後続機能の成立は別途検証する。

## 4. Local and cloud transport acceptance

Local acceptance requires slice 0's credential lifecycle checks and local synthetic upload/download, SHA-256, ZIP extraction, unsigned-access denial, and unchanged repository status. Passing these permits slice 2's local implementation. Cloud support is accepted separately for each platform only when:

- A disposable synthetic object is uploaded to the R2 bucket with a unique key and is not visible through an anonymous public URL.
- Cursor Cloud and Codex Cloud each retrieve the same object in their own remote execution environment, verify SHA-256, extract the ZIP, and report the log marker. Record image-viewing capability separately for slice 4-cloud; its failure does not invalidate proven storage integrity or add a condition to 4-local.
- Each Cloud Agent role can upload a new synthetic object and read it back. Credentials are scoped to the artifact bucket and their intended read/write operations; no credentials are stored in the repository.
- Codex's blocked network path is resolved with the minimum required domain/method allowlist, or the slice records a verified alternative transport that both agents can use.
- The probe manifest identifies its object key and hashes; all copies match. Transfer operations introduce no repository changes; compare status before/after and preserve pre-existing edits. Updating the HANDOFF/PR ledger is a separate, intentional documentation operation.

If any Cloud Agent cannot satisfy an operation, classify that operation as unsupported until another concrete route is verified. Do not turn it into a human download/upload step by default.

### Account-owner setup and agent execution boundary

- One-time owner setup: create/revoke bucket-scoped parent credentials, select their trusted storage/issuer, register the chosen platform integration, and configure outbound networking. Secret rotation and incident revocation remain owner responsibilities. These are distinct from repeated file handling.
- Agent-visible nonsecret configuration: R2 S3 endpoint, bucket, assigned object keys, expected hashes, and expiry/retention metadata. Agent-visible authority: short-lived signed URLs for exact operations/objects, or temporary S3 credentials limited by path, operations, and TTL. Never provide a Cloudflare login password, Global API Key, account-administration token, or an OpenAI API key for storage.
- Cursor supports Runtime Secrets and runtime environment injection/redaction. Redaction reduces accidental display; a process using the credential still has its authority. Scope credentials independently of log masking.
- Codex Cloud Secrets are available only during setup and removed before the agent phase. Merely registering a long-lived R2 key there does not enable runtime upload. Do not persist that parent key to a file or ordinary environment variable to bypass the boundary. The proposed grant issuer/delivery mechanism must handle runtime access, expiry, task resumption, and cached environments without exposing the parent key.
- Proposed first probe: owner establishes the local signing credential once; a local helper generates exact-key GET/PUT grants, which are delivered through a private task channel and omitted from logs, Git, PRs, and reports. URLs can be reused until expiry, including overwriting their PUT target; use disposable run keys and do not claim write-once protection. Grant delivery automation is not yet implemented or verified.
- Network: allow the account-specific R2 S3 hostname. Reads need GET/HEAD; writes need PUT and multipart uploads may also need POST. Confirm the platform's available method controls rather than assuming arbitrary per-method rules. Broader network permission does not grant R2 authorization. Runtime transfer connects to Cloudflare R2, not the OpenAI model API.
- Routine cloud operation after onboarding: producer packages existing outputs outside the repository, obtains a run-scoped grant, uploads and verifies read-back, then emits a stable ledger entry. Reviewer obtains a read grant for the fixed findings-free key, verifies its hash, and inspects the payload. Automated issuance/renewal is a prerequisite to calling this unattended; manual URL exchange is only a probe. The local 4-local path instead uses the stored profile within the accepted same-Windows-user boundary.

Sources checked for r1 (not revalidated by this sequencing-only proposal): [R2 presigned URLs](https://developers.cloudflare.com/r2/api/s3/presigned-urls/), [R2 temporary credentials](https://developers.cloudflare.com/r2/api/s3/temporary-credentials/), [Codex cloud environments](https://learn.chatgpt.com/docs/environments/cloud-environment), [Codex internet access](https://learn.chatgpt.com/docs/cloud/internet-access), and [Cursor secrets and network](https://cursor.com/docs/cloud-agent/security-network).

### Supplied review disposition

Accepted all six supplied corrections: uploader/Git ledger separation; immutable findings-free C/C' inputs; private authenticated probe; owner onboarding versus recurring agent work; storage integrity versus vision capability; and restricted cleanup with explicit immutability limitations. These corrections are incorporated into the r1 program boundary freeze; they do not prove completed implementation or access.

Follow-up review: accepted bucket separation, non-recording acceptance, package/extraction boundaries, and the trusted-ledger hash requirement. Adopted the suggested four daily commands as slice design inputs. Retained `osm-artifacts` as the private bucket name instead of creating `osm-evidence`; all public distribution is external to that bucket. Incorporated the owner's engine-independence requirement. The separately hidden fourth comment remains pending receipt, not resolved; its arrival requires a new revision.

Freeze review: accepted the restricted A3 scope in section 2, split the local/cloud questions, added the synthetic-only gate until slice 2 server protection passes, excluded OneDrive, and preserved the same-user DPAPI limitation. Freeze applies only to program boundaries, not runtime capability, the open decisions, or individual slice implementation. Reviewer identity/model was not supplied; record this as owner-supplied external review without claiming additional model independence.

r2案の指摘対応（2026-09-27）: [PR #77のA2レビュー](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/77#issuecomment-5851367248)（投稿記載の担当: Grok 4.7、対象head `8e7423d`）と[追加確認](https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework/pull/77#issuecomment-5851411767)を受領。7点の明確化を採用し、4-local / 4-cloudの名称・閲覧範囲、保存前のmanifest照合、形式を増やさないことと安全条件の維持、限定運用の停止規則、初回CLI自身の結果責務、後続inspect条件の分離を本文へ反映した。順序やr1境界の変更ではなく、HANDOFF切出し時の条件脱落を防ぐ修正である。追加確認は既存レビューを読んだ旨が記載されており、別の盲検レビューとして数えない。修正版の再レビュー、A3承認、実接続・閲覧経路の検証は未了。

## 5. Known constraints and open decisions

- R2 access is not yet proven. The bucket exists but the current browser/file-selection tools could not upload the synthetic files, so the Cloudflare-side round trip remains untested.
- Codex Cloud's exact network configuration has not been changed. Its current environment rejected GitHub CONNECT with 403, while Cursor Cloud could retrieve GitHub assets.
- R2 credentials for Cursor and Codex Cloud have not been created or configured. R2 object credentials are distinct from the broad Cloudflare API token; the intended credential must be bucket-scoped.
- Confirm how long closed-PR evidence must remain readable before configuring automatic lifecycle deletion.
- Internal Build publication is private by default. Public release copies require an explicit distribution operation; private-bucket publication never toggles visibility.
- `r2.dev` is a development endpoint with rate limits. Do not use it as the production distribution URL; use private signed/object access or a reviewed custom-domain setup.
- Probe cleanup (pending, owner: program maintainer at experiment close): remove the public `artifact-probe-20260926` Release and its synthetic assets once they are no longer needed for the transport experiment. Preserve only the concise outcome/hash record needed by this plan, not a permanent public example of Evidence storage. Confirm experiment completion before deletion; this revision does not claim the Release was removed or authorize deleting unrelated releases/tags.

## 6. Out of scope

- Changing BuildSystem build behavior or Unity output layouts before the R2 transport and artifact contract pass.
- Migrating existing historical artifacts or rewriting Git history.
- Uploading real game builds or existing Evidence to any bucket before slice 2's server-side protection gate passes.
- OneDrive mirrors, Unity `Library` transfer, mutable latest-build keys, and new CI credential providers. Different artifact categories (such as caches, profiler/crash data, and SBOMs) require their own retention contracts when integrated.
- Adding a Cloudflare Worker, custom web portal, or new storage vendor unless a verified limitation makes the proposed R2 path unsuitable.
- Treating the GitHub Release probe as proof that Codex Cloud can access arbitrary external storage; its actual run failed at the network proxy.
