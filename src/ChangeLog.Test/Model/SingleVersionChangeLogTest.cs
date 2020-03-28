﻿using System;
using System.Linq;
using Xunit;

namespace Grynwald.ChangeLog.Test.Model
{
    public class SingleVersionChangeLogTest : TestBase
    {
        [Fact]
        public void Entries_are_returned_sorted_by_date()
        {
            // ARRANGE
            var entry1 = GetChangeLogEntry(date: new DateTime(2020, 1, 10), type: "feat");
            var entry2 = GetChangeLogEntry(date: new DateTime(2020, 1, 20), type: "feat");
            var entry3 = GetChangeLogEntry(date: new DateTime(2020, 1, 1), type: "fix");
            var entry4 = GetChangeLogEntry(date: new DateTime(2020, 1, 3), type: "fix", isBreakingChange: true);
            var entry5 = GetChangeLogEntry(date: new DateTime(2020, 1, 30), type: "refactor", isBreakingChange: true);

            var sut = GetSingleVersionChangeLog("1.2.3");
            sut.Add(entry1);
            sut.Add(entry2);
            sut.Add(entry3);
            sut.Add(entry4);
            sut.Add(entry5);

            var expectedOrdered = new[] { entry3, entry4, entry1, entry2, entry5 };
            var expectedOrderedFeatures = new[] { entry1, entry2 };
            var expectedOrderedBugFixes = new[] { entry3, entry4 };
            var expectedOrderedBreakingChanges = new[] { entry4, entry5 };

            // ACT 
            var actualOrdered1 = sut.AllEntries.ToArray();
            var actualOrdered2 = sut.ToArray();
            var actualOrderedFeatures = sut.FeatureEntries.ToArray();
            var actualOrderedBugFixes = sut.BugFixEntries.ToArray();
            var actualOrderedBreakingChanges = sut.BreakingChanges.ToArray();

            // ASSERT
            Assert.Equal(expectedOrdered, actualOrdered1);
            Assert.Equal(expectedOrdered, actualOrdered2);
            Assert.Equal(expectedOrderedFeatures, actualOrderedFeatures);
            Assert.Equal(expectedOrderedBugFixes, actualOrderedBugFixes);
            Assert.Equal(expectedOrderedBreakingChanges, actualOrderedBreakingChanges);
        }
    }
}