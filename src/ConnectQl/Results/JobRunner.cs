// MIT License
//
// Copyright (c) 2017 Maarten van Sambeek.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace ConnectQl.Results
{
    using System;
    using System.Collections.Generic;

    using ConnectQl.Interfaces;
    using ConnectQl.Internal;

    using JetBrains.Annotations;

    /// <summary>
    /// The job runner.
    /// </summary>
    public class JobRunner
    {
        /// <summary>
        /// The trigger contexts.
        /// </summary>
        private readonly IList<TriggerContext> triggerContexts = new List<TriggerContext>();

        /// <summary>
        /// Gets or sets the logger.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// The start.
        /// </summary>
        /// <param name="jobs">
        /// The jobs.
        /// </param>
        /// <returns>
        /// The <see cref="IDisposable"/>.
        /// </returns>
        [NotNull]
        public IDisposable Start([NotNull] IEnumerable<IJob> jobs)
        {
            var contexts = new List<TriggerContext>();

            foreach (var job in jobs)
            {
                foreach (var trigger in job.Triggers)
                {
                    var context = new TriggerContext(this, job, trigger);

                    this.triggerContexts.Add(context);

                    contexts.Add(context);

                    trigger.Enable(context);
                }
            }

            return new ActionOnDispose(() => this.Stop(contexts));
        }

        /// <summary>
        /// The create job context.
        /// </summary>
        /// <param name="job">
        /// The job.
        /// </param>
        /// <returns>
        /// The <see cref="IJobContext"/>.
        /// </returns>
        [CanBeNull]
        private IJobContext CreateJobContext(IJob job)
        {
            return null;
        }

        /// <summary>
        /// Stops the triggers.
        /// </summary>
        /// <param name="contexts">
        /// The contexts.
        /// </param>
        private void Stop([NotNull] IEnumerable<TriggerContext> contexts)
        {
            foreach (var context in contexts)
            {
                foreach (var trigger in context.Job.Triggers)
                {
                    trigger.Disable(context);
                }

                this.triggerContexts.Remove(context);
            }
        }

        /// <summary>
        /// The trigger context.
        /// </summary>
        private class TriggerContext : ITriggerContext
        {
            /// <summary>
            /// The job runner.
            /// </summary>
            private readonly JobRunner jobRunner;

            /// <summary>
            /// The trigger.
            /// </summary>
            private readonly IJobTrigger trigger;

            /// <summary>
            /// Initializes a new instance of the <see cref="TriggerContext"/> class.
            /// </summary>
            /// <param name="jobRunner">
            /// The job runner.
            /// </param>
            /// <param name="job">
            /// The job.
            /// </param>
            /// <param name="trigger">
            /// The trigger.
            /// </param>
            public TriggerContext([NotNull] JobRunner jobRunner, IJob job, IJobTrigger trigger)
            {
                this.Logger = jobRunner.Logger;
                this.jobRunner = jobRunner;
                this.trigger = trigger;
                this.Job = job;
            }

            /// <summary>
            /// Raised when a job was executed.
            /// </summary>
            public event EventHandler<JobExecutedArgs> JobExecuted;

            /// <summary>
            /// Gets the job.
            /// </summary>
            public IJob Job { get; }

            /// <summary>
            /// Gets the logger.
            /// </summary>
            public ILogger Logger { get; }

            /// <summary>
            /// Activates the trigger.
            /// </summary>
            public async void Activate()
            {
                this.jobRunner.Logger.Verbose($"Start execute Job {this.Job.Name} triggered by trigger {this.trigger.Name}.");

                var start = DateTime.Now;

                await this.Job.RunAsync(this.jobRunner.CreateJobContext(this.Job));

                var end = DateTime.Now;

                this.jobRunner.Logger.Verbose($"Done executing Job {this.Job.Name} triggered by trigger {this.trigger.Name}.");

                var args = new JobExecutedArgs(this.Job.Name, start, end);

                foreach (var triggerContext in this.jobRunner.triggerContexts)
                {
                    triggerContext.JobExecuted?.Invoke(triggerContext, args);
                }
            }

            /// <summary>
            /// Gets the date and time this job was executed the last time.
            /// </summary>
            /// <param name="jobName">
            /// The name of the job.
            /// </param>
            /// <returns>
            /// The date and time this job was executed, or <c>null</c> if the job was never executed.
            /// </returns>
            public DateTime? GetLastExecutionTime(string jobName)
            {
                return null;
            }
        }
    }
}