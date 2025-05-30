﻿using GitUI;

namespace GitUITests
{
    [TestFixture]
    public sealed class CancellationTokenSequenceTests
    {
        [Test]
        public void Next_cancels_previous_token()
        {
            CancellationTokenSequence sequence = new();

            CancellationToken token1 = sequence.Next();

            ClassicAssert.False(token1.IsCancellationRequested);

            CancellationToken token2 = sequence.Next();

            ClassicAssert.True(token1.IsCancellationRequested);
            ClassicAssert.False(token2.IsCancellationRequested);
        }

        [Test]
        public void Next_throws_if_disposed()
        {
            CancellationTokenSequence sequence = new();

            sequence.Dispose();

            ClassicAssert.Throws<OperationCanceledException>(() => sequence.Next());
        }

        [Test]
        public void Cancel_cancels_previous_token()
        {
            CancellationTokenSequence sequence = new();

            CancellationToken token = sequence.Next();

            ClassicAssert.False(token.IsCancellationRequested);

            sequence.CancelCurrent();

            ClassicAssert.True(token.IsCancellationRequested);
        }

        [Test]
        public void Cancel_is_idempotent()
        {
            CancellationTokenSequence sequence = new();

            CancellationToken token = sequence.Next();

            ClassicAssert.False(token.IsCancellationRequested);

            sequence.CancelCurrent();
            sequence.CancelCurrent();
            sequence.CancelCurrent();
            sequence.CancelCurrent();
        }

        [Test]
        public void Cancel_does_not_throw_if_no_token_yet_issued()
        {
            CancellationTokenSequence sequence = new();

            sequence.CancelCurrent();
        }

        [Test]
        public void Dispose_cancels_previous_token()
        {
            CancellationTokenSequence sequence = new();

            CancellationToken token = sequence.Next();

            ClassicAssert.False(token.IsCancellationRequested);

            sequence.Dispose();

            ClassicAssert.True(token.IsCancellationRequested);
        }

        [Test]
        public void Dispose_is_idempotent()
        {
            CancellationTokenSequence sequence = new();

            sequence.Next();

            sequence.Dispose();
            sequence.Dispose();
            sequence.Dispose();
            sequence.Dispose();
        }

        [Test]
        public void Dispose_does_not_throw_if_no_token_yet_issued()
        {
            CancellationTokenSequence sequence = new();

            sequence.Dispose();
        }

        [Test]
        public async Task Concurrent_callers_to_Next_only_result_in_one_non_cancelled_token_being_issued()
        {
            const int loopCount = 10000;

            int logicalProcessorCount = Environment.ProcessorCount;
            int threadCount = Math.Max(2, logicalProcessorCount);

            using CancellationTokenSequence sequence = new();
            using Barrier barrier = new(threadCount);
            using CountdownEvent countdown = new(loopCount * threadCount);
            int completedCount = 0;

            CancellationTokenSource completionTokenSource = new();
            CancellationToken completionToken = completionTokenSource.Token;
            int[] winnerByIndex = new int[threadCount];

            List<Task> tasks = Enumerable
                .Range(0, threadCount)
                .Select(i => Task.Run(() => ThreadMethodAsync(i)))
                .ToList();

            ClassicAssert.True(
                countdown.Wait(TimeSpan.FromSeconds(10)),
                "Test should have completed within a reasonable amount of time");

            await Task.WhenAll(tasks);

            ClassicAssert.AreEqual(loopCount, completedCount);

            await Console.Out.WriteLineAsync("Winner by index: " + string.Join(",", winnerByIndex));

            // Assume hyper threading, so halve logical processors (could use WMI or P/Invoke for more robust answer)
            if (logicalProcessorCount <= 2)
            {
                ClassicAssert.Inconclusive("This test requires more than one physical processor to run.");
            }

            return;

            async Task ThreadMethodAsync(int i)
            {
                while (true)
                {
                    barrier.SignalAndWait();

                    if (completionToken.IsCancellationRequested)
                    {
                        return;
                    }

                    CancellationToken token = sequence.Next();

                    barrier.SignalAndWait();

                    if (!token.IsCancellationRequested)
                    {
                        Interlocked.Increment(ref completedCount);
                        Interlocked.Increment(ref winnerByIndex[i]);
                    }

                    if (countdown.Signal())
                    {
                        await completionTokenSource.CancelAsync();
                    }
                }
            }
        }
    }
}
