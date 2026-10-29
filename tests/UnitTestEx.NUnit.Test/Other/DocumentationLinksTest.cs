using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnitTestEx.NUnit.Test.Other
{
    /// <summary>
    /// Guards the README and docs against stale relative links; where the link text is a backticked identifier pointing at a <c>.cs</c> file, the identifier must exist within that file.
    /// </summary>
    [TestFixture]
    public partial class DocumentationLinksTest
    {
        [GeneratedRegex(@"\[(?<text>[^\]]*)\]\((?<target>[^)\s]+)\)")]
        private static partial Regex LinkRegex();

        [Test]
        public void RelativeLinks_Resolve()
        {
            var root = FindRepoRoot();
            var files = new List<string> { Path.Combine(root, "README.md") };
            var docs = Path.Combine(root, "docs");
            if (Directory.Exists(docs))
                files.AddRange(Directory.GetFiles(docs, "*.md", SearchOption.AllDirectories));

            var errors = new List<string>();
            foreach (var file in files)
            {
                var content = File.ReadAllText(file);
                foreach (Match m in LinkRegex().Matches(content))
                {
                    var target = m.Groups["target"].Value;
                    if (target.StartsWith('#') || target.Contains("://") || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var path = target.Split('#')[0];
                    var full = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, path.Replace('/', Path.DirectorySeparatorChar)));
                    var rel = Path.GetRelativePath(root, file);

                    if (!File.Exists(full) && !Directory.Exists(full))
                    {
                        errors.Add($"{rel}: '{target}' does not exist.");
                        continue;
                    }

                    var text = m.Groups["text"].Value;
                    if (full.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && text.Length > 2 && text.StartsWith('`') && text.EndsWith('`'))
                    {
                        var identifier = text.Trim('`');
                        if (Regex.IsMatch(identifier, @"^\w+$") && !File.ReadAllText(full).Contains(identifier, StringComparison.Ordinal))
                            errors.Add($"{rel}: '{identifier}' not found in '{target}'.");
                    }
                }
            }

            Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UnitTestEx.sln")))
                dir = dir.Parent;

            return dir?.FullName ?? throw new InvalidOperationException("Unable to locate the repository root (UnitTestEx.sln).");
        }
    }
}
