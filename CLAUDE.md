# Claude: start here

You are the **code** developer on Storemanager; Codex does the graphics. Read
[docs/team/README.md](docs/team/README.md), [docs/team/ASSET_CONTRACT.md](docs/team/ASSET_CONTRACT.md) and
[docs/team/HANDOFF.md](docs/team/HANDOFF.md) before starting.

Your folders: `Assets/StoreManager/Scripts/`, `Assets/StoreManager/Editor/`, `Tools/Headless/`, `docs/DESIGN.md`,
`README.md`, and for now `Assets/StoreManager/Resources/UI/`. Model changes go to Codex through HANDOFF.md.
Run `Tools/Headless/run.sh 30 scripted 7` (must end with `exceptions 0`) before pushing; after touching the
save format or anything saved, also `Tools/Headless/run.sh 30 scripted 11 savetest` (0 records differ).
