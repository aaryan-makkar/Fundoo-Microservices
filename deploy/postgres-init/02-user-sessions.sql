-- Safe to run during first-time initialization or once against an existing
-- fundoo_users database. Keep this schema aligned with UserSession migrations.
CREATE TABLE IF NOT EXISTS "UserSessions" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "RefreshTokenHash" text NOT NULL,
    "PreviousRefreshTokenHash" text,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "ExpiresAtUtc" timestamp with time zone NOT NULL,
    "RevokedAtUtc" timestamp with time zone,
    "LastUsedAtUtc" timestamp with time zone,
    "DeviceInfo" text,
    "Version" integer NOT NULL
);

ALTER TABLE "UserSessions"
    ADD COLUMN IF NOT EXISTS "PreviousRefreshTokenHash" text;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserSessions_RefreshTokenHash"
    ON "UserSessions" ("RefreshTokenHash");

CREATE INDEX IF NOT EXISTS "IX_UserSessions_PreviousRefreshTokenHash"
    ON "UserSessions" ("PreviousRefreshTokenHash");

CREATE INDEX IF NOT EXISTS "IX_UserSessions_UserId"
    ON "UserSessions" ("UserId");
