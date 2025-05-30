﻿using System.Text;
using GitCommands;
using GitCommands.Config;
using GitExtUtils;

namespace GitCommandsTests.Config
{
    /// <summary>
    /// Tests for <see cref="ConfigFile"/>.
    /// The ConfigFile class should respond the same as "git config".
    /// Since .gitconfig is often hidden, also make sure this is tested.
    /// </summary>
    [TestFixture]
    public class ConfigFileTest
    {
        private GitModule _module;

        private GitModule Module => _module ??= new GitModule(GetTempFolder());

        private static string GetTempFolder()
        {
            return Path.GetTempPath();
        }

        private static string GetConfigFileName()
        {
            return Path.Combine(GetTempFolder(), "testconfigfile");
        }

        private static string GetDefaultConfigFileContent()
        {
            StringBuilder content = new();
            content.AppendLine("[section1]");
            content.AppendLine("key1=value1");
            content.AppendLine("[section2.subsection]");
            content.AppendLine("key2=value2");
            content.AppendLine("[section3 \"subsection\"]");
            content.AppendLine("key3=value3");
            return content.ToString();
        }

        private string GetConfigValue(string cfgFile, string key)
        {
            GitArgumentBuilder args = new("config")
            {
                "-f",
                cfgFile.Quote(),
                "--get",

                // Quote the key without unescaping internal quotes
                $"\"{key}\""
            };
            return Module.GitExecutable.Execute(args, throwOnErrorExit: false).StandardOutput.TrimEnd('\n');
        }

        private void CheckValueIsEqual(ConfigFile configFile, string key, string expectedValue)
        {
            ClassicAssert.AreEqual(GetConfigValue(configFile.FileName, key), configFile.GetValue(key, string.Empty), "git config --get");
            ClassicAssert.AreEqual(expectedValue, configFile.GetValue(key, string.Empty), "ConfigFile");
        }

        private void CheckIsNotEqual(ConfigFile configFile, string key, string expectedValue)
        {
            ClassicAssert.AreNotEqual(GetConfigValue(configFile.FileName, key), expectedValue, "git config --get");
            ClassicAssert.AreNotEqual(expectedValue, configFile.GetValue(key, string.Empty), "ConfigFile");
        }

        [Test]
        public void TestWithInvalidFileName()
        {
            // TEST DATA
            {
                // Write test config
                File.WriteAllText(GetConfigFileName(), GetDefaultConfigFileContent(), GitModule.SystemEncoding);
            }

            ConfigFile configFile = new(GetConfigFileName() + "\\");

            ClassicAssert.IsNotNull(configFile);
        }

        [Test]
        public void TestWithNonexistentFile()
        {
            try
            {
                ConfigFile file = new(fileName: null);
                file.GetValue("nonexistentSetting", string.Empty);
            }
            catch (Exception e)
            {
                ClassicAssert.AreEqual("invalid setting name: nonexistentsetting", e.Message.ToLower());
            }
        }

        [Test]
        public void TestSave()
        {
            ConfigFile configFile = new(GetConfigFileName());
            configFile.SetValue("branch.BranchName1.remote", "origin1");
            configFile.Save();

            byte[] expectedFileContent =
                GitModule.SystemEncoding.GetBytes(
                    string.Format("[branch \"BranchName1\"]{0}\tremote = origin1{0}", Environment.NewLine));

            ClassicAssert.IsTrue(File.Exists(GetConfigFileName()));
            byte[] fileContent = File.ReadAllBytes(GetConfigFileName());

            ClassicAssert.AreEqual(expectedFileContent.Length, fileContent.Length);
            for (int index = 0; index < fileContent.Length; index++)
            {
                ClassicAssert.AreEqual(expectedFileContent[index], fileContent[index]);
            }
        }

        [Test]
        public void TestSetValueNonExisting()
        {
            ConfigFile configFile = new(GetConfigFileName());
            configFile.Save();

            configFile.SetValue("section1.key1", "section1key1");
            configFile.SetValue("section2.key1", "section2key1");
            configFile.SetValue("section1.key2", "section1key2");
            configFile.Save();

            configFile = new ConfigFile(GetConfigFileName());
            CheckValueIsEqual(configFile, "section1.key1", "section1key1");
            CheckValueIsEqual(configFile, "section2.key1", "section2key1");
            CheckValueIsEqual(configFile, "section1.key2", "section1key2");
        }

        [Test]
        public void TestSetValueExisting()
        {
            ConfigFile configFile = new(GetConfigFileName());
            configFile.SetValue("section.key", "section.key");
            configFile.Save();

            configFile.SetValue("section.key", "section.keyoverwrite");
            configFile.Save();

            configFile = new ConfigFile(GetConfigFileName());
            CheckValueIsEqual(configFile, "section.key", "section.keyoverwrite");
        }

        [Test]
        public void TestSetValueSectionWithDotNonExisting()
        {
            ConfigFile configFile = new(GetConfigFileName());
            configFile.SetValue("submodule.test.test2.path1", "submodule.test.test2.path1");
            configFile.SetValue("submodule.test.test2.path2", "submodule.test.test2.path2");
            configFile.Save();

            configFile.SetValue("submodule.test.test1.path1", "submodule.test.test1.path1");
            configFile.Save();

            configFile = new ConfigFile(GetConfigFileName());
            CheckValueIsEqual(configFile, "submodule.test.test1.path1", "submodule.test.test1.path1");
            CheckValueIsEqual(configFile, "submodule.test.test2.path1", "submodule.test.test2.path1");
            CheckValueIsEqual(configFile, "submodule.test.test2.path2", "submodule.test.test2.path2");
        }

        [Test]
        public void TestSetValueSectionWithDotExisting()
        {
            ConfigFile configFile = new(GetConfigFileName());
            configFile.SetValue("submodule.test.test1.path1", "invalid");
            configFile.SetValue("submodule.test.test2.path1", "submodule.test.test2.path1");
            configFile.SetValue("submodule.test.test2.path2", "submodule.test.test2.path2");
            configFile.Save();

            configFile.SetValue("submodule.test.test1.path1", "submodule.test.test1.path1");
            configFile.Save();

            configFile = new ConfigFile(GetConfigFileName());
            CheckValueIsEqual(configFile, "submodule.test.test1.path1", "submodule.test.test1.path1");
            CheckValueIsEqual(configFile, "submodule.test.test2.path1", "submodule.test.test2.path1");
            CheckValueIsEqual(configFile, "submodule.test.test2.path2", "submodule.test.test2.path2");
        }

        [Test]
        public void TestGetValue()
        {
            // TEST DATA
            {
                // Write test config
                File.WriteAllText(GetConfigFileName(), GetDefaultConfigFileContent(), GitModule.SystemEncoding);
            }

            ConfigFile configFile = new(GetConfigFileName());
            CheckValueIsEqual(configFile, "section1.key1", "value1");
            CheckValueIsEqual(configFile, "section2.subsection.key2", "value2");
            CheckValueIsEqual(configFile, "section3.subsection.key3", "value3");
        }

        [Test]
        public void TestWithNullSettings()
        {
            ConfigFile file = new(GetConfigFileName());
            ClassicAssert.Throws<ArgumentNullException>(() => file.GetValue(null, null));
        }

        [Test]
        public void TestWithHiddenFile()
        {
            // TEST DATA
            {
                // Write test config
                File.WriteAllText(GetConfigFileName(), GetDefaultConfigFileContent(), GitModule.SystemEncoding);

                // Make sure it is hidden
                FileInfo configFile = new(GetConfigFileName());
                configFile.Attributes = FileAttributes.Hidden;
            }

            // PERFORM TEST
            {
                ConfigFile configFile = new(GetConfigFileName());
                CheckValueIsEqual(configFile, "section1.key1", "value1");
                CheckValueIsEqual(configFile, "section2.subsection.key2", "value2");
                CheckValueIsEqual(configFile, "section3.subsection.key3", "value3");

                configFile.SetValue("section1.key1", "new-value1");
                configFile.Save();
            }

            // CHECK WRITTEN VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                CheckValueIsEqual(configFile, "section1.key1", "new-value1");
            }
        }

        [Test]
        public void RandomTestCase1()
        {
            // TEST DATA
            {
                StringBuilder content = new();

                content.AppendLine("[merge]");
                content.AppendLine("	tool = kdiff3");
                content.AppendLine("[mergetool \"kdiff3\"]");
                content.AppendLine("	path = c:/Program Files (x86)/KDiff3/kdiff3.exe");
                content.AppendLine("[user]");
                content.AppendLine("	name = Sergey Pustovit");
                content.AppendLine("	email = sergiy.pustovit@sintez.co.za");
                content.AppendLine("[core]");
                content.AppendLine("	safecrlf = false");
                content.AppendLine("	editor = C:/Program Files (x86)/Notepad++/notepad++.exe");
                content.AppendLine("[diff]");
                content.AppendLine("	tool = kdiff3");
                content.AppendLine("[difftool \"kdiff3\"]");
                content.AppendLine("	path = c:/Program Files (x86)/KDiff3/kdiff3.exe");

                // Write test config
                File.WriteAllText(GetConfigFileName(), content.ToString(), GitModule.SystemEncoding);
            }

            // CHECK GET CONFIG VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                CheckValueIsEqual(configFile, "user.name", "Sergey Pustovit");
            }

            // CHECK SET CONFIG VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                configFile.SetValue("user.name", "new-value");
                configFile.Save();
            }

            // CHECK WRITTEN VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                CheckValueIsEqual(configFile, "user.name", "new-value");
            }
        }

        [Test]
        public void RandomTestCase2()
        {
            // TEST DATA
            {
                StringBuilder content = new();

                content.AppendLine("[core]");
                content.AppendLine("	repositoryformatversion = 0");
                content.AppendLine("	filemode = false");
                content.AppendLine("	bare = false");
                content.AppendLine("	logallrefupdates = true");
                content.AppendLine("	symlinks = false");
                content.AppendLine("	ignorecase = true");
                content.AppendLine("	editor = C:/Program Files (x86)/Notepad++/notepad++.exe");
                content.AppendLine("[remote \"origin\"]");
                content.AppendLine("	fetch = +refs/heads/*:refs/remotes/origin/*");
                content.AppendLine("	url = git-sp_sable@free1.projectlocker.com:SDWH_Project_01.git");
                content.AppendLine(@"	puttykeyfile = C:\\Users\\sergiy.pustovit\\spustovit_sintez_key_1.ppk");
                content.AppendLine("[branch \"master\"]");
                content.AppendLine("	remote = origin");
                content.AppendLine("	merge = refs/heads/master");
                content.AppendLine("[gui]");
                content.AppendLine("	geometry = 917x503+25+25 201 191");
                content.AppendLine("[merge]");
                content.AppendLine("	tool = kdiff3");
                content.AppendLine("[remote \"test\"]");
                content.AppendLine("	url = git-sp_sable@free1.projectlocker.com:project1.git");
                content.AppendLine("	fetch = +refs/heads/*:refs/remotes/test/*");
                content.AppendLine("	puttykeyfile = C:/Users/sergiy.pustovit/spustovit_sintez_key_1.ppk");

                // Write test config
                File.WriteAllText(GetConfigFileName(), content.ToString(), GitModule.SystemEncoding);
            }

            // CHECK GET CONFIG VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                CheckValueIsEqual(configFile, "core.repositoryformatversion", "0");
            }

            // CHECK SET CONFIG VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                configFile.SetValue("core.repositoryformatversion", "1");
                configFile.Save();
            }

            // CHECK WRITTEN VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                CheckValueIsEqual(configFile, "core.repositoryformatversion", "1");
            }
        }

        [Test]
        public void NewLineTest()
        {
            // TEST DATA
            {
                StringBuilder content = new();

                content.AppendLine("[bugtraq]");
                content.AppendLine("	url = http://192.168.0.1:8080/browse/%BUGID%");
                content.AppendLine("	message = This commit fixes %BUGID%");
                content.AppendLine("	append = true");
                content.AppendLine("	label = Key:");
                content.AppendLine("	number = true");
                content.AppendLine("	logregex = \"\\n([A-Z][A-Z0-9]+-/d+)\"");

                // Write test config
                File.WriteAllText(GetConfigFileName(), content.ToString(), GitModule.SystemEncoding);
            }

            // CHECK GET CONFIG VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                CheckValueIsEqual(configFile, "bugtraq.logregex", "\n([A-Z][A-Z0-9]+-/d+)");
            }

            // CHECK SET CONFIG VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                configFile.SetValue("bugtraq.logregex", "data\nnewline");
                configFile.Save();
            }

            // CHECK WRITTEN VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                CheckValueIsEqual(configFile, "bugtraq.logregex", "data\nnewline");
            }

            // CHECK SET CONFIG VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                configFile.SetValue("bugtraq.logregex", "data\\newline");
                configFile.Save();
            }

            // CHECK WRITTEN VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                CheckValueIsEqual(configFile, "bugtraq.logregex", "data\\newline");
            }
        }

        [Test]
        public void CommentsTest()
        {
            // TEST DATA
            {
                StringBuilder content = new();

                content.AppendLine("# issue tracker configuration");
                content.AppendLine("[bugtraq]");
                content.AppendLine("	url = http://192.168.0.1:8080/browse/%BUGID% # url");
                content.AppendLine("	message =    This commit fixes %BUGID% # commit message");
                content.AppendLine("	append = true");
                content.AppendLine("	label = Key:");
                content.AppendLine("	# temporary disabled");
                content.AppendLine("	;number = true");
                content.AppendLine(@"	logregex = (\\\\////\\\\////)");
                content.AppendLine("[branch \"jb/RevisionLinks#1582\"]");
                content.AppendLine("remote = origin");
                content.AppendLine("merge = \"refs/heads/jb/RevisionLinks#1582\"");

                // Write test config
                File.WriteAllText(GetConfigFileName(), content.ToString(), GitModule.SystemEncoding);
            }

            // CHECK GET CONFIG VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                CheckValueIsEqual(configFile, "bugtraq.url", "http://192.168.0.1:8080/browse/%BUGID%");
                CheckValueIsEqual(configFile, "bugtraq.message", "This commit fixes %BUGID%");
                CheckValueIsEqual(configFile, "bugtraq.number", "");
                CheckValueIsEqual(configFile, "bugtraq.logregex", "(\\\\////\\\\////)");
                CheckValueIsEqual(configFile, "branch.jb/RevisionLinks#1582.merge", "refs/heads/jb/RevisionLinks#1582");
            }
        }

        [Test]
        public void EscapedSectionTest()
        {
            // TEST DATA
            {
                StringBuilder content = new();

                content.AppendLine("# issue tracker configuration");
                content.AppendLine("[bugtraq \"default\\\\5\"]");
                content.AppendLine("	url = http://192.168.0.1:8080/browse/%BUGID% ; url");

                // Write test config
                File.WriteAllText(GetConfigFileName(), content.ToString(), GitModule.SystemEncoding);
            }

            // CHECK GET CONFIG VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                CheckValueIsEqual(configFile, "bugtraq.default\\5.url", "http://192.168.0.1:8080/browse/%BUGID%");
            }
        }

        [Test]
        public void SpacesInSubSectionTest()
        {
            // TEST DATA
            {
                StringBuilder content = new();

                content.AppendLine("[section \"sub section\"]");
                content.AppendLine("	test = test");

                // Write test config
                File.WriteAllText(GetConfigFileName(), content.ToString(), GitModule.SystemEncoding);
            }

            // CHECK GET CONFIG VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                CheckValueIsEqual(configFile, "section.sub section.test", "test");
            }

            // CHECK SET CONFIG VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                configFile.SetValue("section.sub section.test", @"test2");
                configFile.Save();
            }

            // CHECK WRITTEN VALUE
            {
                ConfigFile configFile = new(GetConfigFileName());
                CheckValueIsEqual(configFile, "section.sub section.test", "test2");
            }
        }

        [Test]
        public void CaseSensitive()
        {
            // create test data
            {
                StringBuilder sb = new();
                sb.AppendLine("[branch \"BranchName1\"]");
                sb.AppendLine("remote = origin1");
                sb.AppendLine("[branch \"BranchName2\"]");
                sb.AppendLine("remote = origin2");
                sb.AppendLine("[branch \"branchName2\"]");
                sb.AppendLine("remote = origin3");
                File.WriteAllText(GetConfigFileName(), sb.ToString());
            }

            // verify
            {
                ConfigFile configFile = new(GetConfigFileName());

                string remote = "branch.BranchName1.remote";
                CheckValueIsEqual(configFile, remote, "origin1");

                remote = "branch.branchName1.remote";
                CheckIsNotEqual(configFile, remote, "origin1");

                remote = "branch \"BranchName1\".remote";
                ClassicAssert.AreEqual(GetConfigValue(configFile.FileName, remote.Replace(" ", ".")), configFile.GetValue(remote, string.Empty), "git config --get");
                ClassicAssert.AreEqual("origin1", configFile.GetValue(remote, string.Empty), "ConfigFile");

                remote = "branch \"BranchName2\".remote";
                ClassicAssert.AreEqual(GetConfigValue(configFile.FileName, remote.Replace(" ", ".")), configFile.GetValue(remote, string.Empty), "git config --get");
                ClassicAssert.AreEqual("origin2", configFile.GetValue(remote, string.Empty), "ConfigFile");

                remote = "branch \"branchName2\".remote";
                ClassicAssert.AreNotEqual(GetConfigValue(configFile.FileName, remote.Replace(" ", ".")), "origin2", "git config --get");
                ClassicAssert.AreNotEqual("origin2", configFile.GetValue(remote, string.Empty), "ConfigFile");

                remote = "branch \"branchName2\".remote";
                ClassicAssert.AreEqual(GetConfigValue(configFile.FileName, remote.Replace(" ", ".")), configFile.GetValue(remote, string.Empty), string.Empty, "git config --get");
                ClassicAssert.AreEqual("origin3", configFile.GetValue(remote, string.Empty), "ConfigFile");

                remote = "branch \"branchname2\".remote";
                ClassicAssert.AreEqual(GetConfigValue(configFile.FileName, remote.Replace(" ", ".")), configFile.GetValue(remote, string.Empty), "git config --get");
                ClassicAssert.AreEqual("", configFile.GetValue(remote, string.Empty), "ConfigFile");
            }
        }

        [Test]
        public void TwoSections_ValueInTheLast()
        {
            // test for bug reported in https://github.com/gitextensions/gitextensions/pull/3151/commits/282c6c1df45024c3c997f1a79aa7aba5a96a1a68
            string configFileContent = @"
[status]
[status]
      showUntrackedFiles = no
";
            ConfigFile cfg = new("");
            cfg.LoadFromString(configFileContent);
            string actual = cfg.GetValue("status.showuntrackedfiles", string.Empty);
            string expected = "no";
            ClassicAssert.AreEqual(expected, actual);
        }

        [Test]
        public void TwoSections_ValueInTheFirst()
        {
            // test for bug reported in https://github.com/gitextensions/gitextensions/pull/3151/commits/282c6c1df45024c3c997f1a79aa7aba5a96a1a68
            string configFileContent = @"
[status]
      showUntrackedFiles = no
[status]
";
            ConfigFile cfg = new("");
            cfg.LoadFromString(configFileContent);
            string actual = cfg.GetValue("status.showuntrackedfiles", string.Empty);
            string expected = "no";
            ClassicAssert.AreEqual(expected, actual);
        }

        [Test]
        public void TwoSections_ValueInBoth()
        {
            // test for bug reported in https://github.com/gitextensions/gitextensions/pull/3151/commits/282c6c1df45024c3c997f1a79aa7aba5a96a1a68
            string configFileContent = @"
[status]
      showUntrackedFiles = yes
[status]
      showUntrackedFiles = no
";
            ConfigFile cfg = new("");
            cfg.LoadFromString(configFileContent);
            string actual = cfg.GetValue("status.showuntrackedfiles", string.Empty);
            string expected = "no";
            ClassicAssert.AreEqual(expected, actual);
        }

        [Test]
        public void TwoSections_ValueInBoth_GetValues()
        {
            // test for bug reported in https://github.com/gitextensions/gitextensions/pull/3151/commits/282c6c1df45024c3c997f1a79aa7aba5a96a1a68
            string configFileContent = @"
[status]
      showUntrackedFiles = yes
[status]
      showUntrackedFiles = no
";
            ConfigFile cfg = new("");
            cfg.LoadFromString(configFileContent);
            IEnumerable<string> actual = cfg.GetValues("status.showuntrackedfiles");
            IEnumerable<string> expected = new[] { "yes", "no" };
            ClassicAssert.True(expected.SequenceEqual(actual));
        }

        [Test]
        public void SquareBracketInValue()
        {
            StringBuilder content = new();

            content.AppendLine("[branch \"reporting_bad_behaviour\"]");
            content.AppendLine("    remote = origin");
            content.AppendLine("	merge = refs/heads/[en]reporting_bad_behaviour");

            ConfigFile cfg = new("");
            cfg.LoadFromString(content.ToString());
            string actual = cfg.GetValue("branch.reporting_bad_behaviour.merge", string.Empty);
            string expected = "refs/heads/[en]reporting_bad_behaviour";
            ClassicAssert.AreEqual(expected, actual);
        }

        [Test]
        public void SquareBracketInSectionName()
        {
            StringBuilder content = new();

            content.AppendLine("[branch \"[en]reporting_bad_behaviour\"]");
            content.AppendLine("    remote = origin");
            content.AppendLine("	merge = refs/heads/reporting_bad_behaviour");

            ConfigFile cfg = new("");
            cfg.LoadFromString(content.ToString());
            string actual = cfg.GetValue("branch.[en]reporting_bad_behaviour.merge", string.Empty);
            string expected = "refs/heads/reporting_bad_behaviour";
            ClassicAssert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Always delete the test config file after each test.
        /// </summary>
        [TearDown]
        public void Cleanup()
        {
            if (File.Exists(GetConfigFileName()))
            {
                // Make sure it is hidden
                FileInfo configFile = new(GetConfigFileName());
                configFile.Attributes = FileAttributes.Normal;

                File.Delete(GetConfigFileName());
            }
        }
    }
}
