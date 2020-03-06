﻿using ApprovalTests;
using ApprovalTests.Reporters;
using ChangeLogCreator.ConventionalCommits;
using ChangeLogCreator.Model;
using ChangeLogCreator.Tasks;
using Xunit;

namespace ChangeLogCreator.Test.Tasks
{
    [UseReporter(typeof(DiffReporter))]
    public class RenderMarkdownTaskTest : TestBase
    {
        [Fact]
        public void ChangeLog_is_converted_to_expected_Markdown_01()
        {
            // Empty changelog
            Approve(new ChangeLog());
        }

        [Fact]
        public void ChangeLog_is_converted_to_expected_Markdown_02()
        {
            // Changelog with two versions.
            // All versions are empty (no entries)

            var changeLog = new ChangeLog()
            {
                GetSingleVersionChangeLog("1.2.3"),
                GetSingleVersionChangeLog("4.5.6"),
            };

            Approve(changeLog);
        }

        [Fact]
        public void ChangeLog_is_converted_to_expected_Markdown_03()
        {
            // Changelog with a single versions and multiple entries

            var versionChangeLog = GetSingleVersionChangeLog(
                "1.2.3",
                null,
                GetChangeLogEntry(type: "feat", summary: "Some change"),
                GetChangeLogEntry(type: "fix", summary: "A bug was fixed"),
                GetChangeLogEntry(type: "feat", summary: "Some other change")
            );

            var changeLog = new ChangeLog() { versionChangeLog };

            Approve(changeLog);
        }

        [Fact]
        public void ChangeLog_is_converted_to_expected_Markdown_04()
        {
            // Changelog with a single versions and multiple entries (including entries with scope)

            var versionChangeLog = GetSingleVersionChangeLog(
                "1.2.3",
                null,
                GetChangeLogEntry(scope: "api", type: "feat", summary: "Some change"),
                GetChangeLogEntry(scope: "cli", type: "fix", summary: "A bug was fixed"),
                GetChangeLogEntry(scope: "", type: "feat", summary: "Some other change")
            );

            var changeLog = new ChangeLog() { versionChangeLog };

            Approve(changeLog);
        }

        [Fact]
        public void ChangeLog_is_converted_to_expected_Markdown_05()
        {
            // Changelog that contains entry with a body

            var versionChangeLog = GetSingleVersionChangeLog(
                "1.2.3",
                null,

                GetChangeLogEntry(
                    scope: "api",
                    type: "feat",
                    summary: "Some change",
                    body: new[]
                    {
                        "Changelog entry body Line1\r\nLine2",
                        "Changelog entry body Line3\r\nLine4",
                    }),

                GetChangeLogEntry(scope: "cli", type: "fix", summary: "A bug was fixed")
            );

            var changeLog = new ChangeLog() { versionChangeLog };

            Approve(changeLog);
        }

        [Fact]
        public void ChangeLog_is_converted_to_expected_Markdown_06()
        {
            // Changelog with only a single entry 
            // (Changelog uses simpler format if there is only a single entry for a version)

            var versionChangeLog = GetSingleVersionChangeLog(
                "1.2.3",
                null,

                GetChangeLogEntry(
                    scope: "api",
                    type: "feat",
                    summary: "Some change",
                    body: new[]
                    {
                        "Changelog entry body Line1\r\nLine2",
                        "Changelog entry body Line3\r\nLine4",
                    })
            );

            var changeLog = new ChangeLog() { versionChangeLog };

            Approve(changeLog);
        }

        [Fact]
        public void ChangeLog_is_converted_to_expected_Markdown_07()
        {
            // Changelog that includes breaking changes
            // Breaking changes must be included regardless of the change type

            var versionChangeLog = GetSingleVersionChangeLog(
                "1.2.3",
                null,

                GetChangeLogEntry(
                    scope: "api",
                    type: "feat",
                    summary: "Some change",
                    body: new[]
                    {
                        "Changelog entry body Line1\r\nLine2",
                        "Changelog entry body Line3\r\nLine4",
                    }),

                GetChangeLogEntry(scope: "cli", type: "fix", summary: "A bug was fixed"),

                GetChangeLogEntry(isBreakingChange: true, type: "refactor", summary: "Some breaking change"),

                GetChangeLogEntry(isBreakingChange: true, type: "fix", summary: "A breaking bugfix")
            );


            var changeLog = new ChangeLog() { versionChangeLog };

            Approve(changeLog);
        }

        [Fact]
        public void ChangeLog_is_converted_to_expected_Markdown_08()
        {
            // Changelog uses simpler format if there is only a single entry for a version
            // When a entry is a feature AND a breaking change, it must not count as two entries

            var versionChangeLog = GetSingleVersionChangeLog(
                "1.2.3",
                null,

                GetChangeLogEntry(
                    scope: "api",
                    type: "feat",
                    isBreakingChange: true,
                    summary: "Some change",
                    body: new[]
                    {
                        "Changelog entry body Line1\r\nLine2",
                        "Changelog entry body Line3\r\nLine4",
                    })
            );

            var changeLog = new ChangeLog() { versionChangeLog };

            Approve(changeLog);
        }

        [Fact]
        public void ChangeLog_is_converted_to_expected_Markdown_09()
        {
            // Breaking changes must be included regardless of the change type

            var versionChangeLog = GetSingleVersionChangeLog(
                "1.2.3",
                null,

                GetChangeLogEntry(
                    scope: "api",
                    type: "feat",
                    summary: "Some change",
                    body: new[]
                    {
                        "Changelog entry body Line1\r\nLine2",
                        "Changelog entry body Line3\r\nLine4",
                    }),

                GetChangeLogEntry(scope: "cli", type: "fix", summary: "A bug was fixed"),

                GetChangeLogEntry(isBreakingChange: true, type: "refactor", summary: "Some breaking change"),

                GetChangeLogEntry(
                    type: "fix",
                    summary: "A breaking bugfix",
                    footers: new[]
                    {
                        new CommitMessageFooter(new CommitMessageFooterName("Breaking-Change"), "Description of breaking change")
                    })
            );

            var changeLog = new ChangeLog() { versionChangeLog };

            Approve(changeLog);
        }

        [Fact]
        public void ChangeLog_is_converted_to_expected_Markdown_10()
        {
            // Changelog uses simpler format if there is only a single entry for a version
            // When a entry is a feature AND a breaking change, it must not count as two entries

            var versionChangeLog = GetSingleVersionChangeLog(
                "1.2.3",
                null,

                GetChangeLogEntry(
                    scope: "api",
                    type: "feat",
                    summary: "Some change",
                    body: new[]
                    {
                        "Changelog entry body Line1\r\nLine2",
                        "Changelog entry body Line3\r\nLine4",
                    },
                    footers: new[]
                    {
                        new CommitMessageFooter(new CommitMessageFooterName("Breaking-Change"), "Description of breaking change")
                    })
            );

            var changeLog = new ChangeLog() { versionChangeLog };

            Approve(changeLog);
        }

        [Fact]
        public void ChangeLog_is_converted_to_expected_Markdown_11()
        {
            // Breaking changes must be included regardless of the change type

            var versionChangeLog = GetSingleVersionChangeLog(
                "1.2.3",
                null,

                GetChangeLogEntry(
                    scope: "api",
                    type: "feat",
                    summary: "Some change",
                    body: new[]
                    {
                        "Changelog entry body Line1\r\nLine2",
                        "Changelog entry body Line3\r\nLine4",
                    }),

                GetChangeLogEntry(scope: "cli", type: "fix", summary: "A bug was fixed"),

                GetChangeLogEntry(isBreakingChange: true, type: "refactor", summary: "Some breaking change"),

                GetChangeLogEntry(
                    type: "fix",
                    summary: "A breaking bugfix",
                    footers: new[]
                    {
                        new CommitMessageFooter(new CommitMessageFooterName("Breaking-Change"), "Description of breaking change"),
                        new CommitMessageFooter(new CommitMessageFooterName("BREAKING CHANGE"), "Another breaking change")
                    })
            );

            var changeLog = new ChangeLog() { versionChangeLog };

            Approve(changeLog);
        }

        [Fact]
        public void ChangeLog_is_converted_to_expected_Markdown_12()
        {
            // Changelog uses simpler format if there is only a single entry for a version
            // When a entry is a feature AND a breaking change, it must not count as two entries

            var versionChangeLog = GetSingleVersionChangeLog(
                "1.2.3",
                null,

                GetChangeLogEntry(
                    scope: "api",
                    type: "feat",
                    summary: "Some change",
                    body: new[]
                    {
                        "Changelog entry body Line1\r\nLine2",
                        "Changelog entry body Line3\r\nLine4",
                    },
                    footers: new[]
                    {
                        new CommitMessageFooter(new CommitMessageFooterName("Breaking-Change"), "Description of breaking change"),
                        new CommitMessageFooter(new CommitMessageFooterName("BREAKING CHANGE"), "Another breaking change")
                    }));

            var changeLog = new ChangeLog() { versionChangeLog };

            Approve(changeLog);
        }


        private void Approve(ChangeLog changeLog)
        {
            var sut = new RenderMarkdownTask("DummyPath");

            var doc = sut.GetChangeLogDocument(changeLog);

            Assert.NotNull(doc);

            var markdown = doc.ToString(sut.SerializationOptions);

            var writer = new ApprovalTextWriter(markdown);
            Approvals.Verify(writer, new ApprovalNamer(), Approvals.GetReporter());
        }
    }
}