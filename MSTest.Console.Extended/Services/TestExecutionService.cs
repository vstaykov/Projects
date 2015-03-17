﻿using System.Linq;
using log4net;
using MSTest.Console.Extended.Interfaces;

namespace MSTest.Console.Extended.Services
{
    public class TestExecutionService
    {
        private readonly ILog log;

        private readonly IMsTestTestRunProvider microsoftTestTestRunProvider;

        private readonly IFileSystemProvider fileSystemProvider;

        private readonly IProcessExecutionProvider processExecutionProvider;

        private readonly IConsoleArgumentsProvider consoleArgumentsProvider;

        public TestExecutionService(
            IMsTestTestRunProvider microsoftTestTestRunProvider,
            IFileSystemProvider fileSystemProvider,
            IProcessExecutionProvider processExecutionProvider,
            IConsoleArgumentsProvider consoleArgumentsProvider,
            ILog log)
        {
            this.microsoftTestTestRunProvider = microsoftTestTestRunProvider;
            this.fileSystemProvider = fileSystemProvider;
            this.processExecutionProvider = processExecutionProvider;
            this.consoleArgumentsProvider = consoleArgumentsProvider;
            this.log = log;
        }
        
        public int ExecuteWithRetry()
        {
            this.fileSystemProvider.DeleteTestResultFiles();

            this.processExecutionProvider.ExecuteProcessWithAdditionalArguments();
            this.processExecutionProvider.CurrentProcessWaitForExit();
            var testRun = this.fileSystemProvider.DeserializeTestRun();

            var failedTests = this.microsoftTestTestRunProvider.GetAllNotPassedTests(testRun.Results.ToList());
            int failedTestsPercentage = this.microsoftTestTestRunProvider.CalculatedFailedTestsPercentage(failedTests, testRun.Results.ToList());

            if (failedTestsPercentage < this.consoleArgumentsProvider.FailedTestsThreshold)
            {
                for (int i = 0; i < this.consoleArgumentsProvider.RetriesCount; i++)
                {
                    this.log.InfoFormat("Start to execute again {0} failed tests.", failedTests.Count);
                    if (failedTests.Count > 0)
                    {
                        string currentTestResultPath = this.fileSystemProvider.GetTempTrxFile();
                        string retryRunArguments = this.microsoftTestTestRunProvider.GenerateAdditionalArgumentsForFailedTestsRun(failedTests, currentTestResultPath);

                        this.log.InfoFormat("Run {0} time with arguments {1}", i + 2, retryRunArguments);
                        this.processExecutionProvider.ExecuteProcessWithAdditionalArguments(retryRunArguments);
                        this.processExecutionProvider.CurrentProcessWaitForExit();

                        var currentTestRun = this.fileSystemProvider.DeserializeTestRun(currentTestResultPath);
                        var passedTests = this.microsoftTestTestRunProvider.GetAllPassedTests(currentTestRun);

                        this.microsoftTestTestRunProvider.UpdatePassedTests(passedTests, testRun.Results.ToList());
                        this.microsoftTestTestRunProvider.UpdateResultsSummary(testRun);
                    }
                    else
                    {
                        break;
                    }

                    failedTests = this.microsoftTestTestRunProvider.GetAllNotPassedTests(testRun.Results.ToList());
                }
            }

            this.fileSystemProvider.SerializeTestRun(testRun);

            int exitCode = 0;
            if (failedTests.Count > 0)
            {
                exitCode = 1;
            }

            return exitCode;
        }
    }
}