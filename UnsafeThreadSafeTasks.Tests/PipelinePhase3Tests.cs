using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests
{
    /// <summary>
    /// Tests for pipeline Phase 3: Agent invocation and retry framework.
    /// Validates the run-pipeline.ps1 Phase 3 functions and the pipeline-test-mapping.json structure.
    /// </summary>
    public class PipelinePhase3Tests
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
        private string TestMappingPath => Path.Combine(RepoRoot, "pipeline-test-mapping.json");
        private string ConfigPath => Path.Combine(PipelineDir, "config.json");

        private string ScriptContent => File.ReadAllText(ScriptPath);

        // ─── run-pipeline.ps1 Phase 3 function definitions ────────────────────────

        [Fact]
        public void RunPipelineScript_DefinesGetPipelineConfigFunction()
        {
            Assert.Contains("function Get-PipelineConfig", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_DefinesGetTestMappingFunction()
        {
            Assert.Contains("function Get-TestMapping", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_DefinesGetTaskTestFilterFunction()
        {
            Assert.Contains("function Get-TaskTestFilter", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_DefinesInvokeAgentForTaskFunction()
        {
            Assert.Contains("function Invoke-AgentForTask", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_DefinesInvokeTestsForTaskFunction()
        {
            Assert.Contains("function Invoke-TestsForTask", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_DefinesGetTestResultsFunction()
        {
            Assert.Contains("function Get-TestResults", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_DefinesBuildRetryPromptFunction()
        {
            Assert.Contains("function Build-RetryPrompt", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_DefinesInvokePhase3Function()
        {
            Assert.Contains("function Invoke-Phase3", ScriptContent);
        }

        // ─── Script parameters for Phase 3 ────────────────────────────────────────

        [Fact]
        public void RunPipelineScript_HasPhase3OnlySwitch()
        {
            Assert.Contains("Phase3Only", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_HasMaxRetriesParameter()
        {
            Assert.Contains("$MaxRetries", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_MaxRetriesDefaultIsFive()
        {
            Assert.Contains("$MaxRetries = 5", ScriptContent);
        }

        // ─── Phase 3 invocation patterns ──────────────────────────────────────────

        [Fact]
        public void RunPipelineScript_Phase3OnlyCallsInvokePhase3()
        {
            Assert.Contains("$Phase3Only", ScriptContent);
            Assert.Contains("Invoke-Phase3", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_InvokePhase3_ReadsConfig()
        {
            Assert.Contains("Get-PipelineConfig", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_InvokePhase3_ReadsTestMapping()
        {
            Assert.Contains("Get-TestMapping", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_InvokeAgentForTask_ReferencesAgentConfig()
        {
            // The agent invocation function should use the agent config (command, flags, model)
            Assert.Contains("$AgentConfig", ScriptContent);
            Assert.Contains("$agentCommand", ScriptContent);
            Assert.Contains("$agentModel", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_InvokeTestsForTask_UsesDotnetTest()
        {
            Assert.Contains("dotnet test", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_InvokeTestsForTask_UsesTestFilter()
        {
            Assert.Contains("--filter", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_InvokeTestsForTask_UsesTrxLogger()
        {
            Assert.Contains("trx;LogFileName=", ScriptContent);
        }

        // ─── Retry logic patterns ─────────────────────────────────────────────────

        [Fact]
        public void RunPipelineScript_HasRetryLoop()
        {
            Assert.Contains("$MaxRetries", ScriptContent);
            Assert.Contains("$iteration", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_BuildRetryPrompt_IncludesFailureDetails()
        {
            Assert.Contains("$FailureDetails", ScriptContent);
            Assert.Contains("Previous Failure Details", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_BuildRetryPrompt_IncludesIterationNumber()
        {
            Assert.Contains("Retry Attempt", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_SavesIterationMetadata()
        {
            Assert.Contains("metadata.json", ScriptContent);
            Assert.Contains("ConvertTo-Json", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_CreatesIterationLogDirs()
        {
            Assert.Contains("iteration-$iteration", ScriptContent);
        }

        // ─── Test results parsing ─────────────────────────────────────────────────

        [Fact]
        public void RunPipelineScript_GetTestResults_ParsesTestCounts()
        {
            Assert.Contains("$totalTests", ScriptContent);
            Assert.Contains("$passedTests", ScriptContent);
            Assert.Contains("$failedTests", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_GetTestResults_ParsesTrxFile()
        {
            Assert.Contains("$TrxFile", ScriptContent);
            Assert.Contains("[xml]", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_GetTestResults_ReturnsSuccessFlag()
        {
            // The function should return a hashtable with Success key
            Assert.Contains("Success", ScriptContent);
            Assert.Contains("ExitCode", ScriptContent);
        }

        // ─── GetTaskTestFilter logic ──────────────────────────────────────────────

        [Fact]
        public void RunPipelineScript_GetTaskTestFilter_UsesFullyQualifiedName()
        {
            Assert.Contains("FullyQualifiedName~", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_GetTaskTestFilter_JoinsWithPipeOperator()
        {
            // The filter parts should be joined with " | " for dotnet test --filter
            Assert.Contains("\" | \"", ScriptContent);
        }

        // ─── Phase 3 synopsis in script docs ─────────────────────────────────────

        [Fact]
        public void RunPipelineScript_SynopsisDescribesPhase3()
        {
            Assert.Contains("Phase 3", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_DescribesRetryFramework()
        {
            Assert.Contains("retry", ScriptContent.ToLowerInvariant());
        }

        // ─── pipeline-test-mapping.json structure tests ───────────────────────────

        [Fact]
        public void TestMapping_FileExists()
        {
            Assert.True(File.Exists(TestMappingPath), "pipeline-test-mapping.json should exist at repo root");
        }

        [Fact]
        public void TestMapping_IsValidJson()
        {
            var content = File.ReadAllText(TestMappingPath);
            var doc = JsonDocument.Parse(content);
            Assert.NotNull(doc);
        }

        [Fact]
        public void TestMapping_HasTasksArray()
        {
            using var doc = ParseTestMapping();
            Assert.True(doc.RootElement.TryGetProperty("tasks", out var tasks));
            Assert.Equal(JsonValueKind.Array, tasks.ValueKind);
        }

        [Fact]
        public void TestMapping_TasksArrayIsNotEmpty()
        {
            using var doc = ParseTestMapping();
            var tasks = doc.RootElement.GetProperty("tasks");
            Assert.True(tasks.GetArrayLength() > 0, "tasks array should contain at least one entry");
        }

        [Fact]
        public void TestMapping_HasTotalTasksField()
        {
            using var doc = ParseTestMapping();
            Assert.True(doc.RootElement.TryGetProperty("totalTasks", out var total));
            Assert.True(total.GetInt32() > 0, "totalTasks should be a positive number");
        }

        [Fact]
        public void TestMapping_TotalTasksMatchesArrayLength()
        {
            using var doc = ParseTestMapping();
            var total = doc.RootElement.GetProperty("totalTasks").GetInt32();
            var tasks = doc.RootElement.GetProperty("tasks");
            Assert.Equal(total, tasks.GetArrayLength());
        }

        [Fact]
        public void TestMapping_EachTask_HasRequiredFields()
        {
            using var doc = ParseTestMapping();
            var tasks = doc.RootElement.GetProperty("tasks");

            foreach (var task in tasks.EnumerateArray())
            {
                Assert.True(task.TryGetProperty("maskedFile", out _), "Each task should have 'maskedFile'");
                Assert.True(task.TryGetProperty("classes", out _), "Each task should have 'classes'");
                Assert.True(task.TryGetProperty("fixedTestMethods", out _), "Each task should have 'fixedTestMethods'");
            }
        }

        [Fact]
        public void TestMapping_EachTask_HasNonEmptyClasses()
        {
            using var doc = ParseTestMapping();
            var tasks = doc.RootElement.GetProperty("tasks");

            foreach (var task in tasks.EnumerateArray())
            {
                var classes = task.GetProperty("classes");
                Assert.True(classes.GetArrayLength() > 0,
                    $"Task '{task.GetProperty("maskedFile").GetString()}' should have at least one class");
            }
        }

        [Fact]
        public void TestMapping_EachTask_HasCategory()
        {
            using var doc = ParseTestMapping();
            var tasks = doc.RootElement.GetProperty("tasks");

            foreach (var task in tasks.EnumerateArray())
            {
                var category = task.GetProperty("category").GetString();
                Assert.False(string.IsNullOrWhiteSpace(category),
                    $"Task '{task.GetProperty("maskedFile").GetString()}' should have a non-empty category");
            }
        }

        [Fact]
        public void TestMapping_EachTask_MaskedFilePathStartsWithMaskedTasks()
        {
            using var doc = ParseTestMapping();
            var tasks = doc.RootElement.GetProperty("tasks");

            foreach (var task in tasks.EnumerateArray())
            {
                var maskedFile = task.GetProperty("maskedFile").GetString()!;
                Assert.StartsWith("MaskedTasks/", maskedFile);
            }
        }

        [Fact]
        public void TestMapping_EachTask_HasTestFilesProperty()
        {
            using var doc = ParseTestMapping();
            var tasks = doc.RootElement.GetProperty("tasks");

            foreach (var task in tasks.EnumerateArray())
            {
                Assert.True(task.TryGetProperty("testFiles", out var testFiles),
                    $"Task '{task.GetProperty("maskedFile").GetString()}' should have 'testFiles' property");
                Assert.Equal(JsonValueKind.Array, testFiles.ValueKind);
            }
        }

        [Fact]
        public void TestMapping_AtLeastOneTask_HasTestFiles()
        {
            using var doc = ParseTestMapping();
            var tasks = doc.RootElement.GetProperty("tasks");
            bool anyHasTestFiles = tasks.EnumerateArray()
                .Any(t => t.GetProperty("testFiles").GetArrayLength() > 0);
            Assert.True(anyHasTestFiles, "At least one task should have test files defined");
        }

        [Fact]
        public void TestMapping_EachTask_FixedTestMethodsAreStrings()
        {
            using var doc = ParseTestMapping();
            var tasks = doc.RootElement.GetProperty("tasks");

            foreach (var task in tasks.EnumerateArray())
            {
                var methods = task.GetProperty("fixedTestMethods");
                foreach (var method in methods.EnumerateArray())
                {
                    Assert.Equal(JsonValueKind.String, method.ValueKind);
                    Assert.False(string.IsNullOrWhiteSpace(method.GetString()));
                }
            }
        }

        // ─── config.json agent section tests (Phase 3 dependencies) ───────────────

        [Fact]
        public void ConfigJson_Agent_HasCommandForPhase3()
        {
            using var doc = ParseConfig();
            var command = doc.RootElement.GetProperty("agent").GetProperty("command").GetString();
            Assert.False(string.IsNullOrWhiteSpace(command), "agent.command must be set for Phase 3");
        }

        [Fact]
        public void ConfigJson_Agent_HasFlagsArrayForPhase3()
        {
            using var doc = ParseConfig();
            var flags = doc.RootElement.GetProperty("agent").GetProperty("flags");
            Assert.Equal(JsonValueKind.Array, flags.ValueKind);
            Assert.True(flags.GetArrayLength() > 0, "agent.flags should not be empty");
        }

        [Fact]
        public void ConfigJson_Agent_HasModelForPhase3()
        {
            using var doc = ParseConfig();
            var model = doc.RootElement.GetProperty("agent").GetProperty("model").GetString();
            Assert.False(string.IsNullOrWhiteSpace(model), "agent.model must be set for Phase 3 agent invocation");
        }

        // ─── Script references pipeline-test-mapping.json ─────────────────────────

        [Fact]
        public void RunPipelineScript_ReferencesTestMappingFile()
        {
            Assert.Contains("pipeline-test-mapping.json", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_ReferencesConfigFile()
        {
            Assert.Contains("config.json", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_ReferencesSolutionFile()
        {
            Assert.Contains("SdkMultithreadingMigration.slnx", ScriptContent);
        }

        // ─── Error handling in Phase 3 ────────────────────────────────────────────

        [Fact]
        public void RunPipelineScript_InvokeAgentForTask_HasTryCatch()
        {
            // The agent invocation function should handle errors
            var functionBody = ExtractFunctionBody("Invoke-AgentForTask");
            Assert.Contains("try", functionBody);
            Assert.Contains("catch", functionBody);
        }

        [Fact]
        public void RunPipelineScript_InvokeTestsForTask_HasTryCatch()
        {
            var functionBody = ExtractFunctionBody("Invoke-TestsForTask");
            Assert.Contains("try", functionBody);
            Assert.Contains("catch", functionBody);
        }

        [Fact]
        public void RunPipelineScript_GetTestResults_HandlesTrxParsingGracefully()
        {
            // TRX parsing is best-effort; should have catch block
            var functionBody = ExtractFunctionBody("Get-TestResults");
            Assert.Contains("try", functionBody);
            Assert.Contains("catch", functionBody);
        }

        // ─── Phase 3 output and logging ───────────────────────────────────────────

        [Fact]
        public void RunPipelineScript_SavesAgentOutputToLog()
        {
            Assert.Contains("agent-output.log", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_SavesTestOutputToLog()
        {
            Assert.Contains("test-output.log", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_SavesPromptToIterationDir()
        {
            Assert.Contains("prompt.md", ScriptContent);
        }

        [Fact]
        public void RunPipelineScript_ReportsPassFailSummary()
        {
            Assert.Contains("[PASS]", ScriptContent);
            Assert.Contains("[FAIL]", ScriptContent);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private JsonDocument ParseTestMapping()
        {
            var content = File.ReadAllText(TestMappingPath);
            return JsonDocument.Parse(content);
        }

        private JsonDocument ParseConfig()
        {
            var content = File.ReadAllText(ConfigPath);
            return JsonDocument.Parse(content);
        }

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

            // Read until next top-level function declaration or end of file
            var bodyLines = new System.Collections.Generic.List<string>();
            for (int i = startIndex + 1; i < lines.Length; i++)
            {
                // Stop at next top-level function (not indented)
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
