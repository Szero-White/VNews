# VNews Claude Code — Lean Kit v2

This is a **code-generation instruction kit**, NOT compiled website source. It intentionally shortens session startup context compared to the original kit.

## How to use on Windows
1. Extract ZIP to a dedicated empty folder such as `D:\VNews`. Don't put it in a folder that contains unrelated personal data.
2. Install .NET 10 SDK, Git and SQL Server Express LocalDB if missing; install/login Claude Code. Docker is unnecessary for the main run path.
3. Open the folder in Claude Code (or in PowerShell: `cd D:\VNews`, then `claude`). In Claude Code UI choose **Edit automatically**; keep command approval for operations you don't recognize. Don't use Bypass permissions on your personal machine.
4. Paste `prompts/01-M0-M1-PUBLIC.txt`. Once M0/M1 is truly verified, paste `prompts/02-M1-FINISH-M2-API.txt`, then `03-M3-CMS.txt`, `04-M4-FINISH.txt`.
5. To refine the appearance, optionally place your own screenshot reference at `references/homepage.png` and use `prompts/05-VISUAL-CHECK.txt`. A browser/preview tool must be available for Claude to take a screenshot.
6. On later sessions, paste `prompts/TIEP-TUC-PHIEN-MOI.txt` to resume from `docs/PROGRESS.md`.

## Saving tokens
- Keep `CLAUDE.md` short and stable: Claude Code loads it frequently.
- Don't ask Claude to read every doc at startup or print entire files. Provide only the docs relevant to the milestone.
- Don't repeatedly ask "build the entire site from scratch". Use progress checkpoints and small verified slices.
- Keep errors focused (last relevant lines rather than complete build logs), without hiding important failures.
- `/clear` for a new phase, `/compact` if the current phase's context is large; `/usage` (subscription) or `/cost` depending on setup to inspect usage. Don't run lots of parallel agents just for speed; they can multiply usage.
- Most useful prompt: **specific feature + verification criterion**.

## Repo / safety
- Use original VNews brand and fictional demo news, not copyrighted VnExpress assets or misleading official lookalikes.
- No automatic destructive database resets, no secret keys committed, no uncontrolled Bypass mode.
- Record what was truly tested. No workflow guarantees a hiring outcome.
