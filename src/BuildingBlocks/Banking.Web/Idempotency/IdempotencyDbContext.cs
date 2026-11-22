using Microsoft.EntityFrameworkCore;

namespace Banking.Web.Idempotency;

/// <summary>Idempotency keys of every module's endpoints, in their own <c>idempotency</c> schema.</summary>
public sealed class IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options) : DbContext(options)
{
    public const string Schema = "idempotency";

    public DbSet<IdempotentRequest> Requests => Set<IdempotentRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<IdempotentRequest>(request =>
        {
            request.ToTable("requests");
            request.HasKey(row => row.Key);
            request.Property(row => row.Key).HasMaxLength(IdempotencyFilter.MaxKeyLength);
            request.Property(row => row.Fingerprint).HasMaxLength(64).IsFixedLength();
            request.Property(row => row.CreatedAt).HasDefaultValueSql("now()");
            request.Property(row => row.ContentType).HasMaxLength(100);
            request.Property(row => row.ResponseBody).HasColumnType("text");
        });
    }
}
