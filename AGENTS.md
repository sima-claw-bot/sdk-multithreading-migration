# AGENTS.md — Auto-generated Task Instructions

## Current Task
**Address review feedback for PR #10**

Address the following review feedback for PR #10: "[Agent] [Rework] [Agent] [Rework] [Agent] we only want to keep the skills directory. remove all the rest (consider this fork a separate repository, if need be, we will re (PR #4) (PR #7)"

## Correctness Issues
PR claims to remove everything except the skills directory but only deletes AGENTS.md and adds a .gitignore — no other files or directories were actually removed.




● Read ~\AppData\Local\Temp\copilot-eval-1770889450441.txt
  └ 127 lines read

```json
{
  "passed": false,
  "summary": "PR claims to remove everything except the skills directory but only deletes AGENTS.md and adds a .gitignore — no other files or directories were actually removed.",
  "issues": [
    "The diff only deletes the AGENTS.md file and adds a .gitignore that ignores AGENTS.md; no other files or directories in the repository were removed, so the stated goal of 'remove all the rest' is not accomplished.",
    "The skills directory is not preserved or referenced in any meaningful way — the PR does not fulfill the task of keeping only the skills directory.",
    "Adding AGENTS.md to .gitignore is counterproductive: it prevents future AGENTS.md files from being tracked, which is unrelated to the goal of removing non-skills files and could interfere with the agent orchestrator's workflow.",
    "This is a rework of previous failed PRs (#4 and #7) that had the same issue (empty or insufficient diff), yet the rework still does not perform the required file deletions."
  ]
}
```



## Changed Files
.gitignore
AGENTS.md

## Instructions
1. Read the review feedback carefully
2. Make the minimum necessary changes to fix the identified issues
3. Do NOT refactor beyond what the review requires
4. Do NOT change files that are not listed in "Changed Files" unless strictly necessary
5. Ensure the code still compiles and tests pass
6. Commit your changes

## Working Rules
- Complete the task described above
- Run tests before finishing
- Do not modify files outside the scope of this task
- Commit your changes with a clear commit message


