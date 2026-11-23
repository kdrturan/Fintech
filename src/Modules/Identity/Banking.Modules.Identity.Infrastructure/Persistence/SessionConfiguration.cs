using Banking.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Banking.Modules.Identity.Infrastructure.Persistence;

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");

        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id)
            .HasConversion(id => id.Value, value => new SessionId(value))
            .ValueGeneratedNever();

        builder.Property(session => session.StartedAt);
        builder.Property(session => session.RevokedAt);

        builder.HasOne<User>().WithMany().HasForeignKey(session => session.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
