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

using Microsoft.EntityFrameworkCore;

namespace CbmrWebAdmin.ServerBinding.Data;

public class WebPortalDbContext(DbContextOptions<WebPortalDbContext> options) : DbContext(options)
{
    public DbSet<Ban> Bans => Set<Ban>();

    public DbSet<Mute> Mutes => Set<Mute>();

    public void Initialize()
    {
        Database.EnsureCreated();
        Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "mutes" (
                "id" INTEGER NOT NULL CONSTRAINT "PK_mutes" PRIMARY KEY AUTOINCREMENT,
                "steamid" TEXT NOT NULL,
                "kind" INTEGER NOT NULL,
                "reason" TEXT NOT NULL,
                "duration" INTEGER NULL,
                "started_at" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_mutes_steamid_kind" ON "mutes" ("steamid", "kind");
            """);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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

            entity.HasIndex(ban => ban.SteamId);
        });
    }

    public Ban? FindActiveBan(string steamId, DateTime utcNow)
    {
        return Bans.AsNoTracking()
            .Where(ban => ban.SteamId == steamId)
            .OrderByDescending(ban => ban.Id)
            .AsEnumerable()
            .FirstOrDefault(ban => ban.Duration is null || ban.StartedAt + ban.Duration.Value > utcNow);
    }

    public Mute? FindActiveMute(string steamId, MuteKind kind, DateTime utcNow)
    {
        return Mutes.AsNoTracking()
            .Where(mute => mute.SteamId == steamId && mute.Kind == kind)
            .OrderByDescending(mute => mute.Id)
            .AsEnumerable()
            .FirstOrDefault(mute => mute.Duration is null || mute.StartedAt + mute.Duration.Value > utcNow);
    }

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
