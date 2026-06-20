# DebugSocket Server Inversion Plan

## 1. Background

Unity runtime 縺ｧ `HttpListenerContext.AcceptWebSocketAsync(...)` 縺・`NotImplementedException` 繧呈兜縺偵ｋ縺薙→縺檎｢ｺ隱阪＆繧後◆縲・
縺昴・縺溘ａ縲∫樟陦後・縲袈nity 縺・WebSocket server / DebugStudio 縺・client縲肴ｧ区・縺ｯ縲√％縺ｮ runtime 縺ｧ縺ｯ謌千ｫ九＠縺ｪ縺・・

縺溘□縺励∝撫鬘後・ protocol 縺ｧ縺ｯ縺ｪ縺・transport topology 縺ｫ縺ゅｋ縲・
譌｢蟄倥・ `DebugSocketProtocol`縲｀essagePack envelope縲…apability handshake縲《tore routing 縺ｯ蜀榊茜逕ｨ蜿ｯ閭ｽ縺ｧ縺ゅｊ縲・
蛻・ｊ譖ｿ縺医ｋ縺ｹ縺阪↑縺ｮ縺ｯ縲後←縺｡繧峨′蠕・女縺励√←縺｡繧峨′謗･邯壹ｒ蠑ｵ繧九°縲阪〒縺ゅｋ縲・

## 2. Decision

謗｡逕ｨ譁ｹ驥昴・谺｡縺ｮ縺ｨ縺翫ｊ縲・

- DebugStudio 繧・WebSocket server 縺ｫ蜿崎ｻ｢縺吶ｋ
- Unity 縺ｯ outbound `ClientWebSocket` 縺ｧ DebugStudio 縺ｫ謗･邯壹☆繧・
- 蜿梧婿蜷鷹壻ｿ｡隕∽ｻｶ縺ｯ邯ｭ謖√☆繧・
- protocol / envelope / message types 縺ｯ邯ｭ謖√☆繧・
- transport 螻､縺ｮ蟾ｮ縺玲崛縺医↓螟画峩繧帝哩縺倩ｾｼ繧√ｋ
- 蜊倅ｸ繧ｻ繝・す繝ｧ繝ｳ蜑肴署縺ｯ邯咏ｶ壹☆繧・

## 3. Non-Goals

莉雁屓縺ｮ繧ｹ繧ｳ繝ｼ繝励↓蜷ｫ繧√↑縺・ｂ縺ｮ縺ｯ谺｡縺ｮ縺ｨ縺翫ｊ縲・

- protocol 蜈ｨ髱｢蛻ｷ譁ｰ
- WebSocket 莉･螟悶・ transport 縺ｸ縺ｮ螟画峩
- multi-client 蟇ｾ蠢・
- Unity 蛛ｴ `HttpListener` 螳溯｣・・蟒ｶ蜻ｽ
- 譛ｪ遒ｺ螳壹・螟夜Κ WebSocket server 繝ｩ繧､繝悶Λ繝ｪ蟆主・蜑肴署縺ｮ險ｭ險亥崋螳・

## 4. Invariants

蜿崎ｻ｢蠕後ｂ谺｡縺ｮ荳榊､画擅莉ｶ繧貞ｮ医ｋ縲・

1. `DebugSocketProtocol` 縺ｨ `DebugSocketEnvelopeV1` 縺ｯ邯ｭ謖√☆繧・
2. `DebugSocketMessageType` 縺ｯ邯ｭ謖√☆繧・
3. capability hello / welcome 縺ｮ諢丞袖縺ｯ邯ｭ謖√☆繧・
4. command / result 縺ｮ `requestId` 逶ｸ髢｢縺ｯ邯ｭ謖√☆繧・
5. hierarchy snapshot / delta 縺ｨ inspector query / detail 縺ｮ payload 莠呈鋤縺ｯ邯ｭ謖√☆繧・
6. queue overflow / disconnected drop 縺ｮ runtime diagnostics 縺ｯ邯ｭ謖√☆繧・

## 5. Architecture Direction

### 5.1 Current

- Unity 縺・`HttpListener` 縺ｧ蠕・女縺吶ｋ
- DebugStudio 縺・`ClientWebSocket` 縺ｧ謗･邯壹☆繧・
- Unity 縺・inbound 繧貞・逅・＠ outbound frame 繧定ｿ斐☆
- runtime 縺ｧ縺ｯ `AcceptWebSocketAsync` 縺梧悴螳溯｣・〒蛛懈ｭ｢縺吶ｋ

### 5.2 Target

- DebugStudio 縺・single-session WebSocket listener 繧呈戟縺､
- Unity 縺・outbound `ClientWebSocket` 繧呈戟縺､
- Unity 縺ｯ謗･邯壼ｾ後↓ receive loop 繧帝幕蟋九＠縲∵里蟄倥・ inbound dispatch 繧貞茜逕ｨ縺吶ｋ
- DebugStudio 縺ｯ蜿嶺ｿ｡縺励◆ binary frame 繧呈里蟄・inbound router 縺ｫ豬√☆
- app/store/viewmodel 螻､縺ｯ transport 縺ｮ蜷代″螟画峩繧偵⊇縺ｼ諢剰ｭ倥＠縺ｪ縺・

## 6. Affected Areas

### Unity side

- `Assets/OneStarMaker/Scripts/Runtime/DebugSocketServices/DebugSocketService.cs`
- `Assets/OneStarMaker/Scripts/Foundation/DebugSocket/DebugSocketOptions.cs`
- `Assets/OneStarMaker/Scripts/Runtime/AbstractApplicationInitializer.cs`
- `Assets/SampleGame/Config/app-config.json`

### DebugStudio side

- `DebugStudio/src/DebugStudio.Client/DebugStudioSession.cs`
- `DebugStudio/src/DebugStudio.Client/DebugSocketClientOptions.cs`
- `DebugStudio/src/DebugStudio.App/Core/Services/SessionService.cs`
- `DebugStudio/src/DebugStudio.App/Features/Session/SessionWindowViewModel.cs`
- `DebugStudio/src/DebugStudio.App/Core/Composition/AppCompositionRoot.cs`

### Docs

- `DEBUGSTUDIO-Unity_plannning.md`
- `DEBUGSTUDIO_PROGRESS_2026-04-29.md`
- `DEBUGSTUDIO_MESSAGEPACK_FLOW_2026-04-29.md`

## 7. Work Breakdown

## Ticket DS-TR-01: Extract Transport Seam

### Goal

`DebugStudioSession` 繧・client 蝗ｺ螳壼ｮ溯｣・°繧・transport facade 縺ｸ蛻・屬縺励《erver/client 螳溯｣・ｷｮ縺玲崛縺医ｒ蜿ｯ閭ｽ縺ｫ縺吶ｋ縲・

### Scope

- transport session abstraction 繧貞ｰ主・縺吶ｋ
- `SessionService` 縺・concrete client 螳溯｣・∈逶ｴ謗･萓晏ｭ倥＠縺ｪ縺・ｽ｢縺ｫ縺吶ｋ
- `AppCompositionRoot` 縺ｧ concrete transport 螳溯｣・ｒ蟾ｮ縺玲崛縺医ｉ繧後ｋ繧医≧縺ｫ縺吶ｋ

### Expected changes

- `DebugStudio/src/DebugStudio.Client/DebugStudioSession.cs`
- `DebugStudio/src/DebugStudio.App/Core/Services/SessionService.cs`
- `DebugStudio/src/DebugStudio.App/Core/Composition/AppCompositionRoot.cs`

### Acceptance criteria

1. transport facade 縺ｮ public contract 縺梧・譁・喧縺輔ｌ縺ｦ縺・ｋ
2. 譌｢蟄・client 螳溯｣・′ facade 繧呈ｺ縺溘☆
3. app/store 螻､縺・concrete client 蝙九↓萓晏ｭ倥＠縺ｦ縺・↑縺・

### Notes

縺薙％縺ｧ縺ｯ謖吝虚螟画峩繧貞・繧後★縲∬ｲｬ蜍吶・蛻・ｊ蜃ｺ縺励□縺代↓逡吶ａ繧九・

## Ticket DS-TR-02: Add DebugStudio Server Transport

### Goal

DebugStudio 縺ｫ single-session WebSocket listener 繧定ｿｽ蜉縺励∝女菫｡繝輔Ξ繝ｼ繝繧呈里蟄・inbound router 縺ｸ豬√○繧九ｈ縺・↓縺吶ｋ縲・

### Scope

- listen 髢句ｧ・
- accept
- current session 邂｡逅・
- receive loop 襍ｷ蜍・
- send gateway
- disconnect / fault handling

### Expected changes

- `DebugStudio/src/DebugStudio.Client/` 驟堺ｸ九↓ server transport 螳溯｣・眠險ｭ
- `DebugStudio/src/DebugStudio.App/Core/Composition/AppCompositionRoot.cs`

### Acceptance criteria

1. DebugStudio 縺・listen 繧帝幕蟋九〒縺阪ｋ
2. 蜊倅ｸ繧ｯ繝ｩ繧､繧｢繝ｳ繝医ｒ蜿励￠蜈･繧後ｉ繧後ｋ
3. binary frame 1 莉ｶ繧貞女菫｡縺・inbound router 縺ｸ貂｡縺帙ｋ
4. 譌ｧ謗･邯夂ｽｮ謠帙・繝ｪ繧ｷ繝ｼ縺悟ｮ夂ｾｩ縺輔ｌ縺ｦ縺・ｋ

### Risks

- server 螳溯｣・API 縺ｮ驕ｸ螳・
- WPF app 縺ｮ繝ｩ繧､繝輔し繧､繧ｯ繝ｫ縺ｨ listener 蛛懈ｭ｢繧ｿ繧､繝溘Φ繧ｰ

## Ticket UNITY-TR-01: Unity Outbound Client PoC

### Goal

Unity runtime 縺ｧ outbound `ClientWebSocket.ConnectAsync` 縺梧・遶九☆繧九％縺ｨ繧堤｢ｺ隱阪☆繧九・

### Scope

- DebugStudio server 縺ｸ謗･邯・
- binary frame 繧・1 蠕蠕ｩ縺吶ｋ

### Expected changes

- `Assets/OneStarMaker/Scripts/Runtime/DebugSocketServices/` 驟堺ｸ九↓ PoC 霑ｽ蜉縺ｾ縺溘・譛蟆丞ｷｮ蛻・

### Acceptance criteria

1. Unity Console 縺ｫ connect success 縺悟・繧・
2. DebugStudio 蛛ｴ縺ｧ frame 蜿嶺ｿ｡縺檎｢ｺ隱阪〒縺阪ｋ
3. Unity 蛛ｴ縺ｧ frame 蜿嶺ｿ｡縺檎｢ｺ隱阪〒縺阪ｋ

### Notes

縺薙％縺ｧ螟ｱ謨励☆繧九↑繧・server inversion 蜈ｨ菴薙ｒ豁｢繧√ｋ縲ゆｻ･髯阪・繝√こ繝・ヨ縺ｸ騾ｲ縺ｾ縺ｪ縺・・

## Ticket UNITY-TR-02: Refresh DebugSocketOptions

### Goal

listener prefix 荳ｭ蠢・・險ｭ螳壹Δ繝・Ν繧偵｛utbound endpoint 荳ｭ蠢・∈鄂ｮ縺肴鋤縺医ｋ縲・

### Scope

- `Host`, `Port`, `Path`, `ListenerPrefix` 荳ｭ蠢・°繧・`ServerUri`, reconnect policy, autoConnect 荳ｭ蠢・∈隕狗峩縺・
- 譌｢蟄・config key 縺ｮ遘ｻ陦梧婿驥昴ｒ螳壹ａ繧・

### Expected changes

- `Assets/OneStarMaker/Scripts/Foundation/DebugSocket/DebugSocketOptions.cs`
- `Assets/SampleGame/Config/app-config.json`

### Acceptance criteria

1. 譁ｰ險ｭ螳壹Δ繝・Ν縺ｧ Unity bootstrap 縺梧・遶九☆繧・
2. 繝ｭ繧ｰ譁・ｨ縺・listener 縺ｧ縺ｯ縺ｪ縺・outbound connect 繝｢繝・Ν縺ｫ荳閾ｴ縺吶ｋ
3. config 縺ｮ譌｢螳壼､縺・loopback 螳牙・蛛ｴ縺ｧ縺ゅｋ

## Ticket UNITY-TR-03: Replace Unity Transport Core

### Goal

Unity 縺ｮ `DebugSocketService` 繧・listener/server 螳溯｣・°繧・outbound client/session manager 螳溯｣・∈鄂ｮ縺肴鋤縺医ｋ縲・

### Scope

- connect
- receive loop
- send queue
- disconnect
- reconnect policy
- runtime diagnostics 邯ｭ謖・

### Reuse targets

- `HandleInboundMessageAsync(...)`
- built-in command dispatch
- hierarchy snapshot/delta 逕滓・
- inspector detail 逕滓・
- queue/drop accounting

### Remove or replace targets

- `HttpListener`
- `AcceptLoopAsync(...)`
- `ProcessContextAsync(HttpListenerContext, ...)`
- `AcceptWebSocketAsync(...)`

### Acceptance criteria

1. Unity 縺・DebugStudio server 縺ｫ謗･邯壹〒縺阪ｋ
2. inbound frame 繧呈里蟄・dispatch 縺ｧ蜃ｦ逅・〒縺阪ｋ
3. outbound queue 縺ｨ drop policy 縺檎ｶｭ謖√＆繧後ｋ
4. shutdown 譎ゅ・蛻・妙縺悟ｮ牙・縺ｫ陦後ｏ繧後ｋ

## Ticket PROTO-01: Restore Handshake on Inverted Topology

### Goal

server inversion 蠕後ｂ capability hello / welcome 縺梧ｭ｣縺励＞鬆・ｺ上〒豬√ｌ繧九ｈ縺・↓縺吶ｋ縲・

### Scope

- 謗･邯夂峩蠕後・ hello 騾∽ｿ｡繝医Μ繧ｬ繝ｼ隱ｿ謨ｴ
- welcome 蜿嶺ｿ｡蠕後・ state 譖ｴ譁ｰ遒ｺ隱・

### Expected changes

- DebugStudio transport 螳溯｣・
- `DebugStudio/src/DebugStudio.App/Core/Services/SessionCapabilityCoordinator.cs`
- Unity outbound session manager

### Acceptance criteria

1. capability state store 縺・negotiated state 縺ｸ騾ｲ繧
2. hierarchy/inspector capability gate 縺梧悄蠕・←縺翫ｊ蜍輔￥

## Ticket SLICE-01: Restore Log and Telemetry

### Goal

log / telemetry 縺ｮ one-way push 繧呈怙蛻昴・ vertical slice 縺ｨ縺励※蠕ｩ譌ｧ縺吶ｋ縲・

### Scope

- Unity realtime stream -> outbound transport
- DebugStudio receive loop -> inbound router -> stores -> UI

### Acceptance criteria

1. log 縺・DebugStudio 縺ｫ陦ｨ遉ｺ縺輔ｌ繧・
2. telemetry 縺・DebugStudio 縺ｫ陦ｨ遉ｺ縺輔ｌ繧・
3. export 縺ｸ縺ｮ retain 繝代せ縺悟｣翫ｌ縺ｦ縺・↑縺・

## Ticket SLICE-02: Restore Command Correlation

### Goal

DebugStudio 縺九ｉ縺ｮ command 縺・Unity 縺ｧ dispatch 縺輔ｌ縲～requestId` 莉倥″ result 縺梧綾繧九％縺ｨ繧貞ｾｩ譌ｧ縺吶ｋ縲・

### Scope

- command send
- built-in `debugsocket.runtime-diagnostics`
- request/result correlation
- disconnect 譎ゅ・ pending 邨らｫｯ蛹也｢ｺ隱・

### Acceptance criteria

1. command 繧・1 莉ｶ騾√ｌ繧・
2. `requestId` 縺御ｸ閾ｴ縺吶ｋ result 縺瑚ｿ斐ｋ
3. `runtime-diagnostics` 縺・roundtrip 縺吶ｋ

## Ticket SLICE-03: Restore Hierarchy and Inspector

### Goal

hierarchy snapshot/delta縲（nspector query/detail 繧貞ｾｩ譌ｧ縺吶ｋ縲・

### Scope

- snapshot 蛻晏屓騾∽ｿ｡
- delta 譖ｴ譁ｰ
- inspector query
- inspector detail 蠢懃ｭ・

### Acceptance criteria

1. hierarchy tree 縺瑚｡ｨ遉ｺ縺輔ｌ繧・
2. hierarchy change 縺・delta 縺ｧ蜿肴丐縺輔ｌ繧・
3. selection 縺九ｉ inspector detail 縺悟ｾ蠕ｩ縺吶ｋ

## Ticket UX-01: Session UI Inversion

### Goal

Session UI 繧・client 隕也せ縺九ｉ server/listener 隕也せ縺ｸ螟画峩縺吶ｋ縲・

### Scope

- `ServerUri` 蜈･蜉帙・諢丞袖隕狗峩縺・
- `Connect` / `Disconnect` 縺ｮ陦ｨ迴ｾ螟画峩
- activity / detail 譁・ｨ譖ｴ譁ｰ

### Expected changes

- `DebugStudio/src/DebugStudio.App/Features/Session/SessionWindowViewModel.cs`

### Acceptance criteria

1. UI 縺梧眠縺励＞謗･邯壹Δ繝・Ν縺ｨ謨ｴ蜷医☆繧・
2. operator 縺後御ｽ輔ｒ蜈医↓襍ｷ蜍輔☆繧九°縲阪ｒ隱､隗｣縺励↑縺・

## Ticket DOC-01: Sync Runbook and Architecture Docs

### Goal

譁ｰ topology 繧貞ｮ溯｣・・→驕狗畑閠・・荳｡譁ｹ縺ｫ莨昴ｏ繧句ｽ｢縺ｧ譁・嶌蜷梧悄縺吶ｋ縲・

### Scope

- runbook 譖ｴ譁ｰ
- progress doc 譖ｴ譁ｰ
- protocol flow doc 譖ｴ譁ｰ
- 蠢・ｦ√↑繧・architecture doc 譖ｴ譁ｰ

### Acceptance criteria

1. 螳溯｣・→ runbook 縺ｮ謗･邯夐・ｺ上′荳閾ｴ縺吶ｋ
2. transport 蜿崎ｻ｢逅・罰縺梧枚譖ｸ蛹悶＆繧後ｋ
3. Unity server 譌ｧ險ｭ險医′ obsolete 縺ｨ譏手ｨ倥＆繧後ｋ

## 8. Execution Order

謗ｨ螂ｨ鬆・ｺ上・谺｡縺ｮ縺ｨ縺翫ｊ縲・

1. `DS-TR-01`
2. `DS-TR-02`
3. `UNITY-TR-01`
4. `UNITY-TR-02`
5. `UNITY-TR-03`
6. `PROTO-01`
7. `SLICE-01`
8. `SLICE-02`
9. `SLICE-03`
10. `UX-01`
11. `DOC-01`

荳ｦ蛻怜呵｣懊・谺｡縺ｮ縺ｨ縺翫ｊ縲・

- `DS-TR-02` 縺ｨ `UNITY-TR-01` 縺ｯ seam 謚ｽ蜃ｺ蠕後↓荳ｦ蛻怜庄
- `DOC-01` 縺ｯ譛邨ょ酔譛溷燕謠舌↑繧我ｸ区嶌縺阪・縺ｿ荳ｦ蛻怜庄

## 9. Validation Strategy

蜷・ヵ繧ｧ繝ｼ繧ｺ蠕後↓谺｡縺ｮ narrow validation 繧定｡後≧縲・

1. transport connect/disconnect
2. capability handshake
3. log/telemetry 陦ｨ遉ｺ
4. command/result correlation
5. hierarchy/inspector 蠕蠕ｩ

`git diff` 蜑肴署縺ｮ讀懆ｨｼ縺ｯ荳崎ｦ√ゅ％縺ｮ workspace 縺ｯ迴ｾ蝨ｨ git repository 縺ｨ縺励※隱崎ｭ倥＆繧後※縺・↑縺・◆繧√∝ｮ溯｡悟庄閭ｽ validation 繧貞━蜈医☆繧九・

## 10. Key Tradeoffs

### Why inversion is acceptable

- Unity 繧ょ曙譁ｹ蜷鷹壻ｿ｡繧堤ｶ壹￠繧峨ｌ繧・
- client socket 縺ｧ繧・send / receive 縺ｯ荳｡譁ｹ蜿ｯ閭ｽ
- 蝠城｡後・蜿梧婿蜷第ｧ縺ｧ縺ｯ縺ｪ縺・Unity server API 髱槫ｯｾ蠢懊〒縺ゅｋ

### Why protocol should stay intact

- 譌｢蟄・router/store/UI 縺ｮ蜀榊茜逕ｨ邇・′鬮倥＞
- change surface 繧・transport 螻､縺ｫ髢峨§霎ｼ繧√ｉ繧後ｋ
- vertical slice 縺斐→縺ｮ蠕ｩ譌ｧ縺後＠繧・☆縺・

### Why not keep Unity as server with another library first

- 螟夜Κ萓晏ｭ倥さ繧ｹ繝医′鬮倥＞
- Unity runtime / Editor / Player 蟾ｮ逡ｰ縺ｮ菫晏ｮ医さ繧ｹ繝医′蠅励∴繧・
- 縺ｾ縺壹・ topology inversion 縺ｮ縺ｻ縺・′譌｢蟄倩ｳ・肇豬∫畑邇・′鬮倥＞

## 11. Immediate Next Step

譛蛻昴・逹謇九・ `DS-TR-01`縲・

逅・罰:

- transport 蟾ｮ縺玲崛縺医・蠅・阜繧貞・縺ｫ菴懊ｉ縺ｪ縺・→縲∽ｻ･髯阪・ server/client 螳溯｣・′ app 螻､縺ｸ貍上ｌ繧・
- seam 縺後↑縺・憾諷九〒 server inversion 繧帝ｲ繧√ｋ縺ｨ蟾ｮ蛻・′謨｣繧・
- 縺薙％縺檎ｵゅｏ繧九→ DebugStudio server 螳溯｣・→ Unity client PoC 繧剃ｸｦ蛻怜喧縺ｧ縺阪ｋ