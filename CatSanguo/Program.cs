using System;

try
{
    using var game = new CatSanguo.CatSanguoGame();
    game.Run();
}
catch (Exception ex)
{
    var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
    System.IO.File.WriteAllText(logPath, $"{DateTime.Now}\n{ex}\n");
    throw;
}
