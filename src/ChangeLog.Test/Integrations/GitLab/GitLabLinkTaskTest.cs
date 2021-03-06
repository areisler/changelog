﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using GitLabApiClient;
using GitLabApiClient.Internal.Paths;
using GitLabApiClient.Models.Commits.Responses;
using GitLabApiClient.Models.Issues.Responses;
using GitLabApiClient.Models.MergeRequests.Requests;
using GitLabApiClient.Models.MergeRequests.Responses;
using GitLabApiClient.Models.Milestones.Requests;
using GitLabApiClient.Models.Milestones.Responses;
using Grynwald.ChangeLog.ConventionalCommits;
using Grynwald.ChangeLog.Git;
using Grynwald.ChangeLog.Integrations.GitLab;
using Grynwald.ChangeLog.Model;
using Grynwald.ChangeLog.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Grynwald.ChangeLog.Test.Integrations.GitLab
{
    /// <summary>
    /// Unit tests for <see cref="GitLabLinkTask"/>
    /// </summary>
    public class GitLabLinkTaskTest : TestBase
    {
        private readonly ILogger<GitLabLinkTask> m_Logger = NullLogger<GitLabLinkTask>.Instance;
        private readonly Mock<IGitLabClientFactory> m_ClientFactoryMock;
        private readonly Mock<IGitLabClient> m_ClientMock;
        private readonly Mock<ICommitsClient> m_CommitsClientMock;
        private readonly Mock<IIssuesClient> m_IssuesClientMock;
        private readonly Mock<IMergeRequestsClient> m_MergeRequestsClientMock;
        private readonly Mock<IProjectsClient> m_ProjectsClientMock;
        private readonly Mock<IGitRepository> m_RepositoryMock;

        public GitLabLinkTaskTest()
        {
            m_CommitsClientMock = new Mock<ICommitsClient>(MockBehavior.Strict);

            m_IssuesClientMock = new Mock<IIssuesClient>(MockBehavior.Strict);

            m_MergeRequestsClientMock = new Mock<IMergeRequestsClient>(MockBehavior.Strict);

            m_ProjectsClientMock = new Mock<IProjectsClient>(MockBehavior.Strict);

            m_ClientMock = new Mock<IGitLabClient>(MockBehavior.Strict);
            m_ClientMock.Setup(x => x.Commits).Returns(m_CommitsClientMock.Object);
            m_ClientMock.Setup(x => x.Issues).Returns(m_IssuesClientMock.Object);
            m_ClientMock.Setup(x => x.MergeRequests).Returns(m_MergeRequestsClientMock.Object);
            m_ClientMock.Setup(x => x.Projects).Returns(m_ProjectsClientMock.Object);

            m_ClientFactoryMock = new Mock<IGitLabClientFactory>(MockBehavior.Strict);
            m_ClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(m_ClientMock.Object);

            m_RepositoryMock = new Mock<IGitRepository>(MockBehavior.Strict);
        }

        private ProjectId MatchProjectId(string expected)
        {
            return It.Is<ProjectId>((ProjectId actual) => actual.ToString() == ((ProjectId)expected).ToString());
        }

        /// <summary>
        /// Helper method to test if an Action performs the expected changes to an object
        /// </summary>
        /// <typeparam name="T">The type of object the changes are applied to.</typeparam>
        /// <param name="actionToVerify">The action to apply to the object.</param>
        /// <param name="assertions">The assertions to apply to the object after applying <paramref name="actionToVerify"/></param>
        private bool AssertAction<T>(Action<T> actionToVerify, params Action<T>[] assertions)
        {
            // I'm not sure this is a good idea
            // but the only way we can make any assertions about an action
            // is calling it an afterwards checking if it made the expected
            // changes to the instance.
            // The instance in turn needs to be created through reflection because
            // GitLabApiClient's query types are sealed with an internal constructor

            var type = typeof(T);
            var instance = (T)type.Assembly.CreateInstance(
                type.FullName!, false,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null, null, null, null)!;

            if (instance == null)
                throw new InvalidOperationException($"Failed to create instance of '{type.FullName}'");

            // Apply the action we want to verify
            actionToVerify.Invoke(instance);

            // Apply the assertions to the instance
            foreach (var assertion in assertions)
            {
                assertion.Invoke(instance);
            }

            return true;
        }


        [Fact]
        public async Task Run_does_nothing_if_repository_does_not_have_remotes()
        {
            // ARRANGE            
            m_RepositoryMock.Setup(x => x.Remotes).Returns(Enumerable.Empty<GitRemote>());

            var sut = new GitLabLinkTask(m_Logger, m_RepositoryMock.Object, m_ClientFactoryMock.Object);
            var changeLog = new ApplicationChangeLog();

            // ACT 
            var result = await sut.RunAsync(changeLog);

            // ASSERT
            Assert.Equal(ChangeLogTaskResult.Skipped, result);

            m_ClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("not-a-url")]
        [InlineData("http://not-a-gitlab-url.com")]
        public async Task Run_does_nothing_if_remote_url_cannot_be_parsed(string url)
        {
            // ARRANGE            
            m_RepositoryMock.Setup(x => x.Remotes).Returns(new[] { new GitRemote("origin", url) });

            var sut = new GitLabLinkTask(m_Logger, m_RepositoryMock.Object, m_ClientFactoryMock.Object);
            var changeLog = new ApplicationChangeLog();

            // ACT 
            var result = await sut.RunAsync(changeLog);

            // ASSERT
            Assert.Equal(ChangeLogTaskResult.Skipped, result);

            m_ClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Run_adds_a_link_to_all_commits_if_url_can_be_parsed()
        {
            // ARRANGE
            m_RepositoryMock.Setup(x => x.Remotes).Returns(new[] { new GitRemote("origin", "http://gitlab.com/user/repo.git") });

            m_CommitsClientMock
                .Setup(x => x.GetAsync(MatchProjectId("user/repo"), It.IsAny<string>()))
                .Returns(
                    (ProjectId id, string sha) => Task.FromResult(new Commit() { WebUrl = $"https://example.com/{sha}" })
                );

            var sut = new GitLabLinkTask(m_Logger, m_RepositoryMock.Object, m_ClientFactoryMock.Object);

            var changeLog = new ApplicationChangeLog()
            {
                GetSingleVersionChangeLog(
                    "1.2.3",
                    null,
                    GetChangeLogEntry(summary: "Entry1", commit: "01"),
                    GetChangeLogEntry(summary: "Entry2", commit: "02")
                ),
                GetSingleVersionChangeLog(
                    "4.5.6",
                    null,
                    GetChangeLogEntry(summary: "Entry1", commit: "03"),
                    GetChangeLogEntry(summary: "Entry2", commit: "04")
                )
            };

            // ACT 
            var result = await sut.RunAsync(changeLog);

            // ASSERT
            Assert.Equal(ChangeLogTaskResult.Success, result);

            var entries = changeLog.ChangeLogs.SelectMany(x => x.AllEntries).ToArray();
            Assert.All(entries, entry =>
            {
                Assert.NotNull(entry.CommitWebUri);
                var expectedUri = new Uri($"https://example.com/{entry.Commit}");
                Assert.Equal(expectedUri, entry.CommitWebUri);

                m_CommitsClientMock.Verify(x => x.GetAsync(MatchProjectId("user/repo"), entry.Commit.Id), Times.Once);
            });

            m_CommitsClientMock.Verify(x => x.GetAsync(It.IsAny<ProjectId>(), It.IsAny<string>()), Times.Exactly(entries.Length));
        }

        [Theory]
        [InlineData("gitlab.com")]
        [InlineData("example.com")]
        public async Task Run_creates_client_through_client_factory(string hostName)
        {
            // ARRANGE          
            m_RepositoryMock.Setup(x => x.Remotes).Returns(new[] { new GitRemote("origin", $"http://{hostName}/owner/repo.git") });

            var sut = new GitLabLinkTask(m_Logger, m_RepositoryMock.Object, m_ClientFactoryMock.Object);

            // ACT 
            var result = await sut.RunAsync(new ApplicationChangeLog());

            // ASSERT
            Assert.Equal(ChangeLogTaskResult.Success, result);

            m_ClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Once);
            m_ClientFactoryMock.Verify(x => x.CreateClient(hostName), Times.Once);
        }

        [Theory]
        // reference within the same repository
        [InlineData("#23", 23, "owner/repo")]
        // reference to another project in the same namespace
        [InlineData("anotherRepo#42", 42, "owner/anotherRepo")]
        // reference to another project (including project namespace)
        [InlineData("anotherOwner/anotherRepo#42", 42, "anotherOwner/anotherRepo")]
        [InlineData("another-Owner/another-Repo#42", 42, "another-Owner/another-Repo")]
        [InlineData("another.Owner/another.Repo#42", 42, "another.Owner/another.Repo")]
        [InlineData("another_Owner/another_Repo#42", 42, "another_Owner/another_Repo")]
        // Linking must ignore trailing and leading whitespace
        [InlineData(" #23", 23, "owner/repo")]
        [InlineData("#23 ", 23, "owner/repo")]
        [InlineData(" anotherRepo#42", 42, "owner/anotherRepo")]
        [InlineData("anotherRepo#42 ", 42, "owner/anotherRepo")]
        [InlineData(" anotherOwner/anotherRepo#42", 42, "anotherOwner/anotherRepo")]
        [InlineData("anotherOwner/anotherRepo#42 ", 42, "anotherOwner/anotherRepo")]
        public async Task Run_adds_issue_links_to_footers(string footerText, int id, string projectPath)
        {
            // ARRANGE            
            m_RepositoryMock.Setup(x => x.Remotes).Returns(new[] { new GitRemote("origin", "http://gitlab.com/owner/repo.git") });

            m_CommitsClientMock
                .Setup(x => x.GetAsync(It.IsAny<ProjectId>(), It.IsAny<string>()))
                .Returns(
                    (ProjectId projectId, string sha) => Task.FromResult(new Commit() { WebUrl = $"https://example.com/{sha}" })
                );
            m_IssuesClientMock
                .Setup(x => x.GetAsync(MatchProjectId(projectPath), id))
                .Returns(
                    Task.FromResult(new Issue() { WebUrl = $"https://example.com/{projectPath}/issues/{id}" })
                );

            var sut = new GitLabLinkTask(m_Logger, m_RepositoryMock.Object, m_ClientFactoryMock.Object);

            var changeLog = new ApplicationChangeLog()
            {
                GetSingleVersionChangeLog(
                    "1.2.3",
                    null,
                    GetChangeLogEntry(summary: "Entry1", commit: "01", footers: new []
                    {
                        new ChangeLogEntryFooter(new CommitMessageFooterName("Issue"), footerText)
                    })
                )
            };

            // ACT 
            var result = await sut.RunAsync(changeLog);

            // ASSERT
            Assert.Equal(ChangeLogTaskResult.Success, result);

            var entries = changeLog.ChangeLogs.SelectMany(x => x.AllEntries).ToArray();
            Assert.All(entries, entry =>
            {
                Assert.All(entry.Footers.Where(x => x.Name == new CommitMessageFooterName("Issue")), footer =>
                {
                    Assert.NotNull(footer.WebUri);
                    var expectedUri = new Uri($"https://example.com/{projectPath}/issues/{id}");
                    Assert.Equal(expectedUri, footer.WebUri);
                });

            });

            m_IssuesClientMock.Verify(x => x.GetAsync(It.IsAny<ProjectId>(), It.IsAny<int>()), Times.Once);
            m_IssuesClientMock.Verify(x => x.GetAsync(MatchProjectId(projectPath), id), Times.Once);
        }


        [Theory]
        // reference within the same repository
        [InlineData("#23", 23, "owner/repo")]
        // reference to another project in the same namespace
        [InlineData("anotherRepo#42", 42, "owner/anotherRepo")]
        // reference to another project (including project namespace)
        [InlineData("anotherOwner/anotherRepo#42", 42, "anotherOwner/anotherRepo")]
        public async Task Run_does_not_add_link_if_issue_cannot_be_found(string footerText, int id, string projectPath)
        {
            // ARRANGE            
            m_RepositoryMock.Setup(x => x.Remotes).Returns(new[] { new GitRemote("origin", "http://gitlab.com/owner/repo.git") });

            m_CommitsClientMock
                .Setup(x => x.GetAsync(It.IsAny<ProjectId>(), It.IsAny<string>()))
                .Returns(
                    (ProjectId projectId, string sha) => Task.FromResult(new Commit() { WebUrl = $"https://example.com/{sha}" })
                );
            m_IssuesClientMock
                .Setup(x => x.GetAsync(It.IsAny<ProjectId>(), It.IsAny<int>()))
                .Throws(new GitLabException(HttpStatusCode.NotFound));

            var sut = new GitLabLinkTask(m_Logger, m_RepositoryMock.Object, m_ClientFactoryMock.Object);

            var changeLog = new ApplicationChangeLog()
            {
                GetSingleVersionChangeLog(
                    "1.2.3",
                    null,
                    GetChangeLogEntry(summary: "Entry1", commit: "01", footers: new []
                    {
                        new ChangeLogEntryFooter(new CommitMessageFooterName("Issue"), footerText)
                    })
                )
            };

            // ACT 
            var result = await sut.RunAsync(changeLog);

            // ASSERT
            Assert.Equal(ChangeLogTaskResult.Success, result);

            var entries = changeLog.ChangeLogs.SelectMany(x => x.AllEntries).ToArray();
            Assert.All(entries, entry =>
            {
                Assert.All(entry.Footers.Where(x => x.Name == new CommitMessageFooterName("Issue")), footer =>
                {
                    Assert.Null(footer.WebUri);
                });

            });

            m_IssuesClientMock.Verify(x => x.GetAsync(It.IsAny<ProjectId>(), It.IsAny<int>()), Times.Once);
            m_IssuesClientMock.Verify(x => x.GetAsync(MatchProjectId(projectPath), id), Times.Once);
        }


        [Theory]
        // reference within the same repository
        [InlineData("!23", 23, "owner/repo")]
        // reference to another project in the same namespace
        [InlineData("anotherRepo!42", 42, "owner/anotherRepo")]
        // reference to another project (including project namespace)
        [InlineData("anotherOwner/anotherRepo!42", 42, "anotherOwner/anotherRepo")]
        [InlineData("another-Owner/another-Repo!42", 42, "another-Owner/another-Repo")]
        [InlineData("another.Owner/another.Repo!42", 42, "another.Owner/another.Repo")]
        [InlineData("another_Owner/another_Repo!42", 42, "another_Owner/another_Repo")]
        public async Task Run_adds_merge_request_links_to_footers(string footerText, int id, string projectPath)
        {
            // ARRANGE            
            m_RepositoryMock.Setup(x => x.Remotes).Returns(new[] { new GitRemote("origin", "http://gitlab.com/owner/repo.git") });

            m_CommitsClientMock
                .Setup(x => x.GetAsync(It.IsAny<ProjectId>(), It.IsAny<string>()))
                .Returns(
                    (ProjectId projectId, string sha) => Task.FromResult(new Commit() { WebUrl = $"https://example.com/{sha}" })
                );
            m_MergeRequestsClientMock
                .Setup(x => x.GetAsync(MatchProjectId(projectPath), It.IsAny<Action<ProjectMergeRequestsQueryOptions>>()))
                .Returns(
                    Task.FromResult<IList<MergeRequest>>(new List<MergeRequest>() { new MergeRequest() { WebUrl = $"https://example.com/{projectPath}/merge_requests/{id}" } })
                );

            var sut = new GitLabLinkTask(m_Logger, m_RepositoryMock.Object, m_ClientFactoryMock.Object);

            var changeLog = new ApplicationChangeLog()
            {
                GetSingleVersionChangeLog(
                    "1.2.3",
                    null,
                    GetChangeLogEntry(summary: "Entry1", commit: "01", footers: new []
                    {
                        new ChangeLogEntryFooter(new CommitMessageFooterName("Merge-Request"), footerText)
                    })
                )
            };

            // ACT 
            var result = await sut.RunAsync(changeLog);

            // ASSERT
            Assert.Equal(ChangeLogTaskResult.Success, result);

            var entries = changeLog.ChangeLogs.SelectMany(x => x.AllEntries).ToArray();
            Assert.All(entries, entry =>
            {
                Assert.All(entry.Footers.Where(x => x.Name == new CommitMessageFooterName("Issue")), footer =>
                {
                    Assert.NotNull(footer.WebUri);
                    var expectedUri = new Uri($"https://example.com/{projectPath}/issues/{id}");
                    Assert.Equal(expectedUri, footer.WebUri);
                });

            });

            m_MergeRequestsClientMock.Verify(
                x => x.GetAsync(It.IsAny<ProjectId>(), It.IsAny<Action<ProjectMergeRequestsQueryOptions>>()),
                Times.Once);

            m_MergeRequestsClientMock.Verify(
                x => x.GetAsync(
                    MatchProjectId(projectPath),
                    It.Is<Action<ProjectMergeRequestsQueryOptions>>(
                        action =>
                            AssertAction(
                                action,
                                opts => Assert.Equal(id, Assert.Single(opts.MergeRequestsIds)),
                                opts => Assert.Equal(QueryMergeRequestState.All, opts.State)
                            ))),
                Times.Once);
        }

        [Theory]
        // reference within the same repository
        [InlineData("!23", "owner/repo")]
        // reference to another project in the same namespace
        [InlineData("anotherRepo!42", "owner/anotherRepo")]
        // reference to another project (including project namespace)
        [InlineData("anotherOwner/anotherRepo!42", "anotherOwner/anotherRepo")]
        public async Task Run_does_not_add_link_if_merge_request_cannot_be_found(string footerText, string projectPath)
        {
            // ARRANGE            
            m_RepositoryMock.Setup(x => x.Remotes).Returns(new[] { new GitRemote("origin", "http://gitlab.com/owner/repo.git") });

            m_CommitsClientMock
                .Setup(x => x.GetAsync(It.IsAny<ProjectId>(), It.IsAny<string>()))
                .Returns(
                    (ProjectId projectId, string sha) => Task.FromResult(new Commit() { WebUrl = $"https://example.com/{sha}" })
                );
            m_MergeRequestsClientMock
                .Setup(x => x.GetAsync(It.IsAny<ProjectId>(), It.IsAny<Action<ProjectMergeRequestsQueryOptions>>()))
                .Throws(new GitLabException(HttpStatusCode.NotFound));

            var sut = new GitLabLinkTask(m_Logger, m_RepositoryMock.Object, m_ClientFactoryMock.Object);

            var changeLog = new ApplicationChangeLog()
            {
                GetSingleVersionChangeLog(
                    "1.2.3",
                    null,
                    GetChangeLogEntry(summary: "Entry1", commit: "01", footers: new []
                    {
                        new ChangeLogEntryFooter(new CommitMessageFooterName("Merge-Request"), footerText)
                    })
                )
            };

            // ACT 
            var result = await sut.RunAsync(changeLog);

            // ASSERT
            Assert.Equal(ChangeLogTaskResult.Success, result);

            var entries = changeLog.ChangeLogs.SelectMany(x => x.AllEntries).ToArray();
            Assert.All(entries, entry =>
            {
                Assert.All(entry.Footers.Where(x => x.Name == new CommitMessageFooterName("Issue")), footer =>
                {
                    Assert.Null(footer.WebUri);
                });

            });

            m_MergeRequestsClientMock.Verify(x => x.GetAsync(It.IsAny<ProjectId>(), It.IsAny<Action<ProjectMergeRequestsQueryOptions>>()), Times.Once);
            m_MergeRequestsClientMock.Verify(x => x.GetAsync(MatchProjectId(projectPath), It.IsAny<Action<ProjectMergeRequestsQueryOptions>>()), Times.Once);
        }


        [Theory]
        // reference within the same repository
        [InlineData("%23", 23, "owner/repo")]
        // reference to another project in the same namespace
        [InlineData("anotherRepo%42", 42, "owner/anotherRepo")]
        // reference to another project (including project namespace)
        [InlineData("anotherOwner/anotherRepo%42", 42, "anotherOwner/anotherRepo")]
        [InlineData("another-Owner/another-Repo%42", 42, "another-Owner/another-Repo")]
        [InlineData("another.Owner/another.Repo%42", 42, "another.Owner/another.Repo")]
        [InlineData("another_Owner/another_Repo%42", 42, "another_Owner/another_Repo")]
        public async Task Run_adds_milestone_links_to_footers(string footerText, int id, string projectPath)
        {
            // ARRANGE            
            m_RepositoryMock.Setup(x => x.Remotes).Returns(new[] { new GitRemote("origin", "http://gitlab.com/owner/repo.git") });

            m_CommitsClientMock
                .Setup(x => x.GetAsync(It.IsAny<ProjectId>(), It.IsAny<string>()))
                .Returns(
                    (ProjectId projectId, string sha) => Task.FromResult(new Commit() { WebUrl = $"https://example.com/{sha}" })
                );
            m_ProjectsClientMock
                .Setup(x => x.GetMilestonesAsync(MatchProjectId(projectPath), It.IsAny<Action<MilestonesQueryOptions>>()))
                .Returns(
                    Task.FromResult<IList<Milestone>>(new List<Milestone>() { new Milestone() { WebUrl = $"https://example.com/{projectPath}/milestones/{id}" } })
                );

            var sut = new GitLabLinkTask(m_Logger, m_RepositoryMock.Object, m_ClientFactoryMock.Object);

            var changeLog = new ApplicationChangeLog()
            {
                GetSingleVersionChangeLog(
                    "1.2.3",
                    null,
                    GetChangeLogEntry(summary: "Entry1", commit: "01", footers: new []
                    {
                        new ChangeLogEntryFooter(new CommitMessageFooterName("Merge-Request"), footerText)
                    })
                )
            };

            // ACT 
            var result = await sut.RunAsync(changeLog);

            // ASSERT
            Assert.Equal(ChangeLogTaskResult.Success, result);

            var entries = changeLog.ChangeLogs.SelectMany(x => x.AllEntries).ToArray();
            Assert.All(entries, entry =>
            {
                Assert.All(entry.Footers.Where(x => x.Name == new CommitMessageFooterName("Milestone")), footer =>
                {
                    Assert.NotNull(footer.WebUri);
                    var expectedUri = new Uri($"https://example.com/{projectPath}/milestones/{id}");
                    Assert.Equal(expectedUri, footer.WebUri);
                });

            });

            m_ProjectsClientMock.Verify(
                x => x.GetMilestonesAsync(It.IsAny<ProjectId>(), It.IsAny<Action<MilestonesQueryOptions>>()),
                Times.Once);

            m_ProjectsClientMock.Verify(
                x => x.GetMilestonesAsync(
                    MatchProjectId(projectPath),
                    It.Is<Action<MilestonesQueryOptions>>(action =>
                        AssertAction(
                            action,
                            opts => Assert.Equal(id, Assert.Single(opts.MilestoneIds)),
                            opts => Assert.Equal(MilestoneState.All, opts.State)
                        ))),
                Times.Once);
        }

        [Theory]
        // reference within the same repository
        [InlineData("%23", "owner/repo")]
        // reference to another project in the same namespace
        [InlineData("anotherRepo%42", "owner/anotherRepo")]
        // reference to another project (including project namespace)
        [InlineData("anotherOwner/anotherRepo%42", "anotherOwner/anotherRepo")]
        public async Task Run_does_not_add_link_if_milestone_cannot_be_found(string footerText, string projectPath)
        {
            // ARRANGE            
            m_RepositoryMock.Setup(x => x.Remotes).Returns(new[] { new GitRemote("origin", "http://gitlab.com/owner/repo.git") });

            m_CommitsClientMock
                .Setup(x => x.GetAsync(It.IsAny<ProjectId>(), It.IsAny<string>()))
                .Returns(
                    (ProjectId projectId, string sha) => Task.FromResult(new Commit() { WebUrl = $"https://example.com/{sha}" })
                );
            m_ProjectsClientMock
                .Setup(x => x.GetMilestonesAsync(It.IsAny<ProjectId>(), It.IsAny<Action<MilestonesQueryOptions>>()))
                .Throws(new GitLabException(HttpStatusCode.NotFound));

            var sut = new GitLabLinkTask(m_Logger, m_RepositoryMock.Object, m_ClientFactoryMock.Object);

            var changeLog = new ApplicationChangeLog()
            {
                GetSingleVersionChangeLog(
                    "1.2.3",
                    null,
                    GetChangeLogEntry(summary: "Entry1", commit: "01", footers: new []
                    {
                        new ChangeLogEntryFooter(new CommitMessageFooterName("Merge-Request"), footerText)
                    })
                )
            };

            // ACT 
            var result = await sut.RunAsync(changeLog);

            // ASSERT
            Assert.Equal(ChangeLogTaskResult.Success, result);

            var entries = changeLog.ChangeLogs.SelectMany(x => x.AllEntries).ToArray();
            Assert.All(entries, entry =>
            {
                Assert.All(entry.Footers.Where(x => x.Name == new CommitMessageFooterName("Milestone")), footer =>
                {
                    Assert.Null(footer.WebUri);
                });

            });

            m_ProjectsClientMock.Verify(x => x.GetMilestonesAsync(It.IsAny<ProjectId>(), It.IsAny<Action<MilestonesQueryOptions>>()), Times.Once);
            m_ProjectsClientMock.Verify(x => x.GetMilestonesAsync(MatchProjectId(projectPath), It.IsAny<Action<MilestonesQueryOptions>>()), Times.Once);
        }

        //TODO: Run does not add a commit link if commit cannot be found
    }
}
