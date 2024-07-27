
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Data.SqlClient;
using System.Diagnostics;

namespace DarkLake.Tools.AzureSqlMaintenance
{
    internal class Program
    {
        static void Main(string[] args)
        {

            var host = CreateHostBuilder(args).Build();

            var myService = host.Services.GetRequiredService<MaintenanceService>();
            myService.Run();

            host.RunAsync();
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
       Host.CreateDefaultBuilder(args)
           .ConfigureAppConfiguration((context, config) =>
           {
               config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
           })
           .ConfigureServices((context, services) =>
           {
               services.Configure<AzureSqlMaintentanceSettings>(context.Configuration.GetSection("AzureSqlMaintentanceSettings"));
               services.AddTransient<MaintenanceService>();
           });
    }

    public class AzureSqlMaintentanceSettings
    {
        public List<AzureSqlMaintenanceRun> Runs { get; set; }
    }

    public class AzureSqlMaintenanceRun
    {
        public string ConnectionString { get; set; }
        public int Threshold { get; set; }
    }
    public class MaintenanceService
    {
        private readonly AzureSqlMaintentanceSettings _settings;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public MaintenanceService(IOptions<AzureSqlMaintentanceSettings> settings, IHostApplicationLifetime hostApplicationLifetime)
        {
            _settings = settings.Value;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

            public void Run()
            {
                foreach (var run in _settings.Runs)
                {
                    Console.WriteLine($"Connection String: {run.ConnectionString}");
                    Console.WriteLine($"Threshold: {run.Threshold}");

                    Console.WriteLine("Getting Tables");
                    List<string> statisticsCommands = new List<string>();
                    List<string> rebuildReorgCommands = new List<string>();
                    using (SqlConnection oConn = new SqlConnection(run.ConnectionString))
                    {
                        oConn.Open();
                        using (SqlCommand command = oConn.CreateCommand())
                        {
                            command.CommandText = "SELECT TABLE_SCHEMA + '.[' + TABLE_NAME + ']' FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";

                            command.CommandType = System.Data.CommandType.Text;

                            using SqlDataReader reader = command.ExecuteReader();
                            while (reader.Read())
                            {
                                statisticsCommands.Add($"UPDATE STATISTICS {reader.GetString(0)}");
                            }
                        }
                        Console.WriteLine($"Got {statisticsCommands.Count} tables to process");

                        foreach (string statCommand in statisticsCommands)
                        {
                            Stopwatch sw = new Stopwatch();
                            sw.Start();
                            using (SqlCommand command = oConn.CreateCommand())
                            {
                                command.CommandText = statCommand;
                                command.CommandType = System.Data.CommandType.Text;
                                command.CommandTimeout = 7200;
                                command.ExecuteNonQuery();
                            }
                            sw.Stop();

                            Console.WriteLine($"Executed '{statCommand}' in {sw.Elapsed.TotalSeconds} seconds.");

                        }

                        Console.WriteLine("Statistics Updated");

                        Console.WriteLine("Beginning Index Rebuild/Rebuild");

                        using (SqlCommand oCmd = oConn.CreateCommand())
                        {
                            oCmd.CommandText = @"
                        SELECT 
                            OBJECT_NAME(PS.OBJECT_ID) AS TableName,
                            I.NAME AS IndexName,
                            PS.avg_fragmentation_in_percent AS FragmentationLevel
                        FROM 
                            sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') AS PS
                            INNER JOIN sys.indexes AS I ON PS.OBJECT_ID = I.OBJECT_ID AND PS.index_id = I.index_id
                        WHERE 
                            PS.avg_fragmentation_in_percent > 10 -- Adjust the threshold as needed
                            AND I.name IS NOT NULL";
                            oCmd.CommandTimeout = 2000;
                            using (SqlDataReader reader = oCmd.ExecuteReader())
                            {
                                if (reader.HasRows)
                                {
                                    while (reader.Read())
                                    {
                                        double fragLevel = Convert.ToDouble(reader["FragmentationLevel"]);

                                        if (fragLevel > run.Threshold)
                                        { // rebuild
                                            rebuildReorgCommands.Add($"ALTER INDEX [{reader["IndexName"].ToString()}] ON [{reader["TableName"].ToString()}] REBUILD");
                                            Console.Write($"REBUILD [{reader["IndexName"].ToString()}] ON [{reader["TableName"].ToString()}] ");
                                        }
                                        else
                                        { // reorg
                                            rebuildReorgCommands.Add($"ALTER INDEX [{reader["IndexName"].ToString()}] ON [{reader["TableName"].ToString()}] REORGANIZE");
                                            Console.Write($"REORG [{reader["IndexName"].ToString()}] ON [{reader["TableName"].ToString()}] ");
                                        }
                                        Console.WriteLine($" with frag level {fragLevel}");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("No indexes to process.");
                                }
                            }

                        }

                        foreach (string idxCmd in rebuildReorgCommands)
                        {
                            Stopwatch sw = Stopwatch.StartNew();

                            using (SqlCommand cmd = oConn.CreateCommand())
                            {
                                cmd.CommandTimeout = 7200;
                                cmd.CommandText = idxCmd;

                                cmd.ExecuteNonQuery();
                            }




                            sw.Stop();
                            Console.WriteLine($"Completed: {idxCmd} in {sw.Elapsed.TotalSeconds} seconds.");


                        }
                    }
                    Console.WriteLine("Maintenance Complete.");
                    _hostApplicationLifetime.StopApplication();

                }
            }
    }
}


