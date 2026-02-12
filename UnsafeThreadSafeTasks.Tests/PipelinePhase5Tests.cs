using System;
using System.IO;
using System.Linq;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests
{
    /// <summary>
    /// Tests for pipeline Phase 5: Error analysis and retry.
    /// Validates the run-pipeline.ps1 Phase 5 additions including new parameters,
    /// TaskFilter support in Invoke-Phase3, and Phase 5 documentation.
    /// </summary>
    public class PipelinePhase5Tests
    {
        private static readonly string RepoRoot = FindRepoRoot();

        private static string FindRepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "SdkMultithreadingMigration.slnx")))
            {
                dir = Directory.GetParent(dir)?.FullName;
            }
            return dir ?? throw new InvalidOperationException("Could not find repository root.");
        }

        private string PipelineDir => Path.Combine(RepoRoot, "pipeline");
        private string ScriptPath => Path.Combine(PipelineDir, "run-pipeline.ps1");

        private string ScriptContent => File.ReadAllText(ScriptPath);

        // ─── Phase 5 documentation in script synopsis ─────────────────────────────

        [Fact]
        public void RunPipelineScript_SynopsisDescribesPhase5()
        {
            Assert.Contains("Phase 5", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_Phase5Description_MentionsErrorAnalysis()
        {
            Assert.Contains("Error analysis and retry", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_Phase5Description_MentionsTrxParsing()
        {
            Assert.Contains("Parses TRX results", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_Phase5Description_MentionsFailingTasks()
        {
            Assert.Contains("failing tasks", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_Phase5Description_MentionsOuterIterations()
        {
            Assert.Contains("max 20 total outer iterations", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_Phase5Description_MentionsProgressTracking()
        {
            Assert.Contains("Tracks progress across iterations", ScriptContent);
        }

        // ─── Phase 5 script parameters ────────────────────────────────────────────

        [Fact]
        public void RunPipelineScript_HasPhase5OnlySwitch()
        {
            Assert.Contains("$Phase5Only", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_Phase5OnlyIsSwitchParameter()
        {
            Assert.Contains("[switch]$Phase5Only", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_HasMaxOuterIterationsParameter()
        {
            Assert.Contains("$MaxOuterIterations", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_MaxOuterIterationsDefaultIsTwenty()
        {
            Assert.Contains("$MaxOuterIterations = 20", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_MaxOuterIterationsIsIntType()
        {
            Assert.Contains("[int]$MaxOuterIterations", ScriptContent);
        }

        // ─── Invoke-Phase3 TaskFilter support ─────────────────────────────────────

        [Fact]
        public void RunPipelineScript_InvokePhase3_HasTaskFilterParameter()
        {
            var functionBody = ExtractFunctionBody("Invoke-Phase3");
            Assert.Contains("$TaskFilter", functionBody);
        }

        [Fact]
        public void RunPipelineScript_InvokePhase3_TaskFilterDefaultsToEmptyArray()
        {
            var functionBody = ExtractFunctionBody("Invoke-Phase3");
            Assert.Contains("$TaskFilter = @()", functionBody);
        }

        [Fact]
        public void RunPipelineScript_InvokePhase3_TaskFilterIsStringArray()
        {
            var functionBody = ExtractFunctionBody("Invoke-Phase3");
            Assert.Contains("[string[]]$TaskFilter", functionBody);
        }

        [Fact]
        public void RunPipelineScript_InvokePhase3_FiltersTasksWhenTaskFilterProvided()
        {
            var functionBody = ExtractFunctionBody("Invoke-Phase3");
            Assert.Contains("$TaskFilter.Count -gt 0", functionBody);
        }

        [Fact]
        public void RunPipelineScript_InvokePhase3_UsesWhereObjectForFiltering()
        {
            var functionBody = ExtractFunctionBody("Invoke-Phase3");
            Assert.Contains("Where-Object", functionBody);
            Assert.Contains("$TaskFilter -contains", functionBody);
        }

        [Fact]
        public void RunPipelineScript_InvokePhase3_AssignsFilteredTasksToVariable()
        {
            var functionBody = ExtractFunctionBody("Invoke-Phase3");
            Assert.Contains("$tasksToProcess", functionBody);
        }

        [Fact]
        public void RunPipelineScript_InvokePhase3_LogsFilteredTaskCount()
        {
            var functionBody = ExtractFunctionBody("Invoke-Phase3");
            Assert.Contains("Filtering to", functionBody);
        }

        // ─── Phase 5 coexists with existing phases ────────────────────────────────

        [Fact]
        public void RunPipelineScript_StillHasPhase1OnlySwitch()
        {
            Assert.Contains("[switch]$Phase1Only", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_StillHasPhase3OnlySwitch()
        {
            Assert.Contains("[switch]$Phase3Only", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_StillHasMaxRetriesParameter()
        {
            Assert.Contains("$MaxRetries = 5", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_AllPhaseSwitchesInParamBlock()
        {
            // Verify all phase switches exist in the param block
            var lines = ScriptContent.Split('\n');
            int paramStart = -1;
            int paramEnd = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("param("))
                    paramStart = i;
                if (paramStart >= 0 && lines[i].Trim() == ")")
                {
                    paramEnd = i;
                    break;
                }
            }

            Assert.True(paramStart >= 0 && paramEnd > paramStart, "Script should have a param() block");

            var paramBlock = string.Join("\n", lines.Skip(paramStart).Take(paramEnd - paramStart + 1));
            Assert.Contains("$Phase1Only", paramBlock);
            Assert.Contains("$Phase3Only", paramBlock);
            Assert.Contains("$Phase5Only", paramBlock);
            Assert.Contains("$MaxRetries", paramBlock);
            Assert.Contains("$MaxOuterIterations", paramBlock);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Extracts a rough function body from the PowerShell script by finding the function
        /// declaration and reading until the next top-level function or end of script.
        /// </summary>
        private string ExtractFunctionBody(string functionName)
        {
            var lines = ScriptContent.Split('\n');
            var startIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains($"function {functionName}"))
                {
                    startIndex = i;
                    break;
                }
            }

            Assert.True(startIndex >= 0, $"Function {functionName} should exist in the script");

            var bodyLines = new System.Collections.Generic.List<string>();
            for (int i = startIndex + 1; i < lines.Length; i++)
            {
                if (lines[i].TrimStart() == lines[i] &&
                    lines[i].TrimStart().StartsWith("function ") &&
                    !lines[i].TrimStart().StartsWith("function {"))
                {
                    break;
                }
                bodyLines.Add(lines[i]);
            }

            return string.Join("\n", bodyLines);
        }
    }
}
