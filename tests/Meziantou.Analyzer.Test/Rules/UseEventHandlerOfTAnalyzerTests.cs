﻿using System.Threading.Tasks;
using Meziantou.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace Meziantou.Analyzer.Test.Rules
{
    [TestClass]
    public sealed class UseEventHandlerOfTAnalyzerTests
    {
        private static ProjectBuilder CreateProjectBuilder()
        {
            return new ProjectBuilder()
                .WithAnalyzer<UseEventHandlerOfTAnalyzer>();
        }

        [TestMethod]
        public async Task ValidEvent()
        {
            await CreateProjectBuilder()
                  .WithSourceCode(@"
class Test
{
    event System.EventHandler<string> myevent;
}")
                  .ValidateAsync();
        }

        [TestMethod]
        public async Task InvalidEvent()
        {
            await CreateProjectBuilder()
                  .WithSourceCode(@"
class Test
{
    event System.Action<string> myevent;
}")
                  .ShouldReportDiagnostic(line: 4, column: 33)
                  .ValidateAsync();
        }
    }
}
