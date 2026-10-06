using Eventely.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Eventely.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventImage> EventImages => Set<EventImage>();
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("application_users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Email).HasMaxLength(320).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.PasswordHash).HasMaxLength(512);
            entity.Property(user => user.FirstName).HasMaxLength(100);
            entity.Property(user => user.LastName).HasMaxLength(100);
            entity.Property(user => user.Address).HasMaxLength(500);
            entity.Property(user => user.Gender).HasConversion<string>().HasMaxLength(16);
            entity.Property(user => user.Role).HasMaxLength(32).IsRequired();
            entity.Property(user => user.ProfilePictureUrl).HasMaxLength(2048);
            entity.Property(user => user.ProfilePicturePublicId).HasMaxLength(512);
        });

        modelBuilder.Entity<OtpCode>(entity =>
        {
            entity.ToTable("otp_codes");
            entity.HasKey(code => code.Id);
            entity.Property(code => code.Email).HasMaxLength(320).IsRequired();
            entity.Property(code => code.Code).HasMaxLength(512).IsRequired();
            entity.Property(code => code.FailedAttempts).HasDefaultValue(0);
            entity.HasIndex(code => new { code.Email, code.IsUsed });
            entity.HasIndex(code => code.Email)
                .IsUnique()
                .HasFilter("\"IsUsed\" = false");
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("events");
            entity.HasKey(@event => @event.Id);
            entity.Property(@event => @event.Title).HasMaxLength(160).IsRequired();
            entity.Property(@event => @event.Description).HasMaxLength(4000).IsRequired();
            entity.Property(@event => @event.Location).HasMaxLength(300).IsRequired();
            entity.Property(@event => @event.CoverImageUrl).HasMaxLength(2048);
            entity.Property(@event => @event.CoverImagePublicId).HasMaxLength(512);
            entity.HasIndex(@event => @event.EventDateTime);
            entity.HasIndex(@event => new { @event.CreatorId, @event.CreatedAt });
            entity.HasOne(@event => @event.Creator)
                .WithMany(user => user.CreatedEvents)
                .HasForeignKey(@event => @event.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EventImage>(entity =>
        {
            entity.ToTable("event_images");
            entity.HasKey(image => image.Id);
            entity.Property(image => image.ImageUrl).HasMaxLength(2048).IsRequired();
            entity.Property(image => image.PublicId).HasMaxLength(512).IsRequired();
            entity.HasOne(image => image.Event)
                .WithMany(@event => @event.Images)
                .HasForeignKey(image => image.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventParticipant>(entity =>
        {
            entity.ToTable("event_participants");
            entity.HasKey(participant => participant.Id);
            entity.HasIndex(participant => new { participant.EventId, participant.UserId }).IsUnique();
            entity.HasIndex(participant => participant.UserId);
            entity.HasOne(participant => participant.Event)
                .WithMany(@event => @event.Participants)
                .HasForeignKey(participant => participant.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(participant => participant.User)
                .WithMany(user => user.EventParticipations)
                .HasForeignKey(participant => participant.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RevokedToken>(entity =>
        {
            entity.ToTable("revoked_tokens");
            entity.HasKey(token => token.JwtId);
            entity.Property(token => token.JwtId).HasMaxLength(64);
            entity.HasIndex(token => token.ExpiresAt);
        });
    }
}
