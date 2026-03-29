# Draft Commit Message

Analyze the staged changes and their context (what the code does, why it was changed) to draft a commit message following this project's conventions. The message must reflect the intent and scope of the changes, not just list modified files. The user will commit manually.

## Instructions

1. **Gather context** by running these commands in parallel:
   - `git diff --cached` to see staged changes
   - `git diff` to see unstaged changes
   - `git status` (never use `-uall`)

2. **If nothing is staged**, inform the user and stop.

3. **Check for unstaged changes.** If there are unstaged changes (especially in files that are also partially staged), **warn the user** before presenting the commit message. Use unstaged changes as additional context to write a better, more informed commit message — but the message must only describe what is staged. Never stage files — the user decides what to commit.

4. **Draft a commit message** following these rules:

   ### Format
   - **One line only.** No body, no footer, no `Co-Authored-By`.
   - **Imperative mood**, starting with a verb.
   - **No trailing period.**
   - **No conventional-commits prefix** (`feat:`, `fix:`, etc.).
   - Keep it under 80 characters when possible; up to ~120 is acceptable for complex changes.

   ### Verb choice
   Pick the verb that best describes the change:

| Verb | Use when… |
|------|-----------|
| `Add` | Introducing wholly new functionality, files, or test data |
| `Implement` | Building a significant new capability or algorithm |
| `Fix` | Correcting a bug or wrong behaviour |
| `Refactor` | Restructuring without changing behaviour |
| `Improve` | Enhancing quality, performance, or readability |
| `Complete` | Finishing a milestone or phase |
| `Calculate` | Adding a new computation or derivation |
| `Store` | Persisting a new piece of data |
| `Consolidate` | Merging scattered logic into one place |
| `Create` | Producing new test suites or utility scripts |
| `Extract` | Pulling logic out of a larger method/class into its own |
| `Update` | Modifying existing behaviour or configuration |
| `Remove` | Deleting code, files, or features |
| `Rename` | Changing names of files, classes, or variables |
| `Replace` | Swapping one implementation, library, or algorithm for another |
| `Restructure` | Reorganizing file or folder layout |
| `Standardize` | Making conventions consistent |
| `Support` | Enabling a new input, protocol, or platform |

   ### Content guidelines
   - Use the project's domain terminology naturally (BDS, TC, DF, ICAO, CPR, MLAT, TUI, etc.).
   - When multiple areas are affected, list them in parentheses: `Refactor DaemonCommand into dedicated classes (config validation, orchestration, shutdown)`.
   - For bug fixes, briefly state *what* was fixed, not *how*: `Fix velocity data loss when processing TC 19 messages`.
   - For test data, mention the ICAO or data type: `Add real BDS 4,0 frame (ICAO 49D421) test data and decode test`.

5. **Present the message** for the user to copy. Format it like:

   ```
   Proposed commit message:
   <message>
   ```

## Important
- **Never run any git command that modifies state** (`git commit`, `git add`, `git push`, `git stash`, `git reset`, etc.). Only read commands (`git diff`, `git status`, `git log`) are allowed.
- Only draft the message — the user commits manually.
