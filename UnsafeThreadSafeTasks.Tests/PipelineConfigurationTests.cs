using System;
using System.IO;
using System.Text.Json;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests
{
    public class PipelineConfigurationTests
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

        // --- Directory structure tests ---

        [Fact]
        public void PipelineDirectory_Exists()
        {
            Assert.True(Directory.Exists(PipelineDir), "pipeline/ directory should exist at repo root");
        }

        // --- config.json tests ---

        [Fact]
        public void ConfigJson_Exists()
        {
            Assert.True(File.Exists(Path.Combine(PipelineDir, "config.json")));
        }

        [Fact]
        public void ConfigJson_IsValidJson()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, "config.json"));
            var doc = JsonDocument.Parse(content);
            Assert.NotNull(doc);
        }

        [Fact]
        public void ConfigJson_HasRepoSection()
        {
            using var doc = ParseConfig();
            Assert.True(doc.RootElement.TryGetProperty("repo", out _), "config.json should have a 'repo' section");
        }

        [Fact]
        public void ConfigJson_RepoRoot_PointsToParent()
        {
            using var doc = ParseConfig();
            var root = doc.RootElement.GetProperty("repo").GetProperty("root").GetString();
            Assert.Equal("..", root);
        }

        [Fact]
        public void ConfigJson_RepoSolution_PointsToSlnx()
        {
            using var doc = ParseConfig();
            var solution = doc.RootElement.GetProperty("repo").GetProperty("solution").GetString();
            Assert.Equal("../SdkMultithreadingMigration.slnx", solution);
        }

        [Fact]
        public void ConfigJson_ReferencedSolution_Exists()
        {
            using var doc = ParseConfig();
            var solution = doc.RootElement.GetProperty("repo").GetProperty("solution").GetString()!;
            var resolved = Path.GetFullPath(Path.Combine(PipelineDir, solution));
            Assert.True(File.Exists(resolved), $"Referenced solution '{solution}' should exist at '{resolved}'");
        }

        [Fact]
        public void ConfigJson_HasAgentSection()
        {
            using var doc = ParseConfig();
            Assert.True(doc.RootElement.TryGetProperty("agent", out _), "config.json should have an 'agent' section");
        }

        [Fact]
        public void ConfigJson_AgentCommand_IsCopilot()
        {
            using var doc = ParseConfig();
            var command = doc.RootElement.GetProperty("agent").GetProperty("command").GetString();
            Assert.Equal("copilot", command);
        }

        [Fact]
        public void ConfigJson_AgentFlags_ContainsYolo()
        {
            using var doc = ParseConfig();
            var flags = doc.RootElement.GetProperty("agent").GetProperty("flags");
            Assert.Contains("--yolo", EnumerateStringArray(flags));
        }

        [Fact]
        public void ConfigJson_AgentModel_IsSet()
        {
            using var doc = ParseConfig();
            var model = doc.RootElement.GetProperty("agent").GetProperty("model").GetString();
            Assert.False(string.IsNullOrWhiteSpace(model), "agent.model should be set");
        }

        [Fact]
        public void ConfigJson_AgentVersion_IsSet()
        {
            using var doc = ParseConfig();
            var version = doc.RootElement.GetProperty("agent").GetProperty("version").GetString();
            Assert.False(string.IsNullOrWhiteSpace(version), "agent.version should be set");
        }

        [Fact]
        public void ConfigJson_HasDirectoriesSection()
        {
            using var doc = ParseConfig();
            Assert.True(doc.RootElement.TryGetProperty("directories", out _), "config.json should have a 'directories' section");
        }

        [Fact]
        public void ConfigJson_DirectoriesPrompts_PointsToSkills()
        {
            using var doc = ParseConfig();
            var prompts = doc.RootElement.GetProperty("directories").GetProperty("prompts").GetString();
            Assert.Equal("../skills", prompts);
        }

        [Fact]
        public void ConfigJson_ReferencedPromptsDir_Exists()
        {
            using var doc = ParseConfig();
            var prompts = doc.RootElement.GetProperty("directories").GetProperty("prompts").GetString()!;
            var resolved = Path.GetFullPath(Path.Combine(PipelineDir, prompts));
            Assert.True(Directory.Exists(resolved), $"Referenced prompts directory '{prompts}' should exist at '{resolved}'");
        }

        [Fact]
        public void ConfigJson_DirectoriesLogs_IsLogs()
        {
            using var doc = ParseConfig();
            var logs = doc.RootElement.GetProperty("directories").GetProperty("logs").GetString();
            Assert.Equal("logs", logs);
        }

        [Fact]
        public void ConfigJson_DirectoriesReports_IsReports()
        {
            using var doc = ParseConfig();
            var reports = doc.RootElement.GetProperty("directories").GetProperty("reports").GetString();
            Assert.Equal("reports", reports);
        }

        // --- .gitignore tests ---

        [Fact]
        public void GitIgnore_Exists()
        {
            Assert.True(File.Exists(Path.Combine(PipelineDir, ".gitignore")));
        }

        [Fact]
        public void GitIgnore_IgnoresLogsDirectory()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, ".gitignore"));
            Assert.Contains("logs/", content);
        }

        [Fact]
        public void GitIgnore_IgnoresReportsDirectory()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, ".gitignore"));
            Assert.Contains("reports/", content);
        }

        [Fact]
        public void GitIgnore_IgnoresTrxFiles()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, ".gitignore"));
            Assert.Contains("*.trx", content);
        }

        // --- README.md tests ---

        [Fact]
        public void ReadMe_Exists()
        {
            Assert.True(File.Exists(Path.Combine(PipelineDir, "README.md")));
        }

        [Fact]
        public void ReadMe_ContainsPrerequisitesSection()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, "README.md"));
            Assert.Contains("## Prerequisites", content);
        }

        [Fact]
        public void ReadMe_ContainsUsageSection()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, "README.md"));
            Assert.Contains("## Usage", content);
        }

        [Fact]
        public void ReadMe_MentionsCopilotCli()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, "README.md"));
            Assert.Contains("Copilot CLI", content);
        }

        // --- run-pipeline.ps1 tests ---

        [Fact]
        public void RunPipelineScript_Exists()
        {
            Assert.True(File.Exists(Path.Combine(PipelineDir, "run-pipeline.ps1")),
                "pipeline/run-pipeline.ps1 should exist");
        }

        [Fact]
        public void RunPipelineScript_DefinesInvokePhase1Function()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, "run-pipeline.ps1"));
            Assert.Contains("function Invoke-Phase1", content);
        }

        [Fact]
        public void RunPipelineScript_DefinesBuildMigrationPromptFunction()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, "run-pipeline.ps1"));
            Assert.Contains("function Build-MigrationPrompt", content);
        }

        [Fact]
        public void RunPipelineScript_DefinesGetForbiddenApiReferenceFunction()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, "run-pipeline.ps1"));
            Assert.Contains("function Get-ForbiddenApiReference", content);
        }

        [Fact]
        public void RunPipelineScript_ReferencesSharedPolyfills()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, "run-pipeline.ps1"));
            Assert.Contains("SharedPolyfills", content);
        }

        [Fact]
        public void RunPipelineScript_ReferencesMaskedTasks()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, "run-pipeline.ps1"));
            Assert.Contains("MaskedTasks", content);
        }

        [Fact]
        public void RunPipelineScript_HasSynopsisComment()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, "run-pipeline.ps1"));
            Assert.Contains(".SYNOPSIS", content);
        }

        // --- Additional config.json agent flag tests ---

        [Fact]
        public void ConfigJson_AgentFlags_ContainsNoAskUser()
        {
            using var doc = ParseConfig();
            var flags = doc.RootElement.GetProperty("agent").GetProperty("flags");
            Assert.Contains("--no-ask-user", EnumerateStringArray(flags));
        }

        [Fact]
        public void ConfigJson_AgentFlags_ContainsSilent()
        {
            using var doc = ParseConfig();
            var flags = doc.RootElement.GetProperty("agent").GetProperty("flags");
            Assert.Contains("--silent", EnumerateStringArray(flags));
        }

        // --- Additional .gitignore tests ---

        [Fact]
        public void GitIgnore_IgnoresPromptsDirectory()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, ".gitignore"));
            Assert.Contains("prompts/", content);
        }

        // --- Additional README.md tests ---

        [Fact]
        public void ReadMe_ContainsPhasesSection()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, "README.md"));
            Assert.Contains("## Phases", content);
        }

        [Fact]
        public void ReadMe_ContainsOutputStructureSection()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, "README.md"));
            Assert.Contains("## Output Structure", content);
        }

        [Fact]
        public void ReadMe_DescribesConfigJson()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, "README.md"));
            Assert.Contains("config.json", content);
        }

        // --- Pipeline directory structure completeness ---

        [Fact]
        public void PipelineDirectory_ContainsExpectedFiles()
        {
            var expectedFiles = new[] { "config.json", ".gitignore", "README.md", "run-pipeline.ps1" };
            foreach (var file in expectedFiles)
            {
                Assert.True(File.Exists(Path.Combine(PipelineDir, file)),
                    $"pipeline/{file} should exist");
            }
        }

        // --- Helpers ---

        private JsonDocument ParseConfig()
        {
            var content = File.ReadAllText(Path.Combine(PipelineDir, "config.json"));
            return JsonDocument.Parse(content);
        }

        private static string[] EnumerateStringArray(JsonElement array)
        {
            var items = new System.Collections.Generic.List<string>();
            foreach (var item in array.EnumerateArray())
            {
                items.Add(item.GetString()!);
            }
            return items.ToArray();
        }
    }
}
