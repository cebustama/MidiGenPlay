@../Documentation~/project_instructions.md

## Claude Code only

- This folder is the `com.claudiobustamante.midigenplay` package and its Git repo. Its main consumer is ALWTTT (`D:\Projects\ALWTTT25`), which references it via `file:`; the package is tested from that host project.
- Governed docs live in `Documentation~/` (`SSoT_INDEX.md`, `CURRENT_STATE.md`, `ssot_manifest.yaml`, `runtime/`, `authoring/`). Read them from disk; there is no PK.
- The session-close PK replacement list does not apply here. Instead, list the files created and modified, flag drafted-but-unapplied edits, and remind the user to commit.
- Code truth is the working tree plus `git log` / `git status`. When `CURRENT_STATE.md` lags behind Git, say so.
- Claude Code cannot run Unity, the EditMode suite, the smoke window or the tonality matrix. Regression anchors and test results stay unverified until the user runs them.
- Unity creates a `.meta` file for every new file outside `~` and dot folders. Never hand-write or delete `.meta` files; if a file is moved or deleted, its `.meta` goes with it.
- The user writes in Spanish; answer in Spanish unless asked otherwise.
