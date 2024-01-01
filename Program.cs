using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public class Animal
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
    public bool IsHealthy { get; set; }
    public string Type { get; set; }
    public string Color { get; set; }
    public int FarmId { get; set; }
    public Farm Farm { get; set; }
}

public class Farm
{
    public int Id { get; set; }
    public string Name { get; set; }
    public List<Animal> Animals { get; set; }
}


public class MyDbContext : DbContext
{
    public DbSet<Animal> Animals { get; set; }
    public DbSet<Farm> Farms { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = configuration.GetConnectionString("MyDbContext");
        optionsBuilder.UseSqlServer(connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Animal>()
            .HasKey(a => a.Id);

        modelBuilder.Entity<Animal>()
            .Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(50);
    }
}

class Program
{
    static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

    static async Task Main(string[] args)
    {
        using (var context = new MyDbContext())
        {
            List<Task> tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await GenerateAndSaveEntities(context, "Animal", i);
                    await GenerateAndSaveEntities(context, "Farm", i);
                }));
            }

            await Task.WhenAll(tasks);
        }
        
    }

    static async Task GenerateAndSaveEntities(MyDbContext context, string prefix, int index)
    {
        await semaphore.WaitAsync();
        try
        {
            if (prefix == "Animal")
            {
                var animal = new Animal { Name = $"{prefix}{index}" };

                // Отримуємо ферму, до якої будемо прив'язувати тварину
                var farm = await context.Farms.FirstOrDefaultAsync();
                if (farm != null)
                {
                    animal.FarmId = farm.Id; // Прив'язуємо тварину до ферми
                    context.Animals.Add(animal); // Додаємо тварину до контексту
                }
            }
            else if (prefix == "Farm")
            {
                var farm = new Farm { Name = $"{prefix}{index}" };
                context.Farms.Add(farm); // Додаємо ферму до контексту
            }

            await context.SaveChangesAsync(); // Зберігаємо зміни у базі даних
        }
        finally
        {
            semaphore.Release();
        }
    }
}