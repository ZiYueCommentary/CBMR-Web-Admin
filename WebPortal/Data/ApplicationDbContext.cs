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

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CbmrWebAdmin.WebPortal.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public const string CreateAuditLogsSql = """
        CREATE TABLE IF NOT EXISTS "audit_logs" (
            "id" INTEGER NOT NULL CONSTRAINT "PK_audit_logs" PRIMARY KEY AUTOINCREMENT,
            "timestamp_utc" TEXT NOT NULL,
            "user_name" TEXT NOT NULL,
            "action" TEXT NOT NULL,
            "target" TEXT NULL,
            "details" TEXT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_audit_logs_timestamp_utc" ON "audit_logs" ("timestamp_utc");
        """;

    public DbSet<AuditLogEntry> AuditLogs => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Id).HasColumnName("id");
            entity.Property(entry => entry.TimestampUtc).HasColumnName("timestamp_utc").IsRequired();
            entity.Property(entry => entry.UserName).HasColumnName("user_name").IsRequired();
            entity.Property(entry => entry.Action).HasColumnName("action").IsRequired();
            entity.Property(entry => entry.Target).HasColumnName("target");
            entity.Property(entry => entry.Details).HasColumnName("details");
            entity.HasIndex(entry => entry.TimestampUtc);
        });
    }
}