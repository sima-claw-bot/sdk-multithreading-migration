# AGENTS.md — Auto-generated Task Instructions

## Current Task
**task-29 Validate all fixed tests PASS**

> Part of #14

## Description
Run `dotnet test UnsafeThreadSafeTasks.Tests/ --filter "Target=Fixed"` per category. Every test targeting a fixed task must PASS. Run per-category and report per-category results. If any fail, fix the corresponding fixed task. Document total pass count. [critical] (depends: task-21, task-22, task-23, task-24, task-25, task-26, task-27, task-28a, task-28b, task-28c)

## Metadata
**Task ID:** `task-29`

*Created by agent-orchestrator planning pipeline*

## Working Rules
- Complete the task described above
- Run tests before finishing
- Do not modify files outside the scope of this task
- Commit your changes with a clear commit message


