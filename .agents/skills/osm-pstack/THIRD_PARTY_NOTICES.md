# Third-party notices

This is an unofficial adaptation derived from pstack. It is not affiliated
with or endorsed by Cursor or Lauren Tan.

These OSM-specific rules were added for this repository and are not part of
upstream pstack.

- Upstream: <https://github.com/cursor/plugins/tree/main/pstack>
- Source commit: `c1c0a32802223f4be824112dd83d33ad29a8b26c`
- Copyright: Copyright (c) 2026 Lauren Tan
- License: MIT. See [LICENSE.pstack](LICENSE.pstack).

The adaptation draws from pstack's `how`, `why`, `blast-radius`, `architect`,
`arena`, `interrogate`, `unslop`, `technical-writing`, and
`show-me-your-work` skills; its runtime-forensics and trace-forensics
playbooks; and the principles those files reference.

OSM rewrites and reorganizes those ideas under its existing Phase, HANDOFF,
Unity-operation, test, and pull-request contracts. The files in this directory
are not a complete copy of pstack. They omit pstack's plugin wrapper, sticky
mode, fixed executor configuration, product-specific commands, automatic pull
request flow, automation packs, and autonomous-run behavior.

The OSM-specific precedence rules, routing boundaries, Phase integration, and
Unity restrictions are original to this repository. Removing this directory
removes the adaptation and its attribution without changing the other OSM
skills.
