// by @byte me pls

using System.Text;
using AlkorSimulation;

// === Ana Giriş Noktası ===
Console.OutputEncoding = Encoding.UTF8;
var sim = new Simulation();
sim.Run();
Console.ForegroundColor = ConsoleColor.DarkGray;
Console.WriteLine("\n  [Cikmak icin bir tusa basin]");
Console.ResetColor();
try { Console.ReadKey(true); } catch { }
