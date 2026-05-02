using Spectre.Console.Cli;

CommandApp app = new();

app.Configure(config =>
{
    config.SetApplicationName("razorstyle");

    config.AddCommand<CheckCommand>("check")
        .WithDescription("Checks Razor files for RazorStyle rule violations.")
        .WithExample(["check", "./src"])
        .WithExample(["check", "./src", "--disable", "RS0001"])
        .WithExample(["check", "./src", "--disable", "RS0001", "--disable", "RS0002"]);

    config.AddCommand<FixCommand>("fix")
        .WithDescription("Fixes RazorStyle rule violations in Razor files.")
        .WithExample(["fix", "./src"])
        .WithExample(["fix", "./src", "--disable", "RS0002"])
        .WithExample(["fix", "./src", "--disable", "RS0002", "--disable", "RS0003"]);
});

return app.Run(args);
