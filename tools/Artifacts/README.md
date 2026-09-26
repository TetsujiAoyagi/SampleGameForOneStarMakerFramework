# ローカル Artifact 認証情報

Windows の所有者は PowerShell 7 から、固定の `osm` profile を管理できる。

```powershell
pwsh tools/artifacts.ps1 credentials set --profile osm
pwsh tools/artifacts.ps1 credentials status --profile osm
pwsh tools/artifacts.ps1 credentials set --profile osm --replace
pwsh tools/artifacts.ps1 credentials remove --profile osm
```

`set` と `--replace` は対話 console を必要とする。Access Key ID と Secret Access Key は echo しない prompt で入力する。鍵を引数、redirect した標準入力、ファイル、環境変数から渡す経路はない。通常の `set` は既存 profile を拒否し、`--replace` は既存 profile を必要とする。この段階では bucket を `osm-artifacts` に固定し、endpoint と token の参照情報は未設定のままにする。

暗号化した active record は Windows の `LocalApplicationData` Known Folder にある `OneStarMaker\Artifacts\credentials` 以下に置く。DPAPI の `CurrentUser` を使うため、別 Windows ユーザーや別 PC に blob をコピーしても利用できない。checkout や同期フォルダーにコピーしない。同じ Windows ユーザー権限を持つ別 process は復号可能なので、Windows account 自体を保護する。資格情報専用の directory と file は継承を切った制限 ACL を使う。既存の共有祖先 `OneStarMaker` や、その兄弟 directory の ACL は変更しない。

`status` は active record を復号・検証して安全なローカル metadata だけを表示する。R2 への接続や接続時刻は報告しない。終了 code 0 はローカル操作成功、2 は交換済みだが古い一時ファイルの掃除が保留、1 は失敗を表す。active が欠落・破損した場合は失敗し、backup を自動復元しない。backup や candidate を手動で active に改名せず、ローカル directory と ACL の状態を確認する。

`remove` は冪等で、この profile の active と厳密な命名規則に合う一時・backup のみを削除する。Cloudflare の token は失効しない。失効操作は所有者が Cloudflare 側で別途行う。

実 token の登録、使い捨て object による R2 読み書きと read-back、server 検証を伴う鍵交換、旧 token の失効確認は次のスライスの作業である。このツールは remote での鍵の有効性を主張しない。
