# Auto Git Rules for Claude Code

## Mandatory Workflow
After making ANY file edits, creating new files, or removing code:
1. Automatically run `git status` to verify changes.
2. Automatically stage only core project folders:
   `git add Assets/ Packages/ ProjectSettings/ .gitignore`
3. Automatically commit with a short, descriptive message explaining what changed.
4. Automatically push to the current branch: `git push`

## Strict Rules
- NEVER include `Library/`, `Temp/`, `Logs/`, or `UserSettings/` in git commands.
- ALWAYS finish your response by executing the push automatically without waiting for explicit user instructions.
