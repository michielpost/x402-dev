using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace x402dev.Web.Data;

public class ApplicationDbContext : DbContext
{
    public DbSet<PublicMessage> PublicMessages { get; set; }


    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            //Fix GUID casing for SQLite
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;

                foreach (var propertyInfo in clrType.GetProperties())
                {
                    // Handle non-nullable Guid
                    if (propertyInfo.PropertyType == typeof(Guid))
                    {
                        var entityBuilder = modelBuilder.Entity(clrType);

                        entityBuilder
                            .Property(propertyInfo.Name)
                            .HasConversion(
                                new ValueConverter<Guid, string>(
                                    v => v.ToString(),
                                    v => Guid.Parse(v)))
                            .UseCollation("NOCASE");
                    }

                    // Handle nullable Guid?
                    else if (propertyInfo.PropertyType == typeof(Guid?))
                    {
                        var entityBuilder = modelBuilder.Entity(clrType);

                        entityBuilder
                            .Property(propertyInfo.Name)
                            .HasConversion(
                                new ValueConverter<Guid?, string?>(
                                    v => v.HasValue ? v.Value.ToString() : null,
                                    v => v != null ? Guid.Parse(v) : (Guid?)null))
                            .UseCollation("NOCASE");
                    }
                }
            }


            // SQLite does not have proper support for DateTimeOffset via Entity Framework Core, see the limitations
            // here: https://docs.microsoft.com/en-us/ef/core/providers/sqlite/limitations#query-limitations
            // To work around this, when the Sqlite database provider is used, all model properties of type DateTimeOffset
            // use the DateTimeOffsetToBinaryConverter
            // Based on: https://github.com/aspnet/EntityFrameworkCore/issues/10784#issuecomment-415769754
            // This only supports millisecond precision, but should be sufficient for most use cases.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType.ClrType.GetProperties().Where(p => p.PropertyType == typeof(DateTimeOffset)
                                                                            || p.PropertyType == typeof(DateTimeOffset?));
                foreach (var property in properties)
                {
                    modelBuilder
                        .Entity(entityType.Name)
                        .Property(property.Name)
                        .HasConversion(new DateTimeOffsetToBinaryConverter());
                }
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}