# Local artifact credentials

On Windows, the owner can manage the local `osm` profile with PowerShell 7:

```powershell
pwsh tools/artifacts.ps1 credentials set --profile osm
pwsh tools/artifacts.ps1 credentials status --profile osm
pwsh tools/artifacts.ps1 credentials set --profile osm --replace
pwsh tools/artifacts.ps1 credentials remove --profile osm
```

`set` and `--replace` require an interactive console. Both keys are typed into masked prompts; key arguments, redirected input, files and environment variables are unsupported. A normal `set` refuses an existing profile. `--replace` requires one. A profile holds the fixed private bucket `osm-artifacts`; endpoint and token reference remain unconfigured in this local-only slice.

The encrypted active record is beneath the Windows `LocalApplicationData` Known Folder at `OneStarMaker\Artifacts\credentials`. It is bound to the current Windows user with DPAPI. The directory and files have restricted ACLs. Never copy the credential blob into the checkout or a sync folder; copying it does not make it portable. Same-user processes may still decrypt it, so protect the Windows account.

`status` reports safe local metadata after decrypting and validating the active record. It does not contact R2 or report a connectivity timestamp. Exit 0 means the local operation succeeded; exit 2 means replacement committed but cleanup of old temporary/backup files is pending; exit 1 means failure. If the active record is absent or damaged, the command fails closed and does not restore a backup. Retry after checking the local directory and ACLs; do not rename backup or candidate files into active. `remove` is idempotent, clears only this profile's local active and owned temporary/backup files, and does **not** revoke a Cloudflare token. Token revocation remains an owner action in Cloudflare.

Real-token onboarding, disposable R2 read/write/read-back, server-validated rotation, and old-token revocation are future work in the next slice. This tool makes no remote validity claim.
