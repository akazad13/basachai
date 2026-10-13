using BasaChai.Application.Common;
using BasaChai.Application.Identity;
using BasaChai.Domain;
using BasaChai.Domain.Listings;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace BasaChai.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options), IAppDbContext
{
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingCharge> Charges => Set<ListingCharge>();
    public DbSet<ListingPhoto> Photos => Set<ListingPhoto>();
    public DbSet<ListingAmenity> ListingAmenities => Set<ListingAmenity>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<LocationAlias> LocationAliases => Set<LocationAlias>();
    public DbSet<PropertyType> PropertyTypes => Set<PropertyType>();
    public DbSet<Amenity> Amenities => Set<Amenity>();
    public DbSet<SavedListing> Saved => Set<SavedListing>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<ListingReport> Reports => Set<ListingReport>();
    public DbSet<ModerationAction> ModerationActions => Set<ModerationAction>();
    public DbSet<AvailabilityHistory> AvailabilityHistory => Set<AvailabilityHistory>();
    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();
    public DbSet<ContactEvent> ContactEvents => Set<ContactEvent>();
    public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<HashSalt> HashSalts => Set<HashSalt>();

    Task<int> IAppDbContext.SaveChangesAsync(CancellationToken ct) => SaveChangesAsync(ct);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        BumpVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BumpVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>
    /// A listing's version changes with every edit or transition, including changes to its charges, photos or
    /// amenities, so a stale PATCH or a second moderator is caught. View and reveal counters use atomic
    /// updates outside the change tracker and never move it.
    /// </summary>
    private void BumpVersions()
    {
        var changed = ChangeTracker.Entries<Listing>().Where(e => e.State == EntityState.Modified).Select(e => e.Entity).ToHashSet();
        foreach (var e in ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var listingId = e.Entity switch { ListingCharge c => c.ListingId, ListingPhoto p => p.ListingId, ListingAmenity a => a.ListingId, _ => Guid.Empty };
            if (listingId != Guid.Empty && ChangeTracker.Entries<Listing>().FirstOrDefault(l => l.Entity.Id == listingId) is { State: EntityState.Unchanged } owner)
                changed.Add(owner.Entity);
        }
        foreach (var l in changed)
        {
            Entry(l).Property(x => x.Version).OriginalValue = l.Version;
            l.Version++;
        }
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.HasPostgresExtension("ltree").HasPostgresExtension("pg_trgm").HasPostgresExtension("unaccent");

        // Domain entities create their own Guid ids. Telling EF so makes a new child added through a navigation
        // (a replaced charge, a new photo) an INSERT; otherwise EF sees a set key, assumes the row exists and
        // issues an UPDATE that matches nothing, which surfaces as a false concurrency conflict.
        b.Entity<Listing>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<ListingCharge>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<ListingPhoto>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<PasswordResetToken>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<ListingReport>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<ModerationAction>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<AvailabilityHistory>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<AuditEntry>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<ContactEvent>().Property(x => x.Id).ValueGeneratedNever();
        b.Entity<AnalyticsEvent>().Property(x => x.Id).ValueGeneratedNever();

        // Identity tables, renamed to the data model's names.
        b.Entity<AppUser>(e =>
        {
            e.ToTable("users");
            e.Property(u => u.PhoneE164).HasMaxLength(16);
            e.HasIndex(u => u.PhoneE164).IsUnique();
            e.Property(u => u.DisplayName).HasMaxLength(60);
            e.Property(u => u.Status).HasMaxLength(16);
            e.Property(u => u.PreferredLocale).HasMaxLength(2);
        });
        b.Entity<AppRole>().ToTable("roles");
        b.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        b.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        b.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        b.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        b.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");

        b.Entity<Location>(e =>
        {
            e.ToTable("locations");
            e.Property(l => l.Path).HasColumnType("ltree").HasConversion(v => new LTree(v), v => v.ToString());
            e.HasIndex(l => l.Path).HasMethod("gist");
            e.HasIndex(l => l.Slug);
            e.HasMany(l => l.Aliases).WithOne().HasForeignKey(a => a.LocationId);
        });
        b.Entity<LocationAlias>(e =>
        {
            e.ToTable("location_aliases");
            e.HasIndex(a => a.Normalized).HasMethod("gin").HasOperators("gin_trgm_ops").HasDatabaseName("ix_aliases_trgm");
        });
        b.Entity<PropertyType>(e => { e.ToTable("property_types"); e.HasIndex(t => t.Key).IsUnique(); });
        b.Entity<Amenity>(e => { e.ToTable("amenities"); e.HasIndex(a => a.Key).IsUnique(); });

        b.Entity<Listing>(e =>
        {
            e.ToTable("property_listings");
            e.HasIndex(l => l.ShortId).IsUnique();
            e.Property(l => l.ShortId).HasMaxLength(12);
            e.Property(l => l.Title).HasMaxLength(90);
            e.Property(l => l.Slug).HasMaxLength(120);
            e.Property(l => l.Description).HasMaxLength(2000);
            e.Property(l => l.Rules).HasMaxLength(300);
            e.Property(l => l.MonthlyRent).HasColumnName("monthly_rent_bdt");
            e.Property(l => l.ServiceCharge).HasColumnName("service_charge_bdt");
            e.Property(l => l.Status).HasMaxLength(16).HasConversion(v => v.ToValue(), v => ListingStatusNames.Parse(v));
            e.Property(l => l.SaleDetails).HasColumnType("jsonb");
            e.Property(l => l.Version).IsConcurrencyToken();
            e.Ignore(l => l.PendingHistory);
            e.HasOne<AppUser>().WithMany().HasForeignKey(l => l.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PropertyType>().WithMany().HasForeignKey(l => l.PropertyTypeId);
            e.HasOne<Location>().WithMany().HasForeignKey(l => l.LocationId);
            e.HasMany(l => l.Charges).WithOne().HasForeignKey(c => c.ListingId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(l => l.Photos).WithOne().HasForeignKey(p => p.ListingId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(l => l.Amenities).WithOne().HasForeignKey(a => a.ListingId).OnDelete(DeleteBehavior.Cascade);

            // Full text over title, description and amenity names; 'simple' because PostgreSQL has no Bangla stemmer.
            e.Property<NpgsqlTsVector>("SearchVector").HasColumnName("search_vector")
                .HasComputedColumnSql("to_tsvector('simple'::regconfig, coalesce(title, '') || ' ' || coalesce(description, '') || ' ' || coalesce(amenity_text, ''))", stored: true);
            e.HasIndex("SearchVector").HasMethod("gin").HasDatabaseName("ix_listings_text");

            e.HasIndex(l => new { l.LocationId, l.Bedrooms, l.MonthlyRent, l.LastConfirmedAt, l.Id })
                .HasDatabaseName("ix_listings_search").IsDescending(false, false, false, true, true)
                .HasFilter("status = 'published' AND NOT is_hidden AND purpose = 'rent'");
            e.HasIndex(l => new { l.OwnerId, l.Status }).HasDatabaseName("ix_listings_owner");
            e.HasIndex(l => new { l.OwnerId, l.ExpiresAt }).HasDatabaseName("ix_listings_expiring").HasFilter("status = 'published'");
            e.HasIndex(l => l.SubmittedAt).HasDatabaseName("ix_listings_queue").HasFilter("status = 'pending'");
        });
        b.Entity<ListingCharge>(e =>
        {
            e.ToTable("listing_charges");
            e.Property(c => c.Kind).HasMaxLength(16).HasConversion(
                v => v == ChargeKind.Advance ? "advance" : v == ChargeKind.OtherMonthly ? "other_monthly" : "other_one_time",
                v => v == "advance" ? ChargeKind.Advance : v == "other_monthly" ? ChargeKind.OtherMonthly : ChargeKind.OtherOneTime);
            e.Property(c => c.Label).HasMaxLength(60);
            e.Property(c => c.Amount).HasColumnName("amount_bdt");
        });
        b.Entity<ListingPhoto>(e => { e.ToTable("listing_photos"); e.Property(p => p.CloudinaryPublicId).HasMaxLength(255); });
        b.Entity<ListingAmenity>(e =>
        {
            e.ToTable("listing_amenities");
            e.HasKey(a => new { a.ListingId, a.AmenityId });
            e.HasOne<Amenity>().WithMany().HasForeignKey(a => a.AmenityId);
        });
        b.Entity<SavedListing>(e =>
        {
            e.ToTable("saved_listings");
            e.HasKey(s => new { s.UserId, s.ListingId });
            e.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Listing).WithMany().HasForeignKey(s => s.ListingId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PasswordResetToken>(e =>
        {
            e.ToTable("password_reset_tokens");
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ListingReport>(e =>
        {
            e.ToTable("listing_reports");
            e.Property(r => r.Reason).HasMaxLength(16);
            e.Property(r => r.Status).HasMaxLength(16);
            e.Property(r => r.Details).HasMaxLength(500);
            e.HasOne(r => r.Listing).WithMany().HasForeignKey(r => r.ListingId);
            e.HasIndex(r => r.CreatedAt).HasDatabaseName("ix_reports_open").HasFilter("status = 'open'");
        });
        b.Entity<ModerationAction>(e => { e.ToTable("moderation_actions"); e.HasIndex(m => m.ListingId); });
        b.Entity<AvailabilityHistory>(e =>
        {
            e.ToTable("listing_availability_history");
            e.HasOne(h => h.Listing).WithMany().HasForeignKey(h => h.ListingId);
        });
        b.Entity<AuditEntry>(e =>
        {
            e.ToTable("audit_log");
            e.HasIndex(a => new { a.EntityType, a.EntityId, a.CreatedAt }).HasDatabaseName("ix_audit_entity");
        });
        b.Entity<ContactEvent>(e => { e.ToTable("contact_events"); e.HasOne(c => c.Listing).WithMany().HasForeignKey(c => c.ListingId); });
        b.Entity<AnalyticsEvent>(e => { e.ToTable("analytics_events"); e.HasIndex(a => new { a.Name, a.CreatedAt }); });
        b.Entity<AppSetting>(e => { e.ToTable("app_settings"); e.HasKey(s => s.Key); });
        b.Entity<HashSalt>(e => { e.ToTable("hash_salts"); e.HasKey(s => s.Period); e.Property(s => s.Period).HasMaxLength(7); });
    }
}

/// <summary>
/// One random salt per calendar month for stored IP, device and session hashes (Services/HashSalts.cs). Its own table
/// so backups can leave it out (deploy/backup.sh): an old backup must not bring back a deleted salt.
/// </summary>
public class HashSalt
{
    public string Period { get; set; } = "";   // yyyy-MM, UTC
    public string Salt { get; set; } = "";     // base64, 32 random bytes
    public DateTimeOffset CreatedAt { get; set; }
}
