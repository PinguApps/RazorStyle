using Spectre.Console.Cli;

CommandApp app = new();

app.Configure(config =>
{
    config.SetApplicationName("razorstyle");

    config.AddCommand<CheckCommand>("check")
        .WithDescription("Checks Razor files for RazorStyle rule violations.")
        .WithExample(["check", "./src"])
        .WithExample(["check", "./src", "--disable", "RS0001"]);

    config.AddCommand<FixCommand>("fix")
        .WithDescription("Fixes RazorStyle rule violations in Razor files.")
        .WithExample(["fix", "./src"])
        .WithExample(["fix", "./src", "--disable", "RS0002"]);
});

return app.Run(args);
