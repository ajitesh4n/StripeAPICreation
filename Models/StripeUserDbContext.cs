using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace StripUserIntegration.Models;

public partial class StripeUserDbContext : DbContext
{
    public StripeUserDbContext()
    {
    }

    public StripeUserDbContext(DbContextOptions<StripeUserDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<TblUser> TblUsers { get; set; }
    public DbSet<StripeAccounts> StripeAccounts { get; set; }
    public DbSet<Payment> Payments { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer("Name=DefaultConnection");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TblUser>(entity =>
        {
            entity.HasKey(e => e.Email).HasName("PK__tbl_User__A9D10535F45EE90B");

            entity.ToTable("tbl_Users");

            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.UpdatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Uuid)
                .ValueGeneratedOnAdd()
                .HasColumnName("UUId");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
