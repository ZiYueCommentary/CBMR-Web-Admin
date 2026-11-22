// Copyright 2026 ZiYueCommentary
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CbmrWebAdmin.ServerBinding.Data;

public class WebPortalDbContext(DbContextOptions<WebPortalDbContext> options) : DbContext(options)
{
    public DbSet<Ban> Bans => Set<Ban>();
    public DbSet<Report> Reports => Set<Report>();

    public DbSet<Mute> Mutes => Set<Mute>();

    internal DbSet<MuteAttempt> MuteAttempts => Set<MuteAttempt>();

    internal DbSet<PlayerSessionEntry> PlayerSessions => Set<PlayerSessionEntry>();

    public void Initialize()
    {
        Database.EnsureCreated();
        using IDbContextTransaction transaction = Database.BeginTransaction();
        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Reports" (
                "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                "ReporterSteamId" TEXT NOT NULL, "ReporterName" TEXT NOT NULL,
                "ReportedSteamId" TEXT NOT NULL, "ReportedName" TEXT NOT NULL,
                "Content" TEXT NOT NULL, "CreatedAtUtc" TEXT NOT NULL,
                "Handled" INTEGER NOT NULL, "HandledAtUtc" TEXT NULL,
                "Reply" TEXT NULL, "RepliedBy" TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS "mutes" (
                "id" INTEGER NOT NULL CONSTRAINT "PK_mutes" PRIMARY KEY AUTOINCREMENT,
                "steamid" TEXT NOT NULL,
                "kind" INTEGER NOT NULL,
                "reason" TEXT NOT NULL,
                "duration" INTEGER NULL,
                "started_at" TEXT NOT NULL,
                "expires_at" INTEGER NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_mutes_steamid_kind" ON "mutes" ("steamid", "kind");
            CREATE TABLE IF NOT EXISTS "mute_attempts" (
                "id" INTEGER NOT NULL CONSTRAINT "PK_mute_attempts" PRIMARY KEY AUTOINCREMENT,
                "steamid" TEXT NOT NULL,
                "player_name" TEXT NULL,
                "kind" INTEGER NOT NULL,
                "occurred_at_utc" TEXT NOT NULL,
                "text" TEXT NULL
            );
            CREATE TABLE IF NOT EXISTS "player_session_log" (
                "id" INTEGER NOT NULL CONSTRAINT "PK_player_session_log" PRIMARY KEY AUTOINCREMENT,
                "steamid" TEXT NOT NULL,
                "name" TEXT NOT NULL,
                "ip_address" TEXT NOT NULL,
                "event" INTEGER NOT NULL,
                "occurred_at_utc" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_player_session_log_occurred_at_utc_id"
                ON "player_session_log" ("occurred_at_utc" DESC, "id" DESC);
            """);

        if (!HasExpiryColumn(transaction, "bans"))
        {
            Database.ExecuteSqlRaw("""ALTER TABLE "bans" ADD COLUMN "expires_at" INTEGER NULL""");
            foreach (Ban ban in Bans.Where(ban => ban.Duration != null).ToList())
                ban.ExpiresAt = ban.StartedAt + ban.Duration;
        }

        if (!HasExpiryColumn(transaction, "mutes"))
        {
            Database.ExecuteSqlRaw("""ALTER TABLE "mutes" ADD COLUMN "expires_at" INTEGER NULL""");
            foreach (Mute mute in Mutes.Where(mute => mute.Duration != null).ToList())
                mute.ExpiresAt = mute.StartedAt + mute.Duration;
        }

        SaveChanges();
        transaction.Commit();
    }

    private bool HasExpiryColumn(IDbContextTransaction transaction, string table)
    {
        using DbCommand command = Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = table == "bans" ? """PRAGMA table_info("bans")""" : """PRAGMA table_info("mutes")""";
        using DbDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == "expires_at") return true;
        }
        return false;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerSessionEntry>(entity =>
        {
            entity.ToTable("player_session_log");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Id).HasColumnName("id");
            entity.Property(entry => entry.SteamId).HasColumnName("steamid");
            entity.Property(entry => entry.Name).HasColumnName("name");
            entity.Property(entry => entry.IpAddress).HasColumnName("ip_address");
            entity.Property(entry => entry.Event).HasColumnName("event");
            entity.Property(entry => entry.OccurredAtUtc)
                .HasColumnName("occurred_at_utc")
                .HasConversion(time => time, time => DateTime.SpecifyKind(time, DateTimeKind.Utc));
            entity.HasIndex(entry => new { entry.OccurredAtUtc, entry.Id }).IsDescending();
        });

        modelBuilder.Entity<MuteAttempt>(entity =>
        {
            entity.ToTable("mute_attempts");
            entity.HasKey(attempt => attempt.Id);
            entity.Property(attempt => attempt.Id).HasColumnName("id");
            entity.Property(attempt => attempt.SteamId).HasColumnName("steamid").IsRequired();
            entity.Property(attempt => attempt.PlayerName).HasColumnName("player_name");
            entity.Property(attempt => attempt.Kind).HasColumnName("kind");
            entity.Property(attempt => attempt.OccurredAtUtc)
                .HasColumnName("occurred_at_utc")
                .HasConversion(time => time, time => DateTime.SpecifyKind(time, DateTimeKind.Utc));
            entity.Property(attempt => attempt.Text).HasColumnName("text");
        });

        modelBuilder.Entity<Mute>(entity =>
        {
            entity.ToTable("mutes");
            entity.HasKey(mute => mute.Id);
            entity.Property(mute => mute.Id).HasColumnName("id");
            entity.Property(mute => mute.SteamId)
                .HasColumnName("steamid")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(mute => mute.Kind)
                .HasColumnName("kind")
                .IsRequired();
            entity.Property(mute => mute.Reason)
                .HasColumnName("reason")
                .IsRequired();
            entity.Property(mute => mute.Duration)
                .HasColumnName("duration")
                .HasConversion(
                    duration => duration.HasValue ? duration.Value.Ticks : (long?)null,
                    ticks => ticks.HasValue ? TimeSpan.FromTicks(ticks.Value) : null);
            entity.Property(mute => mute.StartedAt)
                .HasColumnName("started_at")
                .IsRequired();
            entity.Property(mute => mute.ExpiresAt)
                .HasColumnName("expires_at")
                .HasConversion(
                    expiry => expiry.HasValue ? expiry.Value.Ticks : (long?)null,
                    ticks => ticks.HasValue ? new DateTime(ticks.Value, DateTimeKind.Utc) : null);
            entity.HasIndex(mute => new { mute.SteamId, mute.Kind });
        });

        modelBuilder.Entity<Ban>(entity =>
        {
            entity.ToTable("bans");
            entity.HasKey(ban => ban.Id);

            entity.Property(ban => ban.SteamId)
                .HasColumnName("steamid")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(ban => ban.Reason)
                .HasColumnName("reason")
                .IsRequired();
            entity.Property(ban => ban.Duration)
                .HasColumnName("duration")
                .HasConversion(
                    duration => duration.HasValue ? duration.Value.Ticks : (long?)null,
                    ticks => ticks.HasValue ? TimeSpan.FromTicks(ticks.Value) : null);
            entity.Property(ban => ban.StartedAt)
                .HasColumnName("started_at")
                .IsRequired();
            entity.Property(ban => ban.ExpiresAt)
                .HasColumnName("expires_at")
                .HasConversion(
                    expiry => expiry.HasValue ? expiry.Value.Ticks : (long?)null,
                    ticks => ticks.HasValue ? new DateTime(ticks.Value, DateTimeKind.Utc) : null);

            entity.HasIndex(ban => ban.SteamId);
        });
    }

    public IQueryable<Ban> ActiveBans(DateTime utcNow) =>
        Bans.AsNoTracking().Where(ban => ban.ExpiresAt == null || ban.ExpiresAt > utcNow);

    public IQueryable<Mute> ActiveMutes(DateTime utcNow) =>
        Mutes.AsNoTracking().Where(mute => mute.ExpiresAt == null || mute.ExpiresAt > utcNow);

    public static WebPortalDbContext Create(string databasePath)
    {
        string fullPath = Path.GetFullPath(databasePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        DbContextOptions<WebPortalDbContext> options = new DbContextOptionsBuilder<WebPortalDbContext>()
            .UseSqlite($"Data Source={fullPath}")
            .Options;

        return new WebPortalDbContext(options);
    }
}
