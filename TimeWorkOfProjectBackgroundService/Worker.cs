using System.Collections;
using System.Diagnostics;
using System.Management;
using System.Text.Json;
using System.Text.RegularExpressions;
using TimeWorkOfProjectBackgroundService.Models;

namespace TimeWorkOfProjectBackgroundService
{
    public class Worker : IHostedService
    {
        private readonly ILogger<Worker> _logger;
        static string Filename = "list.json";
        static TimeOnly? time;
        private Process? _jobProcess;
        private Job? _job;
        private Job[] _jobs = default!;
        private ManagementEventWatcher _startWatcher;
        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _startWatcher = new ManagementEventWatcher(
               new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName = \"ServiceHub.RoslynCodeAnalysisService.exe\" "));

            _startWatcher.EventArrived += StartWatch_Event;
            _startWatcher.Start();

            while (!cancellationToken.IsCancellationRequested)
            {

                if (_job != null)
                {

                    if (_job.RemainingTime <= TimeSpan.Zero)
                    {
                        if (_jobs.All(x => x.RemainingTime <= TimeSpan.Zero))
                        {
                            foreach (var item in _jobs)
                            {
                                item.RemainingTime = item.TimeLimitation;
                            }
                        }
                        File.Delete(Filename);
                        using (FileStream fs = new FileStream(Filename, FileMode.Create))
                        {
                            await JsonSerializer.SerializeAsync(fs, _jobs);
                            _job = null;
                            _jobs = default!;
                            _jobProcess!.Exited -= CloseProcess_Event!;
                            _jobProcess?.Kill();
                        }

                    }
                    _job.RemainingTime = _job.RemainingTime.Subtract(new TimeSpan(0, 5, 0));
                }

                await Task.Delay(180000); // 3 min
            }
        }
        async void StartWatch_Event(object sender, EventArrivedEventArgs e)
        {
            Console.WriteLine("hello,world");
            using FileStream fs = new FileStream(Filename, FileMode.OpenOrCreate);
            _jobs = await JsonSerializer.DeserializeAsync<Job[]>(fs);

            if (_jobs?.Length > 0)
            {
                var result = Process.GetProcessesByName("devenv");
                _jobProcess = Process.GetProcessesByName("devenv").Where(x=> _jobs.Select(c=> c.Title).Any(c=> x.MainWindowTitle.Contains(c)))?.FirstOrDefault();
                _jobProcess!.Exited += CloseProcess_Event!;
                _jobs.OrderBy(x => x.Order);
                int jobIndex = Array.FindIndex(_jobs, x => _jobProcess!.MainWindowTitle!.Contains(x.Title)!);
                if (jobIndex != -1)
                {
                    if ((_jobs[jobIndex].Order == 1 && _jobs[jobIndex].RemainingTime <= TimeSpan.Zero) || (_jobs[jobIndex].Order > 1 && _jobs[--jobIndex].RemainingTime != TimeSpan.Zero || _jobs[jobIndex].RemainingTime <= TimeSpan.Zero) )
                    {
                        _jobProcess?.Kill();
                        await JsonSerializer.SerializeAsync(fs, _jobs);
                        _job = null;
                        return;
                    }


                    _job = _jobs[jobIndex];
                }
            }

        }


        async void CloseProcess_Event(object sender, EventArgs e)
        {
            using FileStream fs = new FileStream(Filename, FileMode.Append);
            await JsonSerializer.SerializeAsync(fs, _jobs);
            _job = null;

        }
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _startWatcher.Stop();
            await Task.CompletedTask;
        }

    }
}