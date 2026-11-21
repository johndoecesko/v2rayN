namespace AmazTool;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            // If no arguments are provided, display usage guidelines and exit
            if (args.Length == 0)
            {
                ShowHelp();
                return;
            }

            // Log all arguments for debugging purposes
            foreach (var arg in args)
            {
                Console.WriteLine(arg);
            }

            // Parse command based on first argument
            switch (args[0].ToLowerInvariant())
            {
                case "rebootas":
                    // Handle application restart
                    HandleRebootAsync();
                    break;

                case "help":
                case "--help":
                case "-h":
                case "/?":
                    // Display help information
                    ShowHelp();
                    break;

                default:
                    // Default behavior: handle as upgrade data
                    // Maintain backward compatibility with existing usage pattern
                    var argData = Uri.UnescapeDataString(string.Join(" ", args));
                    HandleUpgrade(argData);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Global exception handling
            Console.WriteLine($"Произошла ошибка: {ex.Message}");
            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey();
        }
    }

    /// <summary>
    /// Display help information and usage guidelines
    /// </summary>
    private static void ShowHelp()
    {
        Console.WriteLine(Resx.Resource.Guidelines);
        Console.WriteLine("Доступные команды:");
        Console.WriteLine("  rebootas             - Перезапустить приложение");
        Console.WriteLine("  help                 - Показать эту справку");
        Thread.Sleep(5000);
    }

    /// <summary>
    /// Handle application restart
    /// </summary>
    private static void HandleRebootAsync()
    {
        Console.WriteLine("Перезапуск приложения...");
        Thread.Sleep(1000);
        Utils.StartV2RayN();
    }

    /// <summary>
    /// Handle application upgrade with the provided data
    /// </summary>
    /// <param name="upgradeData">Data for the upgrade process</param>
    private static void HandleUpgrade(string upgradeData)
    {
        Console.WriteLine("Обновление приложения...");
        UpgradeApp.Upgrade(upgradeData);
    }
}
