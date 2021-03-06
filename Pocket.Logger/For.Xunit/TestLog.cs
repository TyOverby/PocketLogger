﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Xunit.Abstractions;

namespace Pocket.For.Xunit
{
    internal class TestLog : IDisposable
    {
        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private readonly ConcurrentQueue<string> text = new ConcurrentQueue<string>();

        public TestLog(
            MethodInfo testMethod,
            bool writeToFile = false,
            string filename = null)
        {
            if (testMethod == null)
            {
                throw new ArgumentNullException(nameof(testMethod));
            }

            var testName = $"{testMethod.DeclaringType.Name}.{testMethod.Name}";

            disposables.Add(Disposable.Create(() =>
            {
                Log.Dispose();
            }));

            if (writeToFile)
            {
                filename = filename ??
                           $"{testName}-{DateTime.Now:yyyy-MM-dd-hh-mm-ss}.log";
                LogFile = new FileInfo(filename);

                LogToFile();
            }

            disposables.Add(
                LogEvents.Subscribe(e => Write(e.ToLogString())));

            TestName = testName;

            Log = new OperationLogger(TestName, logOnStart: true);
        }

        private void LogToFile()
        {
            var stream = File.AppendText(LogFile.FullName);

            stream.AutoFlush = true;

            disposables.Add(
                LogEvents.Subscribe(e =>
                                        stream.WriteLine(e.ToLogString())));

            disposables.Add(stream);
        }

        public OperationLogger Log { get; }

        public FileInfo LogFile { get; }

        public string TestName { get; }

        public IEnumerable<string> Text => text;

        public void Write(string text) => this.text.Enqueue(text);

        private static readonly AsyncLocal<TestLog> current = new AsyncLocal<TestLog>();

        public static TestLog Current
        {
            get => current.Value;
            set => current.Value = value;
        }

        public void Dispose()
        {
            if (Current == this)
            {
                Current = null;
            }

            disposables.Dispose();
        }

        public void LogTo(ITestOutputHelper output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            disposables.Add(LogEvents.Subscribe(e => output.WriteLine(e.ToLogString())));
        }
    }
}
