using Xunit;

namespace Tamp.Core.Tests;

public sealed class CiHostTests
{
    private static Func<string, string?> Env(params (string K, string V)[] pairs)
    {
        var d = pairs.ToDictionary(p => p.K, p => p.V, StringComparer.Ordinal);
        return k => d.TryGetValue(k, out var v) ? v : null;
    }

    // ---- Detection ----

    [Fact]
    public void Detect_Returns_Null_Outside_CI()
    {
        Assert.Null(CiHost.Detect(_ => null));
    }

    [Fact]
    public void Detect_Returns_GitHubActionsHost_For_GH_Env()
    {
        var host = CiHost.Detect(Env(("GITHUB_ACTIONS", "true")));
        Assert.IsType<GitHubActionsHost>(host);
        Assert.Equal(CiVendor.GitHubActions, host!.Vendor);
    }

    [Fact]
    public void Detect_Returns_AzureDevOpsHost_For_TF_BUILD_Env()
    {
        var host = CiHost.Detect(Env(("TF_BUILD", "True")));
        Assert.IsType<AzureDevOpsHost>(host);
        Assert.Equal(CiVendor.AzureDevOps, host!.Vendor);
    }

    [Fact]
    public void Detect_Returns_TeamCityHost_For_TEAMCITY_VERSION_Env()
    {
        var host = CiHost.Detect(Env(("TEAMCITY_VERSION", "2024.03")));
        Assert.IsType<TeamCityHost>(host);
        Assert.Equal(CiVendor.TeamCity, host!.Vendor);
    }

    [Fact]
    public void Detect_Returns_Null_For_Vendors_We_Do_Not_Support_Yet()
    {
        // GitLab CI is detected by HostProfile but Tamp doesn't ship a
        // typed CiHost for it yet.
        var host = CiHost.Detect(Env(("GITLAB_CI", "true")));
        Assert.Null(host);
    }

    // ---- GitHub Actions ----

    [Fact]
    public void GitHub_Reads_Standard_Env_Vars()
    {
        var gh = new GitHubActionsHost(Env(
            ("GITHUB_WORKFLOW", "CI"),
            ("GITHUB_RUN_ID", "12345"),
            ("GITHUB_RUN_NUMBER", "42"),
            ("GITHUB_REPOSITORY", "owner/repo"),
            ("GITHUB_SHA", "abc123"),
            ("GITHUB_REF_NAME", "main"),
            ("GITHUB_ACTOR", "user"),
            ("GITHUB_EVENT_NAME", "push")
        ));
        Assert.Equal("CI", gh.Workflow);
        Assert.Equal("12345", gh.RunId);
        Assert.Equal("42", gh.RunNumber);
        Assert.Equal("owner/repo", gh.Repository);
        Assert.Equal("abc123", gh.Sha);
        Assert.Equal("main", gh.RefName);
        Assert.Equal("user", gh.Actor);
        Assert.Equal("push", gh.EventName);
        Assert.False(gh.IsPullRequest);
    }

    [Theory]
    [InlineData("pull_request", true)]
    [InlineData("pull_request_target", true)]
    [InlineData("push", false)]
    public void GitHub_IsPullRequest_Reflects_EventName(string eventName, bool expected)
    {
        var gh = new GitHubActionsHost(Env(("GITHUB_EVENT_NAME", eventName)));
        Assert.Equal(expected, gh.IsPullRequest);
    }

    [Fact]
    public void GitHub_OpenGroup_CloseGroup_Use_Workflow_Commands()
    {
        var sw = new StringWriter();
        var gh = new GitHubActionsHost(Env(), sw);
        gh.OpenGroup("Build");
        gh.CloseGroup();
        var output = sw.ToString().Split(Environment.NewLine);
        Assert.Equal("::group::Build", output[0]);
        Assert.Equal("::endgroup::", output[1]);
    }

    [Fact]
    public void GitHub_LogError_And_LogWarning_Use_Workflow_Commands()
    {
        var sw = new StringWriter();
        var gh = new GitHubActionsHost(Env(), sw);
        gh.LogError("e");
        gh.LogWarning("w");
        var output = sw.ToString();
        Assert.Contains("::error::e", output);
        Assert.Contains("::warning::w", output);
    }

    [Fact]
    public void GitHub_Error_Escapes_Special_Characters()
    {
        var sw = new StringWriter();
        var gh = new GitHubActionsHost(Env(), sw);
        gh.LogError("100% broken\nnext line");
        var output = sw.ToString();
        Assert.Contains("100%25 broken%0Anext line", output);
    }

    [Fact]
    public void GitHub_SetVariable_Writes_To_GITHUB_ENV_File_When_Set()
    {
        var envFile = Path.GetTempFileName();
        try
        {
            var gh = new GitHubActionsHost(Env(("GITHUB_ENV", envFile)), new StringWriter());
            gh.SetVariable("MY_VAR", "hello");
            var content = File.ReadAllText(envFile);
            Assert.Contains("MY_VAR<<", content);
            Assert.Contains("hello", content);
        }
        finally { File.Delete(envFile); }
    }

    [Fact]
    public void GitHub_SetVariable_Falls_Back_To_Set_Env_Command_When_File_Missing()
    {
        var sw = new StringWriter();
        var gh = new GitHubActionsHost(Env(), sw);
        gh.SetVariable("X", "y");
        Assert.Contains("::set-env name=X::y", sw.ToString());
    }

    [Fact]
    public void GitHub_AppendStepSummary_Writes_To_Summary_File()
    {
        var summaryFile = Path.GetTempFileName();
        try
        {
            var gh = new GitHubActionsHost(Env(("GITHUB_STEP_SUMMARY", summaryFile)));
            gh.AppendStepSummary("# Heading");
            Assert.Contains("# Heading", File.ReadAllText(summaryFile));
        }
        finally { File.Delete(summaryFile); }
    }

    [Fact]
    public void GitHub_MaskValue_Emits_Add_Mask_Command()
    {
        var sw = new StringWriter();
        var gh = new GitHubActionsHost(Env(), sw);
        gh.MaskValue("secret123");
        Assert.Contains("::add-mask::secret123", sw.ToString());
    }

    // ---- Azure DevOps ----

    [Fact]
    public void Ado_Reads_Standard_Env_Vars()
    {
        var ado = new AzureDevOpsHost(Env(
            ("BUILD_BUILDID", "1234"),
            ("BUILD_BUILDNUMBER", "20260510.1"),
            ("BUILD_REASON", "IndividualCI"),
            ("BUILD_REPOSITORY_NAME", "myrepo"),
            ("BUILD_SOURCEBRANCHNAME", "main"),
            ("BUILD_SOURCEVERSION", "abc123"),
            ("AGENT_OS", "Linux")
        ));
        Assert.Equal("1234", ado.BuildId);
        Assert.Equal("20260510.1", ado.BuildNumber);
        Assert.Equal("IndividualCI", ado.Reason);
        Assert.Equal("myrepo", ado.RepositoryName);
        Assert.Equal("main", ado.SourceBranchName);
        Assert.Equal("abc123", ado.SourceVersion);
        Assert.Equal("Linux", ado.AgentOs);
        Assert.False(ado.IsPullRequest);
    }

    [Fact]
    public void Ado_IsPullRequest_Set_When_PullRequestId_Present()
    {
        var ado = new AzureDevOpsHost(Env(("SYSTEM_PULLREQUEST_PULLREQUESTID", "42")));
        Assert.True(ado.IsPullRequest);
        Assert.Equal("42", ado.PullRequestId);
    }

    [Fact]
    public void Ado_OpenGroup_CloseGroup_Use_VSO_Commands()
    {
        var sw = new StringWriter();
        var ado = new AzureDevOpsHost(Env(), sw);
        ado.OpenGroup("Build");
        ado.CloseGroup();
        var output = sw.ToString().Split(Environment.NewLine);
        Assert.Equal("##[group]Build", output[0]);
        Assert.Equal("##[endgroup]", output[1]);
    }

    [Fact]
    public void Ado_LogIssue_Uses_VSO_LogIssue_Command()
    {
        var sw = new StringWriter();
        var ado = new AzureDevOpsHost(Env(), sw);
        ado.LogError("oops");
        ado.LogWarning("careful");
        var output = sw.ToString();
        Assert.Contains("##vso[task.logissue type=error]oops", output);
        Assert.Contains("##vso[task.logissue type=warning]careful", output);
    }

    [Fact]
    public void Ado_SetVariable_And_SetSecretVariable_Distinguished()
    {
        var sw = new StringWriter();
        var ado = new AzureDevOpsHost(Env(), sw);
        ado.SetVariable("normal", "value");
        ado.SetSecretVariable("token", "secret");
        var output = sw.ToString();
        Assert.Contains("##vso[task.setvariable variable=normal]value", output);
        Assert.Contains("##vso[task.setvariable variable=token;issecret=true]secret", output);
    }

    [Fact]
    public void Ado_UpdateBuildNumber_Emits_Build_UpdateBuildNumber()
    {
        var sw = new StringWriter();
        var ado = new AzureDevOpsHost(Env(), sw);
        ado.UpdateBuildNumber("custom-1.2.3");
        Assert.Contains("##vso[build.updatebuildnumber]custom-1.2.3", sw.ToString());
    }

    [Fact]
    public void Ado_UploadArtifact_Emits_UploadFile()
    {
        var sw = new StringWriter();
        var ado = new AzureDevOpsHost(Env(), sw);
        ado.UploadArtifact("/tmp/x.zip");
        Assert.Contains("##vso[task.uploadfile]/tmp/x.zip", sw.ToString());
    }

    // ---- TeamCity ----

    [Fact]
    public void TeamCity_Reads_Standard_Env_Vars()
    {
        var tc = new TeamCityHost(Env(
            ("TEAMCITY_VERSION", "2024.07"),
            ("TEAMCITY_PROJECT_NAME", "MyProject"),
            ("TEAMCITY_BUILDCONF_NAME", "Build"),
            ("BUILD_NUMBER", "1.2.3"),
            ("BUILD_VCS_NUMBER", "abc123")
        ));
        Assert.Equal("2024.07", tc.Version);
        Assert.Equal("MyProject", tc.ProjectName);
        Assert.Equal("Build", tc.BuildConfigurationName);
        Assert.Equal("1.2.3", tc.BuildNumber);
        Assert.Equal("abc123", tc.VcsNumber);
    }

    [Fact]
    public void TeamCity_OpenGroup_CloseGroup_Use_BlockOpened_BlockClosed()
    {
        var sw = new StringWriter();
        var tc = new TeamCityHost(Env(), sw);
        tc.OpenGroup("Build");
        tc.CloseGroup();
        var output = sw.ToString();
        Assert.Contains("##teamcity[blockOpened name='Build']", output);
        Assert.Contains("##teamcity[blockClosed name='']", output);
    }

    [Fact]
    public void TeamCity_LogError_And_LogWarning_Use_Status_Field()
    {
        var sw = new StringWriter();
        var tc = new TeamCityHost(Env(), sw);
        tc.LogError("e");
        tc.LogWarning("w");
        var output = sw.ToString();
        Assert.Contains("status='ERROR'", output);
        Assert.Contains("status='WARNING'", output);
        Assert.Contains("text='e'", output);
        Assert.Contains("text='w'", output);
    }

    [Fact]
    public void TeamCity_Escapes_Pipe_Apostrophe_And_Brackets()
    {
        var sw = new StringWriter();
        var tc = new TeamCityHost(Env(), sw);
        // Special chars per TeamCity escape rules.
        tc.LogError("she said 'hi'|next [bracket]");
        var output = sw.ToString();
        // Apostrophes → |' ; pipes → || ; [ → |[ ; ] → |]
        Assert.Contains("|'hi|'", output);
        Assert.Contains("||next", output);
        Assert.Contains("|[bracket|]", output);
    }

    [Fact]
    public void TeamCity_SetVariable_Uses_SetParameter()
    {
        var sw = new StringWriter();
        var tc = new TeamCityHost(Env(), sw);
        tc.SetVariable("env.MY_VAR", "value");
        Assert.Contains("##teamcity[setParameter name='env.MY_VAR' value='value']", sw.ToString());
    }

    [Fact]
    public void TeamCity_UpdateBuildNumber_Emits_BuildNumber()
    {
        var sw = new StringWriter();
        var tc = new TeamCityHost(Env(), sw);
        tc.UpdateBuildNumber("v1.2.3");
        Assert.Contains("##teamcity[buildNumber 'v1.2.3']", sw.ToString());
    }

    [Fact]
    public void TeamCity_PublishArtifact_Emits_PublishArtifacts()
    {
        var sw = new StringWriter();
        var tc = new TeamCityHost(Env(), sw);
        tc.PublishArtifact("artifacts/*.nupkg");
        Assert.Contains("##teamcity[publishArtifacts 'artifacts/*.nupkg']", sw.ToString());
    }

    [Fact]
    public void TeamCity_ReportBuildProblem_Emits_BuildProblem()
    {
        var sw = new StringWriter();
        var tc = new TeamCityHost(Env(), sw);
        tc.ReportBuildProblem("compile failed");
        Assert.Contains("##teamcity[buildProblem description='compile failed']", sw.ToString());
    }

    // ---- TampBuild integration ----

    [Fact]
    public void TampBuild_CiHost_Property_Resolves_Consistent_With_Environment()
    {
        // This test runs both on developer machines (no CI vars → null) and
        // on GitHub Actions itself (GITHUB_ACTIONS=true → GitHubActionsHost).
        // Assert the property reflects whichever environment we're actually
        // in, rather than assuming a specific one.
        TampBuild.ResetCachedDirectories();
        var host = TampBuild.CiHost;
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
        {
            Assert.IsType<GitHubActionsHost>(host);
        }
        else if (Environment.GetEnvironmentVariable("TF_BUILD") == "True")
        {
            Assert.IsType<AzureDevOpsHost>(host);
        }
        else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TEAMCITY_VERSION")))
        {
            Assert.IsType<TeamCityHost>(host);
        }
        else if (Environment.GetEnvironmentVariable("CI") == "true")
        {
            // Some other CI vendor not yet adapted; CiHost.Detect returns null
            // for unsupported vendors. Acceptable.
            Assert.Null(host);
        }
        else
        {
            Assert.Null(host);
        }
    }
}
