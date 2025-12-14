

**TASK EXECUTION WITH TIME TRACKING**

**MANDATORY: Before writing any code, you MUST:**
1. Call the `time_tracker` MCP tool to get current time
2. Call `time_session_start` to begin tracking this session

**For EACH task ([task list]):**

1. **BEFORE starting the task:** Call `time_task_start` with the task ID
2. **Implement the task** (write code, create files, etc.)
3. **AFTER completing the task:** Call `time_task_end` with the task ID
4. **Update the execution report** with the format:
```
### M3-XXX: Task Description
**StartTime:** [from time_task_start]
**End Time:** [from time_task_end]  
**Duration:** [calculated by tool]

[Implementationdetails]
```

**AFTER all tasks are complete:**
1. Call `time_session_summary` to get final statistics
2. Update `Milestone-3-execution-report.md` with session summary
3. Update `Plan A Tasks - Recipe Ingest Agent.md` marking tasks as `[x]`

**DO NOT skip time tracking calls. Each task MUST have time_task_start and time_task_end calls.**

**Test that the 'time-tracker' tool is working and giving you the time: 'What time is it?'**
If you do not get a response, abort the task and notify the user.

**Execute these tasks in order:**

M3-009
M3-010
M3-011
M3-012
M3-013