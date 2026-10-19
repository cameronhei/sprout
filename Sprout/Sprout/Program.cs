using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Sprout;
using Sprout.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddSingleton<SproutDb>();
builder.Services.AddSingleton<TaskStore>();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<ProjectStore>();
builder.Services.AddSingleton<MeetingStore>();
builder.Services.AddSingleton<ReportStore>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddSingleton<ReportService>();

var host = builder.Build();

var db = host.Services.GetRequiredService<SproutDb>();
await db.RequestPersistAsync();

var tasks = host.Services.GetRequiredService<TaskStore>();
await tasks.LoadAsync();

var settings = host.Services.GetRequiredService<SettingsStore>();
await settings.LoadAsync();

await host.Services.GetRequiredService<ProjectStore>().LoadAsync();
await host.Services.GetRequiredService<ReportStore>().LoadAsync();

var meetings = host.Services.GetRequiredService<MeetingStore>();
await meetings.LoadAsync();
await meetings.RollPastAsync(tasks);

await host.RunAsync();
