# HTC Home Mugen fork

This fork keeps the classic HTC Home look and weather animations while modernizing the runtime behavior around them.

Initial engineering goals:

- run multiple independent Weather/Clock instances from one installation;
- keep per-instance settings and positions;
- make autostart instance-aware;
- add a visual multi-monitor instance manager;
- survive suspend/hibernate and display topology changes reliably;
- preserve the classic Weather/Clock renderer and skins as a visual compatibility target.

The first implementation milestone is profile-aware v2 instances (`--profile <id>`). A later manager will create and launch these profiles automatically.
