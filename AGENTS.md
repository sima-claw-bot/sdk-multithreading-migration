# AGENTS.md — Auto-generated Task Instructions

## Current Task
**Fix review issues for PR #6**

Fix the following review issues in PR #6: "[Agent] we only want to keep the skills directory. remove all the rest (consider this fork a separate repository, if need be, we will re"

## Review Feedback
PR has an empty diff — zero files were changed despite the title and description claiming to remove all files except the skills directory. The PR does not deliver what it claims.

1. The diff is completely empty: zero files changed, zero lines of code modified. The PR performs no actual work.
2. The PR title says to remove all files except the skills directory, but no deletions or modifications were made.
3. The branch agent/issue-1-task-0 no longer exists on the remote and the PR returns 404, suggesting it was never properly pushed or was deleted.
4. The PR description references resolving issue #1, but the task description does not match the linked issue's purpose.
5. The agent must re-execute the task: actually delete all files/directories outside the skills directory and push the changes to a valid branch.

## Failed Stage Details
### correctness
PR claims to remove everything except the skills directory but only adds an AGENTS.md instruction file — no files were actually removed.




● Read ~\AppData\Local\Temp\copilot-eval-1770888419368.txt
  └ 84 lines read

```json
{
  "passed": false,
  "summary": "PR claims to remove everything except the skills directory but only adds an AGENTS.md instruction file — no files were actually removed.",
  "issues": [
    "The diff only adds a new AGENTS.md file; no files or directories were deleted, so the stated goal of 'remove all the rest' is not accomplished.",
    "The skills directory is not preserved or referenced in any way by this change — the PR does not fulfill its described task.",
    "The added AGENTS.md is a task-instruction scaffold (including retry guidance for a failed git push), not a meaningful code change; it should not be merged as a resolution to the issue."
  ]
}
```



### decision
PR has an empty diff — zero files were changed despite the title and description claiming to remove all files except the skills directory. The PR does not deliver what it claims.
1. The diff is completely empty: zero files changed, zero lines of code modified. The PR performs no actual work.
2. The PR title says to remove all files except the skills directory, but no deletions or modifications were made.
3. The branch agent/issue-1-task-0 no longer exists on the remote and the PR returns 404, suggesting it was never properly pushed or was deleted.
4. The PR description references resolving issue #1, but the task description does not match the linked issue's purpose.
5. The agent must re-execute the task: actually delete all files/directories outside the skills directory and push the changes to a valid branch.

## Changed Files


## Instructions
1. Read the review feedback carefully
2. Make the minimum necessary changes to fix the identified issues
3. Do NOT refactor beyond what the review requires
4. Ensure the code still compiles and tests pass
5. Commit your changes

## Working Rules
- Complete the task described above
- Run tests before finishing
- Do not modify files outside the scope of this task
- Commit your changes with a clear commit message


