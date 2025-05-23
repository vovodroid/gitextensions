﻿using GitCommands.Git;
using GitUI;

namespace GitUITests
{
    [TestFixture]
    public sealed class UserEnvironmentInformationTests
    {
        [Test]
        public void GitVersion_is_good()
        {
            string gitString = UserEnvironmentInformation.GetGitVersionInfo("2.21.0.windows.1", new GitVersion("2.18.0"),
                new GitVersion("2.21.0"));
            ClassicAssert.AreEqual("2.21.0.windows.1", gitString);
        }

        [Test]
        public void GitVersion_is_old_but_supported()
        {
            string gitString = UserEnvironmentInformation.GetGitVersionInfo("2.20.1.windows.1", new GitVersion("2.18.0"),
                new GitVersion("2.21.0"));
            ClassicAssert.AreEqual("2.20.1.windows.1 (recommended: 2.21.0 or later)", gitString);
        }

        [Test]
        public void GitVersion_is_not_supported()
        {
            string gitString = UserEnvironmentInformation.GetGitVersionInfo("1.6.5.windows.1", new GitVersion("2.18.0"),
                new GitVersion("2.21.0"));
            ClassicAssert.AreEqual("1.6.5.windows.1 (minimum: 2.18.0, please update!)", gitString);
        }

        [Test]
        public void GitVersion_is_unknown_then_return_all_data()
        {
            string gitString = UserEnvironmentInformation.GetGitVersionInfo(null, new GitVersion("2.18.0"),
                new GitVersion("2.21.0"));
            ClassicAssert.AreEqual("- (minimum: 2.18.0, recommended: 2.21.0)", gitString);
        }
    }
}
